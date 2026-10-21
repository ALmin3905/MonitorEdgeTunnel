using MonitorEdgeTunnelApp.Controls;
using MonitorEdgeTunnelApp.Themes;
using MonitorEdgeTunnelApp.Editing;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace MonitorEdgeTunnelApp
{
    /// <summary>以桌面外透明的真實 HWND 驗證辨識視窗，不移動游標、不啟動原生通道服務。</summary>
    public static class IdentificationTests
    {
        /// <summary>GetWindowRect 輸出的實體像素範圍。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            /// <summary>左邊界。</summary>
            public int Left;
            /// <summary>上邊界。</summary>
            public int Top;
            /// <summary>不含尾端的右邊界。</summary>
            public int Right;
            /// <summary>不含尾端的下邊界。</summary>
            public int Bottom;
        }

        #region -- Public Methods --

        /// <summary>在 STA 執行原生覆蓋視窗與 UI 綁定案例，不更動系統顯示或桌面焦點。</summary>
        /// <returns>全部成功時為零，失敗時輸出原因並回傳一。</returns>
        [STAThread]
        public static int Main()
        {
            Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = ThemeManager.ResourceUri("Controls.xaml") });
            AppTheme requested = AppTheme.Dark;
            using (ThemeManager theme = new ThemeManager(app, () => requested))
            {
                try
                {
                    TestOverlayLifecycle();
                    TestFailureCleanup();
                    TestSystemMonitorDpi();
                    TestEditorButton();
                    TestPhysicalChannels(theme, value => requested = value);
                    TestEditorSynchronization();
                    string snapshot = Environment.GetEnvironmentVariable("MONITOREDGETUNNEL_IDENTIFICATION_SNAPSHOT");
                    if (!string.IsNullOrEmpty(snapshot)) CaptureOverlays(snapshot, theme, value => requested = value);
                    return 0;
                }
                catch (Exception error) { Console.WriteLine(error); return 1; }
            }
        }

        #endregion

        #region -- Private Methods --

        /// <summary>驗證原生邊界、穿透與焦點、重新開始、Esc、實際計時及關閉清理；只建立透明測試視窗。</summary>
        private static void TestOverlayLifecycle()
        {
            Window owner = OwnerWindow();
            MonitorInfo[] monitors = Monitors();
            MonitorIdentification identification = new MonitorIdentification(owner, monitor => new MonitorIdentificationWindow(monitor) { Opacity = 0 }, TimeSpan.FromMilliseconds(350));
            try
            {
                IntPtr foreground = GetForegroundWindow();
                IntPtr context = GetThreadDpiAwarenessContext();
                Require(identification.Show(monitors) && identification.Windows.Count == 2, "one real overlay per usable monitor");
                Pump();
                Require(GetForegroundWindow() == foreground && GetThreadDpiAwarenessContext() == context, "showing overlays must retain foreground window and caller DPI context");
                foreach (MonitorIdentificationWindow overlay in identification.Windows)
                {
                    IntPtr handle = new WindowInteropHelper(overlay).Handle;
                    NativeRect bounds = Bounds(handle);
                    MonitorInfo monitor = overlay.Monitor;
                    Require(bounds.Left == monitor.left && bounds.Top == monitor.top && bounds.Right == (long)monitor.right + 1 && bounds.Bottom == (long)monitor.bottom + 1,
                        "native rectangle must preserve physical inclusive bounds and negative origins");
                    int flags = GetWindowLong(handle, -20);
                    Require((flags & 0x080800A0) == 0x080800A0 && (flags & 0x00040000) == 0 && !overlay.ShowActivated && !overlay.ShowInTaskbar,
                        "layered overlay must be click-through, nonactivating and absent from taskbar");
                    Require(SendMessage(handle, 0x0084, IntPtr.Zero, IntPtr.Zero).ToInt32() == -1 && SendMessage(handle, 0x0021, IntPtr.Zero, IntPtr.Zero).ToInt32() == 3,
                        "native hit testing must reject mouse input and activation");
                    Require(WindowChrome.GetWindowChrome(overlay) == null && !WindowFrame.GetIsEnabled(overlay), "overlay must not inherit interactive caption chrome");
                    double dpi = PresentationSource.FromVisual(overlay).CompositionTarget.TransformToDevice.M11;
                    Require(Math.Abs(overlay.ActualWidth * dpi - (bounds.Right - bounds.Left)) <= 1,
                        "WPF rendering surface must fill the actual physical window at its current DPI");
                }
                IntPtr[] previous = identification.Windows.Select(window => new WindowInteropHelper(window).Handle).ToArray();
                identification.Show(monitors);
                Require(previous.All(handle => !IsWindow(handle)) && identification.Windows.Count == 2, "restarting must close old HWNDs instead of stacking overlays");
                KeyEventArgs escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(owner), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                owner.RaiseEvent(escape);
                Require(escape.Handled && !identification.IsVisible && identification.SecondsRemaining == 0, "Esc must end identification and reset its countdown");
                identification.Show(monitors);
                Require(WaitUntil(() => !identification.IsVisible, TimeSpan.FromSeconds(2)), "real Dispatcher timer must automatically close overlays");
                Require(identification.Windows.Count == 0 && identification.SecondsRemaining == 0, "timeout must release all overlays");
                identification.Show(monitors);
                previous = identification.Windows.Select(window => new WindowInteropHelper(window).Handle).ToArray();
                owner.Close();
                Require(!identification.IsVisible && previous.All(handle => !IsWindow(handle)) && !identification.Show(monitors), "closing owner must stop the timer and prevent reopening");
                Console.WriteLine("PASS physical rectangles negative origins nonactivation click-through DPI restoration restart Escape timeout and owner cleanup");
            }
            finally { identification.Stop(); owner.Close(); }
        }

        /// <summary>第二個視窗建立失敗時也清除前一個 HWND；無效邊界不建立視窗。</summary>
        private static void TestFailureCleanup()
        {
            Window owner = OwnerWindow();
            int created = 0;
            IntPtr first = IntPtr.Zero;
            MonitorIdentification identification = new MonitorIdentification(owner, monitor =>
            {
                if (++created == 2) throw new InvalidOperationException("expected factory failure");
                MonitorIdentificationWindow overlay = new MonitorIdentificationWindow(monitor) { Opacity = 0 };
                overlay.SourceInitialized += (sender, e) => first = new WindowInteropHelper(overlay).Handle;
                return overlay;
            });
            try
            {
                Require(!identification.Show(new[] { new MonitorInfo { id = 0, left = 1, right = 0 } }) && created == 0, "invalid geometry must not create HWNDs");
                bool failed = false;
                try { identification.Show(Monitors()); }
                catch (InvalidOperationException error) { failed = error.Message == "expected factory failure"; }
                Require(failed && !identification.IsVisible && first != IntPtr.Zero && !IsWindow(first), "partial creation failure must close all prior overlays");
                Console.WriteLine("PASS invalid monitor rejection and partial creation rollback");
            }
            finally { identification.Stop(); owner.Close(); }
        }

        /// <summary>驗證辨識入口跟隨螢幕資料啟用，不改動原生服務或草稿。</summary>
        private static void TestEditorButton()
        {
            MonitorEdgeTunnel.Instance.Monitors.Clear();
            MonitorEdgeTunnel.Instance.Monitors.AddRange(Monitors());
            MainWindow window = new MainWindow();
            try
            {
                Pump();
                Require(window.IdentifyMonitorsButton.IsEnabled && Equals(window.IdentifyMonitorsButton.Content, "辨識螢幕與通道"), "editor must expose an enabled identify action");
                MonitorEdgeTunnel.Instance.Monitors.Clear();
                window.RefreshDisplayData();
                Pump();
                Require(!window.IdentifyMonitorsButton.IsEnabled, "no monitors must disable identification");
                Console.WriteLine("PASS identify button follows available monitor data");
            }
            finally { window.Close(); }
        }

        /// <summary>驗證實體通道的含尾端投影、目的端、禁行、未完成輸入、密集標籤與即時配色。</summary>
        /// <param name="theme">目前測試色盤管理器。</param>
        /// <param name="requestTheme">變更替身系統色盤的動作。</param>
        private static void TestPhysicalChannels(ThemeManager theme, Action<AppTheme> requestTheme)
        {
            Window owner = OwnerWindow();
            MonitorInfo[] monitors = Monitors();
            TunnelInfo_Notify[] rules = ChannelRules(monitors);
            MonitorIdentification identification = new MonitorIdentification(owner, monitor => new MonitorIdentificationWindow(monitor) { Opacity = 0 });
            try
            {
                identification.Show(monitors);
                identification.UpdateRules(monitors, rules, true, rules[0], monitors[0].id);
                Pump();
                PhysicalTunnelView source = Descendants<PhysicalTunnelView>((DependencyObject)identification.Windows[0].Content).Single();
                PhysicalTunnelView target = Descendants<PhysicalTunnelView>((DependencyObject)identification.Windows[1].Content).Single();
                Line sourceLine = RuleLine(source, 0);
                Require(Math.Abs(sourceLine.Y1) < 0.001 && Math.Abs(sourceLine.Y2 - source.ActualHeight) < 0.001 && sourceLine.StrokeThickness == 7,
                    "full source must span the complete physical edge and highlight selection");
                Line targetLine = RuleLine(target, 1);
                Require(Math.Abs(targetLine.Y1 - target.ActualHeight * 100 / 1920) < 0.001 && Math.Abs(targetLine.Y2 - target.ActualHeight * 1720 / 1920) < 0.001 && targetLine.StrokeThickness == 7,
                    "relative target must retain inclusive fractions and highlight the remote destination");
                Line custom = RuleLine(source, 2);
                Require(Math.Abs(custom.X1 - source.ActualWidth * 100 / 1920) < 0.001 && Math.Abs(custom.X2 - source.ActualWidth * 200 / 1920) < 0.001 && custom.StrokeDashArray != null,
                    "custom forbidden range must include its last pixel and use dashes");
                Require(RuleLine(source, 3).StrokeDashArray != null && TextOf(source).Contains("設定異常") && TextOf(source).Contains("→ 螢幕 5 · 右側 · 通道 11"),
                    "invalid target must remain visibly invalid and valid target labels must resolve sparse IDs");
                Require(TextOf(source).Contains("距左緣 100–199 px"), "physical labels must use local edge offsets rather than negative desktop origins");
                Require(source.Children.OfType<Line>().Any(line => line.Tag is TunnelPreviewSegment gap && gap.RuleIndex == -1), "force-forbid gaps must appear on the actual edges");
                CheckMarkerBounds(source);
                requestTheme(AppTheme.Light);
                theme.Refresh();
                Pump();
                Require(((SolidColorBrush)sourceLine.Stroke).Color == ((SolidColorBrush)owner.FindResource("SuccessBrush")).Color && source.Children.Contains(sourceLine),
                    "existing physical strokes must follow the live light palette without recreating windows");
                requestTheme(AppTheme.HighContrast);
                theme.Refresh();
                Pump();
                Require(((SolidColorBrush)sourceLine.Stroke).Color == ((SolidColorBrush)owner.FindResource("SuccessBrush")).Color, "physical channels must also follow high contrast");
                requestTheme(AppTheme.Dark);
                theme.Refresh();
                rules[2].StartText = "-";
                identification.UpdateRules(monitors, rules, true, rules[0], monitors[0].id);
                Pump();
                Require(!source.Children.OfType<Line>().Any(line => line.Tag is TunnelPreviewSegment segment && segment.RuleIndex == 2) &&
                    TextOf((DependencyObject)identification.Windows[0].Content).Contains("無法定位"), "unfinished numeric input must remove stale geometry and explain missing markers");
                TunnelInfo_Notify[] dense = Enumerable.Range(0, 101).Select(id => new TunnelInfo_Notify { id = id, displayID = monitors[0].id, edgeType = EdgeType.Left,
                    rangeType = RangeType.Full, relativeID = -1 }).ToArray();
                MonitorInfo[] crowded = (MonitorInfo[])monitors.Clone();
                crowded[0].right = crowded[0].left + 639;
                crowded[0].bottom = crowded[0].top + 479;
                identification.Show(crowded);
                identification.UpdateRules(crowded, dense, false, dense[100], crowded[0].id);
                Pump();
                source = Descendants<PhysicalTunnelView>((DependencyObject)identification.Windows[0].Content).Single();
                Require(source.Children.OfType<Line>().Count(line => line.Tag is TunnelPreviewSegment segment && segment.RuleIndex >= 0) == 101 &&
                    TextOf((DependencyObject)identification.Windows[0].Content).Contains("標籤過密"), "dense rules must retain every edge line and report labels that cannot fit");
                CheckMarkerBounds(source);
                crowded[0].left++;
                identification.UpdateRules(crowded, dense, false, dense[100], crowded[0].id);
                Require(!identification.IsVisible, "topology mismatch must close stale physical markers");
                Console.WriteLine("PASS physical channel ranges sparse destinations states dense label layout themes invalid input and topology guards");
            }
            finally { identification.Stop(); owner.Close(); }
        }

        /// <summary>以完全透明的視窗驗證目前所有真實螢幕的原生邊界與 WPF DPI，不更動桌面內容或焦點。</summary>
        private static void TestSystemMonitorDpi()
        {
            List<MonitorInfo> monitors = new List<MonitorInfo>();
            IntPtr previous = SetThreadDpiAwarenessContext(new IntPtr(-3));
            try
            {
                MonitorEnumProc callback = (IntPtr monitor, IntPtr context, ref NativeRect bounds, IntPtr data) =>
                {
                    monitors.Add(new MonitorInfo { id = monitors.Count, left = bounds.Left, top = bounds.Top, right = bounds.Right - 1, bottom = bounds.Bottom - 1 });
                    return true;
                };
                Require(EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero), "enumerate actual monitor rectangles");
            }
            finally { SetThreadDpiAwarenessContext(previous); }
            Window owner = OwnerWindow();
            MonitorIdentification identification = new MonitorIdentification(owner, monitor => new MonitorIdentificationWindow(monitor) { Opacity = 0 });
            try
            {
                identification.Show(monitors);
                Pump();
                List<uint> dpis = new List<uint>();
                foreach (MonitorIdentificationWindow overlay in identification.Windows)
                {
                    IntPtr handle = new WindowInteropHelper(overlay).Handle;
                    NativeRect bounds = Bounds(handle);
                    uint dpi = GetDpiForWindow(handle);
                    dpis.Add(dpi);
                    Matrix transform = PresentationSource.FromVisual(overlay).CompositionTarget.TransformToDevice;
                    Require(bounds.Left == overlay.Monitor.left && bounds.Top == overlay.Monitor.top && bounds.Right == (long)overlay.Monitor.right + 1 && bounds.Bottom == (long)overlay.Monitor.bottom + 1 &&
                        Math.Abs(transform.M11 * 96 - dpi) < 0.001 && Math.Abs(overlay.ActualWidth * transform.M11 - (bounds.Right - bounds.Left)) <= 1 &&
                        Math.Abs(overlay.ActualHeight * transform.M22 - (bounds.Bottom - bounds.Top)) <= 1,
                        "actual monitor must retain physical coverage and matching WPF/native DPI: " + overlay.Monitor.id + ", DPI=" + dpi + ", transform=" + transform);
                }
                Console.WriteLine("PASS real monitor HWND coverage and WPF DPI: " + string.Join(", ", dpis));
            }
            finally { identification.Stop(); owner.Close(); }
        }

        /// <summary>透過真實主視窗按鈕驗證草稿及選取同步、重新整理與關閉，不寫入服務或設定檔。</summary>
        private static void TestEditorSynchronization()
        {
            MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
            MonitorInfo[] monitors = Monitors();
            service.Monitors.Clear();
            service.Monitors.AddRange(monitors);
            service.Tunnels.Clear();
            service.Tunnels.AddRange(ChannelRules(monitors).Select(TunnelInfo_Notify.Cast));
            MonitorIdentification identification = null;
            MainWindow window = new MainWindow(new EditorWorkspace(), createIdentification: owner =>
                identification = new MonitorIdentification(owner, monitor => new MonitorIdentificationWindow(monitor) { Opacity = 0 }))
            {
                Opacity = 0, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -12000, Top = -12000
            };
            int applies = service.ApplyCalls, saves = service.SaveCalls, starts = service.StartCalls, loads = service.LoadCalls;
            try
            {
                window.Show();
                Pump();
                Invoke(window.IdentifyMonitorsButton);
                Require(identification.IsVisible && window.IdentifyMonitorsCaption.StartsWith("結束辨識"), "real button must show overlays and expose a stop action");
                PhysicalTunnelView source = Descendants<PhysicalTunnelView>((DependencyObject)identification.Windows[0].Content).Single();
                IntPtr handle = new WindowInteropHelper(identification.Windows[0]).Handle;
                window.selectedTunnel = window.tunnelInfos[2];
                window.tunnelInfos[2].from = 240;
                Pump();
                Require(Math.Abs(RuleLine(source, 2).X1 - source.ActualWidth * 240 / 1920) < 0.001 && RuleLine(source, 2).StrokeThickness == 7 &&
                    new WindowInteropHelper(identification.Windows[0]).Handle == handle, "editing and selection must update the current overlay without replacing its HWND");
                window.tunnelInfos[2].StartText = "-";
                Pump();
                Require(!source.Children.OfType<Line>().Any(line => line.Tag is TunnelPreviewSegment segment && segment.RuleIndex == 2), "real unfinished editor text must immediately remove its stale physical range");
                Require(service.ApplyCalls == applies && service.SaveCalls == saves && service.StartCalls == starts && service.LoadCalls == loads,
                    "identification and live preview must never apply save load or start the native tunnel service");
                Invoke(window.IdentifyMonitorsButton);
                Require(!identification.IsVisible && window.IdentifyMonitorsCaption == "辨識螢幕與通道", "second click must end identification");
                Invoke(window.IdentifyMonitorsButton);
                service.Monitors.RemoveAt(1);
                window.RefreshDisplayData();
                Pump();
                Require(!identification.IsVisible && window.monitorInfos.Count == 1, "monitor refresh must clear all physical overlays");
                Invoke(window.IdentifyMonitorsButton);
                Require(identification.IsVisible, "identification can restart after a configuration change");
                window.Close();
                Require(!identification.IsVisible, "editor close must clear its overlays while leaving the draft workspace intact");
                Console.WriteLine("PASS real identify button stop action live draft selection invalid text no native writes refresh and close");
            }
            finally { window.Close(); }
        }

        /// <summary>以示意桌面背景輸出真正的 WPF 覆蓋層，所有原生視窗仍完全透明且位於桌面外。</summary>
        /// <param name="path">第一個螢幕的 PNG 絕對路徑，另產生 monitor2 與 light 版本。</param>
        /// <param name="theme">實際色盤管理器。</param>
        /// <param name="requestTheme">切換色盤的動作。</param>
        private static void CaptureOverlays(string path, ThemeManager theme, Action<AppTheme> requestTheme)
        {
            Window owner = OwnerWindow();
            MonitorInfo[] monitors = { new MonitorInfo { id = 0, left = -8000, right = -6721, top = -5000, bottom = -4281, scaling = 1.75 },
                new MonitorInfo { id = 1, left = -10000, right = -8721, top = -5000, bottom = -4281, scaling = 1 } };
            MonitorIdentification identification = new MonitorIdentification(owner, monitor => new MonitorIdentificationWindow(monitor) { Opacity = 0 });
            try
            {
                requestTheme(AppTheme.Dark);
                theme.Refresh();
                TunnelInfo_Notify[] rules = ChannelRules(monitors);
                identification.Show(monitors);
                identification.UpdateRules(monitors, rules, true, rules[0], 0);
                Pump();
                string prefix = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path));
                RenderOverlay(identification.Windows[0], path);
                RenderOverlay(identification.Windows[1], prefix + ".monitor2.png");
                requestTheme(AppTheme.Light);
                theme.Refresh();
                Pump();
                RenderOverlay(identification.Windows[0], prefix + ".light.png");
                Console.WriteLine("PASS identification snapshots " + path);
            }
            finally { identification.Stop(); owner.Close(); }
        }

        /// <summary>加入僅供截圖辨識的桌面背景，輸出既有覆蓋層內容而不捕捉使用者桌面。</summary>
        /// <param name="window">已呈現但透明的測試覆蓋視窗。</param>
        /// <param name="path">輸出 PNG 路徑。</param>
        private static void RenderOverlay(MonitorIdentificationWindow window, string path)
        {
            Grid root = (Grid)window.Content;
            root.Background = new LinearGradientBrush(Color.FromRgb(25, 40, 60), Color.FromRgb(53, 72, 86), 30);
            root.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            using (FileStream output = File.Create(path)) encoder.Save(output);
        }

        /// <summary>建立雙向、局部禁行及無效目標的規則，不把顯示編號寫回原始 ID。</summary>
        /// <param name="monitors">兩個來源螢幕。</param>
        /// <returns>依穩定順序排列的編輯規則。</returns>
        private static TunnelInfo_Notify[] ChannelRules(MonitorInfo[] monitors) => new[] {
            new TunnelInfo_Notify { id = 7, displayID = monitors[0].id, edgeType = EdgeType.Left, rangeType = RangeType.Full, relativeID = 11 },
            new TunnelInfo_Notify { id = 11, displayID = monitors[1].id, edgeType = EdgeType.Right, rangeType = RangeType.Relative, from = 100, to = 200, relativeID = 7 },
            new TunnelInfo_Notify { id = 23, displayID = monitors[0].id, edgeType = EdgeType.Top, rangeType = RangeType.Customize, from = 100, to = 99, relativeID = -1 },
            new TunnelInfo_Notify { id = 40, displayID = monitors[0].id, edgeType = EdgeType.Bottom, rangeType = RangeType.Full, relativeID = 99 }
        };

        /// <summary>取得保留區段資料的通道主線，不把裝飾外框或連接線誤當成範圍。</summary>
        /// <param name="view">實體通道表面。</param>
        /// <param name="index">來源規則索引。</param>
        /// <returns>該規則唯一的主線。</returns>
        private static Line RuleLine(PhysicalTunnelView view, int index) => view.Children.OfType<Line>().Single(line => line.Tag is TunnelPreviewSegment segment && segment.RuleIndex == index);

        /// <summary>確認標籤完整留在螢幕內且互不重疊，包含密集規則情境。</summary>
        /// <param name="view">已完成 UI 配置的通道表面。</param>
        private static void CheckMarkerBounds(PhysicalTunnelView view)
        {
            List<Rect> occupied = new List<Rect>();
            foreach (Border marker in view.Children.OfType<Border>())
            {
                Rect bounds = new Rect(Canvas.GetLeft(marker), Canvas.GetTop(marker), marker.ActualWidth, marker.ActualHeight);
                Require(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= view.ActualWidth && bounds.Bottom <= view.ActualHeight &&
                    !occupied.Any(previous => previous.IntersectsWith(bounds)), "physical marker labels must remain contained and nonoverlapping");
                occupied.Add(bounds);
            }
        }

        /// <summary>讀取實際呈現的文字，避免只驗證資料模型而漏掉標籤內容。</summary>
        /// <param name="root">受測的 WPF 內容。</param>
        /// <returns>後代 TextBlock 的串接內容。</returns>
        private static string TextOf(DependencyObject root) => string.Join("\n", Descendants<TextBlock>(root).Select(block => block.Text));

        /// <summary>列舉視覺子樹，不進入其他 HWND。</summary>
        /// <typeparam name="T">篩選型別。</typeparam>
        /// <param name="root">視覺根節點。</param>
        /// <returns>符合型別的後代元素。</returns>
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (T descendant in Descendants<T>(child)) yield return descendant;
            }
        }

        /// <summary>以 UI Automation 觸發真實按鈕並完成 UI 派送，不產生桌面滑鼠輸入。</summary>
        /// <param name="button">要觸發的按鈕。</param>
        private static void Invoke(Button button)
        {
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            Pump();
        }

        /// <summary>建立不啟用、桌面外且完全透明的擁有者，不更動其他應用程式焦點。</summary>
        /// <returns>已顯示的測試視窗，由呼叫端關閉。</returns>
        private static Window OwnerWindow()
        {
            Window owner = new Window { Style = null, Opacity = 0, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -12000, Top = -12000, Width = 200, Height = 100 };
            owner.Show();
            Pump();
            return owner;
        }

        /// <summary>產生有負座標、直向尺寸與非連續 ID 的獨立螢幕快照，不對應桌面上的可見區域。</summary>
        /// <returns>兩個供實體邊界測試使用的螢幕。</returns>
        private static MonitorInfo[] Monitors() => new[] {
            new MonitorInfo { id = 0, left = -8000, right = -6081, top = -4000, bottom = -2921, scaling = 1.75 },
            new MonitorInfo { id = 4, left = -10000, right = -8921, top = -4000, bottom = -2081, scaling = 1 }
        };

        /// <summary>在每螢幕 DPI 環境讀取 HWND 的實體範圍，finally 還原測試 UI 執行緒。</summary>
        /// <param name="handle">受測 HWND。</param>
        /// <returns>GetWindowRect 的實體像素矩形。</returns>
        private static NativeRect Bounds(IntPtr handle)
        {
            IntPtr previous = SetThreadDpiAwarenessContext(new IntPtr(-3));
            try { Require(GetWindowRect(handle, out NativeRect rectangle), "read overlay rectangle"); return rectangle; }
            finally { SetThreadDpiAwarenessContext(previous); }
        }

        /// <summary>派送 UI 工作直到條件成功或逾時，不送出真實鍵盤滑鼠輸入。</summary>
        /// <param name="condition">停止等待的條件。</param>
        /// <param name="timeout">最長等待時間。</param>
        /// <returns>條件是否在期限內成立。</returns>
        private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            while (!condition() && elapsed.Elapsed < timeout) { Pump(); Thread.Sleep(10); }
            return condition();
        }

        /// <summary>完成既有 Dispatcher 工作，不啟動原生通道或全域 hook。</summary>
        private static void Pump()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        /// <summary>條件不成立時以對應原因中止案例。</summary>
        /// <param name="condition">預期為 true 的結果。</param>
        /// <param name="message">失敗原因。</param>
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        /// <summary>讀取 HWND 範圍。</summary>
        /// <param name="handle">受測視窗。</param>
        /// <param name="rectangle">輸出不含尾端的邊界。</param>
        /// <returns>是否成功。</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr handle, out NativeRect rectangle);
        /// <summary>讀取視窗樣式。</summary>
        /// <param name="handle">受測視窗。</param>
        /// <param name="index">樣式欄位。</param>
        /// <returns>32 位元旗標。</returns>
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr handle, int index);
        /// <summary>查詢視窗是否仍存在。</summary>
        /// <param name="handle">先前建立的 HWND。</param>
        /// <returns>仍存在時為 true。</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr handle);
        /// <summary>查詢桌面目前前景視窗，不更動焦點。</summary>
        /// <returns>前景 HWND。</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        /// <summary>查詢測試 UI 執行緒的 DPI 環境。</summary>
        /// <returns>目前 DPI 感知環境。</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr GetThreadDpiAwarenessContext();
        /// <summary>暫時切換測試 UI 執行緒的 DPI 環境。</summary>
        /// <param name="context">要套用的環境。</param>
        /// <returns>供 finally 還原的原先環境。</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        /// <summary>同步查詢命中及啟用行為，不送出桌面輸入。</summary>
        /// <param name="handle">受測 HWND。</param>
        /// <param name="message">Win32 訊息。</param>
        /// <param name="wParam">訊息參數。</param>
        /// <param name="lParam">訊息參數。</param>
        /// <returns>訊息結果。</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

        /// <summary>在暫時的每螢幕 DPI 環境收集實體顯示器邊界，不改動顯示設定。</summary>
        /// <param name="monitor">顯示器代碼。</param>
        /// <param name="context">未使用的繪圖環境。</param>
        /// <param name="bounds">目前顯示器的實體矩形。</param>
        /// <param name="data">未使用的呼叫端資料。</param>
        /// <returns>true 繼續列舉。</returns>
        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr context, ref NativeRect bounds, IntPtr data);

        /// <summary>唯讀列舉目前實體螢幕，用於實際 DPI 覆蓋檢查。</summary>
        /// <param name="context">零表示整個桌面。</param>
        /// <param name="clip">零表示不限制矩形。</param>
        /// <param name="callback">同步接收邊界的代理。</param>
        /// <param name="data">傳給代理的資料。</param>
        /// <returns>是否成功。</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(IntPtr context, IntPtr clip, MonitorEnumProc callback, IntPtr data);

        /// <summary>讀取 HWND 目前所在螢幕的有效 DPI。</summary>
        /// <param name="handle">每螢幕 DPI 感知的覆蓋 HWND。</param>
        /// <returns>有效 DPI。</returns>
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr handle);

        #endregion
    }
}
