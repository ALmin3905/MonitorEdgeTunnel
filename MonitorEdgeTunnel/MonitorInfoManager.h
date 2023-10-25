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
/// �d������
/// </summary>
enum class RangeType : int
{
    /// <summary>
    /// ��
    /// </summary>
    Full = 0,
    /// <summary>
    /// �۹�
    /// </summary>
    Relative,
    /// <summary>
    /// �ۭq
    /// </summary>
    Customize
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
    /// �ù��˸mID
    /// </summary>
    int displayID;

    /// <summary>
    /// �������
    /// </summary>
    EdgeType edgeType;

    /// <summary>
    /// �d������
    /// </summary>
    RangeType rangeType;

    /// <summary>
    /// ������ù��y�Ъ�"�q����" (���Φۤv�]�w) 
    /// </summary>
    int displayFrom;

    /// <summary>
    /// ������ù��y�Ъ�"�����" (���Φۤv�]�w) 
    /// </summary>
    int displayTo;

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

    /// <summary>
    /// �O�_���� (���Φۤv�]�w)
    /// </summary>
    bool isPerpendicular;
};

/// <summary>
/// �q�D��T�M��
/// </summary>
using TunnelInfoList = std::vector<std::shared_ptr<TunnelInfo>>;

/// <summary>
/// �ù���T
/// </summary>
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
    TunnelInfoList topTunnel;

    /// <summary>
    /// �q�D��T(�U)
    /// </summary>
    TunnelInfoList bottomTunnel;

    /// <summary>
    /// �q�D��T(��)
    /// </summary>
    TunnelInfoList leftTunnel;

    /// <summary>
    /// �q�D��T(�k)
    /// </summary>
    TunnelInfoList rightTunnel;
};

/// <summary>
/// �ù���T�M��
/// </summary>
using MonitorInfoList = std::vector<std::shared_ptr<MonitorInfo>>;

/// <summary>
/// MonitorInfoManager
/// </summary>
class MonitorInfoManager
{
public:
    /// <summary>
    /// ���o�ù���T�M��
    /// </summary>
    /// <param name="result">��^���G</param>
    /// <returns>�O�_���\</returns>
    static bool GetMonitorInfoList(MonitorInfoList& result);

    /// <summary>
    /// �NTunnelInfoList�[��MonitorInfoList
    /// </summary>
    /// <param name="monitorInfoList">MonitorInfo�M��</param>
    /// <param name="tunnelInfoList">TunnelInfo�M��</param>
    /// <returns></returns>
    static bool AppendTunnelInfoToMonitorInfo(MonitorInfoList& monitorInfoList, TunnelInfoList& tunnelInfoList);
};

