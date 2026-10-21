using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
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

        /// <summary>供標題列樣板呈現原生非用戶區懸停，避免偽造 WPF 的 IsMouseOver。</summary>
        private static readonly DependencyPropertyKey IsCaptionHoveredPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
            "IsCaptionHovered", typeof(bool), typeof(WindowFrame), new PropertyMetadata(false));
        /// <summary>目前原生游標是否位於標題列按鈕。</summary>
        public static readonly DependencyProperty IsCaptionHoveredProperty = IsCaptionHoveredPropertyKey.DependencyProperty;
        /// <summary>供標題列樣板呈現原生按下且仍在按鈕內的狀態。</summary>
        private static readonly DependencyPropertyKey IsCaptionPressedPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
            "IsCaptionPressed", typeof(bool), typeof(WindowFrame), new PropertyMetadata(false));
        /// <summary>目前原生按下動作是否仍可於按鈕內放開。</summary>
        public static readonly DependencyProperty IsCaptionPressedProperty = IsCaptionPressedPropertyKey.DependencyProperty;
        /// <summary>每個視窗自行保留懸停按鈕，離開、停用或關閉時清除。</summary>
        private static readonly DependencyProperty HoveredButtonProperty = DependencyProperty.RegisterAttached(
            "HoveredButton", typeof(Button), typeof(WindowFrame), new PropertyMetadata(null));
        /// <summary>原生按下的按鈕；游標拖出時仍保留，放開或取消後清除。</summary>
        private static readonly DependencyProperty PressedButtonProperty = DependencyProperty.RegisterAttached(
            "PressedButton", typeof(Button), typeof(WindowFrame), new PropertyMetadata(null));

        /// <summary>要求 Windows 通知游標離開非用戶區，讓原生 Snap 選單接手後清除懸停色。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MouseTracking
        {
            /// <summary>結構位元組大小。</summary>
            public int Size;
            /// <summary>追蹤離開非用戶區的旗標。</summary>
            public uint Flags;
            /// <summary>要追蹤的視窗。</summary>
            public IntPtr Handle;
            /// <summary>系統懸停時間，本程式僅要求離開通知。</summary>
            public uint HoverTime;
        }

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

        /// <summary>在 UI 執行緒讀取原生懸停狀態，供樣板切換主題色。</summary>
        /// <param name="button">要讀取的標題列按鈕。</param>
        /// <returns>游標位於按鈕時為 true。</returns>
        public static bool GetIsCaptionHovered(DependencyObject button) => (bool)button.GetValue(IsCaptionHoveredProperty);

        /// <summary>在 UI 執行緒讀取原生按下狀態，不變更 WPF 的滑鼠擷取。</summary>
        /// <param name="button">要讀取的標題列按鈕。</param>
        /// <returns>按下且游標仍在按鈕內時為 true。</returns>
        public static bool GetIsCaptionPressed(DependencyObject button) => (bool)button.GetValue(IsCaptionPressedProperty);

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
            window.Loaded += WindowLoaded;
            window.Closed += WindowClosed;
            AttachSource(window);
        }

        /// <summary>在 HWND 建立後連接訊息處理，不改變視窗顯示狀態。</summary>
        /// <param name="sender">已建立原生來源的 Window。</param>
        /// <param name="e">來源初始化事件。</param>
        private static void SourceInitialized(object sender, EventArgs e) => AttachSource((Window)sender);

        /// <summary>樣板載入後將掛鉤放到 WindowChrome 之前，確保最大化命中結果不被 HTCLIENT 覆蓋；限 UI 執行緒。</summary>
        /// <param name="sender">已載入樣板的 Window。</param>
        /// <param name="e">載入事件，不變更版面。</param>
        private static void WindowLoaded(object sender, RoutedEventArgs e)
        {
            if (((Window)sender).GetValue(SourceProperty) is HwndSource source && !source.IsDisposed)
            {
                source.RemoveHook(MessageHook);
                source.AddHook(MessageHook);
            }
        }

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
            window.Loaded -= WindowLoaded;
            window.Closed -= WindowClosed;
            ResetInteraction(window, new WindowInteropHelper(window).Handle);
            if (window.GetValue(SourceProperty) is HwndSource source && !source.IsDisposed) source.RemoveHook(MessageHook);
            window.ClearValue(SourceProperty);
        }

        /// <summary>橋接原生最大化按鈕命中及滑鼠操作，並修正最大化工作區；限 UI 訊息執行緒。</summary>
        /// <param name="handle">收到訊息的視窗 HWND。</param>
        /// <param name="message">原生視窗訊息；其他標題列操作仍交給 WindowChrome。</param>
        /// <param name="wParam">命中區域、按鍵或視窗啟用狀態。</param>
        /// <param name="lParam">滑鼠座標或 MINMAXINFO 的輸入輸出位址。</param>
        /// <param name="handled">僅在接手最大化命中、按下、放開或工作區計算時設為 true。</param>
        /// <returns>最大化按鈕回傳 HTMAXBUTTON，其餘為零；懸停訊息仍交由系統提供 Snap 選單。</returns>
        private static IntPtr WindowMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            HwndSource source = HwndSource.FromHwnd(handle);
            if (!(source?.RootVisual is Window window) || source.CompositionTarget == null) return IntPtr.Zero;
            switch (message)
            {
                case 0x0084: // WM_NCHITTEST：實體座標含多螢幕的負值。
                    if (MaximizeButtonAt(window, MessagePoint(lParam)) != null)
                    {
                        handled = true;
                        return new IntPtr(9); // HTMAXBUTTON 讓 Windows 11 辨識 Snap 入口。
                    }
                    break;
                case 0x00A0: // WM_NCMOUSEMOVE：保留預設處理，讓系統控制 Snap 懸停計時。
                    SetHoveredButton(window, MaximizeButtonAt(window, MessagePoint(lParam)), handle);
                    break;
                case 0x02A2: // WM_NCMOUSELEAVE：包含游標移到系統 Snap 選單。
                    // 按下擷取後改送 WM_MOUSEMOVE，由其座標判斷拖出，避免擷取本身的離開通知清除按下色。
                    if (window.GetValue(PressedButtonProperty) == null) SetHoveredButton(window, null, handle);
                    break;
                case 0x00A1: // WM_NCLBUTTONDOWN
                case 0x00A3: // WM_NCLBUTTONDBLCLK：一般標題列雙擊仍交給 WindowChrome。
                    Button button = wParam.ToInt32() == 9 ? MaximizeButtonAt(window, MessagePoint(lParam)) : null;
                    if (button != null)
                    {
                        ResetInteraction(window, handle);
                        window.SetValue(PressedButtonProperty, button);
                        SetHoveredButton(window, button, handle);
                        SetCapture(handle);
                        if (GetCapture() != handle) ResetInteraction(window, handle);
                        handled = true;
                    }
                    break;
                case 0x0200: // WM_MOUSEMOVE：擷取後 Windows 改送用戶區座標。
                case 0x0202: // WM_LBUTTONUP
                case 0x00A2: // WM_NCLBUTTONUP
                    if (window.GetValue(PressedButtonProperty) is Button pressed)
                    {
                        Point messagePoint = MessagePoint(lParam);
                        NativePoint point = new NativePoint { X = (int)messagePoint.X, Y = (int)messagePoint.Y };
                        bool mapped = message == 0x00A2 || ClientToScreen(handle, ref point);
                        Button over = mapped ? MaximizeButtonAt(window, new Point(point.X, point.Y)) : null;
                        if (message == 0x0200) SetHoveredButton(window, over, handle);
                        else
                        {
                            ResetInteraction(window, handle);
                            if (over == pressed && pressed.Command is RoutedCommand command && command.CanExecute(pressed.CommandParameter, window))
                                command.Execute(pressed.CommandParameter, window);
                        }
                        handled = true;
                    }
                    else if (message == 0x0200) SetHoveredButton(window, null, handle);
                    break;
                case 0x001F: // WM_CANCELMODE
                case 0x0215: // WM_CAPTURECHANGED
                case 0x0005: // WM_SIZE：外部 Snap、最大化或還原會使按鈕位置改變。
                case 0x0112: // WM_SYSCOMMAND：先放開按鈕擷取，避免阻擋關閉或其他系統命令。
                    ResetInteraction(window, handle);
                    break;
                case 0x0006: // WM_ACTIVATE
                    if ((wParam.ToInt64() & 0xFFFF) == 0) ResetInteraction(window, handle);
                    break;
                case 0x0018: // WM_SHOWWINDOW
                    if (wParam == IntPtr.Zero) ResetInteraction(window, handle);
                    break;
                case 0x0100: // WM_KEYDOWN
                    if (wParam.ToInt32() == 27) ResetInteraction(window, handle);
                    break;
            }
            if (message != 0x0024 || lParam == IntPtr.Zero) return IntPtr.Zero;
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

        /// <summary>以有號座標解開滑鼠訊息，保留位於主螢幕左側或上方的顯示器位置。</summary>
        /// <param name="parameter">原生 LPARAM 座標。</param>
        /// <returns>兩軸均為有號 16 位元的實體像素位置。</returns>
        private static Point MessagePoint(IntPtr parameter) => new Point(unchecked((short)parameter.ToInt64()), unchecked((short)(parameter.ToInt64() >> 16)));

        /// <summary>在 UI 執行緒以按鈕目前的 DPI 與版面命中測試，不快取螢幕座標。</summary>
        /// <param name="window">已建立 HWND 的視窗。</param>
        /// <param name="screenPoint">實體螢幕座標，允許負值。</param>
        /// <returns>可用且命中的最大化或還原按鈕；固定大小、隱藏或停用時回傳 null。</returns>
        private static Button MaximizeButtonAt(Window window, Point screenPoint)
        {
            if (!window.IsEnabled || (window.ResizeMode != ResizeMode.CanResize && window.ResizeMode != ResizeMode.CanResizeWithGrip)) return null;
            Button button = window.Template?.FindName("PART_CaptionMaximize", window) as Button;
            if (button == null || !button.IsVisible || !button.IsEnabled || !button.IsHitTestVisible) return null;
            return new Rect(button.RenderSize).Contains(button.PointFromScreen(screenPoint)) ? button : null;
        }

        /// <summary>更新原生懸停及按下色，首次進入時要求離開通知；不攔截 Windows 的 Snap 懸停訊息。</summary>
        /// <param name="window">狀態所屬視窗，限 UI 執行緒。</param>
        /// <param name="button">目前命中的按鈕，null 表示離開。</param>
        /// <param name="handle">追蹤非用戶區滑鼠的 HWND。</param>
        private static void SetHoveredButton(Window window, Button button, IntPtr handle)
        {
            Button previous = window.GetValue(HoveredButtonProperty) as Button;
            if (previous != button)
            {
                if (previous != null)
                {
                    previous.SetValue(IsCaptionHoveredPropertyKey, false);
                    previous.SetValue(IsCaptionPressedPropertyKey, false);
                }
                window.SetValue(HoveredButtonProperty, button);
                if (button != null)
                {
                    button.SetValue(IsCaptionHoveredPropertyKey, true);
                    if (window.GetValue(PressedButtonProperty) == null)
                    {
                        MouseTracking tracking = new MouseTracking { Size = Marshal.SizeOf(typeof(MouseTracking)), Flags = 0x12, Handle = handle };
                        TrackMouseEvent(ref tracking);
                    }
                }
            }
            if (button != null) button.SetValue(IsCaptionPressedPropertyKey, window.GetValue(PressedButtonProperty) == button);
        }

        /// <summary>取消原生按鈕操作並還原配色；只釋放本次按下所持有的擷取，先清狀態以避免重入。</summary>
        /// <param name="window">狀態所屬視窗，限 UI 執行緒。</param>
        /// <param name="handle">操作的 HWND；不得釋放其他視窗或一般內容控制項的擷取。</param>
        private static void ResetInteraction(Window window, IntPtr handle)
        {
            bool wasPressed = window.GetValue(PressedButtonProperty) != null;
            window.ClearValue(PressedButtonProperty);
            SetHoveredButton(window, null, handle);
            if (wasPressed && handle != IntPtr.Zero && GetCapture() == handle) ReleaseCapture();
        }

        /// <summary>要求原生滑鼠離開通知，不阻擋系統提供 Snap 選單；限 HWND 所屬執行緒。</summary>
        /// <param name="tracking">輸入結構大小、旗標與 HWND。</param>
        /// <returns>是否登記成功。</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TrackMouseEvent(ref MouseTracking tracking);

        /// <summary>將擷取期間的用戶區座標轉為實體螢幕座標；限 HWND 所屬 UI 執行緒。</summary>
        /// <param name="handle">座標所屬 HWND。</param>
        /// <param name="point">輸入用戶區像素，成功時輸出螢幕像素。</param>
        /// <returns>轉換是否成功。</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr handle, ref NativePoint point);

        /// <summary>讓本次按下即使拖出按鈕仍能收到放開事件；限 HWND 所屬 UI 執行緒。</summary>
        /// <param name="handle">接收滑鼠的 HWND。</param>
        /// <returns>原先持有擷取的 HWND，可能為零。</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr SetCapture(IntPtr handle);

        /// <summary>查詢目前 UI 執行緒的滑鼠擷取，不改變輸入。</summary>
        /// <returns>持有擷取的 HWND，沒有擷取時為零。</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr GetCapture();

        /// <summary>放開已確認屬於本次原生按下的滑鼠擷取；限 UI 執行緒。</summary>
        /// <returns>是否成功放開。</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseCapture();

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
            ResetInteraction(window, new WindowInteropHelper(window).Handle);
            if (e.Command == SystemCommands.MinimizeWindowCommand) SystemCommands.MinimizeWindow(window);
            else if (e.Command == SystemCommands.MaximizeWindowCommand) SystemCommands.MaximizeWindow(window);
            else if (e.Command == SystemCommands.RestoreWindowCommand) SystemCommands.RestoreWindow(window);
            else if (e.Command == SystemCommands.CloseWindowCommand) SystemCommands.CloseWindow(window);
            e.Handled = true;
        }

        #endregion
    }
}
