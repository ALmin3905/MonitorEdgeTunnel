"""Compile and run Windows regressions with all outputs in a temporary directory.

Usage: python tests/run_tests.py [compile|window|retry|mouse|lifecycle|pipe|interop|settings]
Requires Visual Studio 2022 C++ tools, Windows SDK, and .NET Framework 4.7.2.
"""

import os
from pathlib import Path
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parent.parent
TESTS = Path(__file__).resolve().parent


def run(command, directory, env, timeout=60):
    result = subprocess.run(
        [str(value) for value in command], cwd=directory, env=env,
        capture_output=True, timeout=timeout,
    )
    output = (result.stdout + result.stderr).decode("utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {command}\n{output}")
    return output


def toolchain():
    installer = Path(os.environ["ProgramFiles(x86)"]) / "Microsoft Visual Studio/Installer/vswhere.exe"
    installation = subprocess.check_output([
        str(installer), "-latest", "-version", "[17.0,18.0)", "-products", "*",
        "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
        "-property", "installationPath",
    ], encoding="utf-8").strip()
    if not installation:
        raise RuntimeError("Visual Studio 2022 C++ tools were not found")
    studio = Path(installation)
    version_key = lambda path: tuple(int(part) for part in path.name.split("."))
    msvc = max((studio / "VC/Tools/MSVC").iterdir(), key=version_key)
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


def managed_test(name, studio, compiler, env, directory):
    sources = {
        "pipe": ["SelfNamedPipeManager.cs", "UtilitiesExtensions.cs"],
        "interop": ["MonitorEdgeTunnel.cs", "Utility.cs"],
        "save_ui": ["MainWindow.xaml.cs", "UtilitiesExtensions.cs"],
    }
    pipe_name = "MonitorEdgeTunnel-test-" + directory.name
    inputs = [TESTS / "managed" / (name + ".cs")]
    inputs += [ROOT / "MonitorEdgeTunnelApp" / file for file in sources[name]]
    copies = []
    for source in inputs:
        copy = directory / source.name
        text = source.read_text(encoding="utf-8-sig").replace("_MonitorEdgeTunnelAppNamedPipe_", pipe_name)
        if name == "save_ui":
            text = text.replace("using MessageBox = System.Windows.MessageBox;", "using MessageBox = MonitorEdgeTunnelApp.TestMessageBox;")
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
        references = ["/r:" + str(ROOT / "packages/Serilog.4.3.0/lib/net471/Serilog.dll")]
    run([
        studio / "MSBuild/Current/Bin/Roslyn/csc.exe", "/nologo", "/langversion:7.3", "/platform:x64",
        "/out:" + str(executable), *references, *copies,
    ], directory, env)
    print(run([executable], directory, env, timeout=20).strip())


def main():
    studio, compiler, env = toolchain()
    requested = sys.argv[1:] or ["compile"] + [path.stem for path in sorted((TESTS / "native").glob("*.cpp"))] + [
        path.stem for path in sorted((TESTS / "managed").glob("*.cs"))
    ]
    with tempfile.TemporaryDirectory(prefix="MonitorEdgeTunnel-tests-") as temporary:
        directory = Path(temporary)
        for name in requested:
            if name == "compile":
                compile_native(compiler, env, directory)
            elif (TESTS / "native" / (name + ".cpp")).exists():
                native_test(name, compiler, env, directory)
            elif (TESTS / "managed" / (name + ".cs")).exists():
                managed_test(name, studio, compiler, env, directory)
            else:
                raise ValueError("Unknown test: " + name)
            print("PASS " + name, flush=True)


if __name__ == "__main__":
    main()
