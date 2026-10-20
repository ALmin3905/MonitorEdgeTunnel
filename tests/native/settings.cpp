#include <Windows.h>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <string>
#include "../../MonitorEdgeTunnelLib/SettingManager.cpp"

namespace
{
    // The executable lives in the runner's temporary directory, so no user settings are touched.
    std::filesystem::path SettingsPath()
    {
        wchar_t executable[MAX_PATH]{};
        GetModuleFileNameW(nullptr, executable, MAX_PATH);
        return std::filesystem::path(executable).parent_path() / L"setting.json";
    }
    void Write(const std::string& text)
    {
        std::ofstream file(SettingsPath());
        file.exceptions(std::ios::failbit | std::ios::badbit);
        file << text;
        file.close();
    }
    void Require(bool condition) { if (!condition) throw std::runtime_error("Settings regression failed"); }
    void RequireOriginal(const SettingManager& manager)
    {
        const auto current = manager.TunnelInfoListStructMap.get_readonly();
        Require(current->size() == 1 && current->count("original") == 1);
    }
}

int main()
{
    SettingManager manager;
    { auto current = manager.TunnelInfoListStructMap.get(); (*current)["original"] = TunnelInfoListStruct{}; }
    for (const auto& invalid : {
        "{broken", "[]",
        "{\"new\":{\"TunnelInfoList\":[],\"forceForbidEdge\":\"wrong\"}}",
        "{\"AA\":{\"TunnelInfoList\":[],\"forceForbidEdge\":true},\"ZZ\":{}}",
        "{\"new\":{\"TunnelInfoList\":{},\"forceForbidEdge\":false}}"})
    {
        Write(invalid);
        bool rejected = false;
        try { manager.Load(); } catch (const std::exception&) { rejected = true; }
        Require(rejected);
        RequireOriginal(manager);
    }
    std::filesystem::remove(SettingsPath());
    bool missingRejected = false;
    try { manager.Load(); } catch (const std::exception&) { missingRejected = true; }
    Require(missingRejected);
    RequireOriginal(manager);

    Write("{\"restored\":{\"TunnelInfoList\":[],\"forceForbidEdge\":true}}");
    manager.Load();
    { const auto current = manager.TunnelInfoListStructMap.get_readonly(); Require(current->size() == 1 && current->at("restored").forceForbidEdge); }
    Write("");
    manager.Load();
    { const auto current = manager.TunnelInfoListStructMap.get_readonly(); Require(current->empty()); }
    // A denied atomic replacement must leave the previously saved bytes intact.
    Write("{\"previous\":true}");
    HANDLE locked = CreateFileW(SettingsPath().c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr);
    Require(locked != INVALID_HANDLE_VALUE);
    bool saveRejected = false;
    try { manager.Save(); } catch (const std::exception&) { saveRejected = true; }
    CloseHandle(locked);
    Require(saveRejected);
    { std::ifstream file(SettingsPath()); std::string contents((std::istreambuf_iterator<char>(file)), {}); Require(contents == "{\"previous\":true}"); }
    for (const auto& entry : std::filesystem::directory_iterator(SettingsPath().parent_path()))
        Require(entry.path().extension() != L".tmp");

    { auto current = manager.TunnelInfoListStructMap.get(); (*current)["saved"].forceForbidEdge = true; }
    manager.Save();
    { auto current = manager.TunnelInfoListStructMap.get(); current->clear(); }
    manager.Load();
    { const auto current = manager.TunnelInfoListStructMap.get_readonly(); Require(current->size() == 1 && current->at("saved").forceForbidEdge); }
    std::puts("Failed loads and saves preserve previous data; atomic save and reload succeed");
}
