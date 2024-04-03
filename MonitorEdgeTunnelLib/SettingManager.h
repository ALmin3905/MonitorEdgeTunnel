#pragma once

#include "MonitorInfoManager.h"

/// <summary>
/// 設定檔管理。
/// 此類別並無執行緒安全，資料讀寫、存檔、讀檔等請自行掌握。
/// </summary>
class SettingManager
{
public:
    /// <summary>
    /// 通道資訊清單Map #(Load載入、Save儲存；直接存取成員讀寫)
    /// </summary>
    TunnelInfoListStructMap TunnelInfoListStructMap;

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

