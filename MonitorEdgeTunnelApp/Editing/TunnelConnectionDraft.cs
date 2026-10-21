using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MonitorEdgeTunnelApp.Editing
{
    /// <summary>保留尚未確認的兩端選擇，以共用驗證建立完整邊緣規則；所有成員限 UI 執行緒。</summary>
    internal sealed class TunnelConnectionDraft : INotifyPropertyChanged
    {
        /// <summary>開啟視窗時的規則快照，僅用於即時檢查可用 ID。</summary>
        private readonly TunnelInfo_Notify[] _existing;
        /// <summary>建立草稿時的螢幕配置，防止熱插拔後誤用舊選擇。</summary>
        private readonly string _layoutKey;
        /// <summary>來源螢幕的原始 ID。</summary>
        private int _sourceMonitorId;
        /// <summary>來源邊緣。</summary>
        private EdgeType _sourceEdge;
        /// <summary>目標螢幕的原始 ID。</summary>
        private int _targetMonitorId;
        /// <summary>目標邊緣。</summary>
        private EdgeType _targetEdge;
        /// <summary>是否同時建立返回方向，預設為雙向。</summary>
        private bool _bidirectional = true;
        /// <summary>對話框可選的螢幕快照，不呼叫原生服務。</summary>
        public IReadOnlyList<MonitorInfo> Monitors { get; }
        /// <summary>來源螢幕選擇，保持原始 ID。</summary>
        public int SourceMonitorId { get => _sourceMonitorId; set { _sourceMonitorId = value; Notify(); } }
        /// <summary>來源完整邊緣。</summary>
        public EdgeType SourceEdge { get => _sourceEdge; set { _sourceEdge = value; Notify(); } }
        /// <summary>目標螢幕選擇，保持原始 ID。</summary>
        public int TargetMonitorId { get => _targetMonitorId; set { _targetMonitorId = value; Notify(); } }
        /// <summary>目標完整邊緣。</summary>
        public EdgeType TargetEdge { get => _targetEdge; set { _targetEdge = value; Notify(); } }
        /// <summary>勾選時讓兩端互相傳送；未勾選時目標規則禁止反向通行。</summary>
        public bool Bidirectional { get => _bidirectional; set { _bidirectional = value; Notify(); } }
        /// <summary>可建立時為空字串，否則說明未修改草稿的原因。</summary>
        public string ValidationMessage { get { TryCreate(Monitors, _existing, out _, out string error); return error; } }
        /// <summary>兩端有效且有兩個可用 ID 才能確認。</summary>
        public bool CanCreate => ValidationMessage.Length == 0;
        /// <summary>說明雙向及單向建立各自的效果，兩者皆需來源與目標兩筆規則。</summary>
        public string Description => Bidirectional ? "新增兩筆互相傳送的規則，套用後可往返兩端。" : "新增來源與目標兩筆規則；目標邊緣不開放返回。";
        /// <summary>通知對話框選擇、說明及確認按鈕狀態。</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #region -- Public Methods --

        /// <summary>從目前配置建立獨立選擇，優先使用選取螢幕並預選兩螢幕相對的邊緣。</summary>
        /// <param name="monitors">目前螢幕快照。</param>
        /// <param name="existing">目前規則，僅讀取，不加入或修改。</param>
        /// <param name="selectedMonitorId">優先作為來源的螢幕，無效或 null 時使用第一個。</param>
        internal TunnelConnectionDraft(IReadOnlyList<MonitorInfo> monitors, IReadOnlyList<TunnelInfo_Notify> existing, int? selectedMonitorId)
        {
            Monitors = monitors.ToArray();
            _existing = existing.ToArray();
            _layoutKey = EditorWorkspace.LayoutKey(Monitors);
            _sourceMonitorId = Monitors.Any(m => m.id == selectedMonitorId) ? selectedMonitorId.Value : Monitors.Count > 0 ? Monitors[0].id : -1;
            _targetMonitorId = Monitors.Where(m => m.id != _sourceMonitorId).Select(m => m.id).DefaultIfEmpty(_sourceMonitorId).First();
            _sourceEdge = EdgeType.Right;
            _targetEdge = EdgeType.Left;
            if (Monitors.Count > 1)
            {
                MonitorInfo source = Monitors.First(m => m.id == _sourceMonitorId);
                MonitorInfo target = Monitors.First(m => m.id == _targetMonitorId);
                long dx = (long)target.left + target.right - source.left - source.right;
                long dy = (long)target.top + target.bottom - source.top - source.bottom;
                bool horizontal = Math.Abs(dx) >= Math.Abs(dy);
                _sourceEdge = horizontal ? dx >= 0 ? EdgeType.Right : EdgeType.Left : dy >= 0 ? EdgeType.Bottom : EdgeType.Top;
                _targetEdge = horizontal ? dx >= 0 ? EdgeType.Left : EdgeType.Right : dy >= 0 ? EdgeType.Top : EdgeType.Bottom;
            }
        }

        /// <summary>依最新 ID 與配置準備兩筆規則，全部檢查完成才輸出；不修改傳入清單或呼叫服務。</summary>
        /// <param name="monitors">確認時的螢幕快照，必須與開啟時相同。</param>
        /// <param name="existing">確認時的規則，依目前未占用的 ID 分配。</param>
        /// <param name="created">成功時輸出來源、目標兩筆獨立規則；失敗時為空陣列。</param>
        /// <param name="error">失敗原因，成功時為空字串。</param>
        /// <returns>可一次加入兩筆規則時為 true。</returns>
        internal bool TryCreate(IReadOnlyList<MonitorInfo> monitors, IReadOnlyList<TunnelInfo_Notify> existing,
            out TunnelInfo_Notify[] created, out string error)
        {
            created = new TunnelInfo_Notify[0];
            error = string.Empty;
            if (EditorWorkspace.LayoutKey(monitors) != _layoutKey) error = "螢幕配置已變更，請重新選擇兩端。";
            else if (!monitors.Any(m => m.id == SourceMonitorId) || !monitors.Any(m => m.id == TargetMonitorId)) error = "請選擇目前可用的來源與目標螢幕。";
            else if (SourceMonitorId == TargetMonitorId && SourceEdge == TargetEdge) error = "來源與目標不能是同一個螢幕邊緣。";
            int[] ids = Enumerable.Range(0, 101).Where(id => existing.All(rule => rule.id != id)).Take(2).ToArray();
            if (error.Length == 0 && ids.Length < 2) error = "建立連接需要兩個可用的通道 ID（0–100）。";
            if (error.Length > 0) return false;
            TunnelInfo_Notify[] pair = {
                new TunnelInfo_Notify { id = ids[0], displayID = SourceMonitorId, edgeType = SourceEdge, rangeType = RangeType.Full, relativeID = ids[1] },
                new TunnelInfo_Notify { id = ids[1], displayID = TargetMonitorId, edgeType = TargetEdge, rangeType = RangeType.Full, relativeID = Bidirectional ? ids[0] : -1 }
            };
            error = EditorValidation.Evaluate(monitors, existing.Concat(pair).ToArray()).Skip(existing.Count).Select(segment => segment.Error).FirstOrDefault(message => message.Length > 0) ?? string.Empty;
            if (error.Length > 0) return false;
            created = pair;
            return true;
        }

        #endregion

        #region -- Private Methods --

        /// <summary>在 UI 執行緒通知所有選擇及相依狀態，不更改正式編輯規則。</summary>
        private void Notify()
        {
            foreach (string property in new[] { nameof(SourceMonitorId), nameof(SourceEdge), nameof(TargetMonitorId), nameof(TargetEdge), nameof(Bidirectional), nameof(ValidationMessage), nameof(CanCreate), nameof(Description) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        #endregion
    }
}
