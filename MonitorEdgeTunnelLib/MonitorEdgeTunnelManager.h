#pragma once

#include "MonitorInfoManager.h"
#include <functional>
#include <mutex>

/// <summary>
/// �\����~�X
/// </summary>
enum class MonitorEdgeTunnelManagerErrorMsg : int
{
    /// <summary>
    /// �L���~
    /// </summary>
    Null = 0,
    /// <summary>
    /// �S���]�w��
    /// </summary>
    NoSettingFile,
    /// <summary>
    /// hook�\�ಧ�`����
    /// </summary>
    HookFail,
    /// <summary>
    /// �L�k���o�ù���T
    /// </summary>
    GetMonitorInfoFailed,
    /// <summary>
    /// �S���ù���T
    /// </summary>
    NoMonitorInfo,
    /// <summary>
    /// �q�D��T���~
    /// </summary>
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
    /// �}�l(���s�Ұ�)�C
    /// �Y���ѥi�� "GetErrorMsgCode" ���o���~�X
    /// </summary>
    /// <returns>�O�_���\</returns>
    bool Start();

    /// <summary>
    /// ����C
    /// �Y���ѥi�� "GetErrorMsgCode" ���o���~�X
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
    /// <returns>hook�B�椤�|��^false</returns>
    bool SetKeycodeCallback(unsigned long keyCode, const std::function<bool(unsigned long)>& callback);

    /// <summary>
    /// ���o�ù���T�M�� (�S���aTunnelInfo�F�õL�\��P���@��shared_ptr�A�i��߭ק�)
    /// </summary>
    /// <returns>�ù���T�M��</returns>
    MonitorInfoList GetMonitorInfoList();

    /// <summary>
    /// �]�w�q�D��T�M�浲�c
    /// </summary>
    /// <param name="base64Key">�ù���T�M��Base64�s�X</param>
    /// <param name="tunnelInfoListStruct">�q�D��T�M�浲�c</param>
    void SetTunnelInfoListStruct(const std::string& base64Key, const TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// ���o�q�D��T�M�浲�c (�õL�\��P���@��shared_ptr�A�i��߭ק�)
    /// </summary>
    /// <param name="base64Key">�ù���T�M��Base64�s�X</param>
    /// <param name="tunnelInfoListStruct">��^ �q�D��T�M�浲�c</param>
    /// <returns>�O�_����T</returns>
    bool GetTunnelInfoListStruct(const std::string& base64Key, TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// �]�w��e���q�D��T�M�浲�c�C
    /// �Y���ѥi�� "GetErrorMsgCode" ���o���~�X
    /// </summary>
    /// <param name="tunnelInfoListStruct">�q�D��T�M�浲�c</param>
    /// <returns>�O�_���\</returns>
    bool SetCurrentTunnelInfoListStruct(const TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// ���o��e���q�D��T�M�浲�c (�õL�\��P���@��shared_ptr�A�i��߭ק�)�C
    /// �Y���ѥi�� "GetErrorMsgCode" ���o���~�X
    /// </summary>
    /// <param name="tunnelInfoListStruct">��^ �q�D��T�M�浲�c</param>
    /// <returns>�O�_���\</returns>
    bool GetCurrentTunnelInfoListStruct(TunnelInfoListStruct& tunnelInfoListStruct);

    /// <summary>
    /// �x�s�]�w
    /// </summary>
    void SaveSetting();

    /// <summary>
    /// ���J�]�w (���m�]�w)�C
    /// �Y���ѥi�� "GetErrorMsgCode" ���o���~�X
    /// </summary>
    /// <returns>�O�_���\</returns>
    bool LoadSetting();

    /// <summary>
    /// ���o���~�T���C
    /// �Y�h������ϥ����O�\��i��|�ɭP�T���л\�����D (�Ȥ��B�z)
    /// </summary>
    /// <returns>��^���~�T��</returns>
    MonitorEdgeTunnelManagerErrorMsg GetErrorMsgCode();

private:
    /// <summary>
    /// �W��
    /// </summary>
    std::mutex m_mtx;

    /// <summary>
    /// �غc�l
    /// </summary>
    MonitorEdgeTunnelManager();

    /// <summary>
    /// �Ѻc�l
    /// </summary>
    ~MonitorEdgeTunnelManager();
};
