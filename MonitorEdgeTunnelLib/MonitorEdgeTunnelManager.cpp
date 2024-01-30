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
    s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

    if (!Stop())
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

    if (s_settingManager.TunnelInfoListMap.count(monitorInfoListBase64) &&
        !MonitorInfoManager::AppendTunnelInfoToMonitorInfo(monitorInfoList, s_settingManager.TunnelInfoListMap[monitorInfoListBase64])
    )
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::AppendTunnelInfoFailed;
        return false;
    }

    try
    {
        s_mouseEdgeManager.UpdateMonitorInfo(monitorInfoList, s_settingManager.ForceForbidEdge);
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

void MonitorEdgeTunnelManager::SetTunnelInfoList(const std::string& base64Key, const TunnelInfoList& tunnelInfoList)
{
    // 不存在就建立，存在就清除資料
    if (!s_settingManager.TunnelInfoListMap.count(base64Key))
        s_settingManager.TunnelInfoListMap[base64Key] = {};
    else
        s_settingManager.TunnelInfoListMap[base64Key].clear();

    // copy
    for (const auto& tunnelInfo : tunnelInfoList)
    {
        s_settingManager.TunnelInfoListMap[base64Key].push_back(std::make_shared<TunnelInfo>(*tunnelInfo));
    }
}

TunnelInfoList MonitorEdgeTunnelManager::GetTunnelInfoList(const std::string& base64Key)
{
    // 不存在就返回empty vector
    if (!s_settingManager.TunnelInfoListMap.count(base64Key))
        return {};

    // Get TunnelInfoList
    const TunnelInfoList& tunnelInfoList = s_settingManager.TunnelInfoListMap[base64Key];

    // copy
    TunnelInfoList ret;
    for (const auto& tunnelInfo : tunnelInfoList)
    {
        ret.push_back(std::make_shared<TunnelInfo>(*tunnelInfo));
    }

    return ret;
}

bool MonitorEdgeTunnelManager::SetCurrentTunnelInfoList(const TunnelInfoList& tunnelInfoList)
{
    // 取得當前的螢幕資訊清單Base64編碼
    std::string monitorInfoListBase64;
    if (!MonitorInfoManager::GetMonitorInfoListBase64(monitorInfoListBase64))
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    SetTunnelInfoList(monitorInfoListBase64, tunnelInfoList);

    return true;
}

bool MonitorEdgeTunnelManager::GetCurrentTunnelInfoList(TunnelInfoList& tunnelInfoList)
{
    // init
    tunnelInfoList.clear();

    // 取得當前的螢幕資訊清單Base64編碼
    std::string monitorInfoListBase64;
    if (!MonitorInfoManager::GetMonitorInfoListBase64(monitorInfoListBase64))
    {
        s_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    tunnelInfoList = GetTunnelInfoList(monitorInfoListBase64);

    return true;
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

bool MonitorEdgeTunnelManager::LoadSetting()
{
    try
    {
        s_settingManager.Load();
    }
    catch (const std::exception& e)
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