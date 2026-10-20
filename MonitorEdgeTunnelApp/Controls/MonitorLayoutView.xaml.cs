using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>顯示目前桌面螢幕配置及選取資訊，不讀取或修改 Windows 顯示設定。</summary>
    public partial class MonitorLayoutView : UserControl
    {
        /// <summary>外部提供的螢幕資料來源。</summary>
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(MonitorLayoutView));

        /// <summary>與外部清單雙向同步的原始螢幕 ID。</summary>
        public static readonly DependencyProperty SelectedMonitorIdProperty = DependencyProperty.Register(
            nameof(SelectedMonitorId), typeof(int?), typeof(MonitorLayoutView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

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
        }

        #endregion
    }
}
