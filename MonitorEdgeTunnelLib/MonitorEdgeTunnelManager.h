#pragma once

#include "MonitorInfoManager.h"
#include "HookManager.h"
#include "SettingManager.h"
#include "MouseEdgeManager.h"
#include "Logger.h"
#include <functional>
#include <mutex>

/// <summary>
/// 功能錯誤碼
/// </summary>
enum class MonitorEdgeTunnelManagerErrorMsg : int
{
    /// <summary>
    /// 無錯誤
    /// </summary>
    Null = 0,
    /// <summary>
    /// 沒有設定檔
    /// </summary>
    NoSettingFile,
    /// <summary>
    /// hook功能異常失敗
    /// </summary>
    HookFail,
    /// <summary>
    /// 無法取得螢幕資訊
    /// </summary>
    GetMonitorInfoFailed,
    /// <summary>
    /// 沒有螢幕資訊
    /// </summary>
    NoMonitorInfo,
    /// <summary>
    /// 通道資訊錯誤
    /// </summary>
    TunnelInfoError,
    /// <summary>
    /// 儲存設定檔失敗
    /// </summary>
    SaveSettingFail,
    /// <summary>
    /// 載入設定檔失敗
    /// </summary>
    LoadSettingFail
};

/// <summary>
/// 功能聚合的類別，有什麼需求都在此開發
/// </summary>
class MonitorEdgeTunnelManager
{
public:
    /// <summary>
    /// 取得實例
    /// </summary>
    /// <returns></returns>
    static MonitorEdgeTunnelManager& GetInstance();

    /// <summary>
    /// 開始(重新啟動)。
    /// 若失敗可用 "GetErrorMsgCode" 取得錯誤碼
    /// </summary>
    /// <returns>是否成功</returns>
    bool Start();

    /// <summary>
    /// 停止。
    /// 若失敗可用 "GetErrorMsgCode" 取得錯誤碼
    /// </summary>
    /// <returns>是否成功</returns>
    bool Stop();

    /// <summary>
    /// 是否執行中
    /// </summary>
    /// <returns>是否執行中</returns>
    bool IsStart();

    /// <summary>
    /// 設定鍵盤按鍵對應的Callback
    /// </summary>
    /// <param name="keyCode">按鍵(SysCode)，使用大寫</param>
    /// <param name="callback">Callback</param>
    /// <returns>hook運行中會返回false</returns>
    bool SetKeycodeCallback(unsigned long keyCode, const std::function<bool(unsigned long)>& callback);

    /// <summary>
    /// 設定通道資訊清單結構
    /// </summary>
    /// <param name="base64Key">螢幕資訊清單Base64編碼</param>
    /// <param name="tunnelInfoListStruct">通道資訊清單結構</param>
    void SetTunnelInfoListStruct(const std::string& base64Key, const TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// 取得通道資訊清單結構
    /// </summary>
    /// <param name="base64Key">螢幕資訊清單Base64編碼</param>
    /// <param name="tunnelInfoListStruct">返回 通道資訊清單結構</param>
    /// <returns>是否有資訊</returns>
    bool GetTunnelInfoListStruct(const std::string& base64Key, TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// 設定當前的通道資訊清單結構
    /// 若失敗可用 "GetErrorMsgCode" 取得錯誤碼
    /// </summary>
    /// <param name="tunnelInfoListStruct">通道資訊清單結構</param>
    /// <returns>是否成功</returns>
    bool SetCurrentTunnelInfoListStruct(const TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// 取得當前的通道資訊清單結構
    /// 若失敗可用 "GetErrorMsgCode" 取得錯誤碼
    /// </summary>
    /// <param name="tunnelInfoListStruct">返回 通道資訊清單結構</param>
    /// <returns>是否成功</returns>
    bool GetCurrentTunnelInfoListStruct(TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// 儲存設定
    /// 若失敗可用 "GetErrorMsgCode" 取得錯誤碼
    /// </summary>
    bool SaveSetting();

    /// <summary>
    /// 載入設定 (重置設定)
    /// 若失敗可用 "GetErrorMsgCode" 取得錯誤碼
    /// </summary>
    /// <returns>是否成功</returns>
    bool LoadSetting();

    /// <summary>
    /// 取得螢幕資訊清單
    /// </summary>
    /// <returns>螢幕資訊清單</returns>
    static MonitorInfoList GetMonitorInfoList();

    /// <summary>
    /// 取得錯誤訊息 (每個執行緒都有自己儲存錯誤訊息的空間，不會被其他執行緒的錯誤訊息覆蓋)
    /// </summary>
    /// <returns>返回錯誤訊息</returns>
    static MonitorEdgeTunnelManagerErrorMsg GetErrorMsgCode();

    /// <summary>
    /// 設定logger的LogCallback
    /// <para>注意!! 注入的log callback需要支援thread safe</para>
    /// </summary>
    static void SetLogCallback(const Logger::LogCallback& logCallback);

private:
    /// <summary>
    /// 建構子
    /// </summary>
    MonitorEdgeTunnelManager();

    /// <summary>
    /// 解構子
    /// </summary>
    ~MonitorEdgeTunnelManager();

    /// <summary>
    /// 上鎖
    /// </summary>
    std::mutex m_mtx;

    /// <summary>
    /// Hook相關功能
    /// </summary>
    HookManager m_hookManager;

    /// <summary>
    /// 儲存設定相關功能
    /// </summary>
    SettingManager m_settingManager;

    /// <summary>
    /// 滑鼠點位在螢幕邊緣座標邏輯轉換相關功能
    /// </summary>
    MouseEdgeManager m_mouseEdgeManager;

    /// <summary>
    /// 錯誤訊息碼
    /// </summary>
    static thread_local MonitorEdgeTunnelManagerErrorMsg g_errorMsgCode;
};
