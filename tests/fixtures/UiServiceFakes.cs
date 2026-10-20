using System;
using System.Collections.Generic;
using System.Windows;

namespace MonitorEdgeTunnelApp
{
    /// <summary>記錄對話框呼叫，不建立實際視窗。</summary>
    public static class TestMessageBox
    {
        /// <summary>已記錄的提示次數。</summary>
        public static int Calls;
        #region -- Public Methods --
        /// <summary>接受提示 message、標題 caption、buttons 與 icon，增加計數並回傳 OK。</summary>
        public static MessageBoxResult Show(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon)
        { ++Calls; return MessageBoxResult.OK; }
        #endregion
    }

    /// <summary>固定測試中的開機啟動設定，避免存取系統捷徑。</summary>
    public static class AutoStartManager
    {
        #region -- Public Methods --
        /// <summary>回傳未啟用開機自動啟動。</summary>
        public static bool IsAutorun() => false;
        /// <summary>接受 requested 設定並回傳成功，不寫入系統。</summary>
        public static bool SetAutorun(bool requested) => true;
        #endregion
    }

    /// <summary>測試用的螢幕資料。</summary>
    public struct MonitorInfo
    {
        /// <summary>原始螢幕 ID。</summary>
        public int id { get; set; }
        /// <summary>上邊界。</summary>
        public int top { get; set; }
        /// <summary>下邊界。</summary>
        public int bottom { get; set; }
        /// <summary>左邊界。</summary>
        public int left { get; set; }
        /// <summary>右邊界。</summary>
        public int right { get; set; }
        /// <summary>Windows 縮放比例。</summary>
        public double scaling { get; set; }
    }
    /// <summary>測試用的通道資料，欄位意義與正式模型一致。</summary>
    public struct TunnelInfo
    {
        /// <summary>通道 ID。</summary>
        public int id;
        /// <summary>起點。</summary>
        public int from;
        /// <summary>終點。</summary>
        public int to;
        /// <summary>目標通道。</summary>
        public int relativeID;
        /// <summary>螢幕 ID。</summary>
        public int displayID;
        /// <summary>螢幕邊緣。</summary>
        public EdgeType edgeType;
        /// <summary>範圍類型。</summary>
        public RangeType rangeType;
    }
    /// <summary>測試用邊緣值。</summary>
    public enum EdgeType { Left, Right, Top, Bottom }
    /// <summary>測試用範圍值。</summary>
    public enum RangeType { Full, Relative, Customize }
    /// <summary>將測試錯誤碼轉成固定提示。</summary>
    public static class MonitorEdgeTunnelErrorMsgConvertor
    {
        #region -- Public Methods --
        /// <summary>接受 error 並回傳測試提示文字。</summary>
        public static string ToErrorMsgString(int error) => "Expected test failure";
        #endregion
    }

    /// <summary>控制原生服務結果的替身，驗證 UI 對失敗的處理。</summary>
    public class MonitorEdgeTunnel
    {
        /// <summary>共用替身。</summary>
        public static MonitorEdgeTunnel Instance { get; } = new MonitorEdgeTunnel();
        /// <summary>實際儲存呼叫數。</summary>
        public int SaveCalls;
        /// <summary>通道更新結果。</summary>
        public int ListResult;
        /// <summary>模擬原生執行狀態。</summary>
        public bool Running;
        /// <summary>控制停止結果。</summary>
        public bool StopSucceeds = true;
        /// <summary>控制啟動結果。</summary>
        public bool StartSucceeds = true;
        /// <summary>控制存檔結果。</summary>
        public bool SaveSucceeds;
        /// <summary>由各測試注入的螢幕清單，預設為空。</summary>
        public List<MonitorInfo> Monitors { get; } = new List<MonitorInfo>();
        /// <summary>由各測試注入的通道清單，預設為空。</summary>
        public List<TunnelInfo> Tunnels { get; } = new List<TunnelInfo>();
        /// <summary>記錄載入設定次數，用於檢查純 UI 操作不會重載設定。</summary>
        public int LoadCalls;
        #region -- Public Methods --
        /// <summary>回傳目前模擬執行狀態。</summary>
        public bool IsStart() => Running;
        /// <summary>依設定結果停止模擬服務，回傳是否成功。</summary>
        public bool Stop() { if (StopSucceeds) Running = false; return StopSucceeds; }
        /// <summary>依設定結果啟動模擬服務，回傳是否成功。</summary>
        public bool Start() { Running = StartSucceeds; return StartSucceeds; }
        /// <summary>接受 tunnels 並回傳預設更新結果。</summary>
        public int SetCurrentTunnelInfoList(List<TunnelInfo> tunnels) => ListResult;
        /// <summary>接受 force 選項並回傳成功。</summary>
        public bool SetCurrentForceForbidEdge(bool force) => true;
        /// <summary>增加存檔次數並回傳受控結果。</summary>
        public bool SaveSetting() { ++SaveCalls; return SaveSucceeds; }
        /// <summary>回傳載入成功，不讀取檔案，並記錄呼叫次數。</summary>
        public bool LoadSetting() { ++LoadCalls; return true; }
        /// <summary>預設回傳空的螢幕清單；可由測試注入資料，回傳獨立清單。</summary>
        public List<MonitorInfo> GetMonitorInfoList() => new List<MonitorInfo>(Monitors);
        /// <summary>預設回傳空的通道清單；可由測試注入資料，回傳獨立清單。</summary>
        public List<TunnelInfo> GetCurrentTunnelInfoList() => new List<TunnelInfo>(Tunnels);
        /// <summary>回傳未限制邊界。</summary>
        public bool IsCurrentForceForbidEdge() => false;
        /// <summary>回傳固定的模擬錯誤碼。</summary>
        public int GetErrorMsgCode() => 1;
        #endregion
    }
}
