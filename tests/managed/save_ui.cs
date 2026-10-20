using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;

namespace MonitorEdgeTunnelApp
{
    /// <summary>以可控制的服務結果測試真正的視窗存檔事件，不顯示 UI。</summary>
    public static class SaveUiTests
    {
        #region -- Public Methods --
        /// <summary>在 STA 建立未顯示的視窗，驗證失敗提示及停止存檔；回傳零代表通過。</summary>
        /// <returns>程序結束碼。</returns>
        [STAThread]
        public static int Main()
        {
            MainWindow window = new MainWindow();
            MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
            ClickSave(window);
            Require(service.SaveCalls == 1 && TestMessageBox.Calls == 1);
            service.ListResult = -3;
            ClickSave(window);
            Require(service.SaveCalls == 1 && TestMessageBox.Calls == 2);
            service.ListResult = 0;
            service.Running = true;
            service.StopSucceeds = false;
            ClickSave(window);
            Require(service.SaveCalls == 1 && TestMessageBox.Calls == 3);
            service.StopSucceeds = true;
            service.StartSucceeds = false;
            ClickSave(window);
            Require(service.SaveCalls == 1 && TestMessageBox.Calls == 4);
            service.Running = true;
            service.StartSucceeds = true;
            service.SaveSucceeds = true;
            ClickSave(window);
            Require(service.SaveCalls == 2 && TestMessageBox.Calls == 4 && window.isStart);
            window.Close();
            Console.WriteLine("UI reports save failure and never persists a failed apply or restart");
            return 0;
        }
        #endregion

        #region -- Private Methods --
        /// <summary>呼叫 window 的真正存檔事件，使用空 sender 與事件參數。</summary>
        /// <param name="window">未顯示的受測視窗。</param>
        private static void ClickSave(MainWindow window)
        {
            typeof(MainWindow).GetMethod("SaveSettingButton_Click", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(window, new object[] { null, null });
        }
        /// <summary>驗證 condition；失敗時中止測試。</summary>
        /// <param name="condition">待驗證的條件。</param>
        private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Save UI regression failed"); }
        #endregion
    }

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

    /// <summary>提供測試 XAML 欄位所需的簡單資料容器。</summary>
    public class TestControl
    {
        /// <summary>綁定來源。</summary>
        public object DataContext { get; set; }
        /// <summary>編輯清單的項目容器。</summary>
        public ArrayList Items { get; } = new ArrayList();
    }

    /// <summary>提供受測 code-behind 的 XAML 欄位，不載入實際 UI 配置。</summary>
    public partial class MainWindow
    {
        /// <summary>螢幕清單的替代控制項。</summary>
        private readonly TestControl MonitorInfoListView = new TestControl();
        /// <summary>通道清單的替代控制項。</summary>
        private readonly TestControl TunnelInfoListView = new TestControl();
        /// <summary>邊界選項的替代控制項。</summary>
        private readonly TestControl ForceForbidEdgeCheckbox = new TestControl();
        /// <summary>啟停按鈕的替代控制項。</summary>
        private readonly TestControl IsStartButton = new TestControl();
        /// <summary>自動啟動選單的替代控制項。</summary>
        private readonly TestControl AutorunMenuItem = new TestControl();
        #region -- Private Methods --
        /// <summary>測試中不載入或顯示 XAML。</summary>
        private void InitializeComponent() { }
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
    public struct MonitorInfo { }
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
        /// <summary>回傳載入成功，不讀取檔案。</summary>
        public bool LoadSetting() => true;
        /// <summary>回傳空的螢幕清單。</summary>
        public List<MonitorInfo> GetMonitorInfoList() => new List<MonitorInfo>();
        /// <summary>回傳空的通道清單。</summary>
        public List<TunnelInfo> GetCurrentTunnelInfoList() => new List<TunnelInfo>();
        /// <summary>回傳未限制邊界。</summary>
        public bool IsCurrentForceForbidEdge() => false;
        /// <summary>回傳固定的模擬錯誤碼。</summary>
        public int GetErrorMsgCode() => 1;
        #endregion
    }
}
