using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>把共用驗證結果畫到單一實體螢幕的透明表面，不接收輸入或修改通道；限 UI 執行緒。</summary>
    internal sealed class PhysicalTunnelView : Canvas
    {
        /// <summary>本表面對應的螢幕快照索引。</summary>
        private int _monitorIndex = -1;
        /// <summary>實體螢幕原點，只用於把標籤座標轉為距上緣或左緣的像素。</summary>
        private MonitorInfo _monitor;
        /// <summary>不持有編輯事件的規則值快照。</summary>
        private IReadOnlyList<TunnelInfo> _rules = new TunnelInfo[0];
        /// <summary>與編輯器相同、含未完成文字錯誤的驗證結果。</summary>
        private IReadOnlyList<TunnelPreviewSegment> _segments = new TunnelPreviewSegment[0];
        /// <summary>封鎖未設定邊緣時的剩餘區段。</summary>
        private IReadOnlyList<TunnelPreviewSegment> _gaps = new TunnelPreviewSegment[0];
        /// <summary>選取來源及目標的快照索引，-1 表示沒有選取。</summary>
        private int _selected = -1, _target = -1;
        /// <summary>中央辨識卡的實際容器，用來避免通道標籤遮住編號或提示。</summary>
        private readonly FrameworkElement _badge;
        /// <summary>最近重畫時的辨識卡範圍，避免 LayoutUpdated 造成重複重建。</summary>
        private Rect _reserved = Rect.Empty;
        /// <summary>文字無法容納時通知辨識卡顯示數量，邊緣線仍完整保留。</summary>
        internal event Action<int> LabelsOmitted;

        #region -- Public Methods --

        /// <summary>建立不可點擊的透明繪製表面，隨實際可用 DIP 尺寸重畫。</summary>
        /// <param name="badge">同一視覺樹上的中央辨識卡，null 使用預設保留區。</param>
        internal PhysicalTunnelView(FrameworkElement badge = null)
        {
            _badge = badge;
            IsHitTestVisible = false;
            ClipToBounds = true;
            SizeChanged += SurfaceSizeChanged;
            LayoutUpdated += SurfaceLayoutUpdated;
        }

        /// <summary>套用獨立快照，依實際視窗大小與區段比例重畫，不把螢幕縮放比例重複乘入；限 UI 執行緒。</summary>
        /// <param name="monitorIndex">目前螢幕在原始快照中的索引。</param>
        /// <param name="monitor">目前螢幕的實體原點與邊界。</param>
        /// <param name="rules">依清單順序排列的原生規則值。</param>
        /// <param name="segments">與 rules 順序一致的共用驗證結果。</param>
        /// <param name="gaps">未設定邊緣的禁行區段；空集合表示不繪製。</param>
        /// <param name="selected">選取規則的快照索引。</param>
        internal void Update(int monitorIndex, MonitorInfo monitor, IReadOnlyList<TunnelInfo> rules, IReadOnlyList<TunnelPreviewSegment> segments,
            IReadOnlyList<TunnelPreviewSegment> gaps, int selected)
        {
            _monitorIndex = monitorIndex;
            _monitor = monitor;
            _rules = rules;
            _segments = segments;
            _gaps = gaps;
            _selected = selected;
            _target = selected >= 0 && selected < segments.Count ? segments[selected].TargetIndex : -1;
            Draw();
        }

        /// <summary>辨識視窗永久關閉時解除 Dispatcher 的配置通知，避免重複辨識留下舊表面；限 UI 執行緒。</summary>
        internal void StopTrackingLayout() => LayoutUpdated -= SurfaceLayoutUpdated;

        #endregion

        #region -- Private Methods --

        /// <summary>在 UI 配置尺寸變更時重新定位，不重新建立 HWND。</summary>
        /// <param name="sender">本繪製表面。</param>
        /// <param name="e">新舊尺寸。</param>
        private void SurfaceSizeChanged(object sender, SizeChangedEventArgs e) => Draw();

        /// <summary>中央卡片完成配置或因提示換行而改變範圍時重新避讓，範圍不變時不重畫。</summary>
        /// <param name="sender">WPF 配置來源。</param>
        /// <param name="e">配置完成事件。</param>
        private void SurfaceLayoutUpdated(object sender, EventArgs e)
        {
            if (ActualWidth > 0 && ActualHeight > 0 && ReservedBounds() != _reserved) Draw();
        }

        /// <summary>取得中央卡片的實際表面座標，尚未配置時先保留安全區。</summary>
        /// <returns>包含標籤間距的中央禁用範圍。</returns>
        private Rect ReservedBounds()
        {
            if (_badge != null && _badge.ActualWidth > 0 && _badge.ActualHeight > 0 && VisualTreeHelper.GetParent(_badge) != null)
            {
                Rect bounds = _badge.TransformToVisual(this).TransformBounds(new Rect(_badge.RenderSize));
                bounds.Inflate(12, 12);
                return bounds;
            }
            double width = Math.Min(360, ActualWidth), height = Math.Min(330, ActualHeight);
            return new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height);
        }

        /// <summary>繪製所有邊緣、優先配置選取標籤，密集標籤避開中央辨識卡並回報略過數量。</summary>
        private void Draw()
        {
            Children.Clear();
            if (ActualWidth < 1 || ActualHeight < 1) return;
            foreach (TunnelPreviewSegment gap in _gaps.Where(segment => segment.MonitorIndex == _monitorIndex)) AddSegment(gap, true);
            TunnelPreviewSegment[] visible = _segments.Where(segment => segment.MonitorIndex == _monitorIndex && segment.HasGeometry).ToArray();
            foreach (TunnelPreviewSegment segment in visible.OrderBy(IsEmphasized)) AddSegment(segment, false);
            _reserved = ReservedBounds();
            List<Rect> occupied = new List<Rect> { _reserved };
            int omitted = 0;
            foreach (TunnelPreviewSegment segment in visible.OrderByDescending(IsEmphasized))
                if (!AddMarker(segment, occupied)) ++omitted;
            LabelsOmitted?.Invoke(omitted);
        }

        /// <summary>判斷是否需強調目前選取的來源或傳送目標。</summary>
        /// <param name="segment">待繪製規則。</param>
        /// <returns>為選取來源或目標時為 true。</returns>
        private bool IsEmphasized(TunnelPreviewSegment segment) => segment.RuleIndex >= 0 && (segment.RuleIndex == _selected || segment.RuleIndex == _target);

        /// <summary>將含尾端像素的正規化範圍投影到實體螢幕表面，重疊規則內縮分層。</summary>
        /// <param name="segment">共用計算器的區段。</param>
        /// <param name="start">輸出第一個像素的邊緣位置。</param>
        /// <param name="end">輸出最後像素之後的位置。</param>
        private void Endpoints(TunnelPreviewSegment segment, out Point start, out Point end)
        {
            bool vertical = segment.Edge == EdgeType.Left || segment.Edge == EdgeType.Right;
            double inset = Math.Min(4 + segment.Lane * 9, (vertical ? ActualWidth : ActualHeight) / 3);
            if (vertical)
            {
                double x = segment.Edge == EdgeType.Left ? inset : ActualWidth - inset;
                start = new Point(x, segment.StartFraction * ActualHeight);
                end = new Point(x, segment.EndFraction * ActualHeight);
            }
            else
            {
                double y = segment.Edge == EdgeType.Top ? inset : ActualHeight - inset;
                start = new Point(segment.StartFraction * ActualWidth, y);
                end = new Point(segment.EndFraction * ActualWidth, y);
            }
        }

        /// <summary>加入有對比外緣的通道線，禁行與錯誤同時使用虛線而非僅靠顏色。</summary>
        /// <param name="segment">可定位的區段。</param>
        /// <param name="implicitForbid">是否為沒有通道 ID 的剩餘禁行區段。</param>
        private void AddSegment(TunnelPreviewSegment segment, bool implicitForbid)
        {
            Endpoints(segment, out Point start, out Point end);
            double thickness = implicitForbid ? 2 : IsEmphasized(segment) ? 7 : 4;
            AddLine(start, end, "MarkerBackgroundBrush", thickness + 3, null, 1, null);
            AddLine(start, end, BrushKey(segment), thickness,
                segment.Error.Length > 0 ? new DoubleCollection { 1, 2 } : segment.IsForbidden ? new DoubleCollection { 3, 2 } : null,
                implicitForbid ? 0.65 : 1, segment);
        }

        /// <summary>加入一條非互動線，動態資源會直接跟隨系統主題。</summary>
        /// <param name="start">起點。</param>
        /// <param name="end">終點。</param>
        /// <param name="brush">筆刷資源名稱。</param>
        /// <param name="thickness">DIP 線寬。</param>
        /// <param name="dash">虛線樣式，null 為實線。</param>
        /// <param name="opacity">不透明度。</param>
        /// <param name="tag">保留區段供辨識與測試，裝飾線為 null。</param>
        private void AddLine(Point start, Point end, string brush, double thickness, DoubleCollection dash, double opacity, object tag)
        {
            Line line = new Line { X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y, StrokeThickness = thickness, StrokeDashArray = dash, Opacity = opacity, Tag = tag };
            line.SetResourceReference(Shape.StrokeProperty, brush);
            Children.Add(line);
        }

        /// <summary>建立含編號、目的螢幕與範圍的標籤；找不到不重疊的位置時只保留邊緣線。</summary>
        /// <param name="segment">明確的通道規則。</param>
        /// <param name="occupied">已使用的位置，包含中央辨識卡。</param>
        /// <returns>標籤已顯示時為 true。</returns>
        private bool AddMarker(TunnelPreviewSegment segment, List<Rect> occupied)
        {
            TunnelInfo rule = _rules[segment.RuleIndex];
            string action = segment.Error.Length > 0 ? "設定異常 · " + segment.Error : segment.IsForbidden ? "禁止通行" :
                "→ 螢幕 " + ((long)_rules[segment.TargetIndex].displayID + 1) + " · " + EdgeName(_rules[segment.TargetIndex].edgeType) + " · 通道 " + rule.relativeID;
            string state = segment.RuleIndex == _selected ? " · 已選取" : segment.RuleIndex == _target ? " · 目的端" : string.Empty;
            if (segment.Warning.Length > 0) state += " · 重疊";
            StackPanel text = new StackPanel();
            text.Children.Add(Label("通道 " + rule.id + state, 12, BrushKey(segment), FontWeights.SemiBold));
            text.Children.Add(Label(action, 11, "TextBrush", FontWeights.Normal));
            bool vertical = segment.Edge == EdgeType.Left || segment.Edge == EdgeType.Right;
            long origin = vertical ? _monitor.top : _monitor.left;
            text.Children.Add(Label(EdgeName(segment.Edge) + " · 距" + (vertical ? "上緣 " : "左緣 ") +
                (segment.From - origin) + "–" + (segment.To - origin) + " px", 10, "MutedTextBrush", FontWeights.Normal));
            double width = Math.Min(224, Math.Max(1, ActualWidth - 32));
            Border marker = new Border { Width = width, Padding = new Thickness(10, 6, 10, 6), CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(IsEmphasized(segment) ? 2 : 1), Child = text, Tag = segment };
            marker.SetResourceReference(Border.BackgroundProperty, "MarkerBackgroundBrush");
            marker.SetResourceReference(Border.BorderBrushProperty, BrushKey(segment));
            marker.Measure(new Size(width, double.PositiveInfinity));
            Endpoints(segment, out Point start, out Point end);
            Point middle = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            Rect bounds = FindMarkerBounds(segment.Edge, middle, new Size(width, marker.DesiredSize.Height), occupied);
            if (bounds.IsEmpty) return false;
            occupied.Add(bounds);
            Point anchor = new Point(Math.Max(bounds.Left, Math.Min(middle.X, bounds.Right)), Math.Max(bounds.Top, Math.Min(middle.Y, bounds.Bottom)));
            AddLine(middle, anchor, BrushKey(segment), 1, new DoubleCollection { 2, 2 }, 0.8, null);
            SetLeft(marker, bounds.X);
            SetTop(marker, bounds.Y);
            Children.Add(marker);
            return true;
        }

        /// <summary>沿邊緣上下或左右交替尋位，再向內換列，維持標籤完整且不蓋住既有標籤。</summary>
        /// <param name="edge">來源邊緣。</param>
        /// <param name="middle">原始區段中點。</param>
        /// <param name="size">標籤實際測量大小。</param>
        /// <param name="occupied">禁止重疊的位置。</param>
        /// <returns>可用的表面座標，放不下時為 Empty。</returns>
        private Rect FindMarkerBounds(EdgeType edge, Point middle, Size size, List<Rect> occupied)
        {
            bool vertical = edge == EdgeType.Left || edge == EdgeType.Right;
            double along = vertical ? size.Height + 8 : size.Width + 8;
            double across = vertical ? size.Width + 8 : size.Height + 8;
            int shifts = (int)Math.Ceiling((vertical ? ActualHeight : ActualWidth) / along) * 2 + 1;
            int lanes = (int)Math.Ceiling((vertical ? ActualWidth : ActualHeight) / across);
            for (int lane = 0; lane < lanes; ++lane)
                for (int shift = 0; shift < shifts; ++shift)
                {
                    double offset = ((shift + 1) / 2) * (shift % 2 == 0 ? 1 : -1) * along;
                    double x = vertical ? edge == EdgeType.Left ? 16 + lane * across : ActualWidth - 16 - size.Width - lane * across : middle.X - size.Width / 2 + offset;
                    double y = vertical ? middle.Y - size.Height / 2 + offset : edge == EdgeType.Top ? 16 + lane * across : ActualHeight - 16 - size.Height - lane * across;
                    Rect candidate = new Rect(x, y, size.Width, size.Height);
                    if (candidate.Left < 8 || candidate.Top < 8 || candidate.Right > ActualWidth - 8 || candidate.Bottom > ActualHeight - 8) continue;
                    if (!occupied.Any(rectangle => rectangle.IntersectsWith(candidate))) return candidate;
                }
            return Rect.Empty;
        }

        /// <summary>建立會隨主題更新且可換行的通道標籤。</summary>
        /// <param name="text">標籤文字。</param>
        /// <param name="size">DIP 字體大小。</param>
        /// <param name="brush">前景筆刷名稱。</param>
        /// <param name="weight">字重。</param>
        /// <returns>不可互動的文字元素。</returns>
        private static TextBlock Label(string text, double size, string brush, FontWeight weight)
        {
            TextBlock label = new TextBlock { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap };
            label.SetResourceReference(TextBlock.ForegroundProperty, brush);
            return label;
        }

        /// <summary>取得含錯誤、禁行或正常傳送狀態的主題筆刷。</summary>
        /// <param name="segment">區段驗證結果。</param>
        /// <returns>色盤資源名稱。</returns>
        private static string BrushKey(TunnelPreviewSegment segment) => segment.Error.Length > 0 ? "WarningBrush" : segment.IsForbidden ? "DangerBrush" : "SuccessBrush";

        /// <summary>將原生邊緣列舉轉為一起使用的繁體中文方位。</summary>
        /// <param name="edge">有效邊緣。</param>
        /// <returns>來源或目標的方位文字。</returns>
        private static string EdgeName(EdgeType edge) => edge == EdgeType.Left ? "左側" : edge == EdgeType.Right ? "右側" : edge == EdgeType.Top ? "上側" : "下側";

        #endregion
    }
}
