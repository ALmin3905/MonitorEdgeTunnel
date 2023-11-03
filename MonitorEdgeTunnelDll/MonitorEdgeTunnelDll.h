#pragma once

#ifdef MONITOREDGETUNNELDLL_EXPORTS
#define MONITOREDGETUNNELDLL_API __declspec(dllexport)
#else
#define MONITOREDGETUNNELDLL_API __declspec(dllimport)
#endif

extern "C"
{
    /// <summary>
    /// ����Callback
    /// </summary>
    typedef bool (*KeycodeCallback)(unsigned long);

    /// <summary>
    /// C Style MonitorInfo (�u���S�ݭn���ܼ�)
    /// </summary>
    typedef struct C_MonitorInfo
    {
        int id;
        int top;
        int bottom;
        int left;
        int right;
        double scaling;
    };

    /// <summary>
    /// C Style TunnelInfo (�u���S�ݭn���ܼ�)
    /// </summary>
    typedef struct C_TunnelInfo
    {
        int id;
        int from;
        int to;
        int relativeID;
        int displayID;
        int edgeType;
        int rangeType;
    };

    /// <summary>
    /// �Ұ�
    /// </summary>
    /// <returns>�O�_���\</returns>
    MONITOREDGETUNNELDLL_API bool __stdcall Start();

    /// <summary>
    /// ����
    /// </summary>
    /// <returns>�O�_���\</returns>
    MONITOREDGETUNNELDLL_API bool __stdcall Stop();

    /// <summary>
    /// �]�w����Callback
    /// </summary>
    /// <param name="keyCode">���� (SysCode)</param>
    /// <param name="callback">Callback</param>
    MONITOREDGETUNNELDLL_API void __stdcall SetKeycodeCallback(unsigned long keyCode, const KeycodeCallback callback);

    /// <summary>
    /// ���o�ù���T�M��
    /// </summary>
    /// <param name="monitorInfoList">(COM) �إ�C_MonitorInfo���СA�öǤJ�������СC�`�N���귽������覡�A��ĳ�ϥ�::CoTaskMemFree()�AC#�h�|�ۦ汱��</param>
    /// <param name="length">�^��list length</param>
    MONITOREDGETUNNELDLL_API void __stdcall GetMonitorInfoList(C_MonitorInfo** monitorInfoList, unsigned int* length);

    /// <summary>
    /// ���otunnel��T�M��
    /// </summary>
    /// <param name="tunnelInfoList">(COM) �إ�C_TunnelInfo���СA�öǤJ�������СC�`�N���귽������覡�A��ĳ�ϥ�::CoTaskMemFree()�AC#�h�|�ۦ汱��</param>
    /// <param name="length">�^��list length</param>
    MONITOREDGETUNNELDLL_API void __stdcall GetTunnelInfoList(C_TunnelInfo** tunnelInfoList, unsigned int* length);

    /// <summary>
    /// �]�wtunnel��T�M��
    /// </summary>
    /// <param name="tunnelInfoList">C_TunnelInfo�M��</param>
    /// <param name="length">list length</param>
    MONITOREDGETUNNELDLL_API void __stdcall SetTunnelInfoList(C_TunnelInfo* tunnelInfoList, unsigned int length);

    /// <summary>
    /// �O�_�j��T����t�q��
    /// </summary>
    /// <returns>�O�_�j��</returns>
    MONITOREDGETUNNELDLL_API bool __stdcall IsForceForbidEdge();

    /// <summary>
    /// �]�w�O�_�j��T����t�q��
    /// </summary>
    /// <param name="isForce">�O�_�j��</param>
    MONITOREDGETUNNELDLL_API void __stdcall SetForceForbidEdge(bool isForce);

    /// <summary>
    /// �x�s�]�w
    /// </summary>
    MONITOREDGETUNNELDLL_API void __stdcall SaveSetting();
}