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

    MonitorEdgeTunnelManagerErrorMsg s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;
}

/*static*/ MonitorEdgeTunnelManager& MonitorEdgeTunnelManager::GetInstance()
{
    static MonitorEdgeTunnelManager instance;
    return instance;
}

MonitorEdgeTunnelManager::MonitorEdgeTunnelManager()
{
    LoadSetting();
}

MonitorEdgeTunnelManager::~MonitorEdgeTunnelManager()
{

}

bool MonitorEdgeTunnelManager::Start()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

    if (!s_hookManager.Stop())
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::HookFail;
        return false;
    }

    MonitorInfoList monitorInfoList;
    if (!MonitorInfoManager::GetMonitorInfoList(monitorInfoList))
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::GetMonitorInfoFailed;
        return false;
    }

    if (monitorInfoList.empty())
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    std::string monitorInfoListBase64;
    if (!MonitorInfoManager::GetMonitorInfoListBase64(monitorInfoListBase64, monitorInfoList))
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    if (s_settingManager.TunnelInfoListStructMap.count(monitorInfoListBase64) &&
        !MonitorInfoManager::AppendTunnelInfoToMonitorInfo(monitorInfoList, s_settingManager.TunnelInfoListStructMap[monitorInfoListBase64].tunnelInfoList)
    )
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::TunnelInfoError;
        return false;
    }

    try
    {
        s_mouseEdgeManager.UpdateMonitorInfo(monitorInfoList, s_settingManager.TunnelInfoListStructMap[monitorInfoListBase64].forceForbidEdge);
    }
    catch (const std::exception& e)
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::TunnelInfoError;
        std::cout << e.what() << std::endl;
        return false;
    }

    s_hookManager.SetMouseMoveCallback([](POINT& pt) { return s_mouseEdgeManager.EdgeTunnelTransport(pt); });

    if (!s_hookManager.Start())
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::HookFail;
        return false;
    }

    return true;
}

bool MonitorEdgeTunnelManager::Stop()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

    if (!s_hookManager.Stop())
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::HookFail;
        return false;
    }

    return true;
}

bool MonitorEdgeTunnelManager::IsStart()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    return s_hookManager.IsRunning();
}

bool MonitorEdgeTunnelManager::SetKeycodeCallback(unsigned long keyCode, const std::function<bool(unsigned long)>& callback)
{
    std::lock_guard<std::mutex> lock(m_mtx);

    return s_hookManager.SetKeycodeCallback(keyCode, callback);
}

MonitorInfoList MonitorEdgeTunnelManager::GetMonitorInfoList()
{
    MonitorInfoList monitorInfoList;
    MonitorInfoManager::GetMonitorInfoList(monitorInfoList);
    return monitorInfoList;
}

void MonitorEdgeTunnelManager::SetTunnelInfoListStruct(const std::string& base64Key, const TunnelInfoListStruct& tunnelInfoListStruct)
{
    std::lock_guard<std::mutex> lock(m_mtx);

    // 清除資料
    s_settingManager.TunnelInfoListStructMap[base64Key] = {};

    // copy tunnelInfoList
    for (const auto& tunnelInfo : tunnelInfoListStruct.tunnelInfoList)
    {
        s_settingManager.TunnelInfoListStructMap[base64Key].tunnelInfoList.push_back(std::make_shared<TunnelInfo>(*tunnelInfo));
    }

    // forceForbidEdge
    s_settingManager.TunnelInfoListStructMap[base64Key].forceForbidEdge = tunnelInfoListStruct.forceForbidEdge;
}

bool MonitorEdgeTunnelManager::GetTunnelInfoListStruct(const std::string& base64Key, TunnelInfoListStruct& tunnelInfoListStruct)
{
    std::lock_guard<std::mutex> lock(m_mtx);

    // 不存在就返回 false
    if (!s_settingManager.TunnelInfoListStructMap.count(base64Key))
        return false;

    // copy tunnelInfoList
    TunnelInfoList tunnelInfoList;
    {
        for (const auto& tunnelInfo : s_settingManager.TunnelInfoListStructMap[base64Key].tunnelInfoList)
        {
            tunnelInfoList.push_back(std::make_shared<TunnelInfo>(*tunnelInfo));
        }
    }

    tunnelInfoListStruct.tunnelInfoList = std::move(tunnelInfoList);
    tunnelInfoListStruct.forceForbidEdge = s_settingManager.TunnelInfoListStructMap[base64Key].forceForbidEdge;

    return true;
}

bool MonitorEdgeTunnelManager::SetCurrentTunnelInfoListStruct(const TunnelInfoListStruct& tunnelInfoListStruct)
{
    // 取得當前的螢幕資訊清單Base64編碼
    std::string monitorInfoListBase64;
    if (!MonitorInfoManager::GetMonitorInfoListBase64(monitorInfoListBase64))
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    SetTunnelInfoListStruct(monitorInfoListBase64, tunnelInfoListStruct);

    return true;
}

bool MonitorEdgeTunnelManager::GetCurrentTunnelInfoListStruct(TunnelInfoListStruct& tunnelInfoListStruct)
{
    // init
    tunnelInfoListStruct.tunnelInfoList.clear();
    tunnelInfoListStruct.forceForbidEdge = false;

    // 取得當前的螢幕資訊清單Base64編碼
    std::string monitorInfoListBase64;
    if (!MonitorInfoManager::GetMonitorInfoListBase64(monitorInfoListBase64))
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    return GetTunnelInfoListStruct(monitorInfoListBase64, tunnelInfoListStruct);
}

void MonitorEdgeTunnelManager::SaveSetting()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    s_settingManager.Save();
}

bool MonitorEdgeTunnelManager::LoadSetting()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    try
    {
        s_settingManager.Load();
    }
    catch (...)
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoSettingFile;

        return false;
    }

    return true;
}

MonitorEdgeTunnelManagerErrorMsg MonitorEdgeTunnelManager::GetErrorMsgCode()
{
    return s_errorMsgCode;
}