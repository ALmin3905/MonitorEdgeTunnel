using MonitorEdgeTunnelApp.Themes;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
