using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MonitorEdgeTunnelApp
{
    /// <summary>隱藏目標選單的自己與特殊顯示項目，跨 Popup 時不依賴 ListView 的視覺祖先。</summary>
    public class RelativeIdValidMultiValueConverter : IMultiValueConverter
    {
        #region -- Public Methods --

        /// <summary>依 ComboBoxItem 的產生者判斷目標是否可選；僅用於 UI 執行緒。</summary>
        /// <param name="values">候選 ID 與目前 ComboBoxItem，缺少任一值時隱藏。</param>
        /// <param name="targetType">目標 Visibility 型別。</param>
        /// <param name="parameter">未使用。</param>
        /// <param name="culture">未使用。</param>
        /// <returns>合法的其他規則為 Visible；自己與特殊值為 Collapsed。</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(values[0] is int candidate) || candidate < 0 || !(values[1] is ComboBoxItem container)) return Visibility.Collapsed;
            ComboBox owner = ItemsControl.ItemsControlFromItemContainer(container) as ComboBox;
            // ComboBoxItem id 與所屬編輯列 id；Popup 不屬於 ListView 的視覺樹。
            return owner?.DataContext is TunnelInfo_Notify rule && candidate != rule.id ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>可見性不回寫原始規則。</summary>
        /// <param name="value">未使用的可見性。</param>
        /// <param name="targetTypes">來源欄位型別。</param>
        /// <param name="parameter">未使用。</param>
        /// <param name="culture">未使用。</param>
        /// <returns>此路徑不支援反向轉換，呼叫會拋出例外。</returns>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
