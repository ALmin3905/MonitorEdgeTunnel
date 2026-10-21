using MonitorEdgeTunnelApp.Editing;
using MonitorEdgeTunnelApp.Themes;
using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Input;
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
                TestReloadControls();
                TestMissingSettings();
                TestSaveFailure();
                TestStartProtection();
                TestNumericProjection();
                TestInvalidDraft();
                TestEditorControls();
                TestValidationGate();
                TestSemanticErrors();
                TestOverlapConfirmation();
                TestTopologyGate();
                TestDeletedValidation();
                TestTargetPopup();
                TestModalContextChange();
                TestSparseRuleIdentity();
                TestConnectionCreation();
                TestConnectionGuards();
                TestConnectionDialog();
                TestResponsiveEditor();
                TestSelectionAfterAddDelete();
            }
            Console.WriteLine("23/23 editing workflow cases passed");
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
                int errors = TestMessageBox.Calls;
                TestMessageBox.ConfirmResults.Enqueue(true);
                Click(window, "ResetButton_Click");
                Require(window.forceForbidEdgeChecked && window.HasUnsavedChanges && TestMessageBox.Calls == errors + 1, "real load failures must still show an error and preserve the draft");
                MonitorEdgeTunnel.Instance.LoadSucceeds = true;
                TestMessageBox.ConfirmResults.Enqueue(true);
                Click(window, "ResetButton_Click");
                Require(!window.forceForbidEdgeChecked && !window.HasUnsavedChanges, "confirmed successful reload restores saved values");
                Console.WriteLine("PASS reload cancellation and failures preserve edits");
            }
            finally { window.Close(); }
        }

        /// <summary>從真實詳細表單重新載入失敗時，保留選單選取、原生欄位及未正規化文字，避免清空來源集合破壞綁定。</summary>
        private static void TestReloadControls()
        {
            MainWindow window = CreateWindow();
            try
            {
                TunnelInfo_Notify rule = window.tunnelInfos[0];
                rule.rangeType = RangeType.Relative;
                rule.ExtentText = "0031";
                rule.relativeID = 1;
                window.tunnelInfos.Add(new TunnelInfo_Notify { id = 1, displayID = 0, edgeType = EdgeType.Right });
                window.selectedTunnel = rule;
                Arrange(window);
                DependencyObject fields = (DependencyObject)window.FindName("RuleFields");
                ComboBox source = Descendants<ComboBox>(fields).Single(combo => combo.GetBindingExpression(ComboBox.SelectedValueProperty)?.ParentBinding.Path.Path == "displayID");
                Require(Equals(source.SelectedValue, 0) && !Validation.GetHasError(source), "source monitor must initially be selected");
                MonitorEdgeTunnel.Instance.LoadSucceeds = false;
                for (int attempt = 0; attempt < 2; ++attempt)
                {
                    TestMessageBox.ConfirmResults.Enqueue(true);
                    Click(window, "ResetButton_Click");
                    Pump();
                    Require(ReferenceEquals(window.selectedTunnel, rule) && rule.displayID == 0 && rule.ExtentText == "0031" && rule.relativeID == 1,
                        "failed reload must preserve the same rule and all edited values");
                    Require(Equals(source.SelectedValue, 0) && source.SelectedIndex >= 0 && !Validation.GetHasError(source),
                        "failed reload must not blank or invalidate the unchanged monitor selector");
                    Require(Descendants<ComboBox>(fields).All(combo => combo.SelectedIndex >= 0 && !Validation.GetHasError(combo)),
                        "edge, range, action and destination selections must also remain intact");
                }
                MonitorEdgeTunnel.Instance.LoadSucceeds = true;
                MonitorEdgeTunnel.Instance.SavedTunnels = new System.Collections.Generic.List<TunnelInfo> { TunnelInfo_Notify.Cast(rule), TunnelInfo_Notify.Cast(window.tunnelInfos[1]) };
                TestMessageBox.ConfirmResults.Enqueue(true);
                Click(window, "ResetButton_Click");
                window.selectedTunnel = window.tunnelInfos[0];
                Pump();
                Require(!window.HasUnsavedChanges && Equals(source.SelectedValue, 0) && !Validation.GetHasError(source),
                    "successful reload must bind the newly loaded model to the same available monitors");
                Console.WriteLine("PASS reload failures preserve real editor selections and successful reload rebinds correctly");
            }
            finally { window.Close(); }
        }

        /// <summary>缺少設定檔時確認後清空草稿、服務與預覽，不寫入檔案；取消保留原值，原本運行時恢復服務。</summary>
        private static void TestMissingSettings()
        {
            foreach (bool running in new[] { false, true })
            {
                EditorWorkspace workspace = new EditorWorkspace();
                MainWindow window = CreateWindow(workspace);
                try
                {
                    MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
                    TunnelInfo_Notify rule = window.tunnelInfos[0];
                    rule.rangeType = RangeType.Relative;
                    rule.StartText = "unfinished";
                    window.selectedTunnel = rule;
                    window.forceForbidEdgeChecked = service.ForceForbid = true;
                    service.Running = window.isStart = running;
                    service.SettingsFileExists = false;
                    int errors = TestMessageBox.Calls;
                    Arrange(window, 860, 540);
                    Click(window, "ResetButton_Click");
                    Require(service.LoadCalls == 0 && service.StopCalls == 0 && service.Tunnels.Count == 1 && service.ForceForbid &&
                        ReferenceEquals(window.selectedTunnel, rule) && rule.StartText == "unfinished",
                        "cancelling missing-file reset must preserve draft and applied settings without I/O");
                    TestMessageBox.ConfirmResults.Enqueue(true);
                    Click(window, "ResetButton_Click");
                    Arrange(window, 860, 540);
                    Require(TestMessageBox.Calls == errors && window.WorkspaceNotice.Length == 0 && window.tunnelInfos.Count == 0 &&
                        window.selectedTunnel == null && !window.forceForbidEdgeChecked && !window.HasUnappliedChanges && !window.HasUnsavedChanges,
                        "confirmed missing-file reload must restore empty rules and default options without errors");
                    Require(service.Tunnels.Count == 0 && !service.ForceForbid && !service.SettingsFileExists && service.SaveCalls == 0 && service.ApplyCalls == 0,
                        "missing-file reload must clear native state without saving or applying the discarded draft");
                    ListView list = (ListView)window.FindName("TunnelInfoListView");
                    Controls.MonitorLayoutView preview = (Controls.MonitorLayoutView)window.FindName("MonitorLayout");
                    Require(list.Items.Count == 0 && preview.PreviewSegments.Count == 0 &&
                        ((CheckBox)window.FindName("ForceForbidEdgeCheckbox")).IsChecked == false &&
                        ((FrameworkElement)window.FindName("RuleFields")).DataContext == null,
                        "real rule list, preview, checkbox and selected detail form must all reflect cleared settings");
                    Require(service.Running == running && window.isStart == running && service.StopCalls == (running ? 1 : 0) && service.StartCalls == (running ? 1 : 0),
                        "resetting missing settings must preserve the previous running state");
                    rule.StartText = "12";
                    Require(!window.HasUnsavedChanges, "discarded rules must no longer affect workspace state");
                    int confirmations = TestMessageBox.ConfirmCalls;
                    Click(window, "ResetButton_Click");
                    Require(window.tunnelInfos.Count == 0 && !window.HasUnsavedChanges && TestMessageBox.ConfirmCalls == confirmations &&
                        TestMessageBox.Calls == errors && !service.SettingsFileExists && service.SaveCalls == 0,
                        "repeated reset of empty defaults must remain clean and never create a file");
                    window.Close();
                    MainWindow reopened = new MainWindow(workspace);
                    try
                    {
                        Require(reopened.tunnelInfos.Count == 0 && !reopened.forceForbidEdgeChecked && !reopened.HasUnsavedChanges,
                            "reopening must retain the empty baseline rather than restore the discarded draft");
                    }
                    finally { reopened.Close(); }
                }
                finally { window.Close(); }
            }
            Console.WriteLine("PASS missing-file reload clears rules options preview and applied state without saving");
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
                window.selectedTunnel = rule;
                FrameworkElement content = (FrameworkElement)window.Content;
                content.Measure(new Size(1240, 700));
                content.Arrange(new Rect(0, 0, 1240, 700));
                content.UpdateLayout();
                Pump();
                ListView table = (ListView)window.FindName("TunnelInfoListView");
                DependencyObject row = (DependencyObject)window.FindName("RuleFields");
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

        /// <summary>驗證無效文字不會停止、套用或存檔，但仍可停止正在運行的服務。</summary>
        private static void TestValidationGate()
        {
            MainWindow window = CreateWindow();
            try
            {
                MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
                service.Running = window.isStart = true;
                TunnelInfo_Notify rule = window.tunnelInfos[0];
                rule.rangeType = RangeType.Relative;
                rule.ExtentText = "bad input";
                window.selectedTunnel = rule;
                Arrange(window);
                Require(!window.CanApply && window.CanStartStop && rule.HasErrors, "invalid draft must block writes but allow stop");
                TextBox input = Descendants<TextBox>((DependencyObject)window.Content).Single(box => box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "ExtentText");
                Require(Validation.GetHasError(input) && input.Text == "bad input", "actual input must show a validation error without losing its text");
                Require(rule.ValidationMessage.Split(new[] { rule.ExtentInputError }, StringSplitOptions.None).Length == 2, "validation messages must not duplicate the same input error");
                // 使用較長的說明壓力驗證換行，避免只因目前文字剛好放得下而漏掉配置問題。
                rule.SetValidation(nameof(rule.ExtentText), rule.ValidationMessage + string.Concat(Enumerable.Repeat("請檢查目前螢幕邊緣的可用像素範圍。", 6)), string.Empty, rule.RangeHint);
                Arrange(window, 860, 540);
                TextBlock details = Descendants<TextBlock>((DependencyObject)window.Content).Single(text => text.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path.Path == "selectedTunnel.ValidationMessage");
                FrameworkElement heading = (FrameworkElement)window.FindName("RuleHeader");
                Require(details.ActualWidth <= heading.ActualWidth + 0.1 && details.ActualHeight > 20, "long validation messages must wrap within the compact rule card: " + details.ActualWidth + "x" + details.ActualHeight + ", heading=" + heading.ActualWidth + ", text=" + details.Text);
                Click(window, "ApplyButton_Click");
                Click(window, "SaveSettingButton_Click");
                Require(service.StopCalls == 0 && service.ApplyCalls == 0 && service.SaveCalls == 0 && service.Running,
                    "invalid drafts must be rejected before stopping or changing the service");
                Click(window, "StartStopButton_Click");
                Require(!service.Running && service.StopCalls == 1 && !window.CanStartStop, "stop must remain available even with invalid input");
                rule.ExtentText = "10";
                Pump();
                Require(window.CanApply && !rule.HasErrors && !Validation.GetHasError(input), "correcting input must clear field and global errors");
                Console.WriteLine("PASS invalid inputs never mutate service and stop remains available");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證超界、未選目標、自己與不存在螢幕的錯誤會定位到正確欄位。</summary>
        private static void TestSemanticErrors()
        {
            MainWindow window = CreateWindow();
            try
            {
                TunnelInfo_Notify rule = window.tunnelInfos[0];
                rule.rangeType = RangeType.Relative;
                rule.from = 800;
                Require(rule.GetErrors(nameof(rule.StartText)).Cast<string>().Any() && rule.RangeHint.Contains("799"), "source offset must report actual edge bounds");
                rule.from = 0;
                rule.to = 800;
                Require(rule.GetErrors(nameof(rule.ExtentText)).Cast<string>().Any(), "invalid ending inset must mark extent field");
                rule.to = 0;
                rule.IsTransport = true;
                Require(rule.GetErrors(nameof(rule.relativeID)).Cast<string>().Any() && !window.CanApply, "transport requires a target");
                rule.relativeID = 0;
                Require(rule.GetErrors(nameof(rule.relativeID)).Cast<string>().Any(), "self target must be rejected");
                rule.relativeID = 99;
                Require(rule.GetErrors(nameof(rule.relativeID)).Cast<string>().Any(), "missing target must be rejected");
                rule.IsTransport = false;
                rule.displayID = 99;
                Require(rule.GetErrors(nameof(rule.displayID)).Cast<string>().Any(), "missing monitor must mark monitor field");
                rule.displayID = 0;
                Require(window.CanApply && !rule.HasErrors, "corrected semantic errors must clear");
                Console.WriteLine("PASS semantic validation identifies fields and real pixel limits");
            }
            finally { window.Close(); }
        }

        /// <summary>重疊是可確認的優先順序警告，不作為無法修復的錯誤或重複儲存提示。</summary>
        private static void TestOverlapConfirmation()
        {
            MainWindow window = CreateWindow();
            try
            {
                window.tunnelInfos.Add(new TunnelInfo_Notify { id = 1, displayID = 0 });
                Require(window.CanApply && window.HasOverlapWarnings && !window.tunnelInfos[1].HasErrors, "overlap must remain a warning");
                Click(window, "ApplyButton_Click");
                Require(MonitorEdgeTunnel.Instance.ApplyCalls == 0, "cancelled overlap warning must not change service");
                TestMessageBox.ConfirmResults.Enqueue(true);
                Click(window, "ApplyButton_Click");
                Require(MonitorEdgeTunnel.Instance.ApplyCalls == 1 && !window.HasUnappliedChanges, "confirmed order must be applied");
                int prompts = TestMessageBox.ConfirmCalls;
                Click(window, "SaveSettingButton_Click");
                Require(!window.HasUnsavedChanges && TestMessageBox.ConfirmCalls == prompts, "saving unchanged applied rules must not repeat overlap confirmation");
                Console.WriteLine("PASS overlap warnings require confirmation only for unapplied changes");
            }
            finally { window.Close(); }
        }

        /// <summary>實體配置在編輯與套用之間改變時，不將舊草稿寫入新配置。</summary>
        private static void TestTopologyGate()
        {
            MainWindow window = CreateWindow();
            try
            {
                MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
                window.forceForbidEdgeChecked = true;
                service.Running = true;
                service.Monitors[0] = Monitor(2000);
                Click(window, "ApplyButton_Click");
                Require(service.StopCalls == 0 && service.ApplyCalls == 0 && !window.forceForbidEdgeChecked, "a new topology must be selected without applying old IDs");
                service.Monitors[0] = Monitor(1000);
                window.RefreshDisplayData();
                Require(window.forceForbidEdgeChecked && window.HasUnappliedChanges, "rejected old-topology draft must remain available");
                Console.WriteLine("PASS topology changes between edit and apply retain old draft without writes");
            }
            finally { window.Close(); }
        }

        /// <summary>移除錯誤規則後恢復可套用狀態，舊規則之後改動不再通知視窗。</summary>
        private static void TestDeletedValidation()
        {
            MainWindow window = CreateWindow();
            try
            {
                TunnelInfo_Notify invalid = new TunnelInfo_Notify { id = 1, displayID = 99 };
                window.tunnelInfos.Add(invalid);
                Require(!window.CanApply, "invalid added rule must block apply");
                window.tunnelInfos.Remove(invalid);
                Require(window.CanApply, "deleting invalid rule must clear global error");
                int updates = 0;
                window.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(window.ValidationSummary)) ++updates; };
                invalid.rangeType = RangeType.Relative;
                invalid.ExtentText = "bad";
                Require(updates == 0 && window.CanApply, "deleted models must not keep notifying the window");
                Console.WriteLine("PASS removing invalid rules releases validation subscriptions");
            }
            finally { window.Close(); }
        }

        /// <summary>在透明彈出層實際展開目標選單，自己的 ID 與顯示用項目不能成為候選目標。</summary>
        private static void TestTargetPopup()
        {
            MainWindow window = CreateWindow();
            try
            {
                TunnelInfo_Notify source = window.tunnelInfos[0];
                TunnelInfo_Notify targetRule = new TunnelInfo_Notify { id = 1, displayID = 0, edgeType = EdgeType.Right };
                window.tunnelInfos.Add(targetRule);
                source.IsTransport = true;
                using (HwndSource host = new HwndSource(new HwndSourceParameters("Target choices test")
                    { Width = 1240, Height = 700, WindowStyle = unchecked((int)0x80000000) }))
                {
                    host.RootVisual = (Visual)window.Content;
                    window.selectedTunnel = source;
                    Arrange(window);
                    ListView table = (ListView)window.FindName("TunnelInfoListView");
                    DependencyObject row = (DependencyObject)window.FindName("RuleFields");
                    ComboBox target = Descendants<ComboBox>(row).Single(combo => combo.SelectedValuePath == "id" && combo.Items.Count > 0 && combo.Items[0] is TunnelInfo_Notify);
                    Popup popup = (Popup)target.Template.FindName("PART_Popup", target);
                    popup.Child.Opacity = 0;
                    target.IsDropDownOpen = true;
                    Pump();
                    ComboBoxItem own = (ComboBoxItem)target.ItemContainerGenerator.ContainerFromItem(source);
                    ComboBoxItem other = (ComboBoxItem)target.ItemContainerGenerator.ContainerFromItem(targetRule);
                    Require(own.Visibility == Visibility.Collapsed && other.Visibility == Visibility.Visible, "Popup choices must exclude self while allowing the other rule");
                    target.SelectedValue = 1;
                    target.IsDropDownOpen = false;
                    Pump();
                    Require(source.relativeID == 1 && window.CanApply, "valid popup selection must clear missing-target error");
                    Console.WriteLine("PASS target popup excludes self and resolves required selection");
                }
            }
            finally { window.Close(); }
        }

        /// <summary>確認對話框期間配置改變時取消操作，未完成數字仍可明確捨棄並重新載入。</summary>
        private static void TestModalContextChange()
        {
            MainWindow window = CreateWindow();
            try
            {
                MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
                window.tunnelInfos.Add(new TunnelInfo_Notify { id = 1, displayID = 0 });
                TestMessageBox.ConfirmResults.Enqueue(true);
                TestMessageBox.DuringConfirm = () => { service.Monitors[0] = Monitor(2000); window.RefreshDisplayData(); };
                Click(window, "ApplyButton_Click");
                Require(service.ApplyCalls == 0 && service.StopCalls == 0 && window.tunnelInfos.Count == 1, "confirmation must not authorize applying a newly selected layout");
                service.Monitors[0] = Monitor(1000);
                window.RefreshDisplayData();
                Require(window.tunnelInfos.Count == 2 && window.HasUnappliedChanges, "old draft must survive cancellation after modal layout changes");
                window.tunnelInfos[0].rangeType = RangeType.Relative;
                window.tunnelInfos[0].ExtentText = "unfinished";
                TestMessageBox.ConfirmResults.Enqueue(true);
                Click(window, "ResetButton_Click");
                Require(service.LoadCalls == 1 && window.CanApply && !window.HasUnsavedChanges, "confirmed reload must allow discarding invalid raw input");
                Console.WriteLine("PASS modal layout changes cancel writes and invalid drafts can be discarded explicitly");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證非連續或不同順序的 ID 在刪除重編號後仍指向同一規則，新增也不能使用已占用 ID。</summary>
        private static void TestSparseRuleIdentity()
        {
            MainWindow window = CreateWindow();
            try
            {
                window.tunnelInfos.Clear();
                TunnelInfo_Notify first = new TunnelInfo_Notify { id = 1, displayID = 0, relativeID = 2 };
                TunnelInfo_Notify second = new TunnelInfo_Notify { id = 2, displayID = 0, relativeID = 1, edgeType = EdgeType.Right };
                TunnelInfo_Notify third = new TunnelInfo_Notify { id = 0, displayID = 0, edgeType = EdgeType.Top };
                TunnelInfo_Notify removed = new TunnelInfo_Notify { id = 3, displayID = 0, edgeType = EdgeType.Bottom };
                window.tunnelInfos.Add(first);
                window.tunnelInfos.Add(second);
                window.tunnelInfos.Add(third);
                window.tunnelInfos.Add(removed);
                Arrange(window);
                typeof(MainWindow).GetMethod("TunnelInfoRemoveButton_Click", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(window, new object[] { new Button { DataContext = removed }, new RoutedEventArgs() });
                Pump();
                Require(first.id == 0 && second.id == 1 && first.relativeID == second.id && second.relativeID == first.id,
                    "renumbering unsorted IDs must preserve both original target objects");
                window.tunnelInfos.Clear();
                window.tunnelInfos.Add(new TunnelInfo_Notify { id = 1, displayID = 0 });
                Click(window, "AddTunnelInfoButton_Click");
                Require(window.tunnelInfos.Select(rule => rule.id).Distinct().Count() == window.tunnelInfos.Count,
                    "adding with sparse IDs must choose an unused native ID");
                Console.WriteLine("PASS sparse rule IDs preserve targets across deletion and creation");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證雙向預選、稀疏 ID、單向目標及原生 DTO，新增過程只改草稿。</summary>
        private static void TestConnectionCreation()
        {
            MainWindow window = CreateWindow();
            try
            {
                MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
                service.Monitors.Add(new MonitorInfo { id = 1, left = -1000, right = -1, bottom = 799, scaling = 1 });
                window.RefreshDisplayData();
                window.tunnelInfos.Add(new TunnelInfo_Notify { id = 2, displayID = 0, edgeType = EdgeType.Top });
                TunnelInfo_Notify original = window.tunnelInfos[0];
                TunnelConnectionDraft draft = new TunnelConnectionDraft(window.monitorInfos.ToArray(), window.tunnelInfos.ToArray(), 1);
                Require(draft.Bidirectional && draft.SourceMonitorId == 1 && draft.TargetMonitorId == 0 && draft.SourceEdge == EdgeType.Right && draft.TargetEdge == EdgeType.Left,
                    "connection defaults must use the selected monitor and facing edges");
                Require(window.AddConnection(draft), "valid bidirectional selection must be added");
                TunnelInfo_Notify first = window.tunnelInfos[2], second = window.tunnelInfos[3];
                Require(first.id == 1 && second.id == 3 && first.relativeID == second.id && second.relativeID == first.id,
                    "both directions must use distinct free IDs without rewriting existing rules");
                Require(first.displayID == 1 && second.displayID == 0 && first.rangeType == RangeType.Full && second.rangeType == RangeType.Full &&
                    ReferenceEquals(window.selectedTunnel, first) && ReferenceEquals(window.tunnelInfos[0], original) && window.HasUnappliedChanges,
                    "both full edges must be visible as an unapplied draft with source selected");
                Require(TunnelInfo_Notify.Cast(first).relativeID == 3 && window.CanApply, "native projection must retain paired references");
                draft.Bidirectional = false;
                draft.SourceEdge = EdgeType.Top;
                draft.TargetEdge = EdgeType.Bottom;
                Require(window.AddConnection(draft) && window.tunnelInfos[4].relativeID == window.tunnelInfos[5].id && window.tunnelInfos[5].relativeID == -1,
                    "one-way creation still requires a destination rule, with return blocked");
                Require(service.ApplyCalls == 0 && service.LoadCalls == 0 && service.SaveCalls == 0 && service.StartCalls == 0,
                    "creation must not apply, load, save or start native state");
                Console.WriteLine("PASS paired creation allocates free IDs and preserves one-way native semantics");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證相同邊緣、ID 不足及配置變更皆不留下半組連接；目前草稿的原有錯誤不阻擋獨立有效連接。</summary>
        private static void TestConnectionGuards()
        {
            MainWindow window = CreateWindow();
            try
            {
                TunnelConnectionDraft draft = new TunnelConnectionDraft(window.monitorInfos.ToArray(), window.tunnelInfos.ToArray(), 0);
                draft.TargetEdge = draft.SourceEdge;
                Require(!draft.CanCreate && !window.AddConnection(draft) && window.tunnelInfos.Count == 1, "identical endpoints must not create any rules");
                draft.TargetEdge = EdgeType.Left;
                window.tunnelInfos[0].rangeType = RangeType.Relative;
                window.tunnelInfos[0].StartText = "unfinished";
                Require(window.AddConnection(draft) && window.tunnelInfos.Count == 3 && window.tunnelInfos[0].StartText == "unfinished",
                    "unrelated invalid text must survive successful pair creation");
                window.tunnelInfos.Clear();
                for (int id = 0; id < 100; ++id) window.tunnelInfos.Add(new TunnelInfo_Notify { id = id });
                TunnelConnectionDraft crowded = new TunnelConnectionDraft(window.monitorInfos.ToArray(), window.tunnelInfos.ToArray(), 0);
                Require(!window.CanConnectMonitors && !crowded.CanCreate && !window.AddConnection(draft) && window.tunnelInfos.Count == 100,
                    "fresh allocation must refuse a pair when only one ID remains");
                MonitorEdgeTunnel.Instance.Monitors[0] = Monitor(2000);
                Require(!window.AddConnection(draft) && window.tunnelInfos.All(rule => rule.relativeID == -1) && window.WorkspaceNotice.Contains("配置已變更"),
                    "hotplug must reject stale endpoints and restore the new layout without adding half a pair");
                Console.WriteLine("PASS connection guards reject stale layouts and insufficient IDs without partial creation");
            }
            finally { window.Close(); }
        }

        /// <summary>以實際 WPF 對話框驗證選擇、停用確認及取消不更動主視窗，僅在 STA UI 執行緒執行。</summary>
        private static void TestConnectionDialog()
        {
            MainWindow window = CreateWindow();
            TunnelConnectionDraft draft = new TunnelConnectionDraft(window.monitorInfos.ToArray(), window.tunnelInfos.ToArray(), 0);
            TunnelConnectionDialog dialog = new TunnelConnectionDialog(draft) { Opacity = 0, WindowStartupLocation = WindowStartupLocation.Manual, Left = -16000, Top = -16000 };
            try
            {
                dialog.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Button create = Descendants<Button>(dialog).Single(button => Equals(button.Content, "建立通道"));
                    Button cancel = Descendants<Button>(dialog).Single(button => Equals(button.Content, "取消"));
                    ComboBox targetEdge = Descendants<ComboBox>(dialog).Single(combo => combo.GetBindingExpression(ComboBox.SelectedItemProperty)?.ParentBinding.Path.Path == "TargetEdge");
                    targetEdge.SelectedItem = draft.SourceEdge;
                    Pump();
                    Require(!create.IsEnabled && cancel.IsCancel && cancel.IsDefault, "invalid endpoints must disable confirmation and retain cancel as default");
                    targetEdge.SelectedItem = EdgeType.Left;
                    Pump();
                    Require(create.IsEnabled && draft.TargetEdge == EdgeType.Left, "real endpoint binding must restore valid confirmation");
                    dialog.Close();
                }), DispatcherPriority.ApplicationIdle);
                Require(dialog.ShowDialog() != true && window.tunnelInfos.Count == 1 && !window.HasUnappliedChanges,
                    "closing the composer must not edit the main draft");
                Console.WriteLine("PASS themed connection dialog binds endpoints and cancels without changing rules");
            }
            finally { dialog.Close(); window.Close(); }
        }

        /// <summary>在實際 WPF 主視覺驗證窄寬切換、垂直捲動、規則切換及分隔線鍵盤調整不遺失輸入。</summary>
        private static void TestResponsiveEditor()
        {
            MainWindow window = CreateWindow();
            try
            {
                TunnelInfo_Notify rule = window.tunnelInfos[0];
                rule.rangeType = RangeType.Relative;
                rule.StartText = "007";
                rule.ExtentText = "unfinished";
                for (int id = 1; id <= 12; ++id) window.tunnelInfos.Add(new TunnelInfo_Notify { id = id });
                window.selectedTunnel = rule;
                using (HwndSource host = new HwndSource(new HwndSourceParameters("Responsive editor")
                    { Width = 1240, Height = 700, WindowStyle = unchecked((int)0x80000000) }))
                {
                    host.RootVisual = (Visual)window.Content;
                    Arrange(window);
                    ListView list = (ListView)window.FindName("TunnelInfoListView");
                    FrameworkElement editor = (FrameworkElement)window.FindName("RuleEditor");
                    DependencyObject fields = (DependencyObject)window.FindName("RuleFields");
                    TextBox input = Descendants<TextBox>(fields).Single(box => box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "ExtentText");
                    Require(!Validation.GetHasError(fields) && Validation.GetHasError(input), "only the invalid field should carry a validation adorner, not the whole form");
                    Require(!window.IsCompactEditor && editor.TranslatePoint(new Point(), (UIElement)window.Content).X > list.TranslatePoint(new Point(), (UIElement)window.Content).X + list.ActualWidth,
                        "wide workspace must place summary and form side by side");
                    Arrange(window, 860, 540);
                    ScrollViewer scroll = (ScrollViewer)window.FindName("RuleEditorScroll");
                    Require(window.IsCompactEditor && editor.TranslatePoint(new Point(), (UIElement)window.Content).Y >= list.TranslatePoint(new Point(), (UIElement)window.Content).Y + list.ActualHeight,
                        "compact workspace must stack the form below the summary");
                    Require(scroll.ScrollableHeight > 0 && scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled &&
                        Descendants<ScrollViewer>((DependencyObject)window.FindName("RuleWorkspace")).All(view => view.ScrollableWidth < 0.1),
                        "compact controls must scroll vertically without horizontal overflow");
                    input.BringIntoView();
                    Pump();
                    Point position = input.TranslatePoint(new Point(), scroll);
                    Require(input.ActualWidth >= 100 && position.Y >= -0.1 && position.Y + input.ActualHeight <= scroll.ViewportHeight + 1,
                        "numeric field must be readable and reachable inside the compact form");
                    list.SelectedItem = window.tunnelInfos[5];
                    Pump();
                    Require(ReferenceEquals(((FrameworkElement)fields).DataContext, window.tunnelInfos[5]), "selection must show only the selected rule's fields");
                    list.SelectedItem = rule;
                    Pump();
                    Require(input.Text == "unfinished" && rule.StartText == "007", "switching rules must preserve unfinished and unnormalized input");
                    Arrange(window);
                    ColumnDefinition previewColumn = (ColumnDefinition)window.FindName("PreviewColumn");
                    double before = previewColumn.ActualWidth;
                    GridSplitter splitter = (GridSplitter)window.FindName("WorkspaceSplitter");
                    splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, host, 0, Key.Right) { RoutedEvent = Keyboard.KeyDownEvent });
                    Arrange(window);
                    Require(previewColumn.ActualWidth > before && ReferenceEquals(window.selectedTunnel, rule) && ReferenceEquals(input,
                        Descendants<TextBox>(fields).Single(box => box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "ExtentText")),
                        "actual splitter keyboard interaction must resize panes without rebuilding the editor");
                    previewColumn.Width = new GridLength(620);
                    Arrange(window);
                    Require(window.IsCompactEditor && input.Text == "unfinished" && ReferenceEquals(window.selectedTunnel, rule),
                        "pane width changes must adapt the editor independently of window width");
                    Require(MonitorEdgeTunnel.Instance.LoadCalls == 0 && MonitorEdgeTunnel.Instance.ApplyCalls == 0,
                        "responsive layout must not read settings or apply drafts");
                }
                Console.WriteLine("PASS compact forms scroll vertically and splitter resizing preserves selection and unfinished input");
            }
            finally { window.Close(); }
        }

        /// <summary>新增空白規則立即顯示詳細表單，刪除目前規則後選取相鄰規則，避免清單與編輯器脫節。</summary>
        private static void TestSelectionAfterAddDelete()
        {
            MainWindow window = CreateWindow();
            try
            {
                Click(window, "AddTunnelInfoButton_Click");
                TunnelInfo_Notify added = window.tunnelInfos.Last();
                Arrange(window, 860, 540);
                Require(ReferenceEquals(window.selectedTunnel, added) && ReferenceEquals(((FrameworkElement)window.FindName("RuleFields")).DataContext, added),
                    "new blank rule must open in the selected detail form");
                typeof(MainWindow).GetMethod("TunnelInfoRemoveButton_Click", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(window, new object[] { new Button { DataContext = added }, new RoutedEventArgs() });
                Pump();
                Require(ReferenceEquals(window.selectedTunnel, window.tunnelInfos[0]) && ReferenceEquals(((FrameworkElement)window.FindName("RuleFields")).DataContext, window.selectedTunnel),
                    "deleting the selected rule must keep adjacent details usable");
                Console.WriteLine("PASS add and delete keep the selected detail form synchronized");
            }
            finally { window.Close(); }
        }

        /// <summary>配置實際控制項並完成綁定，供查詢欄位與操作彈出層。</summary>
        /// <param name="window">受測主視窗。</param>
        /// <param name="width">內容寬度，預設為一般桌面視窗。</param>
        /// <param name="height">內容高度，預設為一般桌面視窗。</param>
        private static void Arrange(MainWindow window, double width = 1240, double height = 700)
        {
            FrameworkElement content = (FrameworkElement)window.Content;
            content.Measure(new Size(width, height));
            content.Arrange(new Rect(0, 0, width, height));
            content.UpdateLayout();
            Pump();
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
            service.LoadCalls = service.ApplyCalls = service.SaveCalls = service.StopCalls = service.StartCalls = 0;
            service.ErrorCode = MonitorEdgeTunnelErrorMsg.LoadSettingFail;
            service.SavedTunnels = null;
            service.SettingsFileExists = true;
            service.Monitors.Clear();
            service.Monitors.Add(Monitor(1000));
            service.Tunnels.Clear();
            service.Tunnels.Add(new TunnelInfo { id = 0, displayID = 0, relativeID = -1 });
            TestMessageBox.ConfirmResults.Clear();
            TestMessageBox.DuringConfirm = null;
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
