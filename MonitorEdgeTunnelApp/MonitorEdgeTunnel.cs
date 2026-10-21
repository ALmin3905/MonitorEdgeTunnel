using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MonitorEdgeTunnelApp
{
    /// <summary>原生螢幕資訊的循序配置資料，座標為螢幕邊界。</summary>
    public struct MonitorInfo
    {
        /// <summary>資料識別碼。</summary>
        public int id { get; set; }
        /// <summary>螢幕上邊界。</summary>
        public int top { get; set; }
        /// <summary>螢幕下邊界。</summary>
        public int bottom { get; set; }
        /// <summary>螢幕左邊界。</summary>
        public int left { get; set; }
        /// <summary>螢幕右邊界。</summary>
        public int right { get; set; }
        /// <summary>螢幕縮放比。</summary>
        /// <remarks>顯示用 Windows 有效 DPI / 96，例如 175% 為 1.75；0 代表未知，與原生滑鼠換算係數分開。</remarks>
        public double scaling { get; set; }
    }

    /// <summary>傳入及取回原生通道設定的循序配置資料。</summary>
    public struct TunnelInfo
    {
        /// <summary>資料識別碼。</summary>
        public int id { get; set; }
        /// <summary>通道起點或相對起點位移。</summary>
        public int from { get; set; }
        /// <summary>通道終點位移或自訂長度。</summary>
        public int to { get; set; }
        /// <summary>目標通道 ID；-1 代表禁止通行。</summary>
        public int relativeID { get; set; }
        /// <summary>通道所屬的螢幕 ID。</summary>
        public int displayID { get; set; }
        /// <summary>通道所在的螢幕邊緣。</summary>
        public EdgeType edgeType { get; set; }
        /// <summary>通道範圍的解讀方式。</summary>
        public RangeType rangeType { get; set; }
    }

    public enum EdgeType : int
    {
        Left = 0,
        Right,
        Top,
        Bottom
    }

    public enum RangeType : int
    {
        Full = 0,
        Relative,
        Customize
    }

    public enum MonitorEdgeTunnelErrorMsg : int
    {
        Null = 0,
        NoSettingInfo,
        HookFail,
        GetMonitorInfoFailed,
        NoMonitorInfo,
        TunnelInfoError,
        SaveSettingFail,
        LoadSettingFail
    }

    /// <summary>
    /// MonitorEdgeTunnelErrorMsg轉換工具
    /// </summary>
    public static class MonitorEdgeTunnelErrorMsgConvertor
    {
        /// <summary>錯誤碼對應的介面提示文字。</summary>
        private static readonly Dictionary<MonitorEdgeTunnelErrorMsg, string> ErrorMsgStrDict;

        #region -- Public Methods --

        /// <summary>
        /// 將MonitorEdgeTunnelErrorMsg轉換成對應要顯示的字串
        /// </summary>
        /// <param name="msg">MonitorEdgeTunnelErrorMsg</param>
        /// <returns>要顯示的字串</returns>
        public static string ToErrorMsgString(MonitorEdgeTunnelErrorMsg msg)
        {
            return ErrorMsgStrDict[msg];
        }

        #endregion

        #region -- Private Methods --

        /// <summary>建立錯誤碼文字表，供 ToErrorMsgString 查詢。</summary>
        static MonitorEdgeTunnelErrorMsgConvertor()
        {
            ErrorMsgStrDict = new Dictionary<MonitorEdgeTunnelErrorMsg, string>() {
                {MonitorEdgeTunnelErrorMsg.Null, "" },
                {MonitorEdgeTunnelErrorMsg.NoSettingInfo, "沒有通道設定資訊" },
                {MonitorEdgeTunnelErrorMsg.HookFail, "應用程式嚴重錯誤，請重啟試試" },
                {MonitorEdgeTunnelErrorMsg.GetMonitorInfoFailed, "取得螢幕資訊失敗" },
                {MonitorEdgeTunnelErrorMsg.NoMonitorInfo, "沒有螢幕資訊" },
                {MonitorEdgeTunnelErrorMsg.TunnelInfoError, "通道資訊錯誤，請確認是否符合規則" },
                {MonitorEdgeTunnelErrorMsg.SaveSettingFail, "儲存設定檔案失敗" },
                {MonitorEdgeTunnelErrorMsg.LoadSettingFail, "讀取設定檔案失敗" }
            };
        }

        #endregion
    }

    /// <summary>管理原生服務、回呼生命週期與跨執行緒的 DLL 呼叫。</summary>
    public class MonitorEdgeTunnel
    {
        public enum LogLevel : int
        {
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3,
            Fatal = 4
        }

        /// <summary>系統按鍵回呼；keycode 為按鍵碼，回傳 true 會截斷後續 Hook。</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool KeycodeCallbackDel(ulong keycode);

        /// <summary>原生日誌回呼；level 為等級，message 為 UTF-8 字串，回呼須支援多執行緒。</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void _LogCallbackDel(int level, IntPtr message);

        /// <summary>受控日誌接收者；level 為等級，message 為內容，可能由任意執行緒呼叫。</summary>
        public delegate void LogCallbackDel(LogLevel level, string message);

        /// <summary>由原生訊息執行緒呼叫的無參數螢幕變更代理。</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void _DisplayChangedCallbackDel();

        /// <summary>由原生訊息執行緒通知的無參數受控回呼。</summary>
        public delegate void DisplayChangedCallbackDel();

        /// <summary>延遲建立且跨執行緒共用的原生服務介面。</summary>
        private static readonly Lazy<MonitorEdgeTunnel> lazy = new Lazy<MonitorEdgeTunnel>(() => new MonitorEdgeTunnel());

        /// <summary>保留鍵盤回呼代理的強參考，避免原生函數指標失效。</summary>
        private readonly Dictionary<ulong, KeycodeCallbackDel> keycodeCallbackDict = new Dictionary<ulong, KeycodeCallbackDel>();

        /// <summary>轉換原生日誌字串的固定代理。</summary>
        private readonly _LogCallbackDel _logCallback = null;

        /// <summary>接收任意原生工作執行緒日誌的受控回呼。</summary>
        private LogCallbackDel logCallback = null;

        /// <summary>保留螢幕變更原生回呼的固定代理。</summary>
        private readonly _DisplayChangedCallbackDel _displayChangedCallback = null;

        /// <summary>由原生訊息執行緒通知的受控訂閱者。</summary>
        private DisplayChangedCallbackDel displayChangedCallback = null;

        #region -- Public Methods --

        /// <summary>取得共用的原生服務介面。</summary>
        public static MonitorEdgeTunnel Instance
        {
            get
            {
                return lazy.Value;
            }
        }

        /// <summary>依目前設定啟動或重啟原生 Hook；呼叫執行緒會等待初始化完成。</summary>
        /// <returns>是否成功啟動。</returns>
        public bool Start()
        {
            return StartImpl();
        }

        /// <summary>停止通道功能並取消重試，保留螢幕變更監聽服務。</summary>
        /// <returns>Hook 是否已停止。</returns>
        public bool Stop()
        {
            return StopImpl();
        }

        /// <summary>停止所有原生背景執行緒；應在 UI 結束且解除訂閱後、卸載 DLL 前呼叫。</summary>
        /// <returns>所有背景服務是否已結束；不可從原生回呼執行緒呼叫。</returns>
        public bool Shutdown()
        {
            return ShutdownImpl();
        }

        /// <summary>查詢原生 Hook 的實際執行狀態。</summary>
        /// <returns>是否正在執行。</returns>
        public bool IsStart()
        {
            return IsStartImpl();
        }

        /// <summary>註冊系統按鍵回呼並保留代理；回呼由原生 Hook 執行緒執行。</summary>
        /// <param name="keycode">Windows 系統按鍵碼。</param>
        /// <param name="callback">回傳 true 時截斷後續 Hook 的回呼。</param>
        /// <returns>原生註冊是否成功。</returns>
        public bool SetKeycodeCallback(ulong keycode, KeycodeCallbackDel callback)
        {
            keycodeCallbackDict.Add(keycode, callback);
            return SetKeycodeCallbackImpl(keycode, callback);
        }

        /// <summary>移除指定系統按鍵的原生回呼及受控強參考。</summary>
        /// <param name="keycode">欲解除註冊的按鍵碼。</param>
        /// <returns>是否完成移除；不存在時亦回傳 true。</returns>
        public bool RemoveKeycodeCallback(ulong keycode)
        {
            if (!keycodeCallbackDict.ContainsKey(keycode))
            {
                return true;
            }

            keycodeCallbackDict.Remove(keycode);
            return SetKeycodeCallbackImpl(keycode, null);
        }

        /// <summary>取得目前螢幕資訊並釋放原生回傳緩衝區。</summary>
        /// <returns>螢幕資訊清單；原生呼叫失敗時回傳空清單並記錄日誌。</returns>
        public List<MonitorInfo> GetMonitorInfoList()
        {
            List<MonitorInfo> monitorInfoList = new List<MonitorInfo>();

            int res = GetMonitorInfoListImpl(out IntPtr monitorInfoArrayPtr, out uint retLen);
            if (res != 0)
            {
                Logger.Log(3, $"error code: {res}");
                return monitorInfoList;
            }

            int size = Marshal.SizeOf(typeof(MonitorInfo));

            for (int i = 0; i < retLen; ++i)
            {
                IntPtr monitorInfoPtr = IntPtr.Add(monitorInfoArrayPtr, i * size);
                monitorInfoList.Add(Marshal.PtrToStructure<MonitorInfo>(monitorInfoPtr));
            }

            Marshal.FreeCoTaskMem(monitorInfoArrayPtr);

            return monitorInfoList;
        }

        /// <summary>取得目前螢幕配置的通道設定並釋放原生緩衝區。</summary>
        /// <returns>通道清單；失敗時回傳空清單並記錄日誌。</returns>
        public List<TunnelInfo> GetCurrentTunnelInfoList()
        {
            List<TunnelInfo> tunnelInfoList = new List<TunnelInfo>();

            int res = GetCurrentTunnelInfoListImpl(out IntPtr tunnelInfoArrayPtr, out uint retLen);
            if (res != 0)
            {
                Logger.Log(3, $"error code: {res}, msg: " + (res == -3 ? MonitorEdgeTunnelErrorMsgConvertor.ToErrorMsgString(GetErrorMsgCode()) : ""));
                return tunnelInfoList;
            }

            int size = Marshal.SizeOf(typeof(TunnelInfo));

            for (int i = 0; i < retLen; ++i)
            {
                IntPtr tunnelInfoPtr = IntPtr.Add(tunnelInfoArrayPtr, i * size);
                tunnelInfoList.Add(Marshal.PtrToStructure<TunnelInfo>(tunnelInfoPtr));
            }

            Marshal.FreeCoTaskMem(tunnelInfoArrayPtr);

            return tunnelInfoList;
        }

        /// <summary>更新目前配置的通道清單；呼叫後由外部決定是否重啟及存檔。</summary>
        /// <param name="tunnelInfoList">要設定的完整通道清單。</param>
        /// <returns>原生錯誤碼，零代表成功。</returns>
        public int SetCurrentTunnelInfoList(List<TunnelInfo> tunnelInfoList)
        {
            return SetCurrentTunnelInfoListImpl(tunnelInfoList.ToArray(), (uint)tunnelInfoList.Count);
        }

        /// <summary>將記憶體中的全部螢幕配置設定寫入設定檔。</summary>
        /// <returns>是否儲存成功；失敗時可讀取 GetErrorMsgCode。</returns>
        public bool SaveSetting()
        {
            return SaveSettingImpl();
        }

        /// <summary>從設定檔重新載入全部螢幕配置的通道規則；檔案不存在時清空設定，不建立檔案，真正失敗時保留原設定。</summary>
        /// <returns>成功載入或還原空設定時為 true；讀取或格式錯誤時為 false，可讀取 GetErrorMsgCode。</returns>
        public bool LoadSetting()
        {
            return LoadSettingImpl();
        }

        /// <summary>讀取目前螢幕配置的邊界禁止設定。</summary>
        /// <returns>是否只允許通道規則通行。</returns>
        public bool IsCurrentForceForbidEdge()
        {
            return IsCurrentForceForbidEdgeImpl();
        }

        /// <summary>更新目前配置的邊界禁止設定，尚未直接重啟 Hook。</summary>
        /// <param name="isForce">是否只允許通道規則通行。</param>
        /// <returns>是否更新成功。</returns>
        public bool SetCurrentForceForbidEdge(bool isForce)
        {
            return SetCurrentForceForbidEdgeImpl(isForce);
        }

        /// <summary>讀取目前呼叫執行緒最近一次原生操作的錯誤碼。</summary>
        /// <returns>目前執行緒的錯誤碼。</returns>
        public MonitorEdgeTunnelErrorMsg GetErrorMsgCode()
        {
            return (MonitorEdgeTunnelErrorMsg)GetErrorMsgCodeImpl();
        }

        /// <summary>設定或解除日誌回呼；非空回呼必須支援多執行緒。</summary>
        /// <param name="callback">日誌接收者；null 代表解除註冊。</param>
        public void SetLogCallback(LogCallbackDel callback)
        {
            logCallback = callback;

            if (logCallback != null)
            {
                SetLogCallbackImpl(_logCallback);
            }
            else
            {
                SetLogCallbackImpl(null);
            }
        }

        /// <summary>加入螢幕變更訂閱；回呼由原生訊息執行緒執行，UI 更新須自行派送。</summary>
        /// <param name="callback">要加入的受控回呼。</param>
        /// <returns>必要的原生註冊是否成功。</returns>
        public bool AddDisplayChangedCallback(DisplayChangedCallbackDel callback)
        {
            bool hasDelegateBefore = displayChangedCallback != null;
            displayChangedCallback += callback;
            bool hasDelegateAfter = displayChangedCallback != null;

            if (!hasDelegateBefore && hasDelegateAfter)
                return AddDisplayChangedCallbackImpl(_displayChangedCallback);

            return true;
        }

        /// <summary>解除螢幕變更訂閱；最後一位訂閱者移除時一併解除原生回呼。</summary>
        /// <param name="callback">先前加入的回呼。</param>
        /// <returns>必要的原生解除註冊是否成功。</returns>
        public bool RemoveDisplayChangedCallback(DisplayChangedCallbackDel callback)
        {
            bool hasDelegateBefore = displayChangedCallback != null;
            displayChangedCallback -= callback;
            bool hasDelegateAfter = displayChangedCallback != null;

            if (hasDelegateBefore && !hasDelegateAfter)
                return RemoveDisplayChangedCallbackImpl(_displayChangedCallback);

            return true;
        }
        #endregion

        #region -- Private Methods --

        /// <summary>呼叫原生 Start 入口；輸入輸出與同名公開方法一致。</summary>
        /// <returns>原生操作成功與否。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "Start", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool StartImpl();

        /// <summary>呼叫原生 Stop 入口；輸入輸出與同名公開方法一致。</summary>
        /// <returns>原生操作成功與否。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "Stop", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool StopImpl();

        /// <summary>停止全部原生服務並回傳結果；由非回呼執行緒在卸載前呼叫。</summary>
        /// <returns>原生操作成功與否。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "Shutdown", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool ShutdownImpl();

        /// <summary>呼叫原生 IsStart 入口；輸入輸出與同名公開方法一致。</summary>
        /// <returns>原生操作成功與否。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "IsStart", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool IsStartImpl();

        /// <summary>呼叫原生 SetKeycodeCallback 入口；輸入輸出與同名公開方法一致。</summary>
        /// <param name="keycode">系統按鍵碼。</param>
        /// <param name="callback">由原生 Hook 執行緒執行的代理。</param>
        /// <returns>原生操作成功與否。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SetKeycodeCallback", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SetKeycodeCallbackImpl(ulong keycode, KeycodeCallbackDel callback);

        /// <summary>呼叫原生 GetMonitorInfoList 入口；輸入輸出與同名公開方法一致。</summary>
        /// <param name="monitorInfo">回傳需以 CoTaskMemFree 釋放的資料指標。</param>
        /// <param name="length">回傳項目數。</param>
        /// <returns>原生結果或錯誤碼。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "GetMonitorInfoList", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I4)]
        private static extern int GetMonitorInfoListImpl(out IntPtr monitorInfo, out uint length);

        /// <summary>呼叫原生 GetCurrentTunnelInfoList 入口；輸入輸出與同名公開方法一致。</summary>
        /// <param name="monitorInfo">回傳需以 CoTaskMemFree 釋放的通道資料。</param>
        /// <param name="length">回傳通道數。</param>
        /// <returns>原生結果或錯誤碼。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "GetCurrentTunnelInfoList", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I4)]
        private static extern int GetCurrentTunnelInfoListImpl(out IntPtr monitorInfo, out uint length);

        /// <summary>呼叫原生 SetCurrentTunnelInfoList 入口；輸入輸出與同名公開方法一致。</summary>
        /// <param name="monitorInfo">要寫入的完整通道陣列。</param>
        /// <param name="length">陣列項目數。</param>
        /// <returns>原生結果或錯誤碼。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SetCurrentTunnelInfoList", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I4)]
        private static extern int SetCurrentTunnelInfoListImpl([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 1)] TunnelInfo[] monitorInfo, uint length);

        /// <summary>呼叫原生 SaveSetting 入口；輸入輸出與同名公開方法一致。</summary>
        /// <returns>原生操作成功與否。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SaveSetting", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SaveSettingImpl();

        /// <summary>呼叫原生 LoadSetting 入口；輸入輸出與同名公開方法一致。</summary>
        /// <returns>原生操作成功與否。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "LoadSetting", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool LoadSettingImpl();

        /// <summary>呼叫原生 IsCurrentForceForbidEdge 入口；輸入輸出與同名公開方法一致。</summary>
        /// <returns>原生操作成功與否。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "IsCurrentForceForbidEdge", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool IsCurrentForceForbidEdgeImpl();

        /// <summary>呼叫原生 SetCurrentForceForbidEdge 入口；輸入輸出與同名公開方法一致。</summary>
        /// <param name="isForce">是否只允許通道規則通行。</param>
        /// <returns>原生操作成功與否。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SetCurrentForceForbidEdge", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SetCurrentForceForbidEdgeImpl(bool isForce);

        /// <summary>呼叫原生 GetErrorMsgCode 入口；輸入輸出與同名公開方法一致。</summary>
        /// <returns>原生結果或錯誤碼。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "GetErrorMsgCode", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I4)]
        private static extern int GetErrorMsgCodeImpl();

        /// <summary>呼叫原生 SetLogCallback 入口；輸入輸出與同名公開方法一致。</summary>
        /// <param name="callback">支援多執行緒的日誌代理；null 表示移除。</param>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SetLogCallback", CallingConvention = CallingConvention.StdCall)]
        private static extern void SetLogCallbackImpl(_LogCallbackDel callback);

        /// <summary>呼叫原生 AddDisplayChangedCallback 入口；輸入輸出與同名公開方法一致。</summary>
        /// <param name="callback">由訊息執行緒呼叫的螢幕變更代理。</param>
        /// <returns>原生操作成功與否。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "AddDisplayChangedCallback", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool AddDisplayChangedCallbackImpl(_DisplayChangedCallbackDel callback);

        /// <summary>呼叫原生 RemoveDisplayChangedCallback 入口；輸入輸出與同名公開方法一致。</summary>
        /// <param name="callback">要解除的已註冊代理。</param>
        /// <returns>原生操作成功與否。</returns>
        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "RemoveDisplayChangedCallback", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool RemoveDisplayChangedCallbackImpl(_DisplayChangedCallbackDel callback);

        /// <summary>建立並保留原生回呼代理，確保服務執行期間不被 GC 回收。</summary>
        private MonitorEdgeTunnel()
        {
            _logCallback = (level, message) =>
            {
                logCallback?.Invoke((LogLevel)level, Utility.PtrToStringUTF8(message));
            };

            _displayChangedCallback = () =>
            {
                displayChangedCallback?.Invoke();
            };
        }

        #endregion
    }
}
