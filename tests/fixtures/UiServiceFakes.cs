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
        /// <summary>確認提示次數，與錯誤提示分開。</summary>
        public static int ConfirmCalls;
        /// <summary>模擬確認對話框期間由訊息迴圈處理的外部變更，使用一次即清除。</summary>
        public static Action DuringConfirm;
        /// <summary>依序回覆確認對話框；未指定時取消。</summary>
        public static Queue<bool> ConfirmResults { get; } = new Queue<bool>();
        #region -- Public Methods --
        /// <summary>接受提示 message、標題 caption、buttons 與 icon，增加計數並回傳 OK。</summary>
        public static MessageBoxResult Show(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon)
        { ++Calls; return MessageBoxResult.OK; }
        /// <summary>記錄確認並回傳預先安排的答案；不顯示 UI。</summary>
        /// <param name="message">操作影響。</param>
        /// <param name="caption">標題。</param>
        /// <param name="action">動作文字。</param>
        /// <returns>明確安排確認時為 true，其餘取消。</returns>
        public static bool Confirm(string message, string caption, string action)
        { ++ConfirmCalls; Action pending = DuringConfirm; DuringConfirm = null; pending?.Invoke(); return ConfirmResults.Count > 0 && ConfirmResults.Dequeue(); }
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
    /// <summary>與原生錯誤碼一致的載入結果，供真實 UI 流程分辨尚未存檔與讀取失敗。</summary>
    public enum MonitorEdgeTunnelErrorMsg
    {
        /// <summary>沒有錯誤。</summary>
        Null = 0,
        /// <summary>讀取、權限或檔案格式錯誤。</summary>
        LoadSettingFail = 7,
        /// <summary>尚未建立設定檔。</summary>
        SettingFileNotFound = 8
    }
    /// <summary>將測試錯誤碼轉成固定提示。</summary>
    public static class MonitorEdgeTunnelErrorMsgConvertor
    {
        #region -- Public Methods --
        /// <summary>接受 error 並回傳測試提示文字。</summary>
        public static string ToErrorMsgString(MonitorEdgeTunnelErrorMsg error) => "Expected test failure";
        #endregion
    }

    /// <summary>控制原生服務結果的替身，驗證 UI 對失敗的處理。</summary>
    public class MonitorEdgeTunnel
    {
        /// <summary>共用替身。</summary>
        public static MonitorEdgeTunnel Instance { get; } = new MonitorEdgeTunnel();
        /// <summary>實際儲存呼叫數。</summary>
        public int SaveCalls;
        /// <summary>記錄套用規則的次數，用於確認純預覽不會更動原生服務。</summary>
        public int ApplyCalls;
        /// <summary>停止服務呼叫數，驗證輸入錯誤不會中斷目前服務。</summary>
        public int StopCalls;
        /// <summary>啟動服務呼叫數。</summary>
        public int StartCalls;
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
        /// <summary>是否允許重新載入。</summary>
        public bool LoadSucceeds = true;
        /// <summary>模擬目前呼叫執行緒的錯誤原因。</summary>
        public MonitorEdgeTunnelErrorMsg ErrorCode = MonitorEdgeTunnelErrorMsg.LoadSettingFail;
        /// <summary>目前服務記憶體的封鎖選項。</summary>
        public bool ForceForbid;
        /// <summary>成功儲存後的規則副本；null 時載入不改動注入資料。</summary>
        public List<TunnelInfo> SavedTunnels;
        /// <summary>成功儲存後的封鎖選項。</summary>
        public bool SavedForceForbid;
        #region -- Public Methods --
        /// <summary>回傳目前模擬執行狀態。</summary>
        public bool IsStart() => Running;
        /// <summary>依設定結果停止模擬服務，回傳是否成功。</summary>
        public bool Stop() { ++StopCalls; if (StopSucceeds) Running = false; return StopSucceeds; }
        /// <summary>依設定結果啟動模擬服務，回傳是否成功。</summary>
        public bool Start() { ++StartCalls; Running = StartSucceeds; return StartSucceeds; }
        /// <summary>接受 tunnels 並回傳預設更新結果。</summary>
        public int SetCurrentTunnelInfoList(List<TunnelInfo> tunnels) { ++ApplyCalls; if (ListResult == 0) { Tunnels.Clear(); Tunnels.AddRange(tunnels); } return ListResult; }
        /// <summary>接受 force 選項並回傳成功。</summary>
        public bool SetCurrentForceForbidEdge(bool force) { ForceForbid = force; return true; }
        /// <summary>增加存檔次數並回傳受控結果。</summary>
        public bool SaveSetting() { ++SaveCalls; if (SaveSucceeds) { SavedTunnels = new List<TunnelInfo>(Tunnels); SavedForceForbid = ForceForbid; } return SaveSucceeds; }
        /// <summary>回傳載入成功，不讀取檔案，並記錄呼叫次數。</summary>
        public bool LoadSetting() { ++LoadCalls; if (LoadSucceeds && SavedTunnels != null) { Tunnels.Clear(); Tunnels.AddRange(SavedTunnels); ForceForbid = SavedForceForbid; } return LoadSucceeds; }
        /// <summary>預設回傳空的螢幕清單；可由測試注入資料，回傳獨立清單。</summary>
        public List<MonitorInfo> GetMonitorInfoList() => new List<MonitorInfo>(Monitors);
        /// <summary>預設回傳空的通道清單；可由測試注入資料，回傳獨立清單。</summary>
        public List<TunnelInfo> GetCurrentTunnelInfoList() => new List<TunnelInfo>(Tunnels);
        /// <summary>回傳未限制邊界。</summary>
        public bool IsCurrentForceForbidEdge() => ForceForbid;
        /// <summary>回傳注入的原生錯誤碼，供 UI 區分載入失敗原因。</summary>
        public MonitorEdgeTunnelErrorMsg GetErrorMsgCode() => ErrorCode;
        #endregion
    }
}
