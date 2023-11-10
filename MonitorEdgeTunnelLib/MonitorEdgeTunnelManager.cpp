#include "pch.h"
#include "MonitorEdgeTunnelManager.h"
#include "HookManager.h"
#include "MouseEdgeManager.h"
#include "SettingManager.h"
#include <iostream>

namespace
{
    HookManager& s_hookManager = HookManager::GetInstance();

    MouseEdgeManager& s_mouseEdgeManager = MouseEdgeManager::GetInstance();

    SettingManager& s_settingManager = SettingManager::GetInstance();
}

/*static*/ MonitorEdgeTunnelManager& MonitorEdgeTunnelManager::GetInstance()
{
    static MonitorEdgeTunnelManager instance;
    return instance;
}

MonitorEdgeTunnelManager::MonitorEdgeTunnelManager()
{
    // ¸ü¤Jsetting
    try
    {
        s_settingManager.Load();
    }
    catch (...)
    {
        std::cout << "No setting file" << std::endl;
    }
}

MonitorEdgeTunnelManager::~MonitorEdgeTunnelManager()
{

}

bool MonitorEdgeTunnelManager::Start()
{
    if (!Stop())
    {
        std::cout << "Failed to stop hook" << std::endl;
        return false;
    }

    MonitorInfoList monitorInfoList;
    if (!MonitorInfoManager::GetMonitorInfoList(monitorInfoList))
    {
        std::cout << "Failed to get monitorInfo" << std::endl;
        return false;
    }

    if (monitorInfoList.empty())
    {
        std::cout << "No monitorInfo" << std::endl;
        return false;
    }

    if (!MonitorInfoManager::AppendTunnelInfoToMonitorInfo(monitorInfoList, s_settingManager.TunnelInfoList))
    {
        std::cout << "Failed to append tunnel info" << std::endl;
        return false;
    }

    try
    {
        s_mouseEdgeManager.UpdateMonitorInfo(monitorInfoList, s_settingManager.ForceForbidEdge);
    }
    catch (const std::exception& e)
    {
        std::cout << e.what() << std::endl;
        return false;
    }

    s_hookManager.SetMouseMoveCallback([](POINT& pt) { return s_mouseEdgeManager.EdgeTunnelTransport(pt); });

    return s_hookManager.Start();
}

bool MonitorEdgeTunnelManager::Stop()
{
    return s_hookManager.Stop();
}

bool MonitorEdgeTunnelManager::IsStart()
{
    return s_hookManager.IsRunning();
}

void MonitorEdgeTunnelManager::SetKeycodeCallback(unsigned long keyCode, const std::function<bool(unsigned long)>& callback)
{
    s_hookManager.SetKeycodeCallback(keyCode, callback);
}

MonitorInfoList MonitorEdgeTunnelManager::GetMonitorInfoList()
{
    MonitorInfoList monitorInfoList;
    MonitorInfoManager::GetMonitorInfoList(monitorInfoList);
    return monitorInfoList;
}

void MonitorEdgeTunnelManager::SetTunnelInfoList(const TunnelInfoList& tunnelInfoList)
{
    s_settingManager.TunnelInfoList.clear();

    // copy
    for (const auto& tunnelInfo : tunnelInfoList)
    {
        s_settingManager.TunnelInfoList.push_back(std::make_shared<TunnelInfo>(*tunnelInfo));
    }
}

TunnelInfoList MonitorEdgeTunnelManager::GetTunnelInfoList()
{
    // copy
    TunnelInfoList ret;
    for (const auto& tunnelInfo : s_settingManager.TunnelInfoList)
    {
        ret.push_back(std::make_shared<TunnelInfo>(*tunnelInfo));
    }

    return ret;
}

bool MonitorEdgeTunnelManager::IsForceForbidEdge()
{
    return s_settingManager.ForceForbidEdge;
}

void MonitorEdgeTunnelManager::SetForceForbidEdge(bool isForce)
{
    s_settingManager.ForceForbidEdge = isForce;
}

void MonitorEdgeTunnelManager::SaveSetting()
{
    s_settingManager.Save();
}

void MonitorEdgeTunnelManager::LoadSetting()
{
    s_settingManager.Load();
}