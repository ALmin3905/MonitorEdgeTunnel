using System;
using System.Globalization;
using System.Windows.Data;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>將內部零起算螢幕 ID 轉成一致的一起算顯示標籤，保留原始資料 ID。</summary>
    public sealed class MonitorNumberConverter : IValueConverter
    {
        #region -- Public Methods --

        /// <summary>將 value 的螢幕 ID 格式化成一起算標籤，供 UI 單向綁定使用。</summary>
        /// <param name="value">原始整數 ID。</param>
        /// <param name="targetType">綁定目標型別，此轉換固定輸出文字。</param>
        /// <param name="parameter">未使用的綁定參數。</param>
        /// <param name="culture">顯示數字使用的文化設定。</param>
        /// <returns>一起算標籤；沒有有效 ID 時為空字串。</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int id && id >= 0 ? ((long)id + 1).ToString(culture) : string.Empty;
        }

        /// <summary>拒絕由顯示文字寫回原始 ID，此轉換器僅供單向綁定。</summary>
        /// <param name="value">要求寫回的顯示文字。</param>
        /// <param name="targetType">要求寫回的型別。</param>
        /// <param name="parameter">未使用的綁定參數。</param>
        /// <param name="culture">呼叫端提供的文化設定。</param>
        /// <returns>不回傳；一律拋出 NotSupportedException。</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Monitor labels are display-only.");
        }

        #endregion
    }
}
