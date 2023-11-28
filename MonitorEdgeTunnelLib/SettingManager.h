#pragma once

#include <string>
#include <unordered_map>
#include "MonitorInfoManager.h"

/// <summary>
/// 定義通道清單Map，Key為螢幕資訊清單base64編碼，請使用"MonitorInfoManager::GetMonitorInfoListBase64"取得
/// </summary>
typedef std::unordered_map<std::string, TunnelInfoList> TunnelInfoListMap;

/// <summary>
/// 設定檔管理
/// </summary>
class SettingManager
{
public:
    /// <summary>
    /// 通道資訊清單Map #(Load載入、Save儲存；直接存取成員讀寫)
    /// </summary>
    TunnelInfoListMap TunnelInfoListMap;

    /// <summary>
    /// 是否強制禁止螢幕邊緣通行(僅有通道規則才能通行) #(Load載入、Save儲存；直接存取成員讀寫)
    /// </summary>
    bool ForceForbidEdge;

    /// <summary>
    /// 取得實例
    /// </summary>
    /// <returns>實例</returns>
    static SettingManager& GetInstance();

    /// <summary>
    /// 儲存檔案
    /// </summary>
    void Save();

    /// <summary>
    /// 載入檔案
    /// </summary>
    void Load();

private:
    /// <summary>
    /// 建構子
    /// </summary>
    SettingManager();

    /// <summary>
    /// 解構子
    /// </summary>
    ~SettingManager();
};

