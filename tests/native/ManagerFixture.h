#pragma once

#include "MouseFixture.h"
#include "MonitorEdgeTunnelManager.h"

namespace ManagerFixture
{
    // Keep the real coordinator and mouse calculations; replace OS services and persistence.
    struct State
    {
        bool enumerate = true, encode = true, append = true;
        bool hookStart = true, hookStop = true, bindMouse = true, keyUpdate = true;
        bool windowStart = true, windowStop = true, repost = true, invokeOnStop = false;
        bool loadFailure = false, loadMissing = false, saveFailure = false;
        int reads = 0, appends = 0, starts = 0, stops = 0, reposts = 0, pending = 0, loads = 0, saves = 0;
        std::string layout = "layout";
        MonitorInfoList monitors{ MouseFixture::Monitor(0) };
        TunnelInfoList appended;
        HookManager::MouseMoveCallback mouse;
        HookManager::SysKeycodeCallback key;
        DWORD keyCode = 0;
        WindowMessageManager* messages = nullptr;
        SettingManager* settings = nullptr;
        std::vector<std::string> shutdownOrder;
    };
    inline State state;
}

#include "../../MonitorEdgeTunnelLib/MonitorEdgeTunnelManager.cpp"

HookManager::HookManager() : m_isRunning(false), m_stopRequested(false), m_threadID(0), m_hookMouse(nullptr), m_hookKeyboard(nullptr) {}
HookManager::~HookManager() {}
bool HookManager::Start() { ++ManagerFixture::state.starts; m_isRunning = ManagerFixture::state.hookStart; return m_isRunning; }
bool HookManager::Stop()
{
    auto& state = ManagerFixture::state;
    ++state.stops;
    state.shutdownOrder.push_back("hook");
    if (!state.hookStop) return false;
    m_isRunning = false;
    return true;
}
bool HookManager::IsRunning() { return m_isRunning; }
bool HookManager::SetMouseMoveCallback(const MouseMoveCallback& callback)
{
    ManagerFixture::state.mouse = callback;
    return ManagerFixture::state.bindMouse;
}
bool HookManager::SetSysKeycodeCallback(DWORD code, const SysKeycodeCallback& callback)
{
    ManagerFixture::state.keyCode = code;
    ManagerFixture::state.key = callback;
    return ManagerFixture::state.keyUpdate;
}
WindowMessageManager::WindowMessageManager() : m_isRunning(false), m_stopRequested(false), m_threadID(0), m_hwnd(nullptr) {}
WindowMessageManager::~WindowMessageManager() {}
bool WindowMessageManager::Start()
{
    ManagerFixture::state.messages = this;
    m_isRunning = ManagerFixture::state.windowStart;
    return m_isRunning;
}
bool WindowMessageManager::Stop()
{
    auto& state = ManagerFixture::state;
    state.shutdownOrder.push_back("window");
    if (state.invokeOnStop) DisplayChangedDelegate.Invoke();
    m_isRunning = false;
    return state.windowStop;
}
bool WindowMessageManager::RePostDisplayChangeEvent()
{
    auto& state = ManagerFixture::state;
    ++state.reposts;
    if (state.repost) ++state.pending;
    return state.repost;
}
SettingManager::SettingManager() { ManagerFixture::state.settings = this; }
SettingManager::~SettingManager() {}
void SettingManager::Load()
{
    ++ManagerFixture::state.loads;
    if (ManagerFixture::state.loadMissing)
        throw std::filesystem::filesystem_error("injected missing setting", "setting.json", std::make_error_code(std::errc::no_such_file_or_directory));
    if (ManagerFixture::state.loadFailure) throw std::runtime_error("injected load failure");
}
void SettingManager::Save()
{
    ++ManagerFixture::state.saves;
    if (ManagerFixture::state.saveFailure) throw std::runtime_error("injected save failure");
}
bool MonitorInfoManager::GetMonitorInfoList(MonitorInfoList& output)
{
    auto& state = ManagerFixture::state;
    ++state.reads;
    output = state.enumerate ? state.monitors : MonitorInfoList{};
    return state.enumerate;
}
bool MonitorInfoManager::GetMonitorInfoListBase64(std::string& output)
{
    output = ManagerFixture::state.encode ? ManagerFixture::state.layout : "";
    return ManagerFixture::state.encode;
}
bool MonitorInfoManager::GetMonitorInfoListBase64(std::string& output, const MonitorInfoList&)
{
    return GetMonitorInfoListBase64(output);
}
bool MonitorInfoManager::AppendTunnelInfoToMonitorInfo(MonitorInfoList&, const TunnelInfoList& tunnels)
{
    ++ManagerFixture::state.appends;
    ManagerFixture::state.appended = tunnels;
    return ManagerFixture::state.append;
}

namespace ManagerFixture
{
    inline void Reset(MonitorEdgeTunnelManager& manager)
    {
        state.enumerate = state.encode = state.append = state.hookStart = state.hookStop = true;
        state.bindMouse = state.keyUpdate = state.windowStart = state.windowStop = state.repost = true;
        state.loadFailure = state.loadMissing = state.saveFailure = state.invokeOnStop = false;
        state.layout = "layout";
        state.monitors = { MouseFixture::Monitor(0) };
        state.pending = state.reposts = state.reads = state.appends = state.starts = state.stops = 0;
        manager.Stop();
        state.shutdownOrder.clear();
        auto map = state.settings->TunnelInfoListStructMap.get();
        map->clear();
    }
    inline void Notify() { state.messages->DisplayChangedDelegate.Invoke(); }
    inline void Error(MonitorEdgeTunnelManagerErrorMsg expected)
    {
        Check(MonitorEdgeTunnelManager::GetErrorMsgCode() == expected, "coordinator error code");
    }
}
