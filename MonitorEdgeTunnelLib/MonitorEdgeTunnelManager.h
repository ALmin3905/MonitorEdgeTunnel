#pragma once

#include "MonitorInfoManager.h"
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
    TunnelInfoError
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
    /// 取得螢幕資訊清單 (沒有帶TunnelInfo；並無功能與之共用shared_ptr，可放心修改)
    /// </summary>
    /// <returns>螢幕資訊清單</returns>
    MonitorInfoList GetMonitorInfoList();

    /// <summary>
    /// 設定通道資訊清單結構
    /// </summary>
    /// <param name="base64Key">螢幕資訊清單Base64編碼</param>
    /// <param name="tunnelInfoListStruct">通道資訊清單結構</param>
    void SetTunnelInfoListStruct(const std::string& base64Key, const TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// 取得通道資訊清單結構 (並無功能與之共用shared_ptr，可放心修改)
    /// </summary>
    /// <param name="base64Key">螢幕資訊清單Base64編碼</param>
    /// <param name="tunnelInfoListStruct">返回 通道資訊清單結構</param>
    /// <returns>是否有資訊</returns>
    bool GetTunnelInfoListStruct(const std::string& base64Key, TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// 設定當前的通道資訊清單結構。
    /// 若失敗可用 "GetErrorMsgCode" 取得錯誤碼
    /// </summary>
    /// <param name="tunnelInfoListStruct">通道資訊清單結構</param>
    /// <returns>是否成功</returns>
    bool SetCurrentTunnelInfoListStruct(const TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// 取得當前的通道資訊清單結構 (並無功能與之共用shared_ptr，可放心修改)。
    /// 若失敗可用 "GetErrorMsgCode" 取得錯誤碼
    /// </summary>
    /// <param name="tunnelInfoListStruct">返回 通道資訊清單結構</param>
    /// <returns>是否成功</returns>
    bool GetCurrentTunnelInfoListStruct(TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// 儲存設定
    /// </summary>
    void SaveSetting();

    /// <summary>
    /// 載入設定 (重置設定)。
    /// 若失敗可用 "GetErrorMsgCode" 取得錯誤碼
    /// </summary>
    /// <returns>是否成功</returns>
    bool LoadSetting();

    /// <summary>
    /// 取得錯誤訊息。
    /// 若多執行緒使用類別功能可能會導致訊息覆蓋的問題 (暫不處理)
    /// </summary>
    /// <returns>返回錯誤訊息</returns>
    MonitorEdgeTunnelManagerErrorMsg GetErrorMsgCode();

private:
    /// <summary>
    /// 上鎖
    /// </summary>
    std::mutex m_mtx;

    /// <summary>
    /// 建構子
    /// </summary>
    MonitorEdgeTunnelManager();

    /// <summary>
    /// 解構子
    /// </summary>
    ~MonitorEdgeTunnelManager();
};
