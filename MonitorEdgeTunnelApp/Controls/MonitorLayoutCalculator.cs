using System;
using System.Collections.Generic;
using System.Windows;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>將桌面像素座標等比例轉換成置中的 WPF 佈局，不受各螢幕 DPI 比例影響。</summary>
    internal static class MonitorLayoutCalculator
    {
        #region -- Public Methods --

        /// <summary>計算各螢幕的顯示矩形；不存取 UI，可在任意執行緒使用。</summary>
        /// <param name="monitors">包含含尾端邊界座標的螢幕清單，順序與輸出一致。</param>
        /// <param name="viewport">以 WPF DIP 表示的可用大小。</param>
        /// <param name="padding">圖形與可用區域邊緣保留的 DIP。</param>
        /// <returns>各螢幕的顯示矩形；無效螢幕或無法配置時對應 Rect.Empty。</returns>
        public static Rect[] Calculate(IReadOnlyList<MonitorInfo> monitors, Size viewport, double padding = 16)
        {
            if (monitors == null)
                throw new ArgumentNullException(nameof(monitors));

            Rect[] result = new Rect[monitors.Count];
            for (int i = 0; i < result.Length; ++i)
                result[i] = Rect.Empty;

            if (!IsFinite(viewport.Width) || !IsFinite(viewport.Height) || !IsFinite(padding) ||
                padding < 0 || viewport.Width <= padding * 2 || viewport.Height <= padding * 2)
                return result;

            double left = double.PositiveInfinity;
            double top = double.PositiveInfinity;
            double right = double.NegativeInfinity;
            double bottom = double.NegativeInfinity;
            foreach (MonitorInfo monitor in monitors)
            {
                if (monitor.right < monitor.left || monitor.bottom < monitor.top)
                    continue;

                left = Math.Min(left, monitor.left);
                top = Math.Min(top, monitor.top);
                right = Math.Max(right, (double)monitor.right + 1);
                bottom = Math.Max(bottom, (double)monitor.bottom + 1);
            }

            if (!IsFinite(left))
                return result;

            double width = right - left;
            double height = bottom - top;
            double scale = Math.Min((viewport.Width - padding * 2) / width, (viewport.Height - padding * 2) / height);
            double offsetX = (viewport.Width - width * scale) / 2;
            double offsetY = (viewport.Height - height * scale) / 2;
            for (int i = 0; i < monitors.Count; ++i)
            {
                MonitorInfo monitor = monitors[i];
                if (monitor.right < monitor.left || monitor.bottom < monitor.top)
                    continue;

                result[i] = new Rect(
                    offsetX + (monitor.left - left) * scale,
                    offsetY + (monitor.top - top) * scale,
                    ((double)monitor.right - monitor.left + 1) * scale,
                    ((double)monitor.bottom - monitor.top + 1) * scale);
            }
            return result;
        }

        #endregion

        #region -- Private Methods --

        /// <summary>檢查 value 是否為有限值，回傳可用於座標計算的狀態。</summary>
        /// <param name="value">待檢查的數值。</param>
        /// <returns>非 NaN 且非無限大時為 true。</returns>
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        #endregion
    }
}
