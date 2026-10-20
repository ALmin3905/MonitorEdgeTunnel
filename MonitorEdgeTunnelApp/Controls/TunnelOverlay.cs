using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>在 UI 執行緒繪製通道區段與選取連線；空白處保留給底下的螢幕清單。</summary>
    public sealed class TunnelOverlay : Canvas
    {
        /// <summary>可傳送區段的顏色，配合既有深色預覽背景。</summary>
        private static readonly Brush PassBrush = new SolidColorBrush(Color.FromRgb(98, 221, 181));
        /// <summary>禁止通行區段的顏色。</summary>
        private static readonly Brush ForbidBrush = new SolidColorBrush(Color.FromRgb(255, 130, 146));
        /// <summary>來源可定位但規則無效時的警告顏色。</summary>
        private static readonly Brush InvalidBrush = new SolidColorBrush(Color.FromRgb(255, 200, 87));
        /// <summary>與螢幕清單共用的桌面資料快照。</summary>
        private IReadOnlyList<MonitorInfo> _monitors = new MonitorInfo[0];
        /// <summary>保留來源物件參照，使 ID 重新編號不會改變點選對象。</summary>
        private IReadOnlyList<TunnelInfo_Notify> _rules = new TunnelInfo_Notify[0];
        /// <summary>依清單順序排列的預覽區段。</summary>
        private IReadOnlyList<TunnelPreviewSegment> _segments = new TunnelPreviewSegment[0];
        /// <summary>目前選取的規則物件；沒有選取時為 null。</summary>
        private TunnelInfo_Notify _selected;
        /// <summary>是否標示明確規則以外的剩餘禁行區段。</summary>
        private bool _forceForbid;

        /// <summary>標籤被點選時，在同一 UI 執行緒通知所屬規則，不套用設定。</summary>
        public event Action<TunnelInfo_Notify> TunnelSelected;

        #region -- Public Methods --

        /// <summary>建立透明覆蓋層並在 UI 大小改變時重畫，不建立背景執行緒。</summary>
        public TunnelOverlay()
        {
            ClipToBounds = true;
            SizeChanged += OverlaySizeChanged;
        }

        /// <summary>在 UI 執行緒更新繪製快照，保持規則物件參照且不修改來源。</summary>
        /// <param name="monitors">與螢幕清單相同的像素邊界快照。</param>
        /// <param name="rules">供點選回傳的編輯物件快照。</param>
        /// <param name="segments">與 rules 順序一致的範圍計算結果。</param>
        /// <param name="selected">目前選取的編輯物件。</param>
        /// <param name="forceForbid">是否繪製剩餘禁行區段。</param>
        internal void UpdatePreview(IReadOnlyList<MonitorInfo> monitors, IReadOnlyList<TunnelInfo_Notify> rules,
            IReadOnlyList<TunnelPreviewSegment> segments, TunnelInfo_Notify selected, bool forceForbid)
        {
            _monitors = monitors;
            _rules = rules;
            _segments = segments;
            _selected = selected;
            _forceForbid = forceForbid;
            DrawPreview();
        }

        #endregion

        #region -- Private Methods --

        /// <summary>依新的可用大小重新投影通道；由 WPF 在 UI 執行緒呼叫。</summary>
        /// <param name="sender">觸發事件的覆蓋層。</param>
        /// <param name="e">新舊大小資料。</param>
        private void OverlaySizeChanged(object sender, SizeChangedEventArgs e) => DrawPreview();

        /// <summary>共用螢幕佈局計算，重建標示與可用鍵盤操作的標籤，不變更任何設定。</summary>
        private void DrawPreview()
        {
            Children.Clear();
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            Rect[] rectangles = MonitorLayoutCalculator.Calculate(_monitors, new Size(ActualWidth, ActualHeight));
            if (_forceForbid)
                foreach (TunnelPreviewSegment gap in TunnelPreviewCalculator.CalculateForbiddenGaps(_monitors, _segments))
                    AddSegment(gap, rectangles[gap.MonitorIndex], false, true);
            int selectedIndex = -1;
            for (int i = 0; i < _rules.Count; ++i)
                if (ReferenceEquals(_rules[i], _selected)) selectedIndex = i;
            int targetIndex = selectedIndex < 0 ? -1 : _segments[selectedIndex].TargetIndex;
            foreach (TunnelPreviewSegment segment in _segments)
                if (segment.HasGeometry && !rectangles[segment.MonitorIndex].IsEmpty)
                    AddSegment(segment, rectangles[segment.MonitorIndex], segment.RuleIndex == selectedIndex || segment.RuleIndex == targetIndex, false);
            if (selectedIndex >= 0 && targetIndex >= 0 && _segments[selectedIndex].Error.Length == 0 && _segments[targetIndex].Error.Length == 0)
                AddConnection(_segments[selectedIndex], _segments[targetIndex], rectangles);
            List<Rect> markerBounds = new List<Rect>();
            foreach (TunnelPreviewSegment segment in _segments)
            {
                if (!segment.HasGeometry || rectangles[segment.MonitorIndex].IsEmpty) continue;
                Endpoints(segment, rectangles[segment.MonitorIndex], out Point start, out Point end);
                Line hit = new Line { X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y, Stroke = Brushes.Transparent,
                    StrokeThickness = 8, Tag = _rules[segment.RuleIndex], ToolTip = "通道 " + _rules[segment.RuleIndex].id +
                    " · " + segment.From + "–" + segment.To + " px" };
                hit.MouseLeftButtonDown += SegmentClicked;
                Children.Add(hit);
            }
            foreach (TunnelPreviewSegment segment in _segments)
                if (segment.HasGeometry && !rectangles[segment.MonitorIndex].IsEmpty)
                    AddMarker(segment, rectangles[segment.MonitorIndex], markerBounds);
        }

        /// <summary>取得區段在螢幕預覽矩形內的端點，重疊規則使用不同內縮分層。</summary>
        /// <param name="segment">含正規化範圍與分層的區段。</param>
        /// <param name="rectangle">與底下螢幕項目完全相同的顯示矩形。</param>
        /// <param name="start">回傳第一個端點。</param>
        /// <param name="end">回傳含尾端像素之後的端點。</param>
        private static void Endpoints(TunnelPreviewSegment segment, Rect rectangle, out Point start, out Point end)
        {
            bool vertical = segment.Edge == EdgeType.Left || segment.Edge == EdgeType.Right;
            double inset = Math.Min(4 + segment.Lane * 8, (vertical ? rectangle.Width : rectangle.Height) / 2);
            if (vertical)
            {
                double x = segment.Edge == EdgeType.Left ? rectangle.Left + inset : rectangle.Right - inset;
                start = new Point(x, rectangle.Top + segment.StartFraction * rectangle.Height);
                end = new Point(x, rectangle.Top + segment.EndFraction * rectangle.Height);
            }
            else
            {
                double y = segment.Edge == EdgeType.Top ? rectangle.Top + inset : rectangle.Bottom - inset;
                start = new Point(rectangle.Left + segment.StartFraction * rectangle.Width, y);
                end = new Point(rectangle.Left + segment.EndFraction * rectangle.Width, y);
            }
        }

        /// <summary>加入不可攔截滑鼠的區段線條，只有明確選取的規則使用外框。</summary>
        /// <param name="segment">待繪製的區段。</param>
        /// <param name="rectangle">所屬螢幕的顯示矩形。</param>
        /// <param name="selected">是否為選取規則或其目標。</param>
        /// <param name="implicitForbid">是否為無使用者 ID 的剩餘禁行區段。</param>
        private void AddSegment(TunnelPreviewSegment segment, Rect rectangle, bool selected, bool implicitForbid)
        {
            if (rectangle.IsEmpty) return;
            Endpoints(segment, rectangle, out Point start, out Point end);
            if (selected) AddLine(start, end, Brushes.White, 9, null, 0.8, null);
            Brush brush = SegmentBrush(segment);
            DoubleCollection dash = segment.Error.Length > 0 ? new DoubleCollection { 2, 2 } :
                segment.IsForbidden ? new DoubleCollection { 3, 2 } : null;
            AddLine(start, end, brush, implicitForbid ? 1.5 : 4, dash, implicitForbid ? 0.65 : 1, segment);
        }

        /// <summary>建立純繪圖線條，保留區段資料供視覺檢查且不攔截底下的螢幕選取。</summary>
        /// <param name="start">起點。</param>
        /// <param name="end">終點。</param>
        /// <param name="brush">線條顏色。</param>
        /// <param name="width">固定 DIP 線寬。</param>
        /// <param name="dash">虛線樣式；null 代表實線。</param>
        /// <param name="opacity">透明度。</param>
        /// <param name="tag">對應區段；外框為 null。</param>
        private void AddLine(Point start, Point end, Brush brush, double width, DoubleCollection dash, double opacity, object tag)
        {
            Children.Add(new Line { X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y, Stroke = brush,
                StrokeThickness = width, StrokeDashArray = dash, Opacity = opacity, IsHitTestVisible = false,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Tag = tag });
        }

        /// <summary>為有有效目標的選取規則畫單向連線，箭頭放在曲線中段以避開標籤。</summary>
        /// <param name="source">來源區段。</param>
        /// <param name="target">相依 ID 指向的目標區段。</param>
        /// <param name="rectangles">螢幕顯示矩形。</param>
        private void AddConnection(TunnelPreviewSegment source, TunnelPreviewSegment target, IReadOnlyList<Rect> rectangles)
        {
            if (rectangles[source.MonitorIndex].IsEmpty || rectangles[target.MonitorIndex].IsEmpty) return;
            Endpoints(source, rectangles[source.MonitorIndex], out Point s1, out Point s2);
            Endpoints(target, rectangles[target.MonitorIndex], out Point t1, out Point t2);
            Point start = new Point((s1.X + s2.X) / 2, (s1.Y + s2.Y) / 2);
            Point end = new Point((t1.X + t2.X) / 2, (t1.Y + t2.Y) / 2);
            Vector normal = source.Edge == EdgeType.Left ? new Vector(-1, 0) : source.Edge == EdgeType.Right ? new Vector(1, 0) :
                source.Edge == EdgeType.Top ? new Vector(0, -1) : new Vector(0, 1);
            Vector bend = (end - start).Length < 28 ? new Vector(-normal.Y, normal.X) * 34 : normal * 34;
            PathFigure figure = new PathFigure { StartPoint = start };
            figure.Segments.Add(new BezierSegment(start + bend, end + bend, end, true));
            PathGeometry geometry = new PathGeometry(new[] { figure });
            Children.Add(new Path { Data = geometry, Stroke = PassBrush, StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false, Tag = "TunnelConnection" });
            geometry.GetPointAtFractionLength(0.55, out Point tip, out Point tangentPoint);
            Vector direction = new Vector(tangentPoint.X, tangentPoint.Y);
            if (direction.Length < 0.001) return;
            direction.Normalize();
            Vector side = new Vector(-direction.Y, direction.X);
            Children.Add(new Polygon { Points = new PointCollection { tip + direction * 5, tip - direction * 5 + side * 4,
                tip - direction * 5 - side * 4 }, Fill = PassBrush, IsHitTestVisible = false });
        }

        /// <summary>為區段建立有足夠命中範圍的標籤，保留清單物件作為點選對象。</summary>
        /// <param name="segment">待標示的使用者規則。</param>
        /// <param name="rectangle">螢幕顯示矩形。</param>
        /// <param name="occupied">先前標籤的命中矩形；加入本次矩形以減少重疊。</param>
        private void AddMarker(TunnelPreviewSegment segment, Rect rectangle, List<Rect> occupied)
        {
            TunnelInfo_Notify rule = _rules[segment.RuleIndex];
            Endpoints(segment, rectangle, out Point start, out Point end);
            Point middle = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            bool vertical = segment.Edge == EdgeType.Left || segment.Edge == EdgeType.Right;
            double x = vertical ? (segment.Edge == EdgeType.Left ? middle.X + 4 : middle.X - 36) : middle.X - 16;
            double y = vertical ? middle.Y - 16 : (segment.Edge == EdgeType.Top ? middle.Y + 4 : middle.Y - 36);
            Rect bounds = new Rect(Math.Max(0, Math.Min(x, ActualWidth - 32)), Math.Max(0, Math.Min(y, ActualHeight - 32)), 32, 32);
            for (int attempt = 0; attempt < _rules.Count && occupied.Any(item => item.IntersectsWith(bounds)); ++attempt)
            {
                if (vertical) bounds.Y = Math.Max(0, Math.Min(y + (attempt + 1) * 34, ActualHeight - 32));
                else bounds.X = Math.Max(0, Math.Min(x + (attempt + 1) * 34, ActualWidth - 32));
            }
            occupied.Add(bounds);
            Brush brush = SegmentBrush(segment);
            Button button = new Button { Width = bounds.Width, Height = bounds.Height, Tag = rule,
                Background = Brushes.Transparent, Template = MarkerTemplate(),
                Content = new Border { Background = new SolidColorBrush(Color.FromRgb(37, 37, 37)), BorderBrush = brush,
                    BorderThickness = new Thickness(ReferenceEquals(rule, _selected) ? 2 : 1), CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(4, 1, 4, 1), Child = new TextBlock { Text = rule.id.ToString(CultureInfo.CurrentCulture),
                        Foreground = Brushes.White, FontSize = 12 } } };
            string message = "通道 " + rule.id + (segment.Error.Length > 0 ? "：" + segment.Error :
                (segment.IsForbidden ? "：禁止通行" : "：傳送到通道 " + rule.relativeID));
            button.ToolTip = message + "\n" + segment.From + "–" + segment.To + " px\n" + segment.Warning;
            AutomationProperties.SetName(button, message);
            button.Click += MarkerClicked;
            SetLeft(button, bounds.X);
            SetTop(button, bounds.Y);
            Children.Add(button);
        }

        /// <summary>建立透明命中區的按鈕範本，保留原生按鈕的鍵盤及自動化操作。</summary>
        /// <returns>以透明 Grid 及置中內容呈現的按鈕範本。</returns>
        private static ControlTemplate MarkerTemplate()
        {
            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            grid.AppendChild(content);
            return new ControlTemplate(typeof(Button)) { VisualTree = grid };
        }

        /// <summary>依錯誤、禁行或傳送狀態選擇線條與標籤顏色。</summary>
        /// <param name="segment">待顯示的區段。</param>
        /// <returns>適合既有深色預覽背景的狀態色。</returns>
        private static Brush SegmentBrush(TunnelPreviewSegment segment) => segment.Error.Length > 0 ? InvalidBrush : segment.IsForbidden ? ForbidBrush : PassBrush;

        /// <summary>點選彩色線段的透明命中區時選取規則，並阻止底下的螢幕誤接收同一次點擊。</summary>
        /// <param name="sender">Tag 保存編輯物件的線段。</param>
        /// <param name="e">設為已處理的左鍵事件。</param>
        private void SegmentClicked(object sender, MouseButtonEventArgs e)
        {
            TunnelSelected?.Invoke((TunnelInfo_Notify)((Line)sender).Tag);
            e.Handled = true;
        }

        /// <summary>將標籤的來源物件通知父控制項，避免以可能重新編號的 ID 反查。</summary>
        /// <param name="sender">Tag 帶有編輯物件的標籤按鈕。</param>
        /// <param name="e">由按鈕處理的路由事件。</param>
        private void MarkerClicked(object sender, RoutedEventArgs e) => TunnelSelected?.Invoke((TunnelInfo_Notify)((Button)sender).Tag);

        #endregion
    }
}
