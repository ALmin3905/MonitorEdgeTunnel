#pragma once

#include "MonitorInfoManager.h"
#include <functional>

/// <summary>
/// �\����~�X
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
/// �\��E�X�����O�A������ݨD���b���}�o
/// </summary>
class MonitorEdgeTunnelManager
{
public:
    /// <summary>
    /// ���o���
    /// </summary>
    /// <returns></returns>
    static MonitorEdgeTunnelManager& GetInstance();

    /// <summary>
    /// �}�l
    /// </summary>
    /// <returns>�O�_���\</returns>
    bool Start();

    /// <summary>
    /// ����
    /// </summary>
    /// <returns>�O�_���\</returns>
    bool Stop();

    /// <summary>
    /// �O�_���椤
    /// </summary>
    /// <returns>�O�_���椤</returns>
    bool IsStart();

    /// <summary>
    /// �]�w��L���������Callback
    /// </summary>
    /// <param name="keyCode">����(SysCode)�A�ϥΤj�g</param>
    /// <param name="callback">Callback</param>
    void SetKeycodeCallback(unsigned long keyCode, const std::function<bool(unsigned long)>& callback);

    /// <summary>
    /// ���o�ù���T�M�� (�S���aTunnelInfo�F�õL�\��P���@��shared_ptr�A�i��߭ק�)
    /// </summary>
    /// <returns>�ù���T�M��</returns>
    MonitorInfoList GetMonitorInfoList();

    /// <summary>
    /// �]�w�q�D��T�M��
    /// </summary>
    /// <param name="tunnelInfoList">�q�D��T�M��</param>
    void SetTunnelInfoList(const TunnelInfoList& tunnelInfoList);

    /// <summary>
    /// ���o�q�D��T�M�� (�õL�\��P���@��shared_ptr�A�i��߭ק�)
    /// </summary>
    /// <returns>�q�D��T�M��</returns>
    TunnelInfoList GetTunnelInfoList();

    /// <summary>
    /// �O�_�j��T����t�q��
    /// </summary>
    /// <returns>�O�_�j��</returns>
    bool IsForceForbidEdge();

    /// <summary>
    /// �]�w�O�_�j��T����t�q��
    /// </summary>
    /// <param name="isForce">�O�_�j��</param>
    void SetForceForbidEdge(bool isForce);

    /// <summary>
    /// �x�s�]�w
    /// </summary>
    void SaveSetting();

    /// <summary>
    /// ���J�]�w (���m�]�w)
    /// </summary>
    void LoadSetting();

    /// <summary>
    /// ���o���~�T��
    /// </summary>
    /// <returns>��^���~�T��</returns>
    MonitorEdgeTunnelManagerErrorMsg GetErrorMsgCode();

private:
    /// <summary>
    /// �غc�l
    /// </summary>
    MonitorEdgeTunnelManager();

    /// <summary>
    /// �Ѻc�l
    /// </summary>
    ~MonitorEdgeTunnelManager();
};
