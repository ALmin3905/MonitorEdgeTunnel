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
    /// Log Callback
    /// <param name="int">log level</param>
    /// <param name="const char*">log message</param>
    /// </summary>
    typedef void (*LogCallback)(int, const char*);

    /// <summary>
    /// Display Changed Callback
    /// </summary>
    typedef void(*DisplayChangedCallback)();

    /// <summary>
    /// C Style MonitorInfo (只暴露需要的變數)
    /// </summary>
    typedef struct
    {
        int id;
        int top;
        int bottom;
        int left;
        int right;
        double scaling;
    } C_MonitorInfo;

    /// <summary>
    /// C Style TunnelInfo (只暴露需要的變數)
    /// </summary>
    typedef struct
    {
        int id;
        int from;
        int to;
        int relativeID;
        int displayID;
        int edgeType;
        int rangeType;
    } C_TunnelInfo;

    /// <summary>
    /// 啟動
    /// <para>若失敗可用 "GetErrorMsgCode" 取得錯誤碼</para>
    /// </summary>
    /// <returns>是否成功</returns>
    MONITOREDGETUNNELDLL_API bool __stdcall Start();

    /// <summary>
    /// 停止
    /// <para>若失敗可用 "GetErrorMsgCode" 取得錯誤碼</para>
    /// </summary>
    /// <returns>是否成功</returns>
    MONITOREDGETUNNELDLL_API bool __stdcall Stop();

    /// <summary>
    /// 是否執行中
    /// </summary>
    /// <returns>是否執行中</returns>
    MONITOREDGETUNNELDLL_API bool __stdcall IsStart();

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
    MONITOREDGETUNNELDLL_API void __stdcall GetCurrentTunnelInfoList(C_TunnelInfo** tunnelInfoList, unsigned int* length);

    /// <summary>
    /// 設定tunnel資訊清單
    /// <para>若失敗可用 "GetErrorMsgCode" 取得錯誤碼</para>
    /// </summary>
    /// <param name="tunnelInfoList">C_TunnelInfo清單</param>
    /// <param name="length">list length</param>
    MONITOREDGETUNNELDLL_API bool __stdcall SetCurrentTunnelInfoList(C_TunnelInfo* tunnelInfoList, unsigned int length);

    /// <summary>
    /// 是否強制禁止邊緣通行
    /// </summary>
    /// <returns>是否強制</returns>
    MONITOREDGETUNNELDLL_API bool __stdcall IsCurrentForceForbidEdge();

    /// <summary>
    /// 設定是否強制禁止邊緣通行
    /// </summary>
    /// <param name="isForce">是否強制</param>
    MONITOREDGETUNNELDLL_API void __stdcall SetCurrentForceForbidEdge(bool isForce);

    /// <summary>
    /// 儲存設定
    /// <para>若失敗可用 "GetErrorMsgCode" 取得錯誤碼</para>
    /// </summary>
    MONITOREDGETUNNELDLL_API bool __stdcall SaveSetting();

    /// <summary>
    /// 載入設定 (重新設定)
    /// <para>若失敗可用 "GetErrorMsgCode" 取得錯誤碼</para>
    /// </summary>
    MONITOREDGETUNNELDLL_API bool __stdcall LoadSetting();

    /// <summary>
    /// 取得錯誤訊息碼
    /// </summary>
    /// <returns>錯誤訊息碼</returns>
    MONITOREDGETUNNELDLL_API int __stdcall GetErrorMsgCode();

    /// <summary>
    /// 設定Log Callback
    /// <para>注意!! 注入的log callback需要支援thread safe</para>
    /// </summary>
    MONITOREDGETUNNELDLL_API void __stdcall SetLogCallback(LogCallback callback);

    /// <summary>
    /// 增加螢幕變更事件的callback (WM_DISPLAYCHANGE)
    /// <para>增加事件後記得要手動移除</para>
    /// </summary>
    MONITOREDGETUNNELDLL_API bool __stdcall AddDisplayChangedCallback(DisplayChangedCallback callback);

    /// <summary>
    /// 移除螢幕變更事件的callback (WM_DISPLAYCHANGE)
    /// </summary>
    MONITOREDGETUNNELDLL_API bool __stdcall RemoveDisplayChangedCallback(DisplayChangedCallback callback);
}
