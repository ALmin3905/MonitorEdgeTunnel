using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace MonitorEdgeTunnelApp.Themes
{
    /// <summary>連接自訂標題列與 Windows 視窗命令；所有操作限視窗的 UI 執行緒。</summary>
    public static class WindowFrame
    {
        /// <summary>啟用共用標題列的縮小、最大化、還原及關閉命令，不改動應用程式生命週期。</summary>
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(WindowFrame), new PropertyMetadata(false, IsEnabledChanged));
        /// <summary>視窗自行持有的命令，停用時完整移除，不保留靜態視窗參考。</summary>
        private static readonly DependencyProperty BindingsProperty = DependencyProperty.RegisterAttached(
            "Bindings", typeof(CommandBinding[]), typeof(WindowFrame), new PropertyMetadata(null));

        /// <summary>借用視窗自己的訊息來源，解除掛鉤時不負責釋放 HWND。</summary>
        private static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
            "Source", typeof(HwndSource), typeof(WindowFrame), new PropertyMetadata(null));
        /// <summary>保留訊息代理，避免 HwndSource 的弱參考在運行期間失效。</summary>
        private static readonly HwndSourceHook MessageHook = WindowMessage;

        /// <summary>Win32 實體像素位置或大小。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            /// <summary>水平位置或寬度。</summary>
            public int X;
            /// <summary>垂直位置或高度。</summary>
            public int Y;
        }

        /// <summary>Win32 實體像素矩形。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            /// <summary>左側位置。</summary>
            public int Left;
            /// <summary>上側位置。</summary>
            public int Top;
            /// <summary>不含端點的右側位置。</summary>
            public int Right;
            /// <summary>不含端點的下側位置。</summary>
            public int Bottom;
        }

        /// <summary>WM_GETMINMAXINFO 的原生資料，保留拖曳限制並修正最大化位置與大小。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            /// <summary>系統保留值。</summary>
            public NativePoint Reserved;
            /// <summary>最大化的實體像素大小。</summary>
            public NativePoint MaxSize;
            /// <summary>相對目前顯示器左上角的最大化位置。</summary>
            public NativePoint MaxPosition;
            /// <summary>允許拖曳的最小大小。</summary>
            public NativePoint MinTrackSize;
            /// <summary>允許拖曳的最大大小。</summary>
            public NativePoint MaxTrackSize;
        }

        /// <summary>所在顯示器的範圍及排除工作列後的工作區。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            /// <summary>結構位元組大小。</summary>
            public int Size;
            /// <summary>顯示器的實體範圍。</summary>
            public NativeRect Monitor;
            /// <summary>不遮住工作列的實體範圍。</summary>
            public NativeRect Work;
            /// <summary>主顯示器等旗標。</summary>
            public int Flags;
        }

        #region -- Public Methods --

        /// <summary>讀取指定視窗是否已啟用自訂標題列命令；限 UI 執行緒。</summary>
        /// <param name="window">要查詢的視窗。</param>
        /// <returns>已啟用時為 true。</returns>
        public static bool GetIsEnabled(DependencyObject window) => (bool)window.GetValue(IsEnabledProperty);

        /// <summary>在 UI 執行緒啟用或移除視窗命令，不顯示或關閉視窗。</summary>
        /// <param name="window">目標 Window。</param>
        /// <param name="value">true 啟用，false 解除命令。</param>
        public static void SetIsEnabled(DependencyObject window, bool value) => window.SetValue(IsEnabledProperty, value);

        #endregion

        #region -- Private Methods --

        /// <summary>在 UI 執行緒維護每個視窗自己的命令，重複啟用不累加綁定。</summary>
        /// <param name="sender">套用附加屬性的 Window；其他型別不處理。</param>
        /// <param name="e">新的啟用值。</param>
        private static void IsEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(sender is Window window)) return;
            DetachSource(window);
            if (window.GetValue(BindingsProperty) is CommandBinding[] previous)
                foreach (CommandBinding binding in previous) window.CommandBindings.Remove(binding);
            window.ClearValue(BindingsProperty);
            if (!(bool)e.NewValue) return;
            CommandBinding[] bindings = {
                new CommandBinding(SystemCommands.MinimizeWindowCommand, Execute, CanExecute),
                new CommandBinding(SystemCommands.MaximizeWindowCommand, Execute, CanExecute),
                new CommandBinding(SystemCommands.RestoreWindowCommand, Execute, CanExecute),
                new CommandBinding(SystemCommands.CloseWindowCommand, Execute, CanExecute)
            };
            foreach (CommandBinding binding in bindings) window.CommandBindings.Add(binding);
            window.SetValue(BindingsProperty, bindings);
            window.SourceInitialized += SourceInitialized;
            window.Closed += WindowClosed;
            AttachSource(window);
        }

        /// <summary>在 HWND 建立後連接訊息處理，不改變視窗顯示狀態。</summary>
        /// <param name="sender">已建立原生來源的 Window。</param>
        /// <param name="e">來源初始化事件。</param>
        private static void SourceInitialized(object sender, EventArgs e) => AttachSource((Window)sender);

        /// <summary>關閉後移除借用來源的訊息掛鉤，不影響通知區應用程式。</summary>
        /// <param name="sender">已關閉的 Window。</param>
        /// <param name="e">關閉事件。</param>
        private static void WindowClosed(object sender, EventArgs e) => DetachSource((Window)sender);

        /// <summary>掛接既有 HWND，未顯示或已掛接的視窗不重複處理；限 UI 執行緒。</summary>
        /// <param name="window">目標視窗，不主動為其建立 HWND。</param>
        private static void AttachSource(Window window)
        {
            if (window.GetValue(SourceProperty) != null) return;
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            HwndSource source = HwndSource.FromHwnd(handle);
            if (source == null || source.IsDisposed) return;
            source.AddHook(MessageHook);
            window.SetValue(SourceProperty, source);
        }

        /// <summary>解除目前視窗的事件與訊息掛鉤，不 Dispose 視窗擁有的來源；限 UI 執行緒。</summary>
        /// <param name="window">停用標題列或已關閉的視窗。</param>
        private static void DetachSource(Window window)
        {
            window.SourceInitialized -= SourceInitialized;
            window.Closed -= WindowClosed;
            if (window.GetValue(SourceProperty) is HwndSource source && !source.IsDisposed) source.RemoveHook(MessageHook);
            window.ClearValue(SourceProperty);
        }

        /// <summary>依 HWND 所在顯示器修正最大化工作區，保留 WPF 最小及最大尺寸；限 UI 訊息執行緒。</summary>
        /// <param name="handle">收到訊息的視窗 HWND。</param>
        /// <param name="message">只處理 WM_GETMINMAXINFO，其餘交給 WindowChrome。</param>
        /// <param name="wParam">未使用。</param>
        /// <param name="lParam">MINMAXINFO 的輸入輸出位址。</param>
        /// <param name="handled">工作區取得成功並寫回時設為 true。</param>
        /// <returns>訊息結果固定為零；失敗時維持未處理狀態。</returns>
        private static IntPtr WindowMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != 0x0024 || lParam == IntPtr.Zero) return IntPtr.Zero;
            HwndSource source = HwndSource.FromHwnd(handle);
            if (!(source?.RootVisual is Window window) || source.CompositionTarget == null) return IntPtr.Zero;
            MonitorInfo monitor = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor)) return IntPtr.Zero;
            MinMaxInfo limits = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));
            double dpiX = source.CompositionTarget.TransformToDevice.M11;
            double dpiY = source.CompositionTarget.TransformToDevice.M22;
            limits.MinTrackSize.X = Math.Max(limits.MinTrackSize.X, (int)Math.Ceiling(window.MinWidth * dpiX));
            limits.MinTrackSize.Y = Math.Max(limits.MinTrackSize.Y, (int)Math.Ceiling(window.MinHeight * dpiY));
            if (!double.IsPositiveInfinity(window.MaxWidth)) limits.MaxTrackSize.X = (int)Math.Floor(window.MaxWidth * dpiX);
            if (!double.IsPositiveInfinity(window.MaxHeight)) limits.MaxTrackSize.Y = (int)Math.Floor(window.MaxHeight * dpiY);
            limits.MaxPosition = new NativePoint { X = monitor.Work.Left - monitor.Monitor.Left, Y = monitor.Work.Top - monitor.Monitor.Top };
            limits.MaxSize = new NativePoint { X = monitor.Work.Right - monitor.Work.Left, Y = monitor.Work.Bottom - monitor.Work.Top };
            if (limits.MaxTrackSize.X > 0) limits.MaxSize.X = Math.Min(limits.MaxSize.X, limits.MaxTrackSize.X);
            if (limits.MaxTrackSize.Y > 0) limits.MaxSize.Y = Math.Min(limits.MaxSize.Y, limits.MaxTrackSize.Y);
            Marshal.StructureToPtr(limits, lParam, false);
            handled = true;
            return IntPtr.Zero;
        }

        /// <summary>查詢視窗最近的顯示器，不使用固定的主螢幕工作區。</summary>
        /// <param name="handle">目標 HWND。</param>
        /// <param name="flags">2 表示使用最近的顯示器。</param>
        /// <returns>顯示器代碼。</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

        /// <summary>讀取指定顯示器的實體範圍，失敗時不覆寫系統尺寸。</summary>
        /// <param name="monitor">顯示器代碼。</param>
        /// <param name="info">傳入結構大小，成功時輸出範圍。</param>
        /// <returns>是否讀取成功，Win32 BOOL 以四位元組解讀。</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        /// <summary>依視窗狀態及 ResizeMode 限制命令，對話框只允許關閉，不更動內容。</summary>
        /// <param name="sender">命令所屬的 Window。</param>
        /// <param name="e">輸入命令，輸出 CanExecute 並標示事件已處理。</param>
        private static void CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            Window window = (Window)sender;
            bool resizable = window.ResizeMode == ResizeMode.CanResize || window.ResizeMode == ResizeMode.CanResizeWithGrip;
            e.CanExecute = e.Command == SystemCommands.CloseWindowCommand ||
                e.Command == SystemCommands.MinimizeWindowCommand && window.ResizeMode != ResizeMode.NoResize ||
                e.Command == SystemCommands.MaximizeWindowCommand && resizable && window.WindowState != WindowState.Maximized ||
                e.Command == SystemCommands.RestoreWindowCommand && window.WindowState != WindowState.Normal;
            e.Handled = true;
        }

        /// <summary>在 UI 執行緒派送原生視窗命令；關閉走原有 Closing/Closed 流程，保留通知區與對話框語意。</summary>
        /// <param name="sender">命令所屬的 Window。</param>
        /// <param name="e">要執行的視窗命令，完成後標示已處理。</param>
        private static void Execute(object sender, ExecutedRoutedEventArgs e)
        {
            Window window = (Window)sender;
            if (e.Command == SystemCommands.MinimizeWindowCommand) SystemCommands.MinimizeWindow(window);
            else if (e.Command == SystemCommands.MaximizeWindowCommand) SystemCommands.MaximizeWindow(window);
            else if (e.Command == SystemCommands.RestoreWindowCommand) SystemCommands.RestoreWindow(window);
            else if (e.Command == SystemCommands.CloseWindowCommand) SystemCommands.CloseWindow(window);
            e.Handled = true;
        }

        #endregion
    }
}
