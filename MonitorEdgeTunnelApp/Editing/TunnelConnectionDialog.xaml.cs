using MonitorEdgeTunnelApp.Themes;
using System.Windows;

namespace MonitorEdgeTunnelApp.Editing
{
    /// <summary>在確認前僅編輯兩端選擇的主題化視窗，不直接加入規則或呼叫原生服務。</summary>
    internal partial class TunnelConnectionDialog : Window
    {
        #region -- Public Methods --

        /// <summary>在 UI 執行緒建立對話框，深淺色及標題列跟隨應用程式主題。</summary>
        /// <param name="draft">尚未確認的選擇，取消時可直接丟棄。</param>
        internal TunnelConnectionDialog(TunnelConnectionDraft draft)
        {
            InitializeComponent();
            DataContext = draft;
            MaxHeight = System.Math.Max(240, SystemParameters.WorkArea.Height - 80);
            SourceInitialized += ThemeManager.WindowSourceInitialized;
        }

        #endregion

        #region -- Private Methods --

        /// <summary>確認有效的兩端選擇；僅關閉 UI 對話框，加入規則由主視窗負責。</summary>
        /// <param name="sender">建立按鈕。</param>
        /// <param name="e">點擊事件資料。</param>
        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TunnelConnectionDraft draft && draft.CanCreate) DialogResult = true;
        }

        #endregion
    }
}
