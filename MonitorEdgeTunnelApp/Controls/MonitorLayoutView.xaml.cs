using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>顯示目前桌面螢幕配置及選取資訊，不讀取或修改 Windows 顯示設定。</summary>
    public partial class MonitorLayoutView : UserControl
    {
        /// <summary>外部提供的螢幕資料來源。</summary>
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(MonitorLayoutView), new PropertyMetadata(null, PreviewInputChanged));

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
        }

        #endregion

        #region -- Private Methods --

        /// <summary>來源或強制禁行選項改變時，在 UI 執行緒重新計算預覽。</summary>
        /// <param name="sender">屬性所屬的預覽控制項。</param>
        /// <param name="e">變更的來源或選項資料。</param>
        private static void PreviewInputChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
            => ((MonitorLayoutView)sender).RefreshPreview();

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
        private void ViewLoaded(object sender, RoutedEventArgs e) => RefreshPreview();

        /// <summary>在 UI 執行緒重新取得值快照並繪製，幾何與現有螢幕佈局共用同一個轉換。</summary>
        private void RefreshPreview()
        {
            if (TunnelMarks == null) return;
            _previewMonitors = ItemsSource == null ? new List<MonitorInfo>() : ItemsSource.OfType<MonitorInfo>().ToList();
            _previewRules = TunnelsSource == null ? new List<TunnelInfo_Notify>() : TunnelsSource.OfType<TunnelInfo_Notify>().ToList();
            PreviewSegments = TunnelPreviewCalculator.Calculate(_previewMonitors, _previewRules.Select(TunnelInfo_Notify.Cast).ToList());
            TunnelMarks.UpdatePreview(_previewMonitors, _previewRules, PreviewSegments, SelectedTunnel, ForceForbidEdge);
            int selectedIndex = _previewRules.IndexOf(SelectedTunnel);
            string description = "點選通道標籤或右側規則以查看範圍與傳送方向";
            if (selectedIndex >= 0)
            {
                TunnelInfo_Notify rule = _previewRules[selectedIndex];
                TunnelPreviewSegment segment = PreviewSegments[selectedIndex];
                string edge = rule.edgeType == EdgeType.Left ? "左側" : rule.edgeType == EdgeType.Right ? "右側" :
                    rule.edgeType == EdgeType.Top ? "上側" : "下側";
                description = "通道 " + rule.id + " · 螢幕 " + (rule.displayID + 1) + " " + edge + " · " +
                    (segment.Error.Length > 0 ? segment.Error : segment.From + "–" + segment.To + " px · " +
                    (segment.IsForbidden ? "禁止通行" : "傳送到通道 " + rule.relativeID));
            }
            SetValue(SelectedTunnelDescriptionPropertyKey, description);
        }

        /// <summary>由覆蓋層回傳原始物件，更新雙向綁定但保留其 Binding。</summary>
        /// <param name="rule">被點選的規則編輯物件。</param>
        private void SelectTunnel(TunnelInfo_Notify rule) => SetCurrentValue(SelectedTunnelProperty, rule);

        #endregion
    }
}
