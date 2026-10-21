using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MonitorEdgeTunnelApp.Editing
{
    /// <summary>保留原始選項值並提供使用者可閱讀的動作名稱。</summary>
    public sealed class EditorChoice
    {
        /// <summary>供 SelectedValue 綁定的原始值。</summary>
        public object Value { get; set; }
        /// <summary>畫面選項的中文文字。</summary>
        public string Caption { get; set; }
    }

    /// <summary>提供明確動作選單，禁止通行與傳送不再以數字表示。</summary>
    public static class EditorOptions
    {
        /// <summary>固定的禁止通行與傳送動作；不包含原生通道 ID。</summary>
        public static IReadOnlyList<EditorChoice> Actions { get; } = new[] {
            new EditorChoice { Value = false, Caption = "禁止通行" }, new EditorChoice { Value = true, Caption = "傳送通道" } };
    }

    /// <summary>只轉換選項顯示文字，不修改傳給原生服務的列舉值。</summary>
    public sealed class EditorOptionLabelConverter : IValueConverter
    {
        #region -- Public Methods --

        /// <summary>取得邊緣或範圍模式的中文文字。</summary>
        /// <param name="value">原始列舉值。</param>
        /// <param name="targetType">目標型別，顯示時通常為字串。</param>
        /// <param name="parameter">未使用的轉換參數。</param>
        /// <param name="culture">未使用的文化設定，介面固定繁體中文。</param>
        /// <returns>中文選項；其他值使用其原始字串。</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EdgeType edge) return EdgeLabel(edge);
            if (value is RangeType range) return range == RangeType.Full ? "完整邊緣" : range == RangeType.Relative ? "相對內縮" : "自訂區段";
            return value?.ToString() ?? string.Empty;
        }

        /// <summary>顯示轉換不反向解析，ComboBox 使用原始列舉物件選取。</summary>
        /// <param name="value">不使用的顯示文字。</param>
        /// <param name="targetType">原始欄位型別。</param>
        /// <param name="parameter">不使用。</param>
        /// <param name="culture">不使用。</param>
        /// <returns>DoNothing，保留原始來源值。</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;

        /// <summary>取得螢幕邊緣名稱，供選項與目標摘要共用。</summary>
        /// <param name="edge">原生邊緣值。</param>
        /// <returns>中文邊緣名稱。</returns>
        internal static string EdgeLabel(EdgeType edge) => edge == EdgeType.Left ? "左側" : edge == EdgeType.Right ? "右側" : edge == EdgeType.Top ? "上側" : "下側";

        #endregion
    }
}
