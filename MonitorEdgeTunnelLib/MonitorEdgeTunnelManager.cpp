#include "pch.h"
#include "MonitorEdgeTunnelManager.h"
#include <iostream>

thread_local MonitorEdgeTunnelManagerErrorMsg MonitorEdgeTunnelManager::g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

MonitorEdgeTunnelManager& MonitorEdgeTunnelManager::GetInstance()
{
    static MonitorEdgeTunnelManager instance;
    return instance;
}

MonitorEdgeTunnelManager::MonitorEdgeTunnelManager()
{
    m_hookManager.SetMouseMoveCallback(std::bind(&MouseEdgeManager::EdgeTunnelTransport, &m_mouseEdgeManager, std::placeholders::_1));

    LoadSetting();
}

MonitorEdgeTunnelManager::~MonitorEdgeTunnelManager()
{

}

bool MonitorEdgeTunnelManager::Start()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    // 取得setting資料 (會上讀鎖，解構自動解鎖)
    auto tunnelInfoListStructMap = m_settingManager.TunnelInfoListStructMap.get_readonly();

    g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

    // 先停止hook (確保修改資料期間不會產生 race condition)
    if (!m_hookManager.Stop())
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Failed to stop hook");
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::HookFail;
        return false;
    }

    // 取得螢幕資訊清單
    MonitorInfoList monitorInfoList;
    if (!MonitorInfoManager::GetMonitorInfoList(monitorInfoList))
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Failed to get monitor info list");
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::GetMonitorInfoFailed;
        return false;
    }

    // 如果沒有螢幕資訊，則返回錯誤
    if (monitorInfoList.empty())
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "No monitor info available");
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    // 取得螢幕資訊清單的Base64編碼
    std::string monitorInfoListBase64;
    if (!MonitorInfoManager::GetMonitorInfoListBase64(monitorInfoListBase64, monitorInfoList))
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Failed to get monitor info list base64 encoding");
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    // 如果setting資料有tunnel資訊，則將其附加到螢幕資訊清單中
    if (tunnelInfoListStructMap->count(monitorInfoListBase64) &&
        !MonitorInfoManager::AppendTunnelInfoToMonitorInfo(monitorInfoList, (*tunnelInfoListStructMap).at(monitorInfoListBase64).tunnelInfoList)
    )
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Failed to append tunnel info to monitor info list");
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::TunnelInfoError;
        return false;
    }

    // 更新資訊
    try
    {
        m_mouseEdgeManager.UpdateMonitorInfo(monitorInfoList, (*tunnelInfoListStructMap).at(monitorInfoListBase64).forceForbidEdge);
    }
    catch (const std::exception& e)
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Failed to update monitor info, Error Message: " + e.what());
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::TunnelInfoError;
        return false;
    }

    // 啟動hook
    if (!m_hookManager.Start())
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Failed to start hook");
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::HookFail;
        return false;
    }

    return true;
}

bool MonitorEdgeTunnelManager::Stop()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

    if (!m_hookManager.Stop())
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Failed to stop hook");
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::HookFail;
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
    std::lock_guard<std::mutex> lock(m_mtx);

    g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

    // 取得當前的螢幕資訊清單Base64編碼
    std::string monitorInfoListBase64;
    if (!MonitorInfoManager::GetMonitorInfoListBase64(monitorInfoListBase64))
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Failed to get monitor info list base64 encoding");
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    SetTunnelInfoListStruct(monitorInfoListBase64, tunnelInfoListStruct);

    return true;
}

bool MonitorEdgeTunnelManager::GetCurrentTunnelInfoListStruct(TunnelInfoListStruct& tunnelInfoListStruct)
{
    std::lock_guard<std::mutex> lock(m_mtx);

    g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

    // 取得當前的螢幕資訊清單Base64編碼
    std::string monitorInfoListBase64;
    if (!MonitorInfoManager::GetMonitorInfoListBase64(monitorInfoListBase64))
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Failed to get monitor info list base64 encoding");
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::NoMonitorInfo;
        return false;
    }

    return GetTunnelInfoListStruct(monitorInfoListBase64, tunnelInfoListStruct);
}

bool MonitorEdgeTunnelManager::SaveSetting()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

    try
    {
        m_settingManager.Save();
    }
    catch (...)
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Failed to save setting file");
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::SaveSettingFail;
        return false;
    }

    return true;
}

bool MonitorEdgeTunnelManager::LoadSetting()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::Null;

    try
    {
        m_settingManager.Load();
    }
    catch (...)
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Failed to load setting file");
        g_errorMsgCode = MonitorEdgeTunnelManagerErrorMsg::LoadSettingFail;
        return false;
    }

    return true;
}

MonitorEdgeTunnelManagerErrorMsg MonitorEdgeTunnelManager::GetErrorMsgCode()
{
    return g_errorMsgCode;
}

void MonitorEdgeTunnelManager::SetLogCallback(const Logger::LogCallback& logCallback)
{
    Logger::SetLogCallback(logCallback);
}
