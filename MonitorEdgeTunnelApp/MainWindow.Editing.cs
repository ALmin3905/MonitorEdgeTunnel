using MonitorEdgeTunnelApp.Editing;
using System;
using MonitorEdgeTunnelApp.Controls;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace MonitorEdgeTunnelApp
{
    /// <summary>協調編輯工作階段、視窗生命週期與原生設定基準；所有成員限 UI 執行緒。</summary>
    public partial class MainWindow
    {
        /// <summary>由應用程式持有，讓關閉視窗與切換配置不會丟失草稿。</summary>
        private readonly EditorWorkspace _workspace;
        /// <summary>由應用程式提供含未儲存提醒的結束流程。</summary>
        private readonly Action _requestExit;
        /// <summary>目前配置的編輯工作階段。</summary>
        private EditorSession _session;
        /// <summary>切換配置或恢復草稿時的非阻斷提示。</summary>
        private string _workspaceNotice = string.Empty;
        /// <summary>右側空間不足時改成摘要在上、表單在下，保留原有控制項與輸入。</summary>
        private bool _isCompactEditor = true;
        /// <summary>僅在使用辨識時建立，隨主視窗或配置變更結束。</summary>
        private MonitorIdentification _identification;
        /// <summary>建立依主視窗生命週期管理的辨識控制器，允許測試提供透明覆蓋視窗。</summary>
        private readonly Func<Window, MonitorIdentification> _createIdentification;
        /// <summary>合併同一輪輸入與驗證通知，避免對所有實體螢幕重複重畫。</summary>
        private bool _identificationUpdatePending;
        /// <summary>沒有螢幕資料時停用辨識操作。</summary>
        public bool CanIdentifyMonitors => monitorInfos.Count > 0;
        /// <summary>辨識入口或關閉倒數，不改變服務啟動狀態。</summary>
        public string IdentifyMonitorsCaption => _identification?.IsVisible == true ? "結束辨識（" + _identification.SecondsRemaining + "）" : "辨識螢幕與通道";
        /// <summary>依規則區實際寬度切換排列，因此拖曳分隔線也會觸發調整。</summary>
        public bool IsCompactEditor => _isCompactEditor;
        /// <summary>顯示草稿、服務記憶體與設定檔的差異。</summary>
        public string EditStatus => _session?.Status ?? "已儲存";
        /// <summary>是否有尚未套用的草稿。</summary>
        public bool HasUnappliedChanges => _session?.HasUnappliedChanges ?? false;
        /// <summary>目前配置是否有尚未儲存的草稿或已套用設定。</summary>
        public bool HasUnsavedChanges => _session?.HasUnsavedChanges ?? false;
        /// <summary>提醒使用者草稿已保留，空字串時不顯示。</summary>
        public string WorkspaceNotice => _workspaceNotice;
        /// <summary>原生 ID 僅允許 0 到 100，有可用 ID 時才開放新增。</summary>
        public bool CanAddRule => _session != null && Enumerable.Range(0, 101).Any(id => tunnelInfos.All(rule => rule.id != id));

        /// <summary>連接的來源與目標共需兩個 ID，至少有一個螢幕時才開放。</summary>
        public bool CanConnectMonitors => _session != null && monitorInfos.Count > 0 && Enumerable.Range(0, 101).Count(id => tunnelInfos.All(rule => rule.id != id)) >= 2;

        #region -- Public Methods --

        /// <summary>更新目前螢幕配置並恢復其草稿；不從檔案重載，不覆蓋其他配置的編輯。</summary>
        public void RefreshDisplayData()
        {
            Dispatcher.VerifyAccess();
            _identification?.Stop();
            EditorSession previous = _session;
            if (previous != null) previous.PropertyChanged -= SessionChanged;
            int? previousSelection = selectedMonitorId;
            MonitorInfo[] monitors = MonitorEdgeTunnel.Instance.GetMonitorInfoList().ToArray();
            // 配置相同時保留選單來源；Clear 會讓雙向 SelectedValue 暫時變成 null，留下轉型錯誤及空白選取。
            if (EditorWorkspace.LayoutKey(monitorInfos) != EditorWorkspace.LayoutKey(monitors))
            {
                monitorInfos.Clear();
                foreach (MonitorInfo monitor in monitors) monitorInfos.Add(monitor);
            }
            selectedMonitorId = monitorInfos.Any(monitor => monitor.id == previousSelection) ? previousSelection :
                monitorInfos.Count > 0 ? (int?)monitorInfos[0].id : null;
            _session = _workspace.Activate(monitorInfos, ReadNativeSettings());
            _session.PropertyChanged += SessionChanged;
            if (!ReferenceEquals(previous, _session)) selectedTunnel = null;
            _workspaceNotice = previous != null && !ReferenceEquals(previous, _session) ?
                "已切換螢幕配置；原配置草稿已保留，切回後可繼續編輯。" :
                previous == null && HasUnsavedChanges ? "已還原這組螢幕配置的草稿。" : string.Empty;
            isStart = MonitorEdgeTunnel.Instance.IsStart();
            NotifySession();
        }

        /// <summary>確認最新配置與可用 ID 後一次加入兩端規則，選取來源；僅修改 UI 草稿，不套用或存檔。</summary>
        /// <param name="draft">對話框確認的兩端選擇，熱插拔後的過期選擇會拒絕。</param>
        /// <returns>完整加入兩筆時為 true，失敗時保留原有規則並顯示原因。</returns>
        internal bool AddConnection(TunnelConnectionDraft draft)
        {
            Dispatcher.VerifyAccess();
            if (EditorWorkspace.LayoutKey(MonitorEdgeTunnel.Instance.GetMonitorInfoList()) != EditorWorkspace.LayoutKey(monitorInfos))
            {
                RefreshDisplayData();
                _workspaceNotice = "螢幕配置已變更，請重新選擇兩端。";
                OnPropertyChanged(nameof(WorkspaceNotice));
                return false;
            }
            if (!draft.TryCreate(monitorInfos.ToArray(), tunnelInfos.ToArray(), out TunnelInfo_Notify[] pair, out string error))
            {
                _workspaceNotice = error;
                OnPropertyChanged(nameof(WorkspaceNotice));
                return false;
            }
            foreach (TunnelInfo_Notify rule in pair) tunnelInfos.Add(rule);
            selectedTunnel = pair[0];
            _workspaceNotice = draft.Bidirectional ? "已建立雙向通道；套用後生效。" : "已建立單向通道與目標邊緣；套用後生效。";
            NotifySession();
            return true;
        }

        #endregion

        #region -- Private Methods --

        /// <summary>切換短暫的實體螢幕辨識，不套用或儲存任何設定；限 UI 執行緒。</summary>
        /// <param name="sender">辨識螢幕按鈕。</param>
        /// <param name="e">點擊事件。</param>
        private void IdentifyMonitorsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_identification == null)
            {
                _identification = _createIdentification(this);
                _identification.Changed += IdentificationChanged;
            }
            if (_identification.IsVisible) { _identification.Stop(); return; }
            try
            {
                if (Content is DependencyObject root) CommitTextInputs(root);
                if (EditorWorkspace.LayoutKey(MonitorEdgeTunnel.Instance.GetMonitorInfoList()) != EditorWorkspace.LayoutKey(monitorInfos)) RefreshDisplayData();
                _identification.Show(monitorInfos.ToArray());
                UpdateIdentificationPreview();
            }
            catch (Exception error) when (error is Win32Exception || error is InvalidOperationException)
            {
                Themes.ThemedMessageBox.Show("無法顯示螢幕辨識：" + error.Message, "螢幕辨識", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>辨識開始、倒數或結束時更新操作文字；限 UI 執行緒。</summary>
        /// <param name="sender">目前辨識控制器。</param>
        /// <param name="e">狀態變更事件。</param>
        private void IdentificationChanged(object sender, EventArgs e) => OnPropertyChanged(nameof(IdentifyMonitorsCaption));

        /// <summary>合併目前草稿與選取的實體標記更新，不延長倒數；限 UI 執行緒。</summary>
        private void UpdateIdentificationPreview()
        {
            if (_identification?.IsVisible != true || _identificationUpdatePending) return;
            _identificationUpdatePending = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
            {
                _identificationUpdatePending = false;
                if (_identification?.IsVisible == true)
                    _identification.UpdateRules(monitorInfos.ToArray(), tunnelInfos.ToArray(), forceForbidEdgeChecked, selectedTunnel, selectedMonitorId);
            }));
        }

        /// <summary>在 UI 執行緒依可用寬度切換摘要與表單排列，不重建控制項、不修改或套用草稿。</summary>
        /// <param name="sender">規則編輯區。</param>
        /// <param name="e">包含編輯區最新 DIP 寬度的配置事件。</param>
        private void RuleWorkspace_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool compact = e.NewSize.Width < 660;
            if (_isCompactEditor == compact) return;
            _isCompactEditor = compact;
            OnPropertyChanged(nameof(IsCompactEditor));
        }

        /// <summary>在 UI 執行緒選擇兩端，確認後才加入草稿；取消或配置變更時保留原有規則。</summary>
        /// <param name="sender">連接螢幕按鈕。</param>
        /// <param name="e">點擊事件資料。</param>
        private void ConnectMonitorsButton_Click(object sender, RoutedEventArgs e)
        {
            EditorSession pending = _session;
            TunnelSettingsSnapshot settings = pending.Capture();
            string layout = EditorWorkspace.LayoutKey(monitorInfos);
            TunnelConnectionDraft draft = new TunnelConnectionDraft(monitorInfos.ToArray(), tunnelInfos.ToArray(), selectedMonitorId);
            TunnelConnectionDialog dialog = new TunnelConnectionDialog(draft) { Owner = this };
            if (dialog.ShowDialog() == true && OperationStillCurrent(pending, settings, layout)) AddConnection(draft);
        }

        /// <summary>讀取目前服務設定，不重載檔案或改動原生狀態。</summary>
        /// <returns>包含規則及邊緣封鎖選項的獨立快照。</returns>
        private static TunnelSettingsSnapshot ReadNativeSettings() => new TunnelSettingsSnapshot(
            MonitorEdgeTunnel.Instance.GetCurrentTunnelInfoList(), MonitorEdgeTunnel.Instance.IsCurrentForceForbidEdge());

        /// <summary>草稿或基準變更時通知畫面狀態，不套用或儲存設定。</summary>
        /// <param name="sender">目前編輯工作階段。</param>
        /// <param name="e">狀態變更通知。</param>
        private void SessionChanged(object sender, PropertyChangedEventArgs e) => NotifySession();

        /// <summary>更新目前配置的綁定及狀態，保留編輯物件的參照。</summary>
        private void NotifySession()
        {
            RevalidateSession();
            OnPropertyChanged(nameof(tunnelInfos));
            OnPropertyChanged(nameof(forceForbidEdgeChecked));
            OnPropertyChanged(nameof(EditStatus));
            OnPropertyChanged(nameof(HasUnappliedChanges));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(WorkspaceNotice));
            OnPropertyChanged(nameof(CanAddRule));
            OnPropertyChanged(nameof(CanConnectMonitors));
            OnPropertyChanged(nameof(CanIdentifyMonitors));
            UpdateIdentificationPreview();
        }

        /// <summary>視窗關閉時解除通知，草稿仍由應用程式工作區保留。</summary>
        /// <param name="sender">已關閉的主視窗。</param>
        /// <param name="e">關閉事件資料。</param>
        private void EditorWindowClosed(object sender, EventArgs e)
        {
            if (_identification != null) _identification.Changed -= IdentificationChanged;
            if (_session != null) _session.PropertyChanged -= SessionChanged;
        }

        #endregion
    }
}
