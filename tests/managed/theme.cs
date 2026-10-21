using MonitorEdgeTunnelApp.Themes;
using MonitorEdgeTunnelApp.Editing;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Shell;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MonitorEdgeTunnelApp
{
    /// <summary>以真實 WPF 資源驗證配色更新、彈出層、UI 派送及監聽器生命週期。</summary>
    public static class ThemeTests
    {
        /// <summary>Windows 回傳的實體像素矩形。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            /// <summary>左側像素位置。</summary>
            public int Left;
            /// <summary>上側像素位置。</summary>
            public int Top;
            /// <summary>右側像素位置，不含端點。</summary>
            public int Right;
            /// <summary>下側像素位置，不含端點。</summary>
            public int Bottom;
        }

        /// <summary>目前顯示器與工作區的 Win32 資訊，只讀取環境，不變更顯示設定。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMonitorInfo
        {
            /// <summary>結構位元組大小。</summary>
            public int Size;
            /// <summary>整個螢幕的實體範圍。</summary>
            public NativeRect Monitor;
            /// <summary>排除工作列的實體工作區。</summary>
            public NativeRect Work;
            /// <summary>主螢幕等標記。</summary>
            public int Flags;
        }

        #region -- Public Methods --

        /// <summary>在 STA 建立替身系統偏好並驗證既有控制項；不修改 Windows 設定。</summary>
        /// <returns>全部檢查成功回傳 0，例外或綁定錯誤回傳 1。</returns>
        [STAThread]
        public static int Main()
        {
            Application application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = ThemeManager.ResourceUri("Controls.xaml") });
            StringWriter errors = new StringWriter();
            TextWriterTraceListener listener = new TextWriterTraceListener(errors);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            try
            {
                TestThemeLifecycle(application);
                TestDialogConfirmation();
                TestConfirmChoice(false);
                TestConfirmChoice(true);
                TestWindowFrame(application);
                TestCaptionDialogClose();
                listener.Flush();
                Require(errors.ToString().Length == 0, errors.ToString());
                Console.WriteLine("PASS themed controls and popups produce no binding errors");
                return 0;
            }
            catch (Exception error) { Console.WriteLine(error); return 1; }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                listener.Dispose();
            }
        }

        #endregion

        #region -- Private Methods --

        /// <summary>以同一批控制項往返切換配色並測試跨執行緒更新及 Dispose，失敗時拋出例外。</summary>
        /// <param name="application">測試專用的 UI 應用程式。</param>
        private static void TestThemeLifecycle(Application application)
        {
            AppTheme requested = AppTheme.Light;
            int uiThread = Thread.CurrentThread.ManagedThreadId;
            application.Resources["UnrelatedResource"] = "preserved";
            using (ThemeManager manager = new ThemeManager(application, () =>
            {
                Require(Thread.CurrentThread.ManagedThreadId == uiThread, "preference reader must run on UI thread");
                return requested;
            }))
            {
                TextBox input = new TextBox { Text = "尚未儲存的內容" };
                ComboBox combo = new ComboBox { ItemsSource = new[] { "螢幕 1", "螢幕 2" }, SelectedIndex = 1 };
                CheckBox check = new CheckBox { Content = "封鎖邊緣", IsChecked = true };
                Button button = new Button { Content = "儲存設定" };
                StackPanel panel = new StackPanel { Width = 220 };
                panel.Children.Add(input);
                panel.Children.Add(combo);
                panel.Children.Add(check);
                panel.Children.Add(button);
                Window window = new Window { Content = panel };
                try
                {
                    using (HwndSource host = new HwndSource(new HwndSourceParameters("Theme test")
                        { Width = 300, Height = 300, WindowStyle = unchecked((int)0x80000000) }))
                    {
                        host.RootVisual = panel;
                        panel.Measure(new Size(300, 300));
                        panel.Arrange(new Rect(0, 0, 300, 300));
                        Pump();
                        Color light = ColorOf(input.Background);
                        Require(ColorOf(input.Foreground) != light, "light text must contrast with background");
                        int dictionaries = application.Resources.MergedDictionaries.Count;
                        requested = AppTheme.Dark;
                        manager.Refresh();
                        Pump();
                        Color dark = ColorOf(input.Background);
                        Require(dark != light && ColorOf(input.Foreground) != dark, "existing input must update its dark palette");
                        Require(ColorOf(button.Background) == dark && ColorOf(combo.Background) == dark, "all existing editors must switch together");
                        Require(input.Text == "尚未儲存的內容" && combo.SelectedIndex == 1 && check.IsChecked == true, "switching themes must preserve edited values");
                        Console.WriteLine("PASS existing editors switch palettes without losing edits");

                        combo.IsDropDownOpen = true;
                        Pump();
                        Popup popup = (Popup)combo.Template.FindName("PART_Popup", combo);
                        Border popupSurface = (Border)popup.Child;
                        Require(popup.IsOpen && ColorOf(popupSurface.Background) == ColorOf((Brush)application.FindResource("ElevatedBrush")), "dark dropdown must use the app palette");
                        requested = AppTheme.Light;
                        manager.Refresh();
                        Pump();
                        Require(popup.IsOpen && ColorOf(popupSurface.Background) != dark && combo.SelectedIndex == 1, "an open dropdown must update without closing or changing selection");
                        combo.IsDropDownOpen = false;
                        Console.WriteLine("PASS open dropdown updates with the system palette");

                        requested = AppTheme.Dark;
                        Require(Task.Run(() => manager.Refresh()).Wait(2000), "background notification must not wait for UI");
                        Require(ColorOf(input.Background) == light, "background notification must not mutate UI resources directly");
                        Pump();
                        Require(ColorOf(input.Background) == dark, "queued UI refresh must apply the latest preference");
                        for (int i = 0; i < 6; ++i)
                        {
                            requested = i % 2 == 0 ? AppTheme.Light : AppTheme.Dark;
                            manager.Refresh();
                        }
                        Require(application.Resources.MergedDictionaries.Count == dictionaries, "theme switches must replace the palette without growing dictionaries");
                        Require((string)application.Resources["UnrelatedResource"] == "preserved", "theme switches must preserve unrelated resources");
                        Console.WriteLine("PASS background refresh stays on UI and repeated switches preserve resources");

                        requested = AppTheme.HighContrast;
                        manager.Refresh();
                        Pump();
                        Require(ColorOf(input.Background) == SystemColors.WindowColor && ColorOf(input.Foreground) == SystemColors.WindowTextColor,
                            "high contrast must use Windows colors");
                        Window dialog = ThemedMessageBox.CreateDialog("完整錯誤內容", "錯誤訊息", MessageBoxImage.Error);
                        try
                        {
                            dialog.ApplyTemplate();
                            Require(ColorOf(dialog.Background) == SystemColors.WindowColor, "error dialog must share high-contrast palette");
                            requested = AppTheme.Dark;
                            manager.Refresh();
                            Pump();
                            Require(ColorOf(dialog.Background) == ColorOf((Brush)application.FindResource("AppBackgroundBrush")), "existing error dialog must follow theme changes");
                        }
                        finally { dialog.Close(); }
                        Console.WriteLine("PASS high contrast and error dialogs use the shared palette");

                        requested = AppTheme.Light;
                        Task.Run(() => manager.Refresh()).Wait();
                        manager.Dispose();
                        Pump();
                        Require(ColorOf(input.Background) == dark, "Dispose must ignore already queued updates");
                        manager.Refresh();
                        Require(ColorOf(input.Background) == dark, "Dispose must release preference tracking");
                        Console.WriteLine("PASS disposing theme manager ignores queued and future refreshes");
                    }
                }
                finally { window.Close(); }
            }
        }

        /// <summary>在桌面外透明開啟真正的模態對話框並執行確認，驗證按鈕能解除模態等待。</summary>
        private static void TestDialogConfirmation()
        {
            Window dialog = ThemedMessageBox.CreateDialog("儲存失敗的完整說明", "錯誤訊息", MessageBoxImage.Error);
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = -32000;
            dialog.Top = -32000;
            dialog.Opacity = 0;
            dialog.ShowActivated = false;
            bool invoked = false;
            bool timedOut = false;
            DispatcherTimer timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timeout.Tick += (sender, e) => { timedOut = true; dialog.Close(); };
            dialog.Loaded += (sender, e) => dialog.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                Button confirm = ((Grid)dialog.Content).Children.OfType<Button>().Single();
                invoked = true;
                ((IInvokeProvider)new ButtonAutomationPeer(confirm).GetPattern(PatternInterface.Invoke)).Invoke();
            }));
            try
            {
                timeout.Start();
                dialog.ShowDialog();
                Require(invoked && !timedOut, "confirm button must close its modal dialog");
                Console.WriteLine("PASS confirming the themed error dialog releases modal wait");
            }
            finally
            {
                timeout.Stop();
                dialog.Close();
            }
        }

        /// <summary>以桌面外透明對話框驗證取消與明確確認，不讓 Enter 預設執行破壞性動作。</summary>
        /// <param name="accept">是否模擬按下執行動作，false 時按取消。</param>
        private static void TestConfirmChoice(bool accept)
        {
            Window dialog = ThemedMessageBox.CreateConfirmationDialog("將捨棄未儲存內容", "重新載入？", "捨棄並重新載入");
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = dialog.Top = -32000;
            dialog.Opacity = 0;
            dialog.ShowActivated = false;
            bool timedOut = false;
            DispatcherTimer timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timeout.Tick += (sender, e) => { timedOut = true; dialog.Close(); };
            dialog.Loaded += (sender, e) => dialog.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                StackPanel actions = ((Grid)dialog.Content).Children.OfType<StackPanel>().Single();
                Button cancel = actions.Children.OfType<Button>().Single(button => button.IsCancel);
                Require(cancel.IsDefault, "destructive confirmation must default to cancel");
                Button choice = accept ? actions.Children.OfType<Button>().Single(button => !button.IsCancel) : cancel;
                ((IInvokeProvider)new ButtonAutomationPeer(choice).GetPattern(PatternInterface.Invoke)).Invoke();
            }));
            try
            {
                timeout.Start();
                Require((dialog.ShowDialog() == true) == accept && !timedOut, "confirmation must reflect the explicit button chosen");
                Console.WriteLine("PASS destructive dialog " + (accept ? "accepts explicit action" : "defaults safely to cancel"));
            }
            finally { timeout.Stop(); dialog.Close(); }
        }

        /// <summary>以桌面外透明的真實主視窗驗證原生命中測試、標題列命令、工作區邊界及主題，不啟動 hook。</summary>
        /// <param name="application">STA 測試應用程式，僅更新其色盤。</param>
        private static void TestWindowFrame(Application application)
        {
            AppTheme requested = AppTheme.Light;
            using (ThemeManager manager = new ThemeManager(application, () => requested))
            {
                EditorWorkspace workspace = new EditorWorkspace();
                bool exitRequested = false;
                MainWindow window = new MainWindow(workspace, () => exitRequested = true)
                {
                    Opacity = 0, ShowActivated = false, ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000
                };
                window.forceForbidEdgeChecked = true;
                try
                {
                    window.Show();
                    Pump();
                    WindowChrome chrome = WindowChrome.GetWindowChrome(window);
                    FrameworkElement title = (FrameworkElement)window.Template.FindName("PART_TitleBar", window);
                    Border frame = (Border)window.Template.FindName("PART_WindowFrame", window);
                    Button maximize = (Button)window.Template.FindName("PART_CaptionMaximize", window);
                    Require(window.WindowStyle == WindowStyle.None && window.Icon == null && !Descendants<Image>(title).Any() &&
                        chrome.CaptionHeight == 40 && !chrome.UseAeroCaptionButtons, "custom title bar must replace the native frame and contain no app icon");
                    IntPtr handle = new WindowInteropHelper(window).Handle;
                    Point captionPoint = title.PointToScreen(new Point(100, 20));
                    Require(SendMessage(handle, 0x0084, IntPtr.Zero, PointParameter(captionPoint)).ToInt32() == 2,
                        "caption blank area must retain native drag, double-click and system-menu hit testing");
                    Point edge = window.PointToScreen(new Point(1, window.ActualHeight / 2));
                    Require(SendMessage(handle, 0x0084, IntPtr.Zero, PointParameter(edge)).ToInt32() == 10, "left edge must retain native resizing");
                    int bindings = window.CommandBindings.Count;
                    WindowFrame.SetIsEnabled(window, false);
                    Require(window.CommandBindings.Count == bindings - 4, "disabled chrome must release its own command bindings");
                    WindowFrame.SetIsEnabled(window, true);
                    WindowFrame.SetIsEnabled(window, true);
                    Require(window.CommandBindings.Count == bindings, "re-enabling chrome must not duplicate commands");
                    Color light = ColorOf(frame.Background);
                    requested = AppTheme.Dark;
                    manager.Refresh();
                    Pump();
                    Require(ColorOf(frame.Background) != light && ColorOf(frame.Background) == ColorOf(window.Background), "existing caption and content must switch to the same dark background");
                    requested = AppTheme.HighContrast;
                    manager.Refresh();
                    Pump();
                    Require(ColorOf(frame.Background) == SystemColors.WindowColor && ColorOf(maximize.Foreground) == SystemColors.WindowTextColor,
                        "caption background and glyphs must honor high contrast");
                    Invoke(maximize);
                    Require(window.WindowState == WindowState.Maximized && maximize.Command == SystemCommands.RestoreWindowCommand && Equals(maximize.ToolTip, "還原"),
                        "maximize must use a native command and expose restore afterward");
                    NativeMonitorInfo monitor = new NativeMonitorInfo { Size = Marshal.SizeOf(typeof(NativeMonitorInfo)) };
                    Require(GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor), "read current monitor work area");
                    Button close = (Button)window.Template.FindName("PART_CaptionClose", window);
                    Point top = close.PointToScreen(new Point());
                    Point bottom = ((FrameworkElement)window.Template.FindName("PART_WindowContent", window)).PointToScreen(
                        new Point(((FrameworkElement)window.Content).ActualWidth, ((FrameworkElement)window.Content).ActualHeight));
                    Require(top.X >= monitor.Work.Left - 1 && top.Y >= monitor.Work.Top - 1 &&
                        top.X + close.ActualWidth * PresentationSource.FromVisual(window).CompositionTarget.TransformToDevice.M11 <= monitor.Work.Right + 1 && bottom.Y <= monitor.Work.Bottom + 1,
                        "maximized caption controls and content must stay within the work area: top=" + top + ", bottom=" + bottom + ", work=" + monitor.Work.Left + "," + monitor.Work.Top + "," + monitor.Work.Right + "," + monitor.Work.Bottom);
                    Invoke(maximize);
                    Require(window.WindowState == WindowState.Normal && maximize.Command == SystemCommands.MaximizeWindowCommand, "restore must return the original resize state");
                    SendMessage(handle, 0x00A3, new IntPtr(2), PointParameter(title.PointToScreen(new Point(100, 20))));
                    Pump();
                    Require(window.WindowState == WindowState.Maximized, "native caption double-click must maximize");
                    SendMessage(handle, 0x00A3, new IntPtr(2), PointParameter(title.PointToScreen(new Point(100, 20))));
                    Pump();
                    Require(window.WindowState == WindowState.Normal, "native caption double-click must restore");
                    window.MaxWidth = 1000;
                    window.MaxHeight = 680;
                    Invoke(maximize);
                    Require(window.WindowState == WindowState.Maximized && window.ActualWidth <= 1000.1 && window.ActualHeight <= 680.1,
                        "custom work-area handling must preserve explicit WPF maximum sizes");
                    Invoke(maximize);
                    window.MaxWidth = window.MaxHeight = double.PositiveInfinity;
                    Invoke((Button)window.Template.FindName("PART_CaptionMinimize", window));
                    Require(window.WindowState == WindowState.Minimized, "minimize button must minimize the actual window");
                    SystemCommands.RestoreWindow(window);
                    Pump();
                    Require(window.WindowState == WindowState.Normal && window.HasUnsavedChanges, "restoring from minimized must retain the draft");
                    Invoke(close);
                    Require(!window.IsVisible && !exitRequested && workspace.HasUnsavedChanges,
                        "caption close must close only the window and retain the tray-owned draft");
                    Console.WriteLine("PASS icon-free caption supports native hit tests commands work-area bounds themes and retained drafts");
                }
                finally { window.Close(); }
            }
        }

        /// <summary>透過真正的自訂關閉按鈕取消模態對話框，驗證固定大小的對話框不提供縮放操作。</summary>
        private static void TestCaptionDialogClose()
        {
            Window dialog = ThemedMessageBox.CreateConfirmationDialog("保留目前設定", "確認操作", "繼續");
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = dialog.Top = -10000;
            dialog.Opacity = 0;
            dialog.ShowActivated = false;
            bool timedOut = false;
            DispatcherTimer timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timeout.Tick += (sender, e) => { timedOut = true; dialog.Close(); };
            dialog.Loaded += (sender, e) => dialog.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                Require(WindowChrome.GetWindowChrome(dialog).ResizeBorderThickness == new Thickness(0) && dialog.Icon == null &&
                    ((Button)dialog.Template.FindName("PART_CaptionMinimize", dialog)).Visibility == Visibility.Collapsed &&
                    ((Button)dialog.Template.FindName("PART_CaptionMaximize", dialog)).Visibility == Visibility.Collapsed &&
                    !SystemCommands.MaximizeWindowCommand.CanExecute(null, dialog), "fixed-size dialogs must only expose caption close");
                Invoke((Button)dialog.Template.FindName("PART_CaptionClose", dialog));
            }));
            try
            {
                timeout.Start();
                Require(dialog.ShowDialog() != true && !timedOut, "caption close must cancel the modal operation");
                Console.WriteLine("PASS shared dialog caption hides resize controls and close cancels the modal operation");
            }
            finally { timeout.Stop(); dialog.Close(); }
        }

        /// <summary>觸發真實按鈕的 UI Automation Invoke 並處理派送與視窗訊息，不模擬桌面滑鼠輸入。</summary>
        /// <param name="button">要執行的已呈現按鈕。</param>
        private static void Invoke(Button button)
        {
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            Pump();
        }

        /// <summary>以可表達的有號螢幕座標建立原生滑鼠訊息參數，不移動實際游標。</summary>
        /// <param name="point">實體像素位置，兩軸需在 16 位元有號整數範圍。</param>
        /// <returns>包含低位 X 與高位 Y 的 LPARAM。</returns>
        private static IntPtr PointParameter(Point point) => new IntPtr(unchecked((int)((uint)(ushort)(short)point.X | ((uint)(ushort)(short)point.Y << 16))));

        /// <summary>列舉實際視覺子項目，不進入其他視窗或變更 UI。</summary>
        /// <typeparam name="T">篩選的視覺型別。</typeparam>
        /// <param name="root">目前受測視覺。</param>
        /// <returns>符合型別的後代。</returns>
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (T descendant in Descendants<T>(child)) yield return descendant;
            }
        }

        /// <summary>傳送命中測試等同步訊息，不產生真實桌面輸入。</summary>
        /// <param name="handle">受測視窗 HWND。</param>
        /// <param name="message">Win32 訊息編號。</param>
        /// <param name="wParam">訊息參數。</param>
        /// <param name="lParam">訊息參數。</param>
        /// <returns>視窗訊息的結果。</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

        /// <summary>查詢受測視窗所在顯示器，不改變視窗位置。</summary>
        /// <param name="handle">受測視窗。</param>
        /// <param name="flags">2 代表找最近顯示器。</param>
        /// <returns>顯示器代碼。</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

        /// <summary>讀取實體螢幕工作區，供檢查最大化不遮住工作列。</summary>
        /// <param name="monitor">顯示器代碼。</param>
        /// <param name="info">傳入結構大小，成功時輸出範圍。</param>
        /// <returns>是否成功。</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref NativeMonitorInfo info);

        /// <summary>取得主題筆刷的實際色彩，非純色時中止案例。</summary>
        /// <param name="brush">控制項解析後的主題筆刷。</param>
        /// <returns>純色筆刷的 ARGB。</returns>
        private static Color ColorOf(Brush brush) => ((SolidColorBrush)brush).Color;

        /// <summary>完成目前 UI 工作及資料綁定，供測試觀察非同步配色更新。</summary>
        private static void Pump()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        /// <summary>驗證必要行為，不符合時使用 message 中止測試。</summary>
        /// <param name="condition">預期為 true 的檢查結果。</param>
        /// <param name="message">失敗時輸出的診斷。</param>
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        #endregion
    }
}
