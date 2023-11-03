#pragma once

#ifdef MONITOREDGETUNNELDLL_EXPORTS
#define MONITOREDGETUNNELDLL_API __declspec(dllexport)
#else
#define MONITOREDGETUNNELDLL_API __declspec(dllimport)
#endif

extern "C"
{
    /// <summary>
    /// 按鍵Callback
    /// </summary>
    typedef bool (*KeycodeCallback)(unsigned long);

    /// <summary>
    /// C Style MonitorInfo (只暴露需要的變數)
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
    /// C Style TunnelInfo (只暴露需要的變數)
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
    /// 啟動
    /// </summary>
    /// <returns>是否成功</returns>
    MONITOREDGETUNNELDLL_API bool __stdcall Start();

    /// <summary>
    /// 停止
    /// </summary>
    /// <returns>是否成功</returns>
    MONITOREDGETUNNELDLL_API bool __stdcall Stop();

    /// <summary>
    /// 設定按鍵Callback
    /// </summary>
    /// <param name="keyCode">按鍵 (SysCode)</param>
    /// <param name="callback">Callback</param>
    MONITOREDGETUNNELDLL_API void __stdcall SetKeycodeCallback(unsigned long keyCode, const KeycodeCallback callback);

    /// <summary>
    /// 取得螢幕資訊清單
    /// </summary>
    /// <param name="monitorInfoList">(COM) 建立C_MonitorInfo指標，並傳入它的指標。注意此資源的釋放方式，建議使用::CoTaskMemFree()，C#則會自行控管</param>
    /// <param name="length">回傳list length</param>
    MONITOREDGETUNNELDLL_API void __stdcall GetMonitorInfoList(C_MonitorInfo** monitorInfoList, unsigned int* length);

    /// <summary>
    /// 取得tunnel資訊清單
    /// </summary>
    /// <param name="tunnelInfoList">(COM) 建立C_TunnelInfo指標，並傳入它的指標。注意此資源的釋放方式，建議使用::CoTaskMemFree()，C#則會自行控管</param>
    /// <param name="length">回傳list length</param>
    MONITOREDGETUNNELDLL_API void __stdcall GetTunnelInfoList(C_TunnelInfo** tunnelInfoList, unsigned int* length);

    /// <summary>
    /// 設定tunnel資訊清單
    /// </summary>
    /// <param name="tunnelInfoList">C_TunnelInfo清單</param>
    /// <param name="length">list length</param>
    MONITOREDGETUNNELDLL_API void __stdcall SetTunnelInfoList(C_TunnelInfo* tunnelInfoList, unsigned int length);

    /// <summary>
    /// 是否強制禁止邊緣通行
    /// </summary>
    /// <returns>是否強制</returns>
    MONITOREDGETUNNELDLL_API bool __stdcall IsForceForbidEdge();

    /// <summary>
    /// 設定是否強制禁止邊緣通行
    /// </summary>
    /// <param name="isForce">是否強制</param>
    MONITOREDGETUNNELDLL_API void __stdcall SetForceForbidEdge(bool isForce);

    /// <summary>
    /// 儲存設定
    /// </summary>
    MONITOREDGETUNNELDLL_API void __stdcall SaveSetting();
}