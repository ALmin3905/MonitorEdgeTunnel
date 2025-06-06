#pragma once

#include <vector>
#include <memory>
#include <string>
#include <unordered_map>

/// <summary>
/// 邊界類型
/// </summary>
enum class EdgeType : int
{
    /// <summary>
    /// 左
    /// </summary>
    Left = 0,
    /// <summary>
    /// 右
    /// </summary>
    Right,
    /// <summary>
    /// 上
    /// </summary>
    Top,
    /// <summary>
    /// 下
    /// </summary>
    Bottom
};

/// <summary>
/// 範圍類型
/// </summary>
enum class RangeType : int
{
    /// <summary>
    /// 填滿
    /// </summary>
    Full = 0,
    /// <summary>
    /// 相對
    /// </summary>
    Relative,
    /// <summary>
    /// 自訂
    /// </summary>
    Customize
};

/// <summary>
/// 通道資訊
/// </summary>
struct TunnelInfo
{
    /// <summary>
    /// ID
    /// </summary>
    int id;

    /// <summary>
    /// 從哪裡
    /// </summary>
    int from;

    /// <summary>
    /// 到哪裡
    /// </summary>
    int to;

    /// <summary>
    /// 對應的通道資訊ID，
    /// 設定-1會變成禁止通行
    /// </summary>
    int relativeID;

    /// <summary>
    /// 螢幕裝置ID
    /// </summary>
    int displayID;

    /// <summary>
    /// 邊界類型
    /// </summary>
    EdgeType edgeType;

    /// <summary>
    /// 範圍類型
    /// </summary>
    RangeType rangeType;

    /// <summary>
    /// 對應到螢幕座標的"從哪裡" (不用自己設定) 
    /// </summary>
    int displayFrom;

    /// <summary>
    /// 對應到螢幕座標的"到哪裡" (不用自己設定) 
    /// </summary>
    int displayTo;

    /// <summary>
    /// y = ax + b 的 a (不用自己設定)
    /// </summary>
    double a;

    /// <summary>
    /// y = ax + b 的 b (不用自己設定)
    /// </summary>
    double b;

    /// <summary>
    /// 對應的通道邊界 (不用自己設定)
    /// </summary>
    int c;

    /// <summary>
    /// 是否禁止通行 (不用自己設定)
    /// </summary>
    bool forbid;

    /// <summary>
    /// 是否垂直 (不用自己設定)
    /// </summary>
    bool isPerpendicular;
};

/// <summary>
/// 通道資訊清單
/// </summary>
using TunnelInfoList = std::vector<std::shared_ptr<TunnelInfo>>;

/// <summary>
/// 通道資訊清單結構，可放其他清單外的資料
/// </summary>
struct TunnelInfoListStruct
{
    /// <summary>
    /// 通道資訊清單
    /// </summary>
    TunnelInfoList tunnelInfoList;

    /// <summary>
    /// 是否強制禁止螢幕邊緣通行(僅有通道規則才能通行)
    /// </summary>
    bool forceForbidEdge = false;
};

/// <summary>
/// 定義通道清單Map，Key為螢幕資訊清單base64編碼，請使用"MonitorInfoManager::GetMonitorInfoListBase64"取得
/// </summary>
using TunnelInfoListStructMap = std::unordered_map<std::string, TunnelInfoListStruct>;

/// <summary>
/// 螢幕資訊
/// </summary>
struct MonitorInfo
{
    /// <summary>
    /// 螢幕ID
    /// </summary>
    int id;

    /// <summary>
    /// 邊界(上)
    /// </summary>
    int top;

    /// <summary>
    /// 邊界(下)
    /// </summary>
    int bottom;

    /// <summary>
    /// 邊界(左)
    /// </summary>
    int left;

    /// <summary>
    /// 邊界(右)
    /// </summary>
    int right;

    /// <summary>
    /// Windows螢幕縮放比值
    /// </summary>
    double scaling;

    /// <summary>
    /// 通道資訊(上)
    /// </summary>
    TunnelInfoList topTunnel;

    /// <summary>
    /// 通道資訊(下)
    /// </summary>
    TunnelInfoList bottomTunnel;

    /// <summary>
    /// 通道資訊(左)
    /// </summary>
    TunnelInfoList leftTunnel;

    /// <summary>
    /// 通道資訊(右)
    /// </summary>
    TunnelInfoList rightTunnel;
};

/// <summary>
/// 螢幕資訊清單
/// </summary>
using MonitorInfoList = std::vector<std::shared_ptr<MonitorInfo>>;

/// <summary>
/// MonitorInfoManager
/// </summary>
class MonitorInfoManager
{
public:
    /// <summary>
    /// 取得螢幕資訊清單
    /// </summary>
    /// <param name="result">返回結果</param>
    /// <returns>是否成功</returns>
    static bool GetMonitorInfoList(MonitorInfoList& result);

    /// <summary>
    /// 將TunnelInfoList加到MonitorInfoList
    /// </summary>
    /// <param name="monitorInfoList">MonitorInfo清單</param>
    /// <param name="tunnelInfoList">TunnelInfo清單</param>
    /// <returns>是否成功</returns>
    static bool AppendTunnelInfoToMonitorInfo(MonitorInfoList& monitorInfoList, const TunnelInfoList& tunnelInfoList);

    /// <summary>
    /// 取得當前的螢幕資訊清單base64編碼
    /// </summary>
    /// <param name="result">返回螢幕資訊清單base64編碼</param>
    /// <returns>是否成功</returns>
    static bool GetMonitorInfoListBase64(std::string& result);

    /// <summary>
    /// 取得螢幕資訊清單base64編碼
    /// </summary>
    /// <param name="result">返回螢幕資訊清單base64編碼</param>
    /// <param name="monitorInfoList">螢幕資訊清單</param>
    /// <returns>是否成功</returns>
    static bool GetMonitorInfoListBase64(std::string& result, const MonitorInfoList& monitorInfoList);
};

