using MonitorEdgeTunnelApp.Editing;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>將可選取的編輯物件與錯誤或優先權說明提供給預覽警告清單。</summary>
    internal sealed class TunnelPreviewNotice
    {
        /// <summary>警告對應的原始編輯物件。</summary>
        public TunnelInfo_Notify Rule { get; internal set; }
        /// <summary>包含通道 ID 及原因的顯示文字。</summary>
        public string Text { get; internal set; }
    }

    /// <summary>顯示目前桌面螢幕配置及選取資訊，不讀取或修改 Windows 顯示設定。</summary>
    public partial class MonitorLayoutView : UserControl
    {
        /// <summary>外部提供的螢幕資料來源。</summary>
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(MonitorLayoutView), new PropertyMetadata(null, PreviewInputChanged));

        /// <summary>控制預覽卡片的背景與外框，供呼叫端將預覽及操作按鈕嵌入同一卡片。</summary>
        public static readonly DependencyProperty ShowFrameProperty = DependencyProperty.Register(
            nameof(ShowFrame), typeof(bool), typeof(MonitorLayoutView), new PropertyMetadata(true));

        /// <summary>與外部清單雙向同步的原始螢幕 ID。</summary>
        public static readonly DependencyProperty SelectedMonitorIdProperty = DependencyProperty.Register(
            nameof(SelectedMonitorId), typeof(int?), typeof(MonitorLayoutView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>外部清單正在編輯的通道來源。</summary>
        public static readonly DependencyProperty TunnelsSourceProperty = DependencyProperty.Register(
            nameof(TunnelsSource), typeof(IEnumerable), typeof(MonitorLayoutView), new PropertyMetadata(null, PreviewInputChanged));
        /// <summary>與通道清單雙向共用的編輯物件，避免 ID 重新編號後選錯對象。</summary>
        public static readonly DependencyProperty SelectedTunnelProperty = DependencyProperty.Register(
            nameof(SelectedTunnel), typeof(TunnelInfo_Notify), typeof(MonitorLayoutView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SelectedTunnelChanged));
        /// <summary>是否預覽明確規則以外的強制禁行區段。</summary>
        public static readonly DependencyProperty ForceForbidEdgeProperty = DependencyProperty.Register(
            nameof(ForceForbidEdge), typeof(bool), typeof(MonitorLayoutView), new PropertyMetadata(false, PreviewInputChanged));
        /// <summary>只由控制項更新的選取規則摘要登錄鍵。</summary>
        private static readonly DependencyPropertyKey SelectedTunnelDescriptionPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(SelectedTunnelDescription), typeof(string), typeof(MonitorLayoutView), new PropertyMetadata(string.Empty));
        /// <summary>供 XAML 顯示的選取規則摘要。</summary>
        public static readonly DependencyProperty SelectedTunnelDescriptionProperty = SelectedTunnelDescriptionPropertyKey.DependencyProperty;
        /// <summary>只由控制項更新的警告清單登錄鍵。</summary>
        private static readonly DependencyPropertyKey PreviewMessagesPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(PreviewMessages), typeof(IEnumerable), typeof(MonitorLayoutView), new PropertyMetadata(null));
        /// <summary>供 XAML 列出錯誤與重疊優先權的唯讀屬性。</summary>
        public static readonly DependencyProperty PreviewMessagesProperty = PreviewMessagesPropertyKey.DependencyProperty;
        /// <summary>警告區是否應顯示的唯讀登錄鍵。</summary>
        private static readonly DependencyPropertyKey HasWarningsPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(HasWarnings), typeof(bool), typeof(MonitorLayoutView), new PropertyMetadata(false));
        /// <summary>供 XAML 控制警告區顯示狀態。</summary>
        public static readonly DependencyProperty HasWarningsProperty = HasWarningsPropertyKey.DependencyProperty;
        /// <summary>警告標題的唯讀登錄鍵。</summary>
        private static readonly DependencyPropertyKey PreviewWarningSummaryPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(PreviewWarningSummary), typeof(string), typeof(MonitorLayoutView), new PropertyMetadata(string.Empty));
        /// <summary>供 XAML 顯示錯誤與重疊規則數量。</summary>
        public static readonly DependencyProperty PreviewWarningSummaryProperty = PreviewWarningSummaryPropertyKey.DependencyProperty;
        /// <summary>已連接弱式事件的集合；來源替換及卸載時解除。</summary>
        private readonly HashSet<INotifyCollectionChanged> _observedCollections = new HashSet<INotifyCollectionChanged>();
        /// <summary>已連接弱式事件的規則；刪除及重置時解除。</summary>
        private readonly HashSet<INotifyPropertyChanged> _observedRules = new HashSet<INotifyPropertyChanged>();
        /// <summary>是否接收來源通知；首次載入前亦可訂閱，卸載後暫停。</summary>
        private bool _observeChanges = true;
        /// <summary>將同一輪編輯與重新編號的通知合併為一次預覽更新。</summary>
        private DispatcherOperation _pendingRefresh;

        /// <summary>已偵測到的錯誤與重疊提示，每一筆均保留對應規則物件。</summary>
        public IEnumerable PreviewMessages => (IEnumerable)GetValue(PreviewMessagesProperty);
        /// <summary>是否存在需要使用者注意的規則。</summary>
        public bool HasWarnings => (bool)GetValue(HasWarningsProperty);
        /// <summary>錯誤及重疊規則的摘要數量。</summary>
        public string PreviewWarningSummary => (string)GetValue(PreviewWarningSummaryProperty);

        /// <summary>本次繪製使用的螢幕快照。</summary>
        private List<MonitorInfo> _previewMonitors = new List<MonitorInfo>();
        /// <summary>保留編輯物件參照的本次規則快照。</summary>
        private List<TunnelInfo_Notify> _previewRules = new List<TunnelInfo_Notify>();
        /// <summary>範圍計算結果，與本次規則快照順序一致。</summary>
        internal IReadOnlyList<TunnelPreviewSegment> PreviewSegments { get; private set; } = new TunnelPreviewSegment[0];

        /// <summary>目前編輯中的規則集合，支援後續預覽重新計算。</summary>
        public IEnumerable TunnelsSource
        {
            get => (IEnumerable)GetValue(TunnelsSourceProperty);
            set => SetValue(TunnelsSourceProperty, value);
        }

        /// <summary>目前選取的規則物件；null 代表未選取。</summary>
        public TunnelInfo_Notify SelectedTunnel
        {
            get => (TunnelInfo_Notify)GetValue(SelectedTunnelProperty);
            set => SetValue(SelectedTunnelProperty, value);
        }

        /// <summary>與右側編輯選項一致的強制邊緣禁行狀態。</summary>
        public bool ForceForbidEdge
        {
            get => (bool)GetValue(ForceForbidEdgeProperty);
            set => SetValue(ForceForbidEdgeProperty, value);
        }

        /// <summary>目前規則的邊緣、範圍與動作；無效時附上錯誤原因。</summary>
        public string SelectedTunnelDescription => (string)GetValue(SelectedTunnelDescriptionProperty);

        /// <summary>是否繪製預覽卡片的背景與外框；嵌入外部卡片時設為 false。</summary>
        public bool ShowFrame
        {
            get => (bool)GetValue(ShowFrameProperty);
            set => SetValue(ShowFrameProperty, value);
        }

        /// <summary>包含目前桌面座標的螢幕集合，支援集合變更通知。</summary>
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>目前選取的原始 ID；沒有選取時為 null，與顯示標籤分開保存。</summary>
        public int? SelectedMonitorId
        {
            get => (int?)GetValue(SelectedMonitorIdProperty);
            set => SetValue(SelectedMonitorIdProperty, value);
        }

        #region -- Public Methods --

        /// <summary>在 UI 執行緒建立佈局、資料綁定及選取樣式。</summary>
        public MonitorLayoutView()
        {
            InitializeComponent();
            TunnelMarks.TunnelSelected += SelectTunnel;
            Loaded += ViewLoaded;
            Unloaded += ViewUnloaded;
        }

        #endregion

        #region -- Private Methods --

        /// <summary>來源或強制禁行選項改變時，在 UI 執行緒重新計算預覽。</summary>
        /// <param name="sender">屬性所屬的預覽控制項。</param>
        /// <param name="e">變更的來源或選項資料。</param>
        private static void PreviewInputChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            MonitorLayoutView view = (MonitorLayoutView)sender;
            if (e.Property == ItemsSourceProperty || e.Property == TunnelsSourceProperty) view.ObserveSources();
            view.QueueRefresh();
        }

        /// <summary>雙向選取變更時重畫關聯區段，並選取規則所屬的螢幕；只影響 UI。</summary>
        /// <param name="sender">屬性所屬的預覽控制項。</param>
        /// <param name="e">新舊選取物件。</param>
        private static void SelectedTunnelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            MonitorLayoutView view = (MonitorLayoutView)sender;
            view.RefreshPreview();
            if (e.NewValue is TunnelInfo_Notify rule && view._previewMonitors.Any(monitor => monitor.id == rule.displayID))
                view.SetCurrentValue(SelectedMonitorIdProperty, (int?)rule.displayID);
        }

        /// <summary>控制項載入後以目前綁定值建立預覽，不重新載入或套用原生設定。</summary>
        /// <param name="sender">目前控制項。</param>
        /// <param name="e">載入路由事件。</param>
        private void ViewLoaded(object sender, RoutedEventArgs e)
        {
            _observeChanges = true;
            ObserveSources();
            QueueRefresh();
        }

        /// <summary>UI 卸載時解除來源通知並取消尚未執行的更新；重新載入時會重新連接。</summary>
        /// <param name="sender">目前控制項。</param>
        /// <param name="e">卸載路由事件。</param>
        private void ViewUnloaded(object sender, RoutedEventArgs e)
        {
            _observeChanges = false;
            DetachSources();
            _pendingRefresh?.Abort();
            _pendingRefresh = null;
        }

        /// <summary>在 UI 執行緒重接目前集合及規則的弱式通知，移除舊項目以避免保留過期資料。</summary>
        private void ObserveSources()
        {
            DetachSources();
            if (!_observeChanges) return;
            foreach (IEnumerable source in new[] { ItemsSource, TunnelsSource })
                if (source is INotifyCollectionChanged collection && _observedCollections.Add(collection))
                    CollectionChangedEventManager.AddHandler(collection, SourceCollectionChanged);
            if (TunnelsSource == null) return;
            foreach (INotifyPropertyChanged rule in TunnelsSource.OfType<INotifyPropertyChanged>())
                if (_observedRules.Add(rule)) PropertyChangedEventManager.AddHandler(rule, RulePropertyChanged, string.Empty);
        }

        /// <summary>在 UI 執行緒解除已記錄的弱式通知，包含 Reset 後已無法由集合取得的舊規則。</summary>
        private void DetachSources()
        {
            foreach (INotifyCollectionChanged collection in _observedCollections)
                CollectionChangedEventManager.RemoveHandler(collection, SourceCollectionChanged);
            foreach (INotifyPropertyChanged rule in _observedRules)
                PropertyChangedEventManager.RemoveHandler(rule, RulePropertyChanged, string.Empty);
            _observedCollections.Clear();
            _observedRules.Clear();
        }

        /// <summary>UI 集合新增、刪除、移動或重置時重接規則通知，並合併本輪重畫。</summary>
        /// <param name="sender">提供變更通知的集合。</param>
        /// <param name="e">集合變更內容。</param>
        private void SourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ObserveSources();
            QueueRefresh();
        }

        /// <summary>編輯物件在 UI 執行緒更新欄位時排程重畫，不呼叫原生服務。</summary>
        /// <param name="sender">被修改的編輯物件。</param>
        /// <param name="e">被修改的屬性名稱。</param>
        private void RulePropertyChanged(object sender, PropertyChangedEventArgs e) => QueueRefresh();

        /// <summary>在 UI Dispatcher 合併當前編輯事件，避免中途重新編號產生閃動或選錯物件。</summary>
        private void QueueRefresh()
        {
            if (!_observeChanges || _pendingRefresh != null) return;
            _pendingRefresh = Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
            {
                _pendingRefresh = null;
                RefreshPreview();
            }));
        }

        /// <summary>在 UI 執行緒重新取得值快照並繪製，幾何與現有螢幕佈局共用同一個轉換。</summary>
        private void RefreshPreview()
        {
            if (TunnelMarks == null) return;
            _previewMonitors = ItemsSource == null ? new List<MonitorInfo>() : ItemsSource.OfType<MonitorInfo>().ToList();
            _previewRules = TunnelsSource == null ? new List<TunnelInfo_Notify>() : TunnelsSource.OfType<TunnelInfo_Notify>().ToList();
            if (SelectedTunnel != null && !_previewRules.Contains(SelectedTunnel))
            {
                SetCurrentValue(SelectedTunnelProperty, null);
                return;
            }
            PreviewSegments = EditorValidation.Evaluate(_previewMonitors, _previewRules);
            UpdateWarnings();
            TunnelMarks.UpdatePreview(_previewMonitors, _previewRules, PreviewSegments, SelectedTunnel, ForceForbidEdge);
            int selectedIndex = _previewRules.IndexOf(SelectedTunnel);
            string description = "點選通道標籤或右側規則以查看範圍與傳送方向";
            if (selectedIndex >= 0)
            {
                TunnelInfo_Notify rule = _previewRules[selectedIndex];
                TunnelPreviewSegment segment = PreviewSegments[selectedIndex];
                if (_previewMonitors.Any(monitor => monitor.id == rule.displayID))
                    SetCurrentValue(SelectedMonitorIdProperty, (int?)rule.displayID);
                string edge = rule.edgeType == EdgeType.Left ? "左側" : rule.edgeType == EdgeType.Right ? "右側" :
                    rule.edgeType == EdgeType.Top ? "上側" : "下側";
                description = "通道 " + rule.id + " · 螢幕 " + (rule.displayID + 1) + " " + edge + " · " +
                    (segment.Error.Length > 0 ? segment.Error : segment.From + "–" + segment.To + " px · " +
                    (segment.IsForbidden ? "禁止通行" : "傳送到通道 " + rule.relativeID));
                if (segment.Warning.Length > 0) description += "\n" + segment.Warning;
            }
            SetValue(SelectedTunnelDescriptionPropertyKey, description);
        }

        /// <summary>更新每筆規則的錯誤或重疊提示，沒有可定位幾何的規則也能由警告清單選取。</summary>
        private void UpdateWarnings()
        {
            List<TunnelPreviewNotice> messages = new List<TunnelPreviewNotice>();
            int errors = 0;
            int overlaps = 0;
            foreach (TunnelPreviewSegment segment in PreviewSegments)
            {
                bool invalid = segment.Error.Length > 0;
                if (!invalid && segment.Warning.Length == 0) continue;
                if (invalid) ++errors; else ++overlaps;
                TunnelInfo_Notify rule = _previewRules[segment.RuleIndex];
                messages.Add(new TunnelPreviewNotice { Rule = rule, Text = "通道 " + rule.id + "：" +
                    (invalid ? segment.Error : segment.Warning) });
            }
            SetValue(PreviewMessagesPropertyKey, messages);
            SetValue(HasWarningsPropertyKey, messages.Count > 0);
            SetValue(PreviewWarningSummaryPropertyKey, "設定問題 " + errors + " 筆 · 重疊規則 " + overlaps + " 筆");
        }

        /// <summary>點選警告時選取原始規則，即使其螢幕或範圍無效也能定位右側清單。</summary>
        /// <param name="sender">Tag 保存編輯物件的警告按鈕。</param>
        /// <param name="e">警告按鈕的路由事件。</param>
        private void NoticeClicked(object sender, RoutedEventArgs e) => SelectTunnel((TunnelInfo_Notify)((Button)sender).Tag);

        /// <summary>由覆蓋層回傳原始物件，更新雙向綁定但保留其 Binding。</summary>
        /// <param name="rule">被點選的規則編輯物件。</param>
        private void SelectTunnel(TunnelInfo_Notify rule) => SetCurrentValue(SelectedTunnelProperty, rule);

        #endregion
    }
}
