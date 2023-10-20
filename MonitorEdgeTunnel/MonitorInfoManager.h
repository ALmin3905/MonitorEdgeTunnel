#pragma once

#include <vector>
#include <memory>

/// <summary>
/// �������
/// </summary>
enum class EdgeType : int
{
    /// <summary>
    /// ��
    /// </summary>
    Left = 0,
    /// <summary>
    /// �k
    /// </summary>
    Right,
    /// <summary>
    /// �W
    /// </summary>
    Top,
    /// <summary>
    /// �U
    /// </summary>
    Bottom
};

/// <summary>
/// �q�D��T
/// </summary>
struct TunnelInfo
{
    /// <summary>
    /// ID
    /// </summary>
    int id;

    /// <summary>
    /// �������
    /// </summary>
    EdgeType type;

    /// <summary>
    /// �q����
    /// </summary>
    int from;

    /// <summary>
    /// �����
    /// </summary>
    int to;

    /// <summary>
    /// �������q�D��TID�A
    /// �]�w-1�|�ܦ��T��q��
    /// </summary>
    int relativeID;

    /// <summary>
    /// �ù��˸mID (���Φۤv�]�w)
    /// </summary>
    int deviceID;

    /// <summary>
    /// y = ax + b �� a (���Φۤv�]�w)
    /// </summary>
    double a;

    /// <summary>
    /// y = ax + b �� b (���Φۤv�]�w)
    /// </summary>
    double b;

    /// <summary>
    /// �������q�D��� (���Φۤv�]�w)
    /// </summary>
    int c;

    /// <summary>
    /// �O�_�T��q�� (���Φۤv�]�w)
    /// </summary>
    bool forbid;
};

struct MonitorInfo
{
    /// <summary>
    /// �ù�ID
    /// </summary>
    int id;

    /// <summary>
    /// ���(�W)
    /// </summary>
    int top;

    /// <summary>
    /// ���(�U)
    /// </summary>
    int bottom;

    /// <summary>
    /// ���(��)
    /// </summary>
    int left;

    /// <summary>
    /// ���(�k)
    /// </summary>
    int right;

    /// <summary>
    /// Windows�ù��Y����
    /// </summary>
    double scaling;

    /// <summary>
    /// �q�D��T(�W)
    /// </summary>
    std::vector<std::shared_ptr<TunnelInfo>> topTunnel;

    /// <summary>
    /// �q�D��T(�U)
    /// </summary>
    std::vector<std::shared_ptr<TunnelInfo>> bottomTunnel;

    /// <summary>
    /// �q�D��T(��)
    /// </summary>
    std::vector<std::shared_ptr<TunnelInfo>> leftTunnel;

    /// <summary>
    /// �q�D��T(�k)
    /// </summary>
    std::vector<std::shared_ptr<TunnelInfo>> rightTunnel;
};

class MonitorInfoManager
{
public:
    /// <summary>
    /// ���o�ù���T�M��
    /// </summary>
    /// <param name="result">��^���G</param>
    /// <returns>�O�_���\</returns>
    static bool GetMonitorInfoList(std::vector<std::shared_ptr<MonitorInfo>>& result);
};

