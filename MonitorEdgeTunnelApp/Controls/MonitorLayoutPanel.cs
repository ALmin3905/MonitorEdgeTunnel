using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>在 UI 執行緒依桌面相對座標配置螢幕清單項目，隨可用大小自動重新縮放。</summary>
    public sealed class MonitorLayoutPanel : Panel
    {
        #region -- Layout Overrides --

        /// <summary>依 availableSize 測量各螢幕項目；由 WPF 在 UI 執行緒呼叫。</summary>
        /// <param name="availableSize">父容器提供的可用 DIP 大小。</param>
        /// <returns>不要求額外空間，由父容器決定最終填滿大小。</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            Rect[] rectangles = MonitorLayoutCalculator.Calculate(GetMonitorInfos(), availableSize);
            for (int i = 0; i < InternalChildren.Count; ++i)
                InternalChildren[i].Measure(rectangles[i].IsEmpty ? new Size() : rectangles[i].Size);
            return new Size();
        }

        /// <summary>依 finalSize 等比例置中並配置項目；由 WPF 在 UI 執行緒呼叫。</summary>
        /// <param name="finalSize">父容器決定的最終 DIP 大小。</param>
        /// <returns>實際使用的配置大小。</returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            Rect[] rectangles = MonitorLayoutCalculator.Calculate(GetMonitorInfos(), finalSize);
            for (int i = 0; i < InternalChildren.Count; ++i)
                InternalChildren[i].Arrange(rectangles[i].IsEmpty ? new Rect() : rectangles[i]);
            return finalSize;
        }

        #endregion

        #region -- Private Methods --

        /// <summary>在 UI 執行緒取得各清單容器的螢幕資料，不修改原始資料。</summary>
        /// <returns>與子項目順序一致的螢幕清單；尚無資料的項目使用無效邊界。</returns>
        private List<MonitorInfo> GetMonitorInfos()
        {
            List<MonitorInfo> monitors = new List<MonitorInfo>(InternalChildren.Count);
            foreach (UIElement child in InternalChildren)
            {
                if (child is FrameworkElement element && element.DataContext is MonitorInfo monitor)
                    monitors.Add(monitor);
                else
                    monitors.Add(new MonitorInfo { right = -1, bottom = -1 });
            }
            return monitors;
        }

        #endregion
    }
}
