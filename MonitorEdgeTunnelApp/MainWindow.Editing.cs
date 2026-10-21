using MonitorEdgeTunnelApp.Editing;
using System;
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
        /// <summary>顯示草稿、服務記憶體與設定檔的差異。</summary>
        public string EditStatus => _session?.Status ?? "已儲存";
        /// <summary>是否有尚未套用的草稿。</summary>
        public bool HasUnappliedChanges => _session?.HasUnappliedChanges ?? false;
        /// <summary>目前配置是否有尚未儲存的草稿或已套用設定。</summary>
        public bool HasUnsavedChanges => _session?.HasUnsavedChanges ?? false;
        /// <summary>提醒使用者草稿已保留，空字串時不顯示。</summary>
        public string WorkspaceNotice => _workspaceNotice;

        #region -- Public Methods --

        /// <summary>更新目前螢幕配置並恢復其草稿；不從檔案重載，不覆蓋其他配置的編輯。</summary>
        public void RefreshDisplayData()
        {
            Dispatcher.VerifyAccess();
            EditorSession previous = _session;
            if (previous != null) previous.PropertyChanged -= SessionChanged;
            int? previousSelection = selectedMonitorId;
            monitorInfos.Clear();
            foreach (MonitorInfo monitor in MonitorEdgeTunnel.Instance.GetMonitorInfoList()) monitorInfos.Add(monitor);
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

        #endregion

        #region -- Private Methods --

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
            OnPropertyChanged(nameof(tunnelInfos));
            OnPropertyChanged(nameof(forceForbidEdgeChecked));
            OnPropertyChanged(nameof(EditStatus));
            OnPropertyChanged(nameof(HasUnappliedChanges));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(WorkspaceNotice));
        }

        /// <summary>視窗關閉時解除通知，草稿仍由應用程式工作區保留。</summary>
        /// <param name="sender">已關閉的主視窗。</param>
        /// <param name="e">關閉事件資料。</param>
        private void EditorWindowClosed(object sender, EventArgs e)
        {
            if (_session != null) _session.PropertyChanged -= SessionChanged;
        }

        #endregion
    }
}
