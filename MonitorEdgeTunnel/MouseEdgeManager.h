#pragma once

#include "MonitorInfoManager.h"
#include <Windows.h>
#include <atomic>

/// <summary>
/// MouseEdgeManager 單例模式
/// </summary>
class MouseEdgeManager
{
public:
    /// <summary>
    /// 取得實例
    /// </summary>
    /// <returns>實例</returns>
    static MouseEdgeManager& GetInstance();

    /// <summary>
    /// 更新(初始化)螢幕資訊，有任何錯誤都會throw error，使用時要catch並處理
    /// </summary>
    /// <param name="monitorInfoList">螢幕資訊清單</param>
    /// <param name="isForceForbidEdge">是否強制禁止通行螢幕邊緣，禁止後僅有tunnel規則可以通行邊緣 (預設true)</param>
    void UpdateMonitorInfo(const std::vector<std::shared_ptr<MonitorInfo>>& monitorInfoList, bool isForceForbidEdge = true);

    /// <summary>
    /// 是否初始化
    /// </summary>
    /// <returns>是否初始化</returns>
    bool IsInit();

    /// <summary>
    /// 邊緣通道傳送 (為了最快計算省略很多檢查，所以要自行確認是否初始化)
    /// </summary>
    /// <param name="pt">帶入POINT，會轉換結果</param>
    /// <returns>是否有轉換</returns>
    bool EdgeTunnelTransport(POINT& pt);

private:
    /// <summary>
    /// 螢幕資訊清單
    /// </summary>
    std::vector<std::shared_ptr<MonitorInfo>> m_monitorInfoList;

    /// <summary>
    /// tunnel資訊清單
    /// </summary>
    std::vector<std::shared_ptr<TunnelInfo>> m_tunnelInfoList;

    /// <summary>
    /// 當前滑鼠所在的螢幕資訊
    /// </summary>
    std::shared_ptr<MonitorInfo> m_currMonitorInfo;

    /// <summary>
    /// 是否初始化
    /// </summary>
    std::atomic_bool m_isInit;

    /// <summary>
    /// 建構子
    /// </summary>
    MouseEdgeManager();

    /// <summary>
    /// 解構子
    /// </summary>
    ~MouseEdgeManager();

    /// <summary>
    /// 強制插入禁止通行的tunnel規則至各螢幕資訊的邊緣
    /// </summary>
    void ForceInsertForbidTunnelToAllEdge();

    /// <summary>
    /// 紀錄所有螢幕邊緣tunnel至"m_tunnelInfoList"
    /// </summary>
    void RecordAllTunnelInfo();

    /// <summary>
    /// 插入tunnel資訊至"m_tunnelInfoList"，因為tunnel資訊會照著id放入陣列，所以不要自行放入
    /// </summary>
    /// <param name="tunnelInfo">tunnel資訊</param>
    void InsertTunnelInfo(const std::shared_ptr<TunnelInfo>& tunnelInfo);

    /// <summary>
    /// 檢查tunnel規則是否有效
    /// </summary>
    void CheckTunnelValid();

    /// <summary>
    /// 先計算好tunnel規則轉換後的參數，加速滑鼠座標轉換
    /// </summary>
    /// <param name="tunnelInfo">tunnel資訊</param>
    void CalcTransportParam(std::shared_ptr<TunnelInfo>& tunnelInfo);
};

