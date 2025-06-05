#include "pch.h"
#include "MonitorEdgeTunnelManager.h"
#include "HookManager.h"
#include "MouseEdgeManager.h"
#include <iostream>

MonitorEdgeTunnelManager& MonitorEdgeTunnelManager::GetInstance()
{
    static MonitorEdgeTunnelManager instance;
    return instance;
}

MonitorEdgeTunnelManager::MonitorEdgeTunnelManager() :
    m_errorMsgCode(MonitorEdgeTunnelManagerErrorMsg::Null)
{
    LoadSetting();
}

MonitorEdgeTunnelManager::~MonitorEdgeTunnelManager()
{

}

bool MonitorEdgeTunnelManager::Start()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    auto tunnelInfoListStructMap = m_settingManager.TunnelInfoListStructMap.get();

    m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

    if (!m_hookManager.Stop())
    {
        m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::HookFail;
        return false;
    }

    MonitorInfoList monitorInfoList;
    if (!MonitorInfoManager::GetMonitorInfoList(monitorInfoList))
    {
        m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::GetMonitorInfoFailed;
        return false;
    }

    if (monitorInfoList.empty())
    {
        m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    std::string monitorInfoListBase64;
    if (!MonitorInfoManager::GetMonitorInfoListBase64(monitorInfoListBase64, monitorInfoList))
    {
        m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    if (tunnelInfoListStructMap->count(monitorInfoListBase64) &&
        !MonitorInfoManager::AppendTunnelInfoToMonitorInfo(monitorInfoList, (*tunnelInfoListStructMap)[monitorInfoListBase64].tunnelInfoList)
    )
    {
        m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::TunnelInfoError;
        return false;
    }

    try
    {
        m_mouseEdgeManager.UpdateMonitorInfo(monitorInfoList, (*tunnelInfoListStructMap)[monitorInfoListBase64].forceForbidEdge);
    }
    catch (const std::exception& e)
    {
        m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::TunnelInfoError;
        std::cout << e.what() << std::endl;
        return false;
    }

    m_hookManager.SetMouseMoveCallback(std::bind(&MouseEdgeManager::EdgeTunnelTransport, &m_mouseEdgeManager, std::placeholders::_1));

    if (!m_hookManager.Start())
    {
        m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::HookFail;
        return false;
    }

    return true;
}

bool MonitorEdgeTunnelManager::Stop()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

    if (!m_hookManager.Stop())
    {
        m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::HookFail;
        return false;
    }

    return true;
}

bool MonitorEdgeTunnelManager::IsStart()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    return m_hookManager.IsRunning();
}

bool MonitorEdgeTunnelManager::SetKeycodeCallback(unsigned long keyCode, const std::function<bool(unsigned long)>& callback)
{
    std::lock_guard<std::mutex> lock(m_mtx);

    return m_hookManager.SetSysKeycodeCallback(keyCode, callback);
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

    auto tunnelInfoListStructMap = m_settingManager.TunnelInfoListStructMap.get();

    // 清除資料
    (*tunnelInfoListStructMap)[base64Key].tunnelInfoList.clear();

    // copy tunnelInfoList
    for (const auto& tunnelInfo : tunnelInfoListStruct.tunnelInfoList)
    {
        (*tunnelInfoListStructMap)[base64Key].tunnelInfoList.push_back(std::make_shared<TunnelInfo>(*tunnelInfo));
    }

    // forceForbidEdge
    (*tunnelInfoListStructMap)[base64Key].forceForbidEdge = tunnelInfoListStruct.forceForbidEdge;
}

bool MonitorEdgeTunnelManager::GetTunnelInfoListStruct(const std::string& base64Key, TunnelInfoListStruct& tunnelInfoListStruct)
{
    std::lock_guard<std::mutex> lock(m_mtx);

    auto tunnelInfoListStructMap = m_settingManager.TunnelInfoListStructMap.get_readonly();

    // init
    tunnelInfoListStruct.tunnelInfoList.clear();
    tunnelInfoListStruct.forceForbidEdge = false;

    // 不存在就返回 false
    if (!tunnelInfoListStructMap->count(base64Key))
        return false;

    // copy tunnelInfoList
    TunnelInfoList tunnelInfoList;
    {
        for (const auto& tunnelInfo : tunnelInfoListStructMap->at(base64Key).tunnelInfoList)
        {
            tunnelInfoList.push_back(std::make_shared<TunnelInfo>(*tunnelInfo));
        }
    }

    tunnelInfoListStruct.tunnelInfoList = std::move(tunnelInfoList);
    tunnelInfoListStruct.forceForbidEdge = tunnelInfoListStructMap->at(base64Key).forceForbidEdge;

    return true;
}

bool MonitorEdgeTunnelManager::SetCurrentTunnelInfoListStruct(const TunnelInfoListStruct& tunnelInfoListStruct)
{
    // 取得當前的螢幕資訊清單Base64編碼
    std::string monitorInfoListBase64;
    if (!MonitorInfoManager::GetMonitorInfoListBase64(monitorInfoListBase64))
    {
        m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    SetTunnelInfoListStruct(monitorInfoListBase64, tunnelInfoListStruct);

    return true;
}

bool MonitorEdgeTunnelManager::GetCurrentTunnelInfoListStruct(TunnelInfoListStruct& tunnelInfoListStruct)
{
    // 取得當前的螢幕資訊清單Base64編碼
    std::string monitorInfoListBase64;
    if (!MonitorInfoManager::GetMonitorInfoListBase64(monitorInfoListBase64))
    {
        m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    return GetTunnelInfoListStruct(monitorInfoListBase64, tunnelInfoListStruct);
}

void MonitorEdgeTunnelManager::SaveSetting()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    m_settingManager.Save();
}

bool MonitorEdgeTunnelManager::LoadSetting()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    try
    {
        m_settingManager.Load();
    }
    catch (...)
    {
        m_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoSettingFile;

        return false;
    }

    return true;
}

MonitorEdgeTunnelManagerErrorMsg MonitorEdgeTunnelManager::GetErrorMsgCode()
{
    return m_errorMsgCode;
}
