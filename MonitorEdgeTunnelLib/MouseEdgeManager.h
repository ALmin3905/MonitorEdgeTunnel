#pragma once

#include "MonitorInfoManager.h"
#include <Windows.h>
#include <atomic>

/// <summary>
/// MouseEdgeManager ��ҼҦ��C
/// ���\�ର - �p��ù���t�ƹ��y���ಾ�A�D�n���O�p���޿�C
/// �����O�õL������w���A"UpdateMonitorInfo" �ɾ��Цۦ�x���C
/// </summary>
class MouseEdgeManager
{
public:
    /// <summary>
    /// ���o���
    /// </summary>
    /// <returns>���</returns>
    static MouseEdgeManager& GetInstance();

    /// <summary>
    /// ��s(��l��)�ù���T�A��������~���|throw error�A�ϥήɭncatch�óB�z
    /// </summary>
    /// <param name="monitorInfoList">�ù���T�M��</param>
    /// <param name="isForceForbidEdge">�O�_�j��T��q��ù���t�A�T���Ȧ�tunnel�W�h�i�H�q����t (�w�]true)</param>
    void UpdateMonitorInfo(const MonitorInfoList monitorInfoList, bool isForceForbidEdge = true);

    /// <summary>
    /// �O�_��l��
    /// </summary>
    /// <returns>�O�_��l��</returns>
    bool IsInit();

    /// <summary>
    /// ��t�q�D�ǰe (���F�̧֭p��ٲ��ܦh�ˬd�A�ҥH�n�ۦ�T�{�O�_��l��)
    /// </summary>
    /// <param name="pt">�a�JPOINT�A�|�ഫ���G</param>
    /// <returns>�O�_���ഫ</returns>
    bool EdgeTunnelTransport(POINT& pt);

private:
    /// <summary>
    /// �ù���T�M��
    /// </summary>
    MonitorInfoList m_monitorInfoList;

    /// <summary>
    /// tunnel��T�M��
    /// </summary>
    TunnelInfoList m_tunnelInfoList;

    /// <summary>
    /// ��e�ƹ��Ҧb���ù���T
    /// </summary>
    std::shared_ptr<MonitorInfo> m_currMonitorInfo;

    /// <summary>
    /// �O�_��l��
    /// </summary>
    std::atomic_bool m_isInit;

    /// <summary>
    /// �غc�l
    /// </summary>
    MouseEdgeManager();

    /// <summary>
    /// �Ѻc�l
    /// </summary>
    ~MouseEdgeManager();

    /// <summary>
    /// �j��J�T��q�檺tunnel�W�h�ܦU�ù���T����t
    /// </summary>
    void ForceInsertForbidTunnelToAllEdge();

    /// <summary>
    /// �����Ҧ��ù���ttunnel��"m_tunnelInfoList"
    /// </summary>
    void RecordAllTunnelInfo();

    /// <summary>
    /// ���Jtunnel��T��"m_tunnelInfoList"�A�]��tunnel��T�|�ӵ�id��J�}�C�A�ҥH���n�ۦ��J
    /// </summary>
    /// <param name="tunnelInfo">tunnel��T</param>
    void InsertTunnelInfo(const std::shared_ptr<TunnelInfo>& tunnelInfo);

    /// <summary>
    /// �p��from�Bto������display from�Bto
    /// </summary>
    void CalcDisplayFromTo();

    /// <summary>
    /// �ˬdtunnel�W�h�O�_����
    /// </summary>
    void CheckTunnelValid();

    /// <summary>
    /// ���p��ntunnel�W�h�ഫ�᪺�ѼơA�[�t�ƹ��y���ഫ
    /// </summary>
    /// <param name="tunnelInfo">tunnel��T</param>
    void CalcTransportParam(std::shared_ptr<TunnelInfo>& tunnelInfo);
};

