using MonitorEdgeTunnelApp.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MonitorEdgeTunnelApp
{
    /// <summary>以實際 WPF XAML 及替身服務驗證配置、選取與重新整理，不啟動原生服務。</summary>
    public static class MonitorLayoutTests
    {
        /// <summary>本次具名案例的失敗數。</summary>
        private static int _failures;
        /// <summary>本次實際執行的具名案例數。</summary>
        private static int _cases;

        #region -- Public Methods --

        /// <summary>在 STA 執行配置及真實介面測試，可額外輸出示意資料的介面快照。</summary>
        /// <param name="args">選用 --snapshot 加輸出 PNG 路徑，僅用於檢查實際 WPF 呈現。</param>
        /// <returns>所有案例及資料綁定檢查通過時回傳 0，否則回傳 1。</returns>
        [STAThread]
        public static int Main(string[] args)
        {
            StringWriter bindingErrors = new StringWriter();
            TextWriterTraceListener listener = new TextWriterTraceListener(bindingErrors);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            try
            {
                Run("inclusive single-monitor bounds and centering", TestSingleMonitor);
                Run("negative origins and mixed DPI preserve proportions", TestNegativeOrigins);
                Run("vertical portrait and separated monitor geometry", TestOtherArrangements);
                Run("empty invalid and extreme coordinate inputs", TestInvalidInputs);
                Run("display labels retain original monitor IDs", TestDisplayLabels);
                Run("real XAML selection synchronizes both directions", TestSelection);
                Run("tunnel markers share monitor projection and preserve inclusive ranges", TestTunnelRendering);
                Run("tunnel marker invocation and table selection synchronize", TestTunnelSelection);
                Run("committed edits update ranges and warnings without applying settings", TestLiveTunnelEdits);
                Run("delete and renumber preserve object selection and release old rules", TestTunnelRemoval);
                Run("source replacement unload and reload update subscriptions", TestPreviewSubscriptions);
                Run("invalid rules remain selectable through preview diagnostics", TestPreviewDiagnostics);
                Run("resize preserves unsaved edits and selection", TestResize);
                Run("refresh preserves valid selection and handles disconnect", TestRefresh);
                if (args.Length == 2 && args[0] == "--snapshot")
                    CapturePreview(args[1]);
                listener.Flush();
                Run("real XAML produces no binding errors", () => Require(bindingErrors.ToString().Length == 0, bindingErrors.ToString()));
                Console.WriteLine((_cases - _failures) + "/" + _cases + " monitor layout cases passed");
                return _failures == 0 ? 0 : 1;
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                listener.Close();
            }
        }

        #endregion

        #region -- Private Methods --

        /// <summary>驗證單一螢幕的含尾端寬高及置中結果，失敗時拋出例外。</summary>
        private static void TestSingleMonitor()
        {
            Rect rectangle = MonitorLayoutCalculator.Calculate(new[] { Monitor(0, 0, 0, 100, 100) }, new Size(300, 200))[0];
            Near(rectangle.X, 66);
            Near(rectangle.Y, 16);
            Near(rectangle.Width, 168);
            Near(rectangle.Height, 168);
        }

        /// <summary>驗證負座標及不同 DPI 不會破壞相鄰邊界或解析度比例。</summary>
        private static void TestNegativeOrigins()
        {
            MonitorInfo[] monitors = { Monitor(0, 0, 0, 2560, 1440, 1.5), Monitor(1, -1280, 0, 1280, 720) };
            Rect[] rectangles = MonitorLayoutCalculator.Calculate(monitors, new Size(600, 300));
            Near(rectangles[0].Width, rectangles[1].Width * 2);
            Near(rectangles[0].Height, rectangles[1].Height * 2);
            Near(rectangles[1].Right, rectangles[0].Left);
            Near(rectangles[0].Top, rectangles[1].Top);
            Near((rectangles[1].Left + rectangles[0].Right) / 2, 300);
            monitors[0].scaling = 2;
            monitors[1].scaling = 1.25;
            Require(rectangles.SequenceEqual(MonitorLayoutCalculator.Calculate(monitors, new Size(600, 300))),
                "DPI metadata must not independently scale desktop coordinates");
        }

        /// <summary>驗證上下、直向及有間距的配置，失敗時拋出例外。</summary>
        private static void TestOtherArrangements()
        {
            Rect[] vertical = MonitorLayoutCalculator.Calculate(new[] { Monitor(0, 0, 0, 100, 100), Monitor(1, 0, -100, 100, 100) }, new Size(400, 300));
            Near(vertical[1].Bottom, vertical[0].Top);
            Near(vertical[1].Left, vertical[0].Left);
            Rect portrait = MonitorLayoutCalculator.Calculate(new[] { Monitor(0, -50, -100, 100, 200) }, new Size(400, 300))[0];
            Near(portrait.Height, portrait.Width * 2);
            Rect[] separated = MonitorLayoutCalculator.Calculate(new[] { Monitor(7, 0, 0, 100, 100), Monitor(3, 200, 0, 100, 100) }, new Size(400, 300));
            Near(separated[1].Left - separated[0].Right, separated[0].Width);
        }

        /// <summary>驗證空清單、無效配置區與超大整數座標不會產生無效矩形。</summary>
        private static void TestInvalidInputs()
        {
            Require(MonitorLayoutCalculator.Calculate(new MonitorInfo[0], new Size(300, 200)).Length == 0, "empty list");
            foreach (Size size in new[] { new Size(), new Size(10, 10), new Size(double.PositiveInfinity, 200) })
                Require(MonitorLayoutCalculator.Calculate(new[] { Monitor(0, 0, 0, 100, 100) }, size)[0].IsEmpty, "unavailable viewport");
            MonitorInfo invalid = new MonitorInfo { left = 100, right = 0 };
            Rect[] mixed = MonitorLayoutCalculator.Calculate(new[] { invalid, Monitor(1, 0, 0, 100, 100) }, new Size(300, 200));
            Require(mixed[0].IsEmpty && !mixed[1].IsEmpty, "invalid entry excluded from desktop bounds");
            MonitorInfo extreme = new MonitorInfo { left = int.MinValue, right = int.MaxValue, top = -100, bottom = 99 };
            Rect rectangle = MonitorLayoutCalculator.Calculate(new[] { extreme }, new Size(400, 300))[0];
            Require(!rectangle.IsEmpty && rectangle.Width > 0 && rectangle.Height > 0 && rectangle.Left >= 0 && rectangle.Right <= 400,
                "coordinate subtraction must not overflow int");
        }

        /// <summary>驗證一起算標籤、解析度及縮放摘要，不修改來源資料。</summary>
        private static void TestDisplayLabels()
        {
            MonitorNumberConverter number = new MonitorNumberConverter();
            Require((string)number.Convert(0, typeof(string), null, CultureInfo.InvariantCulture) == "1", "zero-based ID display");
            Require((string)number.Convert(7, typeof(string), null, CultureInfo.InvariantCulture) == "8", "ID is not a collection index");
            Require((string)number.Convert(-1, typeof(string), null, CultureInfo.InvariantCulture) == string.Empty, "invalid ID has no display label");
            MonitorInfo monitor = Monitor(0, -1280, 0, 1280, 720, 1.25);
            string description = (string)new MonitorDescriptionConverter().Convert(monitor, typeof(string), null, CultureInfo.InvariantCulture);
            Require(description == "1280 × 720 · 縮放比例 125% · 位置 (-1280, 0)" && monitor.id == 0, "display metadata and original ID");
            foreach (double scale in new[] { 1.0, 1.75, 0.0 })
            {
                monitor.scaling = scale;
                description = (string)new MonitorDescriptionConverter().Convert(monitor, typeof(string), null, CultureInfo.InvariantCulture);
                string expected = scale == 0 ? "-" : scale == 1 ? "100%" : "175%";
                Require(description.Contains("縮放比例 " + expected + " ·"), "actual display scaling or unknown marker");
            }
        }

        /// <summary>透過真實清單容器選取螢幕，驗證兩側綁定及通道選單的原始 ID。</summary>
        private static void TestSelection()
        {
            MainWindow window = CreateWindow();
            try
            {
                Arrange(window, 1000, 560);
                ListBox map = LayoutList(window);
                ListView table = (ListView)window.FindName("MonitorInfoListView");
                Require(map.Items.Count == 2 && Equals(map.SelectedValue, 0) && Equals(table.SelectedValue, 0), "initial selection");
                Require(Equals(((GridView)table.View).Columns.Last().Header, "縮放比例"), "display scaling column name");
                ListViewItem firstRow = (ListViewItem)table.ItemContainerGenerator.ContainerFromIndex(0);
                ListViewItem secondRow = (ListViewItem)table.ItemContainerGenerator.ContainerFromIndex(1);
                Require(Descendants<TextBlock>(firstRow).Any(text => text.Text == "175%") &&
                    Descendants<TextBlock>(secondRow).Any(text => text.Text == "100%"), "table shows Windows scaling percentages");
                ListBoxItem item = (ListBoxItem)map.ItemContainerGenerator.ContainerFromIndex(1);
                ISelectionItemProvider provider = (ISelectionItemProvider)new ListBoxItemAutomationPeer(map.Items[1], new ListBoxAutomationPeer(map))
                    .GetPattern(PatternInterface.SelectionItem);
                provider.Select();
                Pump();
                Require(window.selectedMonitorId == 1 && Equals(table.SelectedValue, 1), "map selection reaches information table");
                table.SelectedValue = 0;
                Pump();
                Require(Equals(map.SelectedValue, 0), "information table selection reaches map");
                Require(Descendants<TextBlock>(item).Any(text => text.Text == "2"), "map uses one-based display number");
                ComboBox monitorCombo = Descendants<ComboBox>((ListView)window.FindName("TunnelInfoListView"))
                    .First(combo => combo.Items.Count == 2 && combo.Items[0] is MonitorInfo);
                monitorCombo.SelectedIndex = 1;
                Pump();
                Require(window.tunnelInfos[0].displayID == 1, "display label does not change stored monitor ID");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證真實覆蓋層與螢幕容器使用同一個投影，且可顯示四邊範圍及強制禁行。</summary>
        private static void TestTunnelRendering()
        {
            MainWindow window = CreateTunnelWindow();
            HwndSourceParameters parameters = new HwndSourceParameters("Tunnel preview hit testing")
                { Width = 1000, Height = 560, WindowStyle = unchecked((int)0x80000000) };
            HwndSource host = new HwndSource(parameters);
            host.RootVisual = (Visual)window.Content;
            try
            {
                Arrange(window, 1000, 560);
                MonitorLayoutView view = (MonitorLayoutView)window.FindName("MonitorLayout");
                TunnelOverlay overlay = (TunnelOverlay)view.FindName("TunnelMarks");
                Line[] lines = overlay.Children.OfType<Line>().Where(line => line.Tag is TunnelPreviewSegment).ToArray();
                Require(lines.Length == 3 && overlay.Children.OfType<Button>().Count() == 3, "one marker per explicit rule");
                Line source = lines.Single(line => ((TunnelPreviewSegment)line.Tag).RuleIndex == 0);
                Rect monitor = ItemBounds(LayoutList(window), 1);
                Near(source.X1, monitor.Right - 4);
                Near(source.Y1, monitor.Top + monitor.Height * 180 / 720);
                Near(source.Y2, monitor.Top + monitor.Height * 540 / 720);
                UIElement hit = overlay.InputHitTest(new Point(source.X1, source.Y1 + 1)) as UIElement;
                Require(hit is Line hitLine && ReferenceEquals(hitLine.Tag, window.tunnelInfos[0]), "colored segment has an actual hit region outside its ID button");
                MouseButtonEventArgs click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
                hit.RaiseEvent(click);
                Pump();
                Require(click.Handled && ReferenceEquals(window.selectedTunnel, window.tunnelInfos[0]), "segment click selects its rule and does not leak to the monitor");
                window.forceForbidEdgeChecked = true;
                Pump();
                Require(overlay.Children.OfType<Line>().Any(line => line.Tag is TunnelPreviewSegment segment && segment.RuleIndex == -1),
                    "implicit blocking gaps are visible");
                double oldLength = source.Y2 - source.Y1;
                Arrange(window, 860, 480);
                source = overlay.Children.OfType<Line>().Single(line => line.Tag is TunnelPreviewSegment segment && segment.RuleIndex == 0);
                Require(source.Y2 - source.Y1 < oldLength && source.StrokeThickness == 4, "resize projects geometry while line thickness stays readable");
            }
            finally { host.Dispose(); window.Close(); }
        }

        /// <summary>透過真實按鈕自動化點選通道，再由清單反向選取禁行規則，驗證方向及綁定保留。</summary>
        private static void TestTunnelSelection()
        {
            MainWindow window = CreateTunnelWindow();
            try
            {
                Arrange(window, 1000, 560);
                MonitorLayoutView view = (MonitorLayoutView)window.FindName("MonitorLayout");
                TunnelOverlay overlay = (TunnelOverlay)view.FindName("TunnelMarks");
                ListView table = (ListView)window.FindName("TunnelInfoListView");
                int loadCalls = MonitorEdgeTunnel.Instance.LoadCalls;
                Button marker = overlay.Children.OfType<Button>().Single(button => ReferenceEquals(button.Tag, window.tunnelInfos[0]));
                ((IInvokeProvider)new ButtonAutomationPeer(marker).GetPattern(PatternInterface.Invoke)).Invoke();
                Pump();
                Require(ReferenceEquals(window.selectedTunnel, window.tunnelInfos[0]) && ReferenceEquals(table.SelectedItem, window.tunnelInfos[0]) &&
                    window.selectedMonitorId == 1, "preview selects original rule and owner display");
                Require(overlay.Children.OfType<System.Windows.Shapes.Path>().Count(item => Equals(item.Tag, "TunnelConnection")) == 1,
                    "selected transport has one directional connection");
                table.SelectedItem = window.tunnelInfos[2];
                Pump();
                Require(ReferenceEquals(view.SelectedTunnel, window.tunnelInfos[2]) && window.selectedMonitorId == 0 &&
                    !overlay.Children.OfType<System.Windows.Shapes.Path>().Any(item => Equals(item.Tag, "TunnelConnection")), "blocking rule has no invented return connection");
                Require(BindingOperations.IsDataBound(view, MonitorLayoutView.SelectedTunnelProperty) &&
                    BindingOperations.IsDataBound(view, MonitorLayoutView.SelectedMonitorIdProperty), "UI selection preserves both bindings");
                Require(MonitorEdgeTunnel.Instance.LoadCalls == loadCalls, "selection never reloads native settings");
            }
            finally { window.Close(); }
        }

        /// <summary>建立含相互連結規則及單向禁行規則的真實主視窗，服務仍使用替身。</summary>
        /// <returns>由呼叫端關閉的未顯示視窗。</returns>
        private static MainWindow CreateTunnelWindow()
        {
            MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
            service.Monitors.Clear();
            service.Monitors.Add(Monitor(0, 0, 0, 2560, 1440, 1.75));
            service.Monitors.Add(Monitor(1, -1280, 0, 1280, 720));
            service.Tunnels.Clear();
            service.Tunnels.Add(new TunnelInfo { id = 0, displayID = 1, relativeID = 1, edgeType = EdgeType.Right,
                rangeType = RangeType.Relative, from = 180, to = 180 });
            service.Tunnels.Add(new TunnelInfo { id = 1, displayID = 0, relativeID = 0, edgeType = EdgeType.Left,
                rangeType = RangeType.Relative, from = 360, to = 360 });
            service.Tunnels.Add(new TunnelInfo { id = 2, displayID = 0, relativeID = -1, edgeType = EdgeType.Top,
                rangeType = RangeType.Customize, from = 640, to = 639 });
            return new MainWindow();
        }

        /// <summary>由真實 TextBox 提交編輯後驗證範圍、所屬螢幕、錯誤及重疊提示，且不呼叫套用或載入。</summary>
        private static void TestLiveTunnelEdits()
        {
            MainWindow window = CreateTunnelWindow();
            try
            {
                Arrange(window, 1000, 560);
                MonitorLayoutView view = (MonitorLayoutView)window.FindName("MonitorLayout");
                TunnelOverlay overlay = (TunnelOverlay)view.FindName("TunnelMarks");
                ListView table = (ListView)window.FindName("TunnelInfoListView");
                window.selectedTunnel = window.tunnelInfos[0];
                Pump();
                int loads = MonitorEdgeTunnel.Instance.LoadCalls;
                int applies = MonitorEdgeTunnel.Instance.ApplyCalls;
                FrameworkElement row = (FrameworkElement)table.ItemContainerGenerator.ContainerFromIndex(0);
                TextBox from = Descendants<TextBox>(row).Single(box => box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "from");
                from.Text = "100";
                from.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                Pump();
                Require(window.tunnelInfos[0].from == 100 && view.PreviewSegments[0].From == 100 && view.PreviewSegments[0].To == 539,
                    "committed text edit reaches projection");
                window.tunnelInfos[0].rangeType = RangeType.Customize;
                window.tunnelInfos[0].to = 49;
                window.tunnelInfos[0].edgeType = EdgeType.Bottom;
                window.tunnelInfos[0].displayID = 0;
                Pump();
                Require(view.PreviewSegments[0].From == 100 && view.PreviewSegments[0].To == 149 && window.selectedMonitorId == 0,
                    "range mode and owner edits are followed");
                window.tunnelInfos.Add(new TunnelInfo_Notify { id = 3, displayID = 0, edgeType = EdgeType.Bottom,
                    rangeType = RangeType.Customize, from = 120, to = 99 });
                Pump();
                Require(view.PreviewSegments.Count == 4 && view.PreviewSegments[3].Lane == 1 && view.HasWarnings &&
                    view.PreviewMessages.Cast<TunnelPreviewNotice>().Any(item => ReferenceEquals(item.Rule, window.tunnelInfos[3])), "new overlapping rule appears with priority warning");
                window.tunnelInfos[0].to = int.MaxValue;
                Pump();
                Require(!view.PreviewSegments[0].HasGeometry && view.HasWarnings &&
                    !overlay.Children.OfType<Button>().Any(button => ReferenceEquals(button.Tag, window.tunnelInfos[0])), "invalid range is reported instead of clipped into a valid marker");
                Expander warnings = (Expander)view.FindName("PreviewWarnings");
                warnings.IsExpanded = true;
                window.selectedTunnel = null;
                Arrange(window, 1000, 560);
                Arrange(window, 860, 440);
                ScrollViewer details = (ScrollViewer)view.FindName("PreviewDetails");
                Rect detailsBounds = new Rect(details.TranslatePoint(new Point(), view), details.RenderSize);
                Require(detailsBounds.Bottom <= view.ActualHeight + 0.1 && details.ScrollableHeight > 0,
                    "expanded diagnostics stay inside a small window and can scroll");
                Button notice = Descendants<Button>(warnings).Single(button => ReferenceEquals(button.Tag, window.tunnelInfos[0]));
                ((IInvokeProvider)new ButtonAutomationPeer(notice).GetPattern(PatternInterface.Invoke)).Invoke();
                Pump();
                Require(ReferenceEquals(table.SelectedItem, window.tunnelInfos[0]) && view.SelectedTunnelDescription.Contains("範圍"),
                    "invalid geometry remains selectable from diagnostics");
                Require(MonitorEdgeTunnel.Instance.LoadCalls == loads && MonitorEdgeTunnel.Instance.ApplyCalls == applies,
                    "draft preview never reloads or applies native settings");
            }
            finally { window.Close(); }
        }

        /// <summary>由真實刪除按鈕觸發清單重編號，驗證選取以物件維持並解除已刪規則的通知。</summary>
        private static void TestTunnelRemoval()
        {
            MainWindow window = CreateTunnelWindow();
            try
            {
                Arrange(window, 1000, 560);
                MonitorLayoutView view = (MonitorLayoutView)window.FindName("MonitorLayout");
                ListView table = (ListView)window.FindName("TunnelInfoListView");
                TunnelInfo_Notify selected = window.tunnelInfos[2];
                TunnelInfo_Notify removed = window.tunnelInfos[0];
                window.selectedTunnel = selected;
                Pump();
                FrameworkElement firstRow = (FrameworkElement)table.ItemContainerGenerator.ContainerFromIndex(0);
                Button delete = Descendants<Button>(firstRow).Single(button => Equals(button.Content, "刪除"));
                ((IInvokeProvider)new ButtonAutomationPeer(delete).GetPattern(PatternInterface.Invoke)).Invoke();
                Pump();
                Require(selected.id == 1 && ReferenceEquals(window.selectedTunnel, selected) && ReferenceEquals(view.SelectedTunnel, selected),
                    "renumbering preserves the selected object");
                var before = view.PreviewSegments;
                removed.from = 123;
                Pump();
                Require(ReferenceEquals(before, view.PreviewSegments), "removed rule no longer triggers a preview refresh");
                window.tunnelInfos.Remove(selected);
                Pump();
                Require(window.selectedTunnel == null && view.SelectedTunnel == null, "removed selection is cleared");
                window.tunnelInfos.Clear();
                window.tunnelInfos.Add(new TunnelInfo_Notify { id = 0, displayID = 0, edgeType = EdgeType.Top });
                Pump();
                Require(view.PreviewSegments.Count == 1 && view.PreviewSegments[0].Edge == EdgeType.Top, "collection Reset and Add replace old rules");
            }
            finally { window.Close(); }
        }

        /// <summary>替換來源並模擬控制項卸載重載，驗證舊來源不再重畫、暫停期間不工作且重載會補上最新值。</summary>
        private static void TestPreviewSubscriptions()
        {
            MainWindow window = CreateTunnelWindow();
            try
            {
                Arrange(window, 1000, 560);
                MonitorLayoutView view = (MonitorLayoutView)window.FindName("MonitorLayout");
                TunnelInfo_Notify old = window.tunnelInfos[0];
                ObservableCollection<TunnelInfo_Notify> replacement = new ObservableCollection<TunnelInfo_Notify> {
                    new TunnelInfo_Notify { id = 7, displayID = 0, edgeType = EdgeType.Top, rangeType = RangeType.Customize, from = 10, to = 20 }
                };
                view.TunnelsSource = replacement;
                Pump();
                var before = view.PreviewSegments;
                old.from = 9;
                Pump();
                Require(ReferenceEquals(before, view.PreviewSegments), "replaced source subscriptions released");
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                replacement[0].from = 30;
                Pump();
                Require(ReferenceEquals(before, view.PreviewSegments), "unloaded preview has no background update");
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Pump();
                Require(view.PreviewSegments[0].From == 30, "reload catches up with current values");
                window.monitorInfos.Clear();
                Pump();
                Require(!view.PreviewSegments[0].HasGeometry && view.HasWarnings, "monitor disconnect updates diagnostics");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證單向關聯及沒有螢幕幾何的無效規則，可由警告清單選取並在修正後移除警告。</summary>
        private static void TestPreviewDiagnostics()
        {
            ObservableCollection<MonitorInfo> monitors = new ObservableCollection<MonitorInfo> { Monitor(0, 0, 0, 100, 100) };
            ObservableCollection<TunnelInfo_Notify> rules = new ObservableCollection<TunnelInfo_Notify> {
                new TunnelInfo_Notify { id = 0, displayID = 0, edgeType = EdgeType.Left },
                new TunnelInfo_Notify { id = 1, displayID = 0, edgeType = EdgeType.Right, relativeID = 99 },
                new TunnelInfo_Notify { id = 2, displayID = 7, edgeType = EdgeType.Top }
            };
            MonitorLayoutView view = new MonitorLayoutView { ItemsSource = monitors, TunnelsSource = rules };
            try
            {
                Pump();
                view.Measure(new Size(380, 500));
                view.Arrange(new Rect(0, 0, 380, 500));
                view.UpdateLayout();
                Pump();
                Require(view.HasWarnings && view.PreviewMessages.Cast<TunnelPreviewNotice>().Count() == 2 &&
                    view.PreviewSegments[1].HasGeometry && !view.PreviewSegments[2].HasGeometry, "invalid target and missing display are distinct");
                Expander warnings = (Expander)view.FindName("PreviewWarnings");
                warnings.IsExpanded = true;
                view.Measure(new Size(380, 500));
                view.Arrange(new Rect(0, 0, 380, 500));
                view.UpdateLayout();
                Pump();
                Button notice = Descendants<Button>(warnings).Single(button => ReferenceEquals(button.Tag, rules[2]));
                ((IInvokeProvider)new ButtonAutomationPeer(notice).GetPattern(PatternInterface.Invoke)).Invoke();
                Pump();
                Require(ReferenceEquals(view.SelectedTunnel, rules[2]) && view.SelectedTunnelDescription.Contains("找不到"), "missing geometry still supports selection");
                rules[1].relativeID = 0;
                rules[2].displayID = 0;
                Pump();
                Require(!view.HasWarnings && warnings.Visibility == Visibility.Collapsed && view.PreviewSegments[0].TargetIndex == -1 &&
                    view.PreviewSegments[1].TargetIndex == 0, "fixed rules clear diagnostics without inventing reverse links");
            }
            finally { view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
        }

        /// <summary>改變可用大小，驗證等比例重排且不重載或覆蓋正在編輯的設定。</summary>
        private static void TestResize()
        {
            MainWindow window = CreateWindow();
            try
            {
                Arrange(window, 1000, 560);
                ListBox map = LayoutList(window);
                Rect before = ItemBounds(map, 0);
                window.tunnelInfos[0].from = 42;
                window.forceForbidEdgeChecked = true;
                window.selectedMonitorId = 1;
                int loadCalls = MonitorEdgeTunnel.Instance.LoadCalls;
                Arrange(window, 840, 440);
                Rect after = ItemBounds(map, 0);
                Require(after.Width < before.Width && after.Width > 0, "smaller viewport resizes map");
                Near(ItemBounds(map, 1).Right, after.Left);
                Near(after.Width, ItemBounds(map, 1).Width * 2);
                Require(window.tunnelInfos[0].from == 42 && window.forceForbidEdgeChecked && window.selectedMonitorId == 1 &&
                    MonitorEdgeTunnel.Instance.LoadCalls == loadCalls, "layout operation must preserve unsaved settings");
            }
            finally { window.Close(); }
        }

        /// <summary>重新整理螢幕資料，驗證選取保留、移除後改選及沒有資料的提示。</summary>
        private static void TestRefresh()
        {
            MainWindow window = CreateWindow();
            try
            {
                window.selectedMonitorId = 1;
                window.UpdateData();
                Arrange(window, 1000, 560);
                Require(Equals(LayoutList(window).SelectedValue, 1), "refresh retains an existing selected ID");
                MonitorEdgeTunnel.Instance.Monitors.RemoveAt(1);
                window.UpdateData();
                Arrange(window, 1000, 560);
                Require(Equals(LayoutList(window).SelectedValue, 0) && window.selectedMonitorId == 0, "removed monitor falls back to first");
                MonitorEdgeTunnel.Instance.Monitors.Clear();
                window.UpdateData();
                Arrange(window, 1000, 560);
                MonitorLayoutView view = (MonitorLayoutView)window.FindName("MonitorLayout");
                Require(!window.selectedMonitorId.HasValue && LayoutList(window).Items.Count == 0 &&
                    ((TextBlock)view.FindName("EmptyLayoutMessage")).Visibility == Visibility.Visible, "empty layout clears selection and shows message");
            }
            finally { window.Close(); }
        }

        /// <summary>建立固定資料的真實主視窗，僅在測試 STA 使用，不顯示視窗或啟動原生服務。</summary>
        /// <returns>已載入測試螢幕與通道的主視窗，由呼叫端關閉。</returns>
        private static MainWindow CreateWindow()
        {
            MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
            service.Monitors.Clear();
            service.Monitors.Add(Monitor(0, 0, 0, 2560, 1440, 1.75));
            service.Monitors.Add(Monitor(1, -1280, 0, 1280, 720));
            service.Tunnels.Clear();
            service.Tunnels.Add(new TunnelInfo { id = 0, relativeID = -1, edgeType = EdgeType.Left, rangeType = RangeType.Full });
            return new MainWindow();
        }

        /// <summary>以寬高建立含尾端邊界的測試螢幕資料。</summary>
        /// <param name="id">原始 ID。</param>
        /// <param name="left">左邊界。</param>
        /// <param name="top">上邊界。</param>
        /// <param name="width">像素寬度。</param>
        /// <param name="height">像素高度。</param>
        /// <param name="scale">縮放資訊，不影響幾何配置。</param>
        /// <returns>包含指定座標及縮放資訊的螢幕。</returns>
        private static MonitorInfo Monitor(int id, int left, int top, int width, int height, double scale = 1)
        {
            return new MonitorInfo { id = id, left = left, top = top, right = left + width - 1, bottom = top + height - 1, scaling = scale };
        }

        /// <summary>在 UI 執行緒配置 window 的內容為指定寬高，並完成排程中的資料綁定。</summary>
        /// <param name="window">未顯示的受測視窗。</param>
        /// <param name="width">內容 DIP 寬度。</param>
        /// <param name="height">內容 DIP 高度。</param>
        private static void Arrange(MainWindow window, double width, double height)
        {
            Pump();
            FrameworkElement content = (FrameworkElement)window.Content;
            content.Measure(new Size(width, height));
            content.Arrange(new Rect(0, 0, width, height));
            content.UpdateLayout();
            Pump();
        }

        /// <summary>讓 UI 執行緒完成現有綁定及配置通知，不建立背景服務。</summary>
        private static void Pump()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        /// <summary>從 window 的真實佈局控制項取得螢幕清單。</summary>
        /// <param name="window">受測主視窗。</param>
        /// <returns>控制項內部用於選取的 ListBox。</returns>
        private static ListBox LayoutList(MainWindow window)
        {
            return (ListBox)((MonitorLayoutView)window.FindName("MonitorLayout")).FindName("MonitorList");
        }

        /// <summary>取得 index 項目在 list 內的實際配置矩形，供檢查相鄰邊界與比例。</summary>
        /// <param name="list">已配置的螢幕清單。</param>
        /// <param name="index">要檢查的項目位置。</param>
        /// <returns>轉換至清單座標的顯示矩形。</returns>
        private static Rect ItemBounds(ListBox list, int index)
        {
            FrameworkElement item = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(index);
            return new Rect(item.TranslatePoint(new Point(), list), item.RenderSize);
        }

        /// <summary>遍歷 root 的視覺子樹，供測試尋找實際生成的控制項。</summary>
        /// <typeparam name="T">要尋找的視覺物件型別。</typeparam>
        /// <param name="root">搜尋起點。</param>
        /// <returns>所有符合型別的子孫物件。</returns>
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                    yield return match;
                foreach (T descendant in Descendants<T>(child))
                    yield return descendant;
            }
        }

        /// <summary>將固定測試資料的實際 WPF 畫面輸出為 PNG，不顯示或操作桌面視窗。</summary>
        /// <param name="path">呼叫端指定的 PNG 檔案位置。</param>
        private static void CapturePreview(string path)
        {
            MainWindow window = CreateTunnelWindow();
            window.selectedTunnel = window.tunnelInfos[0];
            try
            {
                HwndSourceParameters parameters = new HwndSourceParameters("Monitor layout test preview")
                {
                    Width = 1000,
                    Height = 560,
                    WindowStyle = unchecked((int)0x80000000)
                };
                using (HwndSource host = new HwndSource(parameters))
                {
                    host.RootVisual = (Visual)window.Content;
                    Arrange(window, 1000, 560);
                    RenderTargetBitmap bitmap = new RenderTargetBitmap(1000, 560, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render((Visual)window.Content);
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (FileStream file = File.Create(path))
                        encoder.Save(file);
                }
            }
            finally { window.Close(); }
        }

        /// <summary>執行 name 所標示的 test，列印結果並累計失敗數。</summary>
        /// <param name="name">案例名稱。</param>
        /// <param name="test">在測試 STA 執行的案例。</param>
        private static void Run(string name, Action test)
        {
            ++_cases;
            try { test(); Console.WriteLine("PASS " + name); }
            catch (Exception error) { ++_failures; Console.WriteLine("FAIL " + name + ": " + error); }
        }

        /// <summary>檢查兩個座標在浮點誤差內相等，否則中止案例。</summary>
        /// <param name="actual">實際座標。</param>
        /// <param name="expected">預期座標。</param>
        private static void Near(double actual, double expected)
        {
            Require(Math.Abs(actual - expected) < 0.001, "coordinate mismatch: " + actual + " != " + expected);
        }

        /// <summary>驗證 condition，失敗時以 message 中止案例。</summary>
        /// <param name="condition">待驗證的條件。</param>
        /// <param name="message">失敗時的診斷訊息。</param>
        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        #endregion
    }
}
