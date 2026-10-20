#include <Windows.h>
#include <cstdio>
#include <stdexcept>
#include "MonitorEdgeTunnelManager.h"

namespace
{
    // Replace OS hooks and display discovery while exercising the real retry coordinator.
    WindowMessageManager* messages = nullptr;
    int pending = 0;
    int failuresRemaining = 0;
    int reads = 0;
    BOOL TestGetCursorPos(LPPOINT point) { *point = POINT{50, 50}; return TRUE; }
    void Require(bool condition) { if (!condition) throw std::runtime_error("Retry regression failed"); }
    void DrainRetries()
    {
        int delivered = 0;
        while (pending)
        {
            Require(++delivered <= 5);
            --pending;
            messages->DisplayChangedDelegate.Invoke();
        }
    }
}

#define GetCursorPos TestGetCursorPos
#include "../../MonitorEdgeTunnelLib/MouseEdgeManager.cpp"
#undef GetCursorPos
#include "../../MonitorEdgeTunnelLib/MonitorEdgeTunnelManager.cpp"

HookManager::HookManager() : m_isRunning(false), m_threadID(0), m_hookMouse(nullptr), m_hookKeyboard(nullptr) {}
HookManager::~HookManager() {}
bool HookManager::Start() { m_isRunning = true; return true; }
bool HookManager::Stop() { m_isRunning = false; return true; }
bool HookManager::IsRunning() { return m_isRunning; }
bool HookManager::SetMouseMoveCallback(const MouseMoveCallback& callback) { m_mouseMoveCallback = callback; return true; }
bool HookManager::SetSysKeycodeCallback(DWORD key, const SysKeycodeCallback& callback) { return true; }
WindowMessageManager::WindowMessageManager() : m_isRunning(false), m_threadID(0), m_hwnd(nullptr) {}
WindowMessageManager::~WindowMessageManager() {}
bool WindowMessageManager::Start() { messages = this; m_isRunning = true; return true; }
bool WindowMessageManager::Stop() { m_isRunning = false; return true; }
bool WindowMessageManager::RePostDisplayChangeEvent() { ++pending; return true; }
SettingManager::SettingManager() {}
SettingManager::~SettingManager() {}
void SettingManager::Load() {}
void SettingManager::Save() {}
bool MonitorInfoManager::GetMonitorInfoList(MonitorInfoList& result)
{
    ++reads;
    if (failuresRemaining > 0) { --failuresRemaining; return false; }
    MonitorInfo monitor{};
    monitor.id = 0; monitor.right = 99; monitor.bottom = 99; monitor.scaling = 1;
    result = {monitor};
    return true;
}
bool MonitorInfoManager::GetMonitorInfoListBase64(std::string& result) { result = "test"; return true; }
bool MonitorInfoManager::GetMonitorInfoListBase64(std::string& result, const MonitorInfoList& monitors) { result = "test"; return true; }
bool MonitorInfoManager::AppendTunnelInfoToMonitorInfo(MonitorInfoList& monitors, const TunnelInfoList& tunnels) { return true; }

int main()
{
    auto& manager = MonitorEdgeTunnelManager::GetInstance();
    Require(manager.Start());
    failuresRemaining = 1;
    messages->DisplayChangedDelegate.Invoke();
    Require(!manager.IsStart() && pending == 1);
    DrainRetries();
    Require(manager.IsStart());

    const int before = reads;
    failuresRemaining = 100;
    messages->DisplayChangedDelegate.Invoke();
    DrainRetries();
    Require(reads - before == 6 && !manager.IsStart());

    failuresRemaining = 0;
    messages->DisplayChangedDelegate.Invoke();
    Require(manager.IsStart());
    failuresRemaining = 1;
    messages->DisplayChangedDelegate.Invoke();
    Require(pending == 1 && manager.Stop());
    const int stoppedReads = reads;
    DrainRetries();
    Require(reads == stoppedReads && !manager.IsStart());
    std::puts("Transient failure recovers, retries are bounded, and manual stop cancels retries");
}
