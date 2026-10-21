using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace MonitorEdgeTunnelApp.Editing
{
    /// <summary>在 UI 執行緒持有單一螢幕配置的草稿與兩份基準，視窗關閉後仍由工作區保留。</summary>
    internal sealed class EditorSession : INotifyPropertyChanged
    {
        /// <summary>草稿或設定基準變更的 UI 通知。</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>目前已訂閱的規則，重置或刪除時解除舊事件。</summary>
        private readonly HashSet<TunnelInfo_Notify> _observed = new HashSet<TunnelInfo_Notify>();
        /// <summary>編輯中的邊緣封鎖選項。</summary>
        private bool _forceForbid;
        /// <summary>服務記憶體中目前採用的設定。</summary>
        private TunnelSettingsSnapshot _applied;
        /// <summary>最後確認儲存成功的設定。</summary>
        private TunnelSettingsSnapshot _saved;
        /// <summary>重新載入期間暫停重複狀態通知。</summary>
        private bool _loading;
        /// <summary>保留原始編輯物件及規則順序。</summary>
        internal ObservableCollection<TunnelInfo_Notify> Rules { get; } = new ObservableCollection<TunnelInfo_Notify>();
        /// <summary>這組配置是否需要在啟用時重新確認檔案基準。</summary>
        internal bool NeedsReload { get; set; }
        /// <summary>尚未套用的規則或選項是否與服務不同。</summary>
        internal bool HasUnappliedChanges => !Capture().Matches(_applied);
        /// <summary>草稿或服務記憶體是否尚未儲存至檔案。</summary>
        internal bool HasUnsavedChanges => !Capture().Matches(_saved) || !_applied.Matches(_saved);
        /// <summary>可供畫面顯示的三種編輯狀態。</summary>
        internal string Status => HasUnappliedChanges ? "尚未套用" : HasUnsavedChanges ? "已套用但未儲存" : "已儲存";
        /// <summary>編輯中的封鎖狀態，變更後立即通知擁有者。</summary>
        internal bool ForceForbid
        {
            get => _forceForbid;
            set { if (_forceForbid == value) return; _forceForbid = value; NotifyChanged(); }
        }

        #region -- Public Methods --

        /// <summary>在 UI 執行緒由服務設定建立獨立草稿，初始視為已儲存。</summary>
        /// <param name="settings">已讀取的原生設定快照。</param>
        internal EditorSession(TunnelSettingsSnapshot settings)
        {
            Rules.CollectionChanged += RulesChanged;
            Replace(settings);
        }

        /// <summary>取得草稿的值快照，不更動 UI 或服務。</summary>
        /// <returns>規則與封鎖選項的獨立快照。</returns>
        internal TunnelSettingsSnapshot Capture() => new TunnelSettingsSnapshot(Rules.Select(TunnelInfo_Notify.Cast), ForceForbid);

        /// <summary>在 UI 執行緒以成功載入的設定取代草稿與基準。</summary>
        /// <param name="settings">已成功從檔案載入的目前配置設定。</param>
        internal void Replace(TunnelSettingsSnapshot settings)
        {
            _loading = true;
            Rules.Clear();
            foreach (TunnelInfo rule in settings.Rules) Rules.Add(new TunnelInfo_Notify(rule));
            _forceForbid = settings.ForceForbid;
            _applied = _saved = settings;
            NeedsReload = false;
            _loading = false;
            NotifyChanged();
        }

        /// <summary>同步服務的實際設定；保留草稿，並在全域重新載入後重設儲存基準。</summary>
        /// <param name="settings">服務目前配置的實際值。</param>
        internal void ObserveApplied(TunnelSettingsSnapshot settings)
        {
            _applied = settings;
            if (NeedsReload) { _saved = settings; NeedsReload = false; }
            NotifyChanged();
        }

        /// <summary>全部設定檔成功儲存後更新基準，不丟棄尚未套用的草稿。</summary>
        internal void MarkSaved() { _saved = _applied; NotifyChanged(); }

        #endregion

        #region -- Private Methods --

        /// <summary>規則集合變更後重接事件，刪除的規則不再保留狀態通知。</summary>
        /// <param name="sender">規則集合。</param>
        /// <param name="e">新增、移除或重置資訊。</param>
        private void RulesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (TunnelInfo_Notify rule in _observed) rule.PropertyChanged -= RuleChanged;
            _observed.Clear();
            foreach (TunnelInfo_Notify rule in Rules)
                if (_observed.Add(rule)) rule.PropertyChanged += RuleChanged;
            NotifyChanged();
        }

        /// <summary>規則值改變後更新草稿狀態；由 UI 編輯流程呼叫。</summary>
        /// <param name="sender">改變的規則。</param>
        /// <param name="e">欄位名稱。</param>
        private void RuleChanged(object sender, PropertyChangedEventArgs e) => NotifyChanged();

        /// <summary>批次替換完成或一般編輯後通知全部衍生狀態。</summary>
        private void NotifyChanged()
        {
            if (!_loading) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }

        #endregion
    }
}
