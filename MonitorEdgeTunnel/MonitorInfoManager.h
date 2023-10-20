#pragma once

#include <vector>
#include <memory>

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
/// 通道資訊
/// </summary>
struct TunnelInfo
{
    /// <summary>
    /// ID
    /// </summary>
    int id;

    /// <summary>
    /// 邊界類型
    /// </summary>
    EdgeType type;

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
    /// 螢幕裝置ID (不用自己設定)
    /// </summary>
    int deviceID;

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
};

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
    std::vector<std::shared_ptr<TunnelInfo>> topTunnel;

    /// <summary>
    /// 通道資訊(下)
    /// </summary>
    std::vector<std::shared_ptr<TunnelInfo>> bottomTunnel;

    /// <summary>
    /// 通道資訊(左)
    /// </summary>
    std::vector<std::shared_ptr<TunnelInfo>> leftTunnel;

    /// <summary>
    /// 通道資訊(右)
    /// </summary>
    std::vector<std::shared_ptr<TunnelInfo>> rightTunnel;
};

class MonitorInfoManager
{
public:
    /// <summary>
    /// 取得螢幕資訊清單
    /// </summary>
    /// <param name="result">返回結果</param>
    /// <returns>是否成功</returns>
    static bool GetMonitorInfoList(std::vector<std::shared_ptr<MonitorInfo>>& result);
};

