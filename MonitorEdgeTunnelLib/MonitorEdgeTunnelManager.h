#pragma once

#include "MonitorInfoManager.h"
#include <functional>

/// <summary>
/// 功能錯誤碼
/// </summary>
enum class MonitorEdgeTunnelManagerErrorMsg : int
{
    Null = 0,
    NoSettingFile,
    HookFail,
    GetMonitorInfoFailed,
    NoMonitorInfo,
    AppendTunnelInfoFailed,
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
    /// 開始
    /// </summary>
    /// <returns>是否成功</returns>
    bool Start();

    /// <summary>
    /// 停止
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
    void SetKeycodeCallback(unsigned long keyCode, const std::function<bool(unsigned long)>& callback);

    /// <summary>
    /// 取得螢幕資訊清單 (沒有帶TunnelInfo；並無功能與之共用shared_ptr，可放心修改)
    /// </summary>
    /// <returns>螢幕資訊清單</returns>
    MonitorInfoList GetMonitorInfoList();

    /// <summary>
    /// 設定通道資訊清單
    /// </summary>
    /// <param name="tunnelInfoList">通道資訊清單</param>
    void SetTunnelInfoList(const TunnelInfoList& tunnelInfoList);

    /// <summary>
    /// 取得通道資訊清單 (並無功能與之共用shared_ptr，可放心修改)
    /// </summary>
    /// <returns>通道資訊清單</returns>
    TunnelInfoList GetTunnelInfoList();

    /// <summary>
    /// 是否強制禁止邊緣通行
    /// </summary>
    /// <returns>是否強制</returns>
    bool IsForceForbidEdge();

    /// <summary>
    /// 設定是否強制禁止邊緣通行
    /// </summary>
    /// <param name="isForce">是否強制</param>
    void SetForceForbidEdge(bool isForce);

    /// <summary>
    /// 儲存設定
    /// </summary>
    void SaveSetting();

    /// <summary>
    /// 載入設定 (重置設定)
    /// </summary>
    void LoadSetting();

    /// <summary>
    /// 取得錯誤訊息
    /// </summary>
    /// <returns>返回錯誤訊息</returns>
    MonitorEdgeTunnelManagerErrorMsg GetErrorMsgCode();

private:
    /// <summary>
    /// 建構子
    /// </summary>
    MonitorEdgeTunnelManager();

    /// <summary>
    /// 解構子
    /// </summary>
    ~MonitorEdgeTunnelManager();
};
