using MonitorEdgeTunnelApp.Editing;
using MonitorEdgeTunnelApp.Themes;
using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Reflection;
using System.Windows;

namespace MonitorEdgeTunnelApp
{
    /// <summary>以實際 WPF 視窗及服務替身驗證工作區草稿、狀態與破壞性操作保護。</summary>
    public static class EditingTests
    {
        #region -- Public Methods --

        /// <summary>在 STA 執行編輯工作區案例，不啟動原生 hook 或寫入真實設定。</summary>
        /// <returns>全部成功時回傳 0，失敗會拋出診斷例外。</returns>
        [STAThread]
        public static int Main()
        {
            Application application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = ThemeManager.ResourceUri("Controls.xaml") });
            using (ThemeManager theme = new ThemeManager(application, () => AppTheme.Light))
            {
                TestStates();
                TestReopenAndLayouts();
                TestReloadProtection();
                TestSaveFailure();
                TestStartProtection();
                TestNumericProjection();
                TestInvalidDraft();
                TestEditorControls();
            }
            Console.WriteLine("8/8 editing workflow cases passed");
            return 0;
        }

        #endregion

        #region -- Private Methods --

        /// <summary>驗證草稿、已套用及已儲存三態，以及回復相同值不會留下假變更。</summary>
        private static void TestStates()
        {
            MainWindow window = CreateWindow();
            try
            {
                Require(window.EditStatus == "已儲存", "initial values must be saved baseline");
                window.forceForbidEdgeChecked = true;
                Require(window.EditStatus == "尚未套用", "draft must differ from applied state");
                Click(window, "ApplyButton_Click");
                Require(window.EditStatus == "已套用但未儲存", "apply must not claim persistence");
                Click(window, "SaveSettingButton_Click");
                Require(window.EditStatus == "已儲存", "successful save must update baseline");
                window.forceForbidEdgeChecked = false;
                window.forceForbidEdgeChecked = true;
                Require(!window.HasUnappliedChanges && !window.HasUnsavedChanges, "returning to saved values must clear dirty status");
                Console.WriteLine("PASS draft applied and saved states reflect actual values");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證關閉重開與切換螢幕配置保留各自草稿，不重新載入檔案。</summary>
        private static void TestReopenAndLayouts()
        {
            EditorWorkspace workspace = new EditorWorkspace();
            MainWindow first = CreateWindow(workspace);
            TunnelInfo_Notify draft = first.tunnelInfos[0];
            draft.from = 12;
            first.Close();
            MainWindow window = new MainWindow(workspace);
            try
            {
                Require(ReferenceEquals(draft, window.tunnelInfos[0]) && window.HasUnappliedChanges, "reopening must restore the original draft");
                MonitorEdgeTunnel.Instance.Monitors[0] = Monitor(2000);
                window.RefreshDisplayData();
                Require(!ReferenceEquals(draft, window.tunnelInfos[0]), "different desktop geometry needs its own session");
                TunnelInfo_Notify secondDraft = window.tunnelInfos[0];
                secondDraft.from = 24;
                MonitorEdgeTunnel.Instance.Monitors[0] = Monitor(1000);
                window.RefreshDisplayData();
                Require(ReferenceEquals(draft, window.tunnelInfos[0]) && draft.from == 12, "returning to original geometry restores its draft");
                MonitorEdgeTunnel.Instance.Monitors[0] = Monitor(2000);
                window.RefreshDisplayData();
                Require(ReferenceEquals(secondDraft, window.tunnelInfos[0]) && secondDraft.from == 24, "second layout draft must also survive");
                Require(MonitorEdgeTunnel.Instance.LoadCalls == 0 && MonitorEdgeTunnel.Instance.ApplyCalls == 0 && workspace.HasUnsavedChanges,
                    "window and display changes must not load or apply drafts");
                Console.WriteLine("PASS reopen and hotplug preserve separate layout drafts");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證取消重新載入完全不動設定，載入失敗保留草稿，成功後回到儲存基準。</summary>
        private static void TestReloadProtection()
        {
            MainWindow window = CreateWindow();
            try
            {
                window.forceForbidEdgeChecked = true;
                Click(window, "ResetButton_Click");
                Require(MonitorEdgeTunnel.Instance.LoadCalls == 0 && window.forceForbidEdgeChecked, "cancel reload must preserve draft without I/O");
                MonitorEdgeTunnel.Instance.LoadSucceeds = false;
                TestMessageBox.ConfirmResults.Enqueue(true);
                Click(window, "ResetButton_Click");
                Require(window.forceForbidEdgeChecked && window.HasUnsavedChanges, "load failure must preserve draft");
                MonitorEdgeTunnel.Instance.LoadSucceeds = true;
                TestMessageBox.ConfirmResults.Enqueue(true);
                Click(window, "ResetButton_Click");
                Require(!window.forceForbidEdgeChecked && !window.HasUnsavedChanges, "confirmed successful reload restores saved values");
                Console.WriteLine("PASS reload cancellation and failures preserve edits");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證存檔與重啟失敗不誤報成功，並保留服務實際已套用的狀態。</summary>
        private static void TestSaveFailure()
        {
            MainWindow window = CreateWindow();
            try
            {
                MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
                window.forceForbidEdgeChecked = true;
                service.SaveSucceeds = false;
                Click(window, "SaveSettingButton_Click");
                Require(window.EditStatus == "已套用但未儲存" && service.SaveCalls == 1, "save failure must not advance saved baseline");
                service.Running = true;
                service.StartSucceeds = false;
                Click(window, "SaveSettingButton_Click");
                Require(!window.isStart && service.SaveCalls == 1 && window.HasUnsavedChanges, "failed restart must prevent saving and reflect stopped service");
                Console.WriteLine("PASS failed save and restart retain accurate state");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證未套用草稿在啟動前須明確確認；取消不啟動，確認後先套用。</summary>
        private static void TestStartProtection()
        {
            MainWindow window = CreateWindow();
            try
            {
                window.forceForbidEdgeChecked = true;
                Click(window, "StartStopButton_Click");
                Require(!window.isStart && MonitorEdgeTunnel.Instance.ApplyCalls == 0, "cancel must not start old settings");
                TestMessageBox.ConfirmResults.Enqueue(true);
                Click(window, "StartStopButton_Click");
                Require(window.isStart && MonitorEdgeTunnel.Instance.ForceForbid && window.EditStatus == "已套用但未儲存", "confirmed start must use the current draft");
                Console.WriteLine("PASS starting unapplied drafts requires confirmation");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證中文範圍欄位與原生含尾端語意的精確換算及模式切換。</summary>
        private static void TestNumericProjection()
        {
            TunnelInfo_Notify rule = new TunnelInfo_Notify { rangeType = RangeType.Customize, from = 640, to = 639 };
            Require(rule.StartText == "640" && rule.ExtentText == "640", "custom extent must show actual pixel count");
            rule.ExtentText = "2147483648";
            Require(rule.to == int.MaxValue && !rule.HasInputErrors, "maximum native extent must convert without overflow");
            rule.StartText = "abc";
            Require(rule.from == 640 && rule.HasInputErrors, "invalid input must preserve both raw text and last numeric value");
            rule.rangeType = RangeType.Full;
            Require(!rule.IsRangeEditable && !rule.HasInputErrors, "full edges do not require unused numeric fields");
            rule.rangeType = RangeType.Relative;
            rule.StartText = "10";
            rule.ExtentText = "20";
            Require(rule.from == 10 && rule.to == 20 && rule.ExtentLabel.Contains("終點內縮"), "relative mode retains offset semantics");
            Console.WriteLine("PASS contextual numeric fields preserve native inclusive ranges");
        }

        /// <summary>無效輸入仍須視為未套用草稿，關閉重開後不能變回先前的數字。</summary>
        private static void TestInvalidDraft()
        {
            EditorWorkspace workspace = new EditorWorkspace();
            MainWindow window = CreateWindow(workspace);
            window.tunnelInfos[0].rangeType = RangeType.Relative;
            window.tunnelInfos[0].ExtentText = "not a number";
            window.Close();
            window = new MainWindow(workspace);
            try
            {
                Require(window.tunnelInfos[0].ExtentText == "not a number" && window.HasUnappliedChanges && workspace.HasUnsavedChanges,
                    "invalid raw text must survive reopening and remain dirty");
                Console.WriteLine("PASS unfinished numeric input survives window close");
            }
            finally { window.Close(); }
        }

        /// <summary>從實際文字框與選單編輯，驗證顯示轉換不會改掉傳給原生的值。</summary>
        private static void TestEditorControls()
        {
            MainWindow window = CreateWindow();
            try
            {
                TunnelInfo_Notify rule = window.tunnelInfos[0];
                rule.edgeType = EdgeType.Top;
                rule.rangeType = RangeType.Customize;
                rule.from = 100;
                rule.to = 639;
                window.tunnelInfos.Add(new TunnelInfo_Notify { id = 1, displayID = 0, edgeType = EdgeType.Right });
                FrameworkElement content = (FrameworkElement)window.Content;
                content.Measure(new Size(1240, 700));
                content.Arrange(new Rect(0, 0, 1240, 700));
                content.UpdateLayout();
                Pump();
                ListView table = (ListView)window.FindName("TunnelInfoListView");
                DependencyObject row = table.ItemContainerGenerator.ContainerFromIndex(0);
                TextBox extent = Descendants<TextBox>(row).Single(box => box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "ExtentText");
                Require(extent.Text == "640", "real field must display native extent plus one");
                extent.Text = "320";
                extent.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                Require(rule.to == 319, "visible pixel count must translate back to native extent");
                ComboBox action = Descendants<ComboBox>(row).Single(combo => combo.Items.Count == 2 && combo.Items[0] is EditorChoice);
                action.SelectedValue = true;
                Pump();
                Require(rule.IsTransport && rule.relativeID == -2, "transport must require an explicit target instead of silently choosing one");
                ComboBox target = Descendants<ComboBox>(row).Single(combo => combo.SelectedValuePath == "id" && combo.Items.Count > 0 && combo.Items[0] is TunnelInfo_Notify);
                target.SelectedValue = 1;
                Pump();
                Require(rule.relativeID == 1, "target choice must retain the raw native ID");
                action.SelectedValue = false;
                Pump();
                Require(rule.relativeID == -1 && !target.IsEnabled, "blocking must be explicit and disable unused target selection");
                Console.WriteLine("PASS actual editor controls map labels actions and pixel lengths correctly");
            }
            finally { window.Close(); }
        }

        /// <summary>列舉已產生的控制項，供操作實際 XAML 內容。</summary>
        /// <typeparam name="T">要尋找的視覺型別。</typeparam>
        /// <param name="root">搜尋根節點。</param>
        /// <returns>所有符合型別的後代。</returns>
        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (T descendant in Descendants<T>(child)) yield return descendant;
            }
        }

        /// <summary>完成目前 UI 資料綁定與預覽更新。</summary>
        private static void Pump()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        /// <summary>清除服務替身並建立真實 XAML 的主視窗。</summary>
        /// <param name="workspace">選用的共享草稿工作區。</param>
        /// <returns>由呼叫端負責關閉的未顯示視窗。</returns>
        private static MainWindow CreateWindow(EditorWorkspace workspace = null)
        {
            MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
            service.Running = false;
            service.ListResult = 0;
            service.StopSucceeds = service.StartSucceeds = service.SaveSucceeds = service.LoadSucceeds = true;
            service.ForceForbid = service.SavedForceForbid = false;
            service.LoadCalls = service.ApplyCalls = service.SaveCalls = 0;
            service.SavedTunnels = null;
            service.Monitors.Clear();
            service.Monitors.Add(Monitor(1000));
            service.Tunnels.Clear();
            service.Tunnels.Add(new TunnelInfo { id = 0, displayID = 0, relativeID = -1 });
            TestMessageBox.ConfirmResults.Clear();
            return new MainWindow(workspace ?? new EditorWorkspace());
        }

        /// <summary>建立相同位置但可切換寬度的螢幕配置。</summary>
        /// <param name="width">螢幕含尾端像素的寬度。</param>
        /// <returns>原始 ID 為零的螢幕。</returns>
        private static MonitorInfo Monitor(int width) => new MonitorInfo { id = 0, right = width - 1, bottom = 799, scaling = 1 };

        /// <summary>觸發真正的 code-behind 操作事件。</summary>
        /// <param name="window">受測視窗。</param>
        /// <param name="handler">私有事件方法名稱。</param>
        private static void Click(MainWindow window, string handler) => typeof(MainWindow).GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(window, new object[] { null, null });

        /// <summary>檢查流程必要條件，失敗時中止案例。</summary>
        /// <param name="condition">預期為 true 的結果。</param>
        /// <param name="message">失敗說明。</param>
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        #endregion
    }
}
