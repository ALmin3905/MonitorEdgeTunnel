#include <Windows.h>
#include <filesystem>
#include <fstream>
#include <string>
#include "TestSupport.h"

namespace
{
    // All settings live next to this test executable in the runner's temporary directory.
    int saveFailure = 0;
    int deleteAttempts = 0;
    DWORD replacementFlags = 0;

    UINT TestTemporaryFile(LPCWSTR folder, LPCWSTR prefix, UINT unique, LPWSTR output)
    {
        if (saveFailure == 1) { SetLastError(ERROR_ACCESS_DENIED); return 0; }
        if (saveFailure == 2)
        {
            const auto missing = std::filesystem::path(folder) / L"nonexistent-parent" / L"write.tmp";
            wcscpy_s(output, MAX_PATH, missing.c_str());
            return 1;
        }
        return GetTempFileNameW(folder, prefix, unique, output);
    }
    BOOL TestReplace(LPCWSTR from, LPCWSTR to, DWORD flags)
    {
        replacementFlags = flags;
        if (saveFailure == 3) { SetLastError(ERROR_ACCESS_DENIED); return FALSE; }
        return MoveFileExW(from, to, flags);
    }
    BOOL TestDelete(LPCWSTR path) { ++deleteAttempts; return DeleteFileW(path); }
}

#define GetTempFileNameW TestTemporaryFile
#define MoveFileExW TestReplace
#define DeleteFileW TestDelete
#include "../../MonitorEdgeTunnelLib/SettingManager.cpp"
#undef GetTempFileNameW
#undef MoveFileExW
#undef DeleteFileW

namespace
{
    std::filesystem::path SettingsPath()
    {
        wchar_t executable[MAX_PATH]{};
        Check(GetModuleFileNameW(nullptr, executable, MAX_PATH) != 0, "locate test executable");
        return std::filesystem::path(executable).parent_path() / L"setting.json";
    }
    void Write(const std::string& contents)
    {
        std::ofstream file;
        file.exceptions(std::ios::failbit | std::ios::badbit);
        file.open(SettingsPath(), std::ios::binary | std::ios::trunc);
        file << contents;
        file.close();
    }
    std::string Read()
    {
        std::ifstream file(SettingsPath(), std::ios::binary);
        return std::string(std::istreambuf_iterator<char>(file), {});
    }
    TunnelInfoListStruct CompleteConfiguration()
    {
        TunnelInfoListStruct settings;
        settings.forceForbidEdge = true;
        for (int edge = 0; edge < 4; ++edge)
            for (int range = 0; range < 3; ++range)
            {
                TunnelInfo tunnel{};
                tunnel.id = edge * 3 + range;
                tunnel.displayID = edge % 2;
                tunnel.relativeID = tunnel.id - 1;
                tunnel.edgeType = static_cast<EdgeType>(edge);
                tunnel.rangeType = static_cast<RangeType>(range);
                tunnel.from = -10 - tunnel.id;
                tunnel.to = 30 + tunnel.id;
                settings.tunnelInfoList.push_back(tunnel);
            }
        return settings;
    }
    void EqualConfiguration(const TunnelInfoListStruct& actual, const TunnelInfoListStruct& expected)
    {
        Check(actual.forceForbidEdge == expected.forceForbidEdge && actual.tunnelInfoList.size() == expected.tunnelInfoList.size(),
            "configuration metadata round trip");
        for (size_t i = 0; i < expected.tunnelInfoList.size(); ++i)
        {
            const auto& a = actual.tunnelInfoList[i];
            const auto& e = expected.tunnelInfoList[i];
            Check(a.id == e.id && a.displayID == e.displayID && a.relativeID == e.relativeID && a.from == e.from &&
                a.to == e.to && a.edgeType == e.edgeType && a.rangeType == e.rangeType, "all tunnel fields and order round trip");
        }
    }
}

int main()
{
    return RunTests({
        { "complete configuration JSON schema and enum names", [] {
            SettingManager manager;
            const auto settings = CompleteConfiguration();
            { auto map = manager.TunnelInfoListStructMap.get(); (*map)["layout"] = settings; }
            manager.Save();
            const auto document = json::parse(Read());
            const auto& tunnels = document.at("layout").at("TunnelInfoList");
            const char* edgeNames[] = { "Left", "Right", "Top", "Bottom" };
            const char* rangeNames[] = { "Full", "Relative", "Customize" };
            Check(document.at("layout").at("forceForbidEdge") == true && tunnels.size() == 12, "serialized configuration shape");
            for (size_t i = 0; i < tunnels.size(); ++i)
            {
                Check(tunnels[i].size() == 7, "only public tunnel fields are serialized");
                Check(tunnels[i].at("edgeType") == edgeNames[i / 3] && tunnels[i].at("rangeType") == rangeNames[i % 3],
                    "all enum values use stable textual names");
                Check(tunnels[i].at("id") == i && tunnels[i].at("from") == -10 - static_cast<int>(i) &&
                    tunnels[i].at("to") == 30 + i, "signed numeric values preserved");
            }
            { auto map = manager.TunnelInfoListStructMap.get(); map->clear(); }
            manager.Load();
            { const auto map = manager.TunnelInfoListStructMap.get_readonly(); EqualConfiguration(map->at("layout"), settings); }
        } },
        { "multiple layouts Unicode keys and empty rules round trip", [] {
            SettingManager writer;
            const auto settings = CompleteConfiguration();
            const std::string key = "版面\"\\\n";
            { auto map = writer.TunnelInfoListStructMap.get(); (*map)[key] = settings; (*map)["empty"] = {}; }
            writer.Save();
            SettingManager reader;
            reader.Load();
            const auto map = reader.TunnelInfoListStructMap.get_readonly();
            Check(map->size() == 2 && map->count(key) == 1, "Unicode and escaped key characters preserved");
            EqualConfiguration(map->at(key), settings);
            Check(map->at("empty").tunnelInfoList.empty() && !map->at("empty").forceForbidEdge, "empty default configuration");
        } },
        { "saving an empty map replaces the previous document", [] {
            Write("{\"old\":true}");
            SettingManager manager;
            manager.Save();
            Check(json::parse(Read()) == json::object(), "empty map writes an object");
            Check((replacementFlags & (MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) ==
                (MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH), "atomic durable replacement requested");
        } },
        { "every required tunnel field is checked transactionally", [] {
            SettingManager writer;
            { auto map = writer.TunnelInfoListStructMap.get(); (*map)["layout"] = CompleteConfiguration(); }
            writer.Save();
            const auto document = json::parse(Read());
            SettingManager reader;
            { auto map = reader.TunnelInfoListStructMap.get(); (*map)["keep"] = {}; }
            for (const char* field : { "id", "displayID", "relativeID", "edgeType", "rangeType", "from", "to" })
            {
                auto damaged = document;
                damaged["layout"]["TunnelInfoList"][1].erase(field);
                Write(damaged.dump());
                CheckThrows<std::exception>([&] { reader.Load(); }, "missing required field rejected");
                const auto map = reader.TunnelInfoListStructMap.get_readonly();
                Check(map->size() == 1 && map->count("keep") == 1, "partially read layout never replaces previous state");
            }
            for (const char* field : { "id", "displayID", "relativeID", "from", "to" })
            {
                auto damaged = document;
                damaged["layout"]["TunnelInfoList"][0][field] = "not a number";
                Write(damaged.dump());
                CheckThrows<std::exception>([&] { reader.Load(); }, "wrong numeric field type rejected");
            }
        } },
        { "temporary-file open and replacement failures preserve saved bytes", [] {
            SettingManager manager;
            { auto map = manager.TunnelInfoListStructMap.get(); (*map)["new"] = CompleteConfiguration(); }
            for (int failure : { 1, 2, 3 })
            {
                Write("previous bytes");
                const int before = deleteAttempts;
                saveFailure = failure;
                bool rejected = false;
                try { manager.Save(); } catch (const std::exception&) { rejected = true; }
                saveFailure = 0;
                Check(rejected && Read() == "previous bytes", "failed save does not truncate destination");
                Check(deleteAttempts == before + (failure == 1 ? 0 : 1), "created temporary path receives cleanup attempt");
                for (const auto& entry : std::filesystem::directory_iterator(SettingsPath().parent_path()))
                    Check(entry.path().extension() != L".tmp", "failed save leaves no temporary file");
            }
            manager.Save();
            Check(json::parse(Read()).contains("new"), "save recovers after I/O failure");
        } },
        { "unreadable existing settings are not classified as missing", [] {
            Write("{\"loaded\":{\"TunnelInfoList\":[],\"forceForbidEdge\":false}}");
            SettingManager manager;
            { auto map = manager.TunnelInfoListStructMap.get(); (*map)["keep"] = {}; }
            HANDLE locked = CreateFileW(SettingsPath().c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, 0, nullptr);
            Check(locked != INVALID_HANDLE_VALUE, "deny concurrent access to the existing file");
            bool rejected = false, missing = false;
            try { manager.Load(); }
            catch (const std::filesystem::filesystem_error& error) { rejected = true; missing = error.code() == std::errc::no_such_file_or_directory; }
            catch (const std::exception&) { rejected = true; }
            CloseHandle(locked);
            Check(rejected && !missing, "sharing or read failures must remain errors instead of an unsaved-settings notice");
            const auto map = manager.TunnelInfoListStructMap.get_readonly();
            Check(map->size() == 1 && map->count("keep") == 1, "unreadable file preserves in-memory settings");
        } },
        { "settings path follows executable rather than current directory", [] {
            struct RestoreDirectory
            {
                std::filesystem::path previous = std::filesystem::current_path();
                ~RestoreDirectory() { std::filesystem::current_path(previous); }
            } restore;
            const auto elsewhere = SettingsPath().parent_path() / L"other-working-directory";
            std::filesystem::create_directory(elsewhere);
            std::filesystem::current_path(elsewhere);
            SettingManager manager;
            manager.Save();
            manager.Load();
            Check(std::filesystem::exists(SettingsPath()) && !std::filesystem::exists(elsewhere / L"setting.json"),
                "process working directory does not redirect settings");
        } },
    });
}
