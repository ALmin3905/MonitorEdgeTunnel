using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MonitorEdgeTunnelApp
{
    public struct MonitorInfo
    {
        public int id { get; set; }
        public int top { get; set; }
        public int bottom { get; set; }
        public int left { get; set; }
        public int right { get; set; }
        public double scaling { get; set; }
    }

    public struct TunnelInfo
    {
        public int id { get; set; }
        public int from { get; set; }
        public int to { get; set; }
        public int relativeID { get; set; }
        public int displayID { get; set; }
        public EdgeType edgeType { get; set; }
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
        NoSettingFile,
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
        private static readonly Dictionary<MonitorEdgeTunnelErrorMsg, string> ErrorMsgStrDict;

        static MonitorEdgeTunnelErrorMsgConvertor()
        {
            ErrorMsgStrDict = new Dictionary<MonitorEdgeTunnelErrorMsg, string>() {
                {MonitorEdgeTunnelErrorMsg.Null, "" },
                {MonitorEdgeTunnelErrorMsg.NoSettingFile, "沒有設定檔" },
                {MonitorEdgeTunnelErrorMsg.HookFail, "應用程式嚴重錯誤，請重啟試試" },
                {MonitorEdgeTunnelErrorMsg.GetMonitorInfoFailed, "取得螢幕資訊失敗" },
                {MonitorEdgeTunnelErrorMsg.NoMonitorInfo, "沒有螢幕資訊" },
                {MonitorEdgeTunnelErrorMsg.TunnelInfoError, "通道資訊錯誤，請確認是否符合規則" },
                {MonitorEdgeTunnelErrorMsg.SaveSettingFail, "儲存設定檔案失敗" },
                {MonitorEdgeTunnelErrorMsg.LoadSettingFail, "讀取設定檔案失敗" }
            };
        }

        /// <summary>
        /// 將MonitorEdgeTunnelErrorMsg轉換成對應要顯示的字串
        /// </summary>
        /// <param name="msg">MonitorEdgeTunnelErrorMsg</param>
        /// <returns>要顯示的字串</returns>
        public static string ToErrorMsgString(MonitorEdgeTunnelErrorMsg msg)
        {
            return ErrorMsgStrDict[msg];
        }
    }

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

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool KeycodeCallbackDel(ulong keycode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void _LogCallbackDel(int level, IntPtr message);

        public delegate void LogCallbackDel(LogLevel level, string message);

        private static readonly Lazy<MonitorEdgeTunnel> lazy = new Lazy<MonitorEdgeTunnel>(() => new MonitorEdgeTunnel());

        private readonly Dictionary<ulong, KeycodeCallbackDel> keycodeCallbackDict = new Dictionary<ulong, KeycodeCallbackDel>();

        private readonly _LogCallbackDel _logCallback = null;

        private LogCallbackDel logCallback = null;

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "Start", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool StartImpl();

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "Stop", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool StopImpl();

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "IsStart", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool IsStartImpl();

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SetKeycodeCallback", CallingConvention = CallingConvention.StdCall)]
        private static extern void SetKeycodeCallbackImpl(ulong keycode, KeycodeCallbackDel callback);

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "GetMonitorInfoList", CallingConvention = CallingConvention.StdCall)]
        private static extern void GetMonitorInfoListImpl([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 1)] out MonitorInfo[] monitorInfo, out uint length);

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "GetCurrentTunnelInfoList", CallingConvention = CallingConvention.StdCall)]
        private static extern void GetCurrentTunnelInfoListImpl([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 1)] out TunnelInfo[] monitorInfo, out uint length);

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SetCurrentTunnelInfoList", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SetCurrentTunnelInfoListImpl([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 1)] TunnelInfo[] monitorInfo, uint length);

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SaveSetting", CallingConvention = CallingConvention.StdCall)]
        private static extern bool SaveSettingImpl();

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "LoadSetting", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool LoadSettingImpl();

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "IsCurrentForceForbidEdge", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool IsCurrentForceForbidEdgeImpl();

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SetCurrentForceForbidEdge", CallingConvention = CallingConvention.StdCall)]
        private static extern void SetCurrentForceForbidEdgeImpl(bool isForce);

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "GetErrorMsgCode", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I4)]
        private static extern int GetErrorMsgCodeImpl();

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SetLogCallback", CallingConvention = CallingConvention.StdCall)]
        private static extern void SetLogCallbackImpl(_LogCallbackDel callback);

        private MonitorEdgeTunnel()
        {
            _logCallback = (int level, IntPtr message) =>
            {
                logCallback?.Invoke((LogLevel)level, Utility.PtrToStringUTF8(message));
            };
        }

        public static MonitorEdgeTunnel Instance
        {
            get
            {
                return lazy.Value;
            }
        }

        public bool Start()
        {
            return StartImpl();
        }

        public bool Stop()
        {
            return StopImpl();
        }

        public bool IsStart()
        {
            return IsStartImpl();
        }

        public void SetKeycodeCallback(ulong keycode, KeycodeCallbackDel callback)
        {
            keycodeCallbackDict.Add(keycode, callback);
            SetKeycodeCallbackImpl(keycode, callback);
        }

        public void RemoveKeycodeCallback(ulong keycode)
        {
            if (!keycodeCallbackDict.ContainsKey(keycode))
            {
                return;
            }

            keycodeCallbackDict.Remove(keycode);
            SetKeycodeCallbackImpl(keycode, null);
        }

        public List<MonitorInfo> GetMonitorInfoList()
        {
            List<MonitorInfo> monitorInfoList = new List<MonitorInfo>();

            GetMonitorInfoListImpl(out MonitorInfo[] monitorInfoArray, out uint retLen);

            for (int i = 0; i < retLen; ++i)
            {
                monitorInfoList.Add(monitorInfoArray[i]);
            }

            return monitorInfoList;
        }

        public List<TunnelInfo> GetCurrentTunnelInfoList()
        {
            List<TunnelInfo> tunnelInfoList = new List<TunnelInfo>();

            GetCurrentTunnelInfoListImpl(out TunnelInfo[] tunnelInfoArray, out uint retLen);

            for (int i = 0; i < retLen; ++i)
            {
                tunnelInfoList.Add(tunnelInfoArray[i]);
            }

            return tunnelInfoList;
        }

        public bool SetCurrentTunnelInfoList(List<TunnelInfo> tunnelInfoList)
        {
            return SetCurrentTunnelInfoListImpl(tunnelInfoList.ToArray(), (uint)tunnelInfoList.Count);
        }

        public bool SaveSetting()
        {
            return SaveSettingImpl();
        }

        public bool LoadSetting()
        {
            return LoadSettingImpl();
        }

        public bool IsCurrentForceForbidEdge()
        {
            return IsCurrentForceForbidEdgeImpl();
        }

        public void SetCurrentForceForbidEdge(bool isForce)
        {
            SetCurrentForceForbidEdgeImpl(isForce);
        }

        public MonitorEdgeTunnelErrorMsg GetErrorMsgCode()
        {
            return (MonitorEdgeTunnelErrorMsg)GetErrorMsgCodeImpl();
        }

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
    }
}
