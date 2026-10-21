using MonitorEdgeTunnelApp.Controls;
using MonitorEdgeTunnelApp.Editing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MessageBox = MonitorEdgeTunnelApp.Themes.ThemedMessageBox;

namespace MonitorEdgeTunnelApp
{
    /// <summary>將相同驗證結果提供給欄位、預覽與原生操作閘門；只在 UI 執行緒運作。</summary>
    public partial class MainWindow
    {
        /// <summary>目前已完成的規則驗證快照。</summary>
        private IReadOnlyList<TunnelPreviewSegment> _validationSegments = new TunnelPreviewSegment[0];
        /// <summary>是否存在阻止套用的錯誤，包括沒有可用螢幕。</summary>
        private bool _hasValidationErrors;
        /// <summary>是否存在可確認後繼續套用的優先權警告。</summary>
        private bool _hasOverlapWarnings;
        /// <summary>表格標題旁顯示的驗證摘要。</summary>
        private string _validationSummary = string.Empty;
        /// <summary>防止驗證通知再度進入同一輪更新。</summary>
        private bool _validating;
        /// <summary>目前草稿通過檢查才允許套用或儲存。</summary>
        public bool CanApply => !_hasValidationErrors;
        /// <summary>錯誤草稿仍可停止正在運行的服務，但不能啟動。</summary>
        public bool CanStartStop => isStart || CanApply;
        /// <summary>目前是否有阻止套用的錯誤。</summary>
        public bool HasValidationErrors => _hasValidationErrors;
        /// <summary>是否有重疊規則需要注意其順序。</summary>
        public bool HasOverlapWarnings => _hasOverlapWarnings;
        /// <summary>錯誤、警告及規則數量的簡短說明。</summary>
        public string ValidationSummary => _validationSummary;

        #region -- Private Methods --

        /// <summary>重算所有編輯規則並定位錯誤欄位，過程不呼叫原生服務。</summary>
        private void RevalidateSession()
        {
            if (_session == null || _validating) return;
            _validating = true;
            try
            {
                _validationSegments = EditorValidation.Evaluate(monitorInfos.ToList(), tunnelInfos.ToList());
                foreach (TunnelPreviewSegment segment in _validationSegments)
                {
                    TunnelInfo_Notify rule = tunnelInfos[segment.RuleIndex];
                    string hint = RangeHint(rule, segment);
                    string message = segment.Error;
                    if (message.Length > 0 && (segment.ErrorField == TunnelValidationField.Start || segment.ErrorField == TunnelValidationField.Extent))
                        message += "。" + hint;
                    rule.SetValidation(EditorValidation.PropertyName(segment.ErrorField), message, segment.Warning, hint);
                }
                int errors = _validationSegments.Count(segment => segment.Error.Length > 0);
                int warnings = _validationSegments.Count(segment => segment.Warning.Length > 0 && segment.Error.Length == 0);
                _hasValidationErrors = errors > 0 || monitorInfos.Count == 0;
                _hasOverlapWarnings = warnings > 0;
                _validationSummary = monitorInfos.Count == 0 ? "目前沒有可用螢幕，無法套用" : tunnelInfos.Count + " 條規則 · " +
                    (errors > 0 ? errors + " 筆錯誤，請修正後套用" : warnings > 0 ? warnings + " 筆重疊警告，前面的規則優先" : "檢查通過");
                foreach (string property in new[] { nameof(CanApply), nameof(CanStartStop), nameof(HasValidationErrors), nameof(HasOverlapWarnings), nameof(ValidationSummary) })
                    OnPropertyChanged(property);
            }
            finally { _validating = false; }
        }

        /// <summary>取得目前邊緣可用像素範圍，供錯誤及一般欄位提示共用。</summary>
        /// <param name="rule">目前編輯規則。</param>
        /// <param name="segment">已定位來源螢幕的計算結果。</param>
        /// <returns>不含桌面絕對座標的內縮、偏移或長度說明。</returns>
        private string RangeHint(TunnelInfo_Notify rule, TunnelPreviewSegment segment)
        {
            if (segment.MonitorIndex < 0) return "請先選擇可用螢幕。";
            MonitorInfo monitor = monitorInfos[segment.MonitorIndex];
            long span = rule.edgeType == EdgeType.Left || rule.edgeType == EdgeType.Right ? (long)monitor.bottom - monitor.top + 1 : (long)monitor.right - monitor.left + 1;
            if (span <= 0) return "螢幕邊界無效。";
            if (rule.rangeType == RangeType.Full) return "使用完整邊緣，共 " + span + " px。";
            int minimum = rule.IsTransport ? 2 : 1;
            if (rule.rangeType == RangeType.Relative) return "起點與終點內縮合計最多 " + System.Math.Max(0, span - minimum) + " px，至少保留 " + minimum + " px。";
            return "起點偏移 0–" + System.Math.Max(0, span - minimum) + " px；此起點的長度範圍 " + minimum + "–" + System.Math.Max(0, span - rule.from) + " px。";
        }

        /// <summary>原生操作前同步輸入、檢查即時螢幕配置，再確認可接受的重疊警告。</summary>
        /// <returns>只有有效且已確認的草稿才為 true；false 時尚未停止或修改服務。</returns>
        private bool PrepareApply()
        {
            if (Content is DependencyObject root) CommitTextInputs(root);
            if (EditorWorkspace.LayoutKey(MonitorEdgeTunnel.Instance.GetMonitorInfoList()) != EditorWorkspace.LayoutKey(monitorInfos))
            {
                RefreshDisplayData();
                _workspaceNotice = "螢幕配置剛變更，原草稿已保留。請檢查目前配置後再套用。";
                OnPropertyChanged(nameof(WorkspaceNotice));
                return false;
            }
            RevalidateSession();
            if (!CanApply)
            {
                TunnelPreviewSegment invalid = _validationSegments.FirstOrDefault(segment => segment.Error.Length > 0);
                if (invalid != null) selectedTunnel = tunnelInfos[invalid.RuleIndex];
                return false;
            }
            EditorSession session = _session;
            TunnelSettingsSnapshot settings = session.Capture();
            string layout = EditorWorkspace.LayoutKey(monitorInfos);
            if (HasOverlapWarnings && HasUnappliedChanges && !MessageBox.Confirm(
                "規則有重疊區段，重疊時以清單前面的規則優先匹配。仍要套用目前順序嗎？", "確認重疊規則", "仍要套用")) return false;
            if (!OperationStillCurrent(session, settings, layout)) return false;
            RevalidateSession();
            return CanApply;
        }

        /// <summary>確認對話框可能處理螢幕通知，返回後必須確認仍是相同配置與草稿。</summary>
        /// <param name="session">提示前的工作階段。</param>
        /// <param name="settings">提示前的設定值。</param>
        /// <param name="layout">提示前的螢幕配置。</param>
        /// <returns>草稿及實際配置未變時為 true，否則保留草稿並取消本次操作。</returns>
        private bool OperationStillCurrent(EditorSession session, TunnelSettingsSnapshot settings, string layout)
        {
            bool layoutChanged = EditorWorkspace.LayoutKey(MonitorEdgeTunnel.Instance.GetMonitorInfoList()) != layout;
            if (layoutChanged) RefreshDisplayData();
            if (!layoutChanged && ReferenceEquals(session, _session) && settings.Matches(_session.Capture())) return true;
            _workspaceNotice = "草稿或螢幕配置已改變，本次操作已取消；原草稿仍保留，請重新確認。";
            OnPropertyChanged(nameof(WorkspaceNotice));
            return false;
        }

        /// <summary>將實際文字框的最新內容送回草稿，包含尚未失去焦點的編輯。</summary>
        /// <param name="root">目前視窗內容的視覺根節點。</param>
        private static void CommitTextInputs(DependencyObject root)
        {
            if (root is TextBox input) input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i) CommitTextInputs(VisualTreeHelper.GetChild(root, i));
        }

        #endregion
    }
}
