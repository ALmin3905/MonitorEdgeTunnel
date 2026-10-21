using MonitorEdgeTunnelApp.Controls;
using MonitorEdgeTunnelApp.Themes;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;

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
            using (ThemeManager theme = new ThemeManager(app, () => AppTheme.Dark))
            {
                try
                {
                    TestOverlayLifecycle();
                    TestFailureCleanup();
                    TestEditorButton();
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
                Require(window.IdentifyMonitorsButton.IsEnabled && Equals(window.IdentifyMonitorsButton.Content, "辨識螢幕"), "editor must expose an enabled identify action");
                MonitorEdgeTunnel.Instance.Monitors.Clear();
                window.RefreshDisplayData();
                Pump();
                Require(!window.IdentifyMonitorsButton.IsEnabled, "no monitors must disable identification");
                Console.WriteLine("PASS identify button follows available monitor data");
            }
            finally { window.Close(); }
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

        #endregion
    }
}
