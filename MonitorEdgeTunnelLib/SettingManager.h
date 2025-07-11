#pragma once

#include "MonitorInfoManager.h"
#include "ThreadSafeObjectWrapper.h"

/// <summary>
/// 設定檔管理
/// </summary>
class SettingManager
{
public:
    /// <summary>
    /// 通道資訊清單Map (Load載入、Save儲存；直接存取成員讀寫)
    /// <para>須呼叫 get/get_read_only 取得物件鎖定訪問器，在鎖定訪問器存在的其中會主動上讀寫鎖</para>
    /// </summary>
    ThreadSafeObjectWrapper<TunnelInfoListStructMap> TunnelInfoListStructMap;

    /// <summary>
    /// 儲存檔案
    /// <para>請勿在使用 TunnelInfoListStructMap 時呼叫，會產生死鎖 (如果是使用 TunnelInfoListStructMap.get_readonly() 則可以呼叫)</para>
    /// <para>請使用try catch捕捉錯誤</para>
    /// </summary>
    void Save();

    /// <summary>
    /// 載入檔案
    /// <para>請勿在使用 TunnelInfoListStructMap 時呼叫，會產生死鎖</para>
    /// <para>請使用try catch捕捉錯誤</para>
    /// </summary>
    void Load();

public:
    /// <summary>
    /// 建構子
    /// </summary>
    SettingManager();

    /// <summary>
    /// 解構子
    /// </summary>
    ~SettingManager();

    // 禁止複製、移動
    SettingManager(const SettingManager&) = delete;
    SettingManager(SettingManager&&) = delete;
    SettingManager& operator=(const SettingManager&) = delete;
    SettingManager& operator=(SettingManager&&) = delete;
};

