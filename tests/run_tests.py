"""Compile and run Windows regressions with all outputs in a temporary directory.

Usage: python tests/run_tests.py [lib | test names... | --list]
Without names, runs all regressions. "lib" runs all native library tests.
Add "solution" for clean x64 Debug/Release builds in a temporary source copy.
Requires the project's Visual Studio C++ toolset, Windows SDK, and .NET Framework 4.7.2.
"""

import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parent.parent
TESTS = Path(__file__).resolve().parent


def run(command, directory, env, timeout=60):
    try:
        result = subprocess.run(
            [str(value) for value in command], cwd=directory, env=env,
            capture_output=True, timeout=timeout,
        )
    except subprocess.TimeoutExpired as error:
        output = (error.stdout or b"") + (error.stderr or b"")
        raise RuntimeError(f"Timed out after {timeout}s: {command}\n" + output.decode("utf-8", errors="replace")) from error
    output = (result.stdout + result.stderr).decode("utf-8", errors="replace").replace("\r\n", "\n")
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {command}\n{output}")
    return output


def toolchain():
    installer = Path(os.environ["ProgramFiles(x86)"]) / "Microsoft Visual Studio/Installer/vswhere.exe"
    installations = json.loads(subprocess.check_output([
        str(installer), "-all", "-products", "*", "-format", "json", "-utf8",
        "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
    ], encoding="utf-8-sig"))
    project = ET.parse(ROOT / "MonitorEdgeTunnelLib/MonitorEdgeTunnelLib.vcxproj")
    toolsets = {element.text for element in project.findall(".//{*}PlatformToolset")}
    if len(toolsets) != 1:
        raise RuntimeError("Expected one native project toolset, found: " + repr(toolsets))
    toolset = toolsets.pop()
    studio = None
    for installation in installations:
        candidate = Path(installation["installationPath"])
        if list((candidate / "MSBuild/Microsoft/VC").glob("*/Platforms/x64/PlatformToolsets/" + toolset)):
            studio = candidate
            break
    if studio is None:
        raise RuntimeError("Visual Studio C++ toolset " + toolset + " was not found")
    version_key = lambda path: tuple(int(part) for part in path.name.split("."))
    version_file = studio / ("VC/Auxiliary/Build/Microsoft.VCToolsVersion." + toolset + ".default.txt")
    if not version_file.exists():
        version_file = studio / "VC/Auxiliary/Build/Microsoft.VCToolsVersion.default.txt"
    msvc = studio / "VC/Tools/MSVC" / version_file.read_text(encoding="utf-8-sig").strip()
    sdk = Path(os.environ["ProgramFiles(x86)"]) / "Windows Kits/10"
    version = max((sdk / "Include").iterdir(), key=version_key).name
    env = os.environ.copy()
    env["INCLUDE"] = ";".join(str(path) for path in [
        msvc / "include", *[sdk / "Include" / version / name for name in ("ucrt", "shared", "um", "winrt")],
    ])
    env["LIB"] = ";".join(str(path) for path in [
        msvc / "lib/x64", sdk / "Lib" / version / "ucrt/x64", sdk / "Lib" / version / "um/x64",
    ])
    env["PATH"] = str(msvc / "bin/Hostx64/x64") + os.pathsep + env["PATH"]
    print("Toolchain: " + toolset + " / MSVC " + msvc.name, flush=True)
    return studio, msvc / "bin/Hostx64/x64/cl.exe", env


def compile_native(compiler, env, directory):
    for folder in ("MonitorEdgeTunnelLib", "MonitorEdgeTunnelDll"):
        for source in sorted((ROOT / folder).glob("*.cpp")):
            run([
                compiler, "/nologo", "/c", "/std:c++20", "/EHsc", "/utf-8", "/permissive-", "/W3", "/Y-",
                "/DMONITOREDGETUNNELDLL_EXPORTS", "/I" + str(ROOT / "MonitorEdgeTunnelLib"),
                source, "/Fo" + str(directory / (folder + "_" + source.stem + ".obj")),
            ], directory, env)


def native_test(name, compiler, env, directory):
    executable = directory / (name + ".exe")
    run([
        compiler, "/nologo", "/std:c++20", "/EHsc", "/utf-8", "/permissive-", "/W3", "/MD", "/Y-",
        "/I" + str(ROOT / "MonitorEdgeTunnelLib"), TESTS / "native" / (name + ".cpp"),
        *[ROOT / "MonitorEdgeTunnelLib" / (source + ".cpp") for source in ("WaitEvent", "Logger", "Utility")],
        "/Fo" + str(directory) + "\\", "/Fe" + str(executable),
        "/link", "user32.lib", "Pathcch.lib", "ole32.lib",
    ], directory, env)
    print(run([executable], directory, env, timeout=30).strip())


def replace_message_box(text):
    """Keep save/error-path tests nonmodal for both system and themed dialog callers."""
    for alias in ("System.Windows.MessageBox", "MonitorEdgeTunnelApp.Themes.ThemedMessageBox"):
        text = text.replace("using MessageBox = " + alias + ";", "using MessageBox = MonitorEdgeTunnelApp.TestMessageBox;")
    return text


def managed_test(name, studio, compiler, env, directory):
    if name in ("monitor_layout", "theme", "save_ui", "editing"):
        monitor_layout_test(name, studio, env, directory)
        return
    sources = {
        "pipe": ["SelfNamedPipeManager.cs", "UtilitiesExtensions.cs"],
        "interop": ["MonitorEdgeTunnel.cs", "Utility.cs"],
        "tunnel_preview": ["MonitorEdgeTunnel.cs", "Utility.cs", "Controls/TunnelPreviewCalculator.cs"],
        "save_ui": ["MainWindow.xaml.cs", "UtilitiesExtensions.cs"],
    }
    pipe_name = "MonitorEdgeTunnel-test-" + directory.name
    inputs = [TESTS / "managed" / (name + ".cs")]
    inputs += [ROOT / "MonitorEdgeTunnelApp" / file for file in sources[name]]
    if name == "save_ui":
        inputs.append(TESTS / "fixtures/UiServiceFakes.cs")
    copies = []
    for source in inputs:
        copy = directory / source.name
        text = source.read_text(encoding="utf-8-sig").replace("_MonitorEdgeTunnelAppNamedPipe_", pipe_name)
        if name == "save_ui":
            text = replace_message_box(text)
        copy.write_bytes(text.replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8-sig"))
        copies.append(copy)
    executable = directory / (name + ".exe")
    references = []
    if name == "save_ui":
        framework = Path(os.environ["ProgramFiles(x86)"]) / "Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2"
        references = ["/r:" + str(framework / (assembly + ".dll")) for assembly in ("PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml")]
    if name == "interop":
        run([
            compiler, "/nologo", "/LD", "/Od", "/MD", TESTS / "fixtures/interop_shim.cpp",
            "/Fo" + str(directory / "interop_shim.obj"), "/Fe" + str(directory / "MonitorEdgeTunnelDll.dll"),
            "/link", "/IMPLIB:" + str(directory / "interop_shim.lib"),
        ], directory, env)
    if name in ("interop", "tunnel_preview"):
        references = ["/r:" + str(ROOT / "packages/Serilog.4.3.0/lib/net471/Serilog.dll")]
    run([
        studio / "MSBuild/Current/Bin/Roslyn/csc.exe", "/nologo", "/langversion:7.3", "/platform:x64",
        "/out:" + str(executable), *references, *copies,
    ], directory, env)
    print(run([executable], directory, env, timeout=20).strip())


def monitor_layout_test(test_name, studio, env, directory):
    """Compile real WPF markup with fake services; all project outputs stay in a temporary copy."""
    project_dir = directory / test_name
    project_dir.mkdir()
    app = ROOT / "MonitorEdgeTunnelApp"
    sources = [app / name for name in (
        "MainWindow.xaml.cs", "MainWindow.Editing.cs", "MainWindow.Validation.cs", "UtilitiesExtensions.cs", "RelativeIdValidMultiValueConverter.cs", "IsStartConverter.cs",
    )] + sorted((app / "Controls").glob("*.cs")) + sorted((app / "Themes").glob("*.cs")) + sorted((app / "Editing").glob("*.cs"))
    pages = [app / "MainWindow.xaml", app / "Controls/MonitorLayoutView.xaml"] + sorted((app / "Themes").glob("*.xaml"))
    project = ET.Element("Project", {"ToolsVersion": "15.0", "xmlns": "http://schemas.microsoft.com/developer/msbuild/2003"})
    ET.SubElement(project, "Import", {"Project": "$(MSBuildExtensionsPath)/$(MSBuildToolsVersion)/Microsoft.Common.props"})
    properties = ET.SubElement(project, "PropertyGroup")
    for name, value in {
        "TargetFrameworkVersion": "v4.7.2", "OutputType": "Exe", "AssemblyName": test_name,
        "RootNamespace": "MonitorEdgeTunnelApp", "PlatformTarget": "x64", "LangVersion": "7.3",
        "StartupObject": "MonitorEdgeTunnelApp." + {"theme": "ThemeTests", "monitor_layout": "MonitorLayoutTests", "save_ui": "SaveUiTests", "editing": "EditingTests"}[test_name], "OutputPath": "bin/",
        "ProjectTypeGuids": "{60dc8134-eba5-43b8-bcc9-bb4bc16c2548};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}",
    }.items():
        ET.SubElement(properties, name).text = value
    references = ET.SubElement(project, "ItemGroup")
    for assembly in ("System", "System.Core", "WindowsBase", "PresentationCore", "PresentationFramework", "System.Xaml",
                     "UIAutomationTypes", "UIAutomationProvider"):
        ET.SubElement(references, "Reference", {"Include": assembly})
    inputs = ET.SubElement(project, "ItemGroup")
    for source in sources + pages + [TESTS / ("managed/" + test_name + ".cs"), TESTS / "fixtures/UiServiceFakes.cs"]:
        relative = source.relative_to(app) if source.is_relative_to(app) else Path(source.name)
        copy = project_dir / relative
        copy.parent.mkdir(parents=True, exist_ok=True)
        text = replace_message_box(source.read_text(encoding="utf-8-sig"))
        copy.write_bytes(text.replace("\n", "\r\n").encode("utf-8-sig"))
        ET.SubElement(inputs, "Page" if source.suffix == ".xaml" else "Compile", {"Include": str(relative)})
    icon = project_dir / "Resources/monitor.ico"
    icon.parent.mkdir()
    shutil.copy2(app / "Resources/monitor.ico", icon)
    ET.SubElement(inputs, "Resource", {"Include": "Resources/monitor.ico"})
    ET.SubElement(project, "Import", {"Project": "$(MSBuildToolsPath)/Microsoft.CSharp.targets"})
    project_file = project_dir / "monitor_layout.csproj"
    ET.ElementTree(project).write(project_file, encoding="utf-8", xml_declaration=True)
    run([studio / "MSBuild/Current/Bin/MSBuild.exe", project_file, "/nologo", "/nr:false", "/m:1", "/verbosity:minimal"],
        project_dir, env, timeout=120)
    command = [project_dir / ("bin/" + test_name + ".exe")]
    if test_name == "monitor_layout" and env.get("MONITOREDGETUNNEL_TEST_SNAPSHOT"):
        command += ["--snapshot", env["MONITOREDGETUNNEL_TEST_SNAPSHOT"]]
    print(run(command, project_dir, env, timeout=30).strip())


def solution_test(studio, env, directory):
    source = directory / "source"
    source.mkdir()
    tracked = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT).decode("utf-8").split("\0")
    for name in filter(None, tracked):
        destination = source / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / name, destination)
    shutil.copytree(ROOT / "packages", source / "packages")
    for configuration in ("Debug", "Release"):
        run([
            studio / "MSBuild/Current/Bin/MSBuild.exe", source / "MonitorEdgeTunnel.sln",
            "/nologo", "/nr:false", "/m:1", "/verbosity:minimal",
            "/p:Platform=x64", "/p:Configuration=" + configuration,
        ], source, env, timeout=180)
        for name in ("MonitorEdgeTunnelApp.exe", "MonitorEdgeTunnelDll.dll"):
            binary = (source / "x64" / configuration / name).read_bytes()
            pe = struct.unpack_from("<I", binary, 0x3C)[0]
            if struct.unpack_from("<H", binary, pe + 4)[0] != 0x8664:
                raise AssertionError("The app and DLL must both target AMD64: " + name)
        print("Clean " + configuration + "|x64 solution build and matching PE architectures verified", flush=True)


def check_csharp_format():
    for folder in (ROOT / "MonitorEdgeTunnelApp", TESTS / "managed", TESTS / "fixtures"):
        for source in folder.rglob("*.cs"):
            if "obj" in source.parts or "bin" in source.parts:
                continue
            data = source.read_bytes()
            if not data.startswith(b"\xef\xbb\xbf") or re.search(rb"(?<!\r)\n|\r(?!\n)", data):
                raise AssertionError("C# sources must use UTF-8 BOM and CRLF: " + str(source))
            if re.search(rb"(?m)^ *\t", data):
                raise AssertionError("C# indentation must use spaces: " + str(source))


def main():
    check_csharp_format()
    native = [path.stem for path in sorted((TESTS / "native").glob("*.cpp"))]
    available = ["compile"] + native + [
        path.stem for path in sorted((TESTS / "managed").glob("*.cs"))
    ]
    if sys.argv[1:] == ["--list"]:
        print("\n".join(available + ["lib", "solution"]))
        return
    requested = sys.argv[1:] or available
    requested = list(dict.fromkeys(test for name in requested for test in (native if name == "lib" else [name])))
    unknown = set(requested) - set(available + ["solution"])
    if unknown:
        raise ValueError("Unknown tests: " + ", ".join(sorted(unknown)))
    studio, compiler, env = toolchain()
    started = time.monotonic()
    with tempfile.TemporaryDirectory(prefix="MonitorEdgeTunnel-tests-") as temporary:
        directory = Path(temporary)
        for name in requested:
            if name == "compile":
                compile_native(compiler, env, directory)
            elif name == "solution":
                solution_test(studio, env, directory)
            elif (TESTS / "native" / (name + ".cpp")).exists():
                native_test(name, compiler, env, directory)
            elif (TESTS / "managed" / (name + ".cs")).exists():
                managed_test(name, studio, compiler, env, directory)
            else:
                raise ValueError("Unknown test: " + name)
            print("PASS " + name, flush=True)
    print(f"Passed {len(requested)} suites in {time.monotonic() - started:.1f}s", flush=True)


if __name__ == "__main__":
    main()
