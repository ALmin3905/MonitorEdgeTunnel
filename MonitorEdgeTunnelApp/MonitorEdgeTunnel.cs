using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MonitorEdgeTunnelApp
{
    public struct MonitorInfo
    {
        public int id;
        public int top;
        public int bottom;
        public int left;
        public int right;
        public double scaling;
    }

    public struct TunnelInfo
    {
        public int id;
        public int from;
        public int to;
        public int relativeID;
        public int displayID;
        public EdgeType edgeType;
        public RangeType rangeType;
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

    public class MonitorEdgeTunnel
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool KeycodeCallbackDel(ulong keycode);

        private static readonly Lazy<MonitorEdgeTunnel> lazy = new Lazy<MonitorEdgeTunnel>(() => new MonitorEdgeTunnel());

        private readonly Dictionary<ulong, KeycodeCallbackDel> keycodeCallbackDict = new Dictionary<ulong, KeycodeCallbackDel>();

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "Start", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool StartImpl();

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "Stop", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool StopImpl();

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SetKeycodeCallback", CallingConvention = CallingConvention.StdCall)]
        private static extern void SetKeycodeCallbackImpl(ulong keycode, KeycodeCallbackDel callback);

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "GetMonitorInfoList", CallingConvention = CallingConvention.StdCall)]
        private static extern void GetMonitorInfoListImpl([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 1)] out MonitorInfo[] monitorInfo, out uint length);

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "GetTunnelInfoList", CallingConvention = CallingConvention.StdCall)]
        private static extern void GetTunnelInfoListImpl([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 1)] out TunnelInfo[] monitorInfo, out uint length);

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SetTunnelInfoList", CallingConvention = CallingConvention.StdCall)]
        private static extern void SetTunnelInfoListImpl([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 1)] TunnelInfo[] monitorInfo, uint length);

        [DllImport("MonitorEdgeTunnelDll.dll", EntryPoint = "SaveSetting", CallingConvention = CallingConvention.StdCall)]
        private static extern void SaveSettingImpl();

        private MonitorEdgeTunnel()
        {

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

        public void SetKeycodeCallback(ulong keycode, KeycodeCallbackDel callback)
        {
            keycodeCallbackDict.Add(keycode, callback);
            SetKeycodeCallbackImpl(keycode, callback);
        }

        public void RemoveKeycodeCallback(ulong keycode)
        {
            if (!keycodeCallbackDict.ContainsKey(keycode))
                return;

            keycodeCallbackDict.Remove(keycode);
            SetKeycodeCallbackImpl(keycode, null);
        }

        public List<MonitorInfo> GetMonitorInfoList()
        {
            List<MonitorInfo> monitorInfoList = new List<MonitorInfo>();

            GetMonitorInfoListImpl(out MonitorInfo[] monitorInfoArray, out uint retLen);

            for (int i = 0; i < retLen; ++i)
                monitorInfoList.Add(monitorInfoArray[i]);

            return monitorInfoList;
        }

        public List<TunnelInfo> GetTunnelInfoList()
        {
            List<TunnelInfo> tunnelInfoList = new List<TunnelInfo>();

            GetTunnelInfoListImpl(out TunnelInfo[] tunnelInfoArray, out uint retLen);

            for (int i = 0; i < retLen; ++i)
                tunnelInfoList.Add(tunnelInfoArray[i]);

            return tunnelInfoList;
        }

        public void SetTunnelInfoList(List<TunnelInfo> tunnelInfoList)
        {
            SetTunnelInfoListImpl(tunnelInfoList.ToArray(), (uint)tunnelInfoList.Count);
        }

        public void SaveSetting()
        {
            SaveSettingImpl();
        }
    }
}
