using System;
using System.Globalization;
using System.Windows.Data;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>提供選取螢幕的解析度、縮放與桌面位置文字，不影響配置計算。</summary>
    public sealed class MonitorDescriptionConverter : IValueConverter
    {
        #region -- Public Methods --

        /// <summary>將 value 的螢幕資訊格式化為可換行的摘要，供 UI 單向綁定使用。</summary>
        /// <param name="value">原始 MonitorInfo。</param>
        /// <param name="targetType">綁定目標型別，此轉換固定輸出文字。</param>
        /// <param name="parameter">未使用的綁定參數。</param>
        /// <param name="culture">顯示數字使用的文化設定。</param>
        /// <returns>含解析度、縮放及座標的摘要；沒有螢幕資料時為空字串。</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is MonitorInfo monitor))
                return string.Empty;

            long width = (long)monitor.right - monitor.left + 1;
            long height = (long)monitor.bottom - monitor.top + 1;
            return string.Format(culture, "{0} × {1} · 縮放 {2}% · 位置 ({3}, {4})", width, height,
                (monitor.scaling * 100).ToString("0.##", culture), monitor.left, monitor.top);
        }

        /// <summary>拒絕由摘要文字寫回螢幕資訊，此轉換器僅供單向綁定。</summary>
        /// <param name="value">要求寫回的摘要文字。</param>
        /// <param name="targetType">要求寫回的型別。</param>
        /// <param name="parameter">未使用的綁定參數。</param>
        /// <param name="culture">呼叫端提供的文化設定。</param>
        /// <returns>不回傳；一律拋出 NotSupportedException。</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Monitor descriptions are display-only.");
        }

        #endregion
    }
}
