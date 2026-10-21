using System;
using System.ComponentModel;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>單一實體螢幕的透明辨識覆蓋層，不啟用視窗、不接收桌面滑鼠；限 UI 執行緒。</summary>
    internal sealed class MonitorIdentificationWindow : Window
    {
        /// <summary>與預覽共用 ID 及含尾端邊界的實體螢幕資料。</summary>
        internal MonitorInfo Monitor { get; }
        /// <summary>辨識卡倒數文字，不因每秒更新而重建視窗。</summary>
        private readonly TextBlock _countdown;
        /// <summary>保留原生訊息代理，直到視窗關閉時解除。</summary>
        private readonly HwndSourceHook _messageHook;
        /// <summary>借用視窗擁有的原生來源，不由本類別 Dispose。</summary>
        private HwndSource _source;
        /// <summary>實際螢幕邊緣上的通道繪製表面。</summary>
        private readonly PhysicalTunnelView _tunnels;
        /// <summary>中央卡片的規則與無法定位提示。</summary>
        private readonly TextBlock _ruleSummary;
        /// <summary>目前選取的螢幕使用較清楚的卡片外框。</summary>
        private readonly Border _badge;
        /// <summary>最近一次規則數量摘要，標籤過密時再補充省略說明。</summary>
        private string _summary = string.Empty;

        #region -- Public Methods --

        /// <summary>建立跟隨系統主題的辨識卡，不顯示視窗或更動顯示設定；限 UI 執行緒。</summary>
        /// <param name="monitor">有效的實體邊界及一起算顯示編號的來源 ID。</param>
        internal MonitorIdentificationWindow(MonitorInfo monitor)
        {
            Monitor = monitor;
            Style = null;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            ShowActivated = false;
            ShowInTaskbar = false;
            Focusable = false;
            Topmost = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = Top = -10000;
            Width = Height = 1;
            FontFamily = new FontFamily("Segoe UI, Microsoft JhengHei UI");
            _messageHook = WindowMessage;
            SourceInitialized += InitializeSource;
            Closed += WindowClosed;
            StackPanel details = new StackPanel();
            details.Children.Add(Label("螢幕辨識", 12, "MutedTextBrush"));
            details.Children.Add(Label(((long)monitor.id + 1).ToString(CultureInfo.CurrentCulture), 64, "TextBrush"));
            TextBlock description = Label(new MonitorDescriptionConverter().Convert(monitor, typeof(string), null, CultureInfo.CurrentCulture).ToString(), 12, "MutedTextBrush");
            description.TextWrapping = TextWrapping.Wrap;
            details.Children.Add(description);
            TextBlock preview = Label("通道預覽 · 目前編輯內容", 12, "TextBrush");
            preview.Margin = new Thickness(0, 12, 0, 4);
            details.Children.Add(preview);
            _ruleSummary = Label(string.Empty, 11, "MutedTextBrush");
            _ruleSummary.TextWrapping = TextWrapping.Wrap;
            details.Children.Add(_ruleSummary);
            _countdown = Label(string.Empty, 12, "MutedTextBrush");
            _countdown.Margin = new Thickness(0, 12, 0, 0);
            details.Children.Add(_countdown);
            _badge = new Border { Width = 320, Padding = new Thickness(24, 16, 24, 18), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Child = details };
            _badge.SetResourceReference(Border.BackgroundProperty, "MarkerBackgroundBrush");
            _badge.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            Viewbox badgeHost = new Viewbox { Child = _badge, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _tunnels = new PhysicalTunnelView(badgeHost);
            _tunnels.LabelsOmitted += UpdateOmittedSummary;
            Content = new Grid { Children = { _tunnels, badgeHost }, IsHitTestVisible = false };
        }

        /// <summary>建立每螢幕 DPI 感知的 HWND 並以實體像素定位，不改變其他視窗的 DPI 或焦點。</summary>
        internal void ShowOverlay()
        {
            Dispatcher.VerifyAccess();
            IntPtr previous = SetThreadDpiAwarenessContext(new IntPtr(-3));
            if (previous == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                IntPtr handle = new WindowInteropHelper(this).EnsureHandle();
                PlaceWindow(handle);
                Show();
                PlaceWindow(handle);
            }
            finally { SetThreadDpiAwarenessContext(previous); }
        }

        /// <summary>更新既有辨識卡的倒數，不重建 HWND；限 UI 執行緒。</summary>
        /// <param name="seconds">距離關閉的非負秒數。</param>
        internal void UpdateCountdown(int seconds) => _countdown.Text = seconds + " 秒後結束 · 主視窗按 Esc 關閉";

        /// <summary>更新實體邊緣與中央規則摘要，不更動視窗位置或倒數；限 UI 執行緒。</summary>
        /// <param name="monitorIndex">此螢幕的快照索引。</param>
        /// <param name="rules">原生規則值快照。</param>
        /// <param name="segments">共用的輸入及語意驗證結果。</param>
        /// <param name="gaps">未設定邊緣的禁行區段。</param>
        /// <param name="selected">目前選取的來源索引。</param>
        /// <param name="selectedMonitor">是否為主視窗目前選取的螢幕。</param>
        /// <param name="forceForbid">是否封鎖剩餘邊緣，供摘要說明。</param>
        internal void UpdateRules(int monitorIndex, IReadOnlyList<TunnelInfo> rules, IReadOnlyList<TunnelPreviewSegment> segments,
            IReadOnlyList<TunnelPreviewSegment> gaps, int selected, bool selectedMonitor, bool forceForbid)
        {
            int count = segments.Count(segment => segment.MonitorIndex == monitorIndex && segment.HasGeometry);
            int unavailable = segments.Count(segment => !segment.HasGeometry && (segment.MonitorIndex == monitorIndex || segment.MonitorIndex < 0));
            int errors = segments.Count(segment => segment.Error.Length > 0 && (segment.MonitorIndex == monitorIndex || segment.MonitorIndex < 0));
            _summary = count + " 條通道" + (errors > 0 ? " · " + errors + " 條設定異常" : string.Empty) +
                (forceForbid ? "\n其餘邊緣禁行" : string.Empty) + (unavailable > 0 ? "\n" + unavailable + " 條規則無法定位，請回編輯器修正" : string.Empty);
            _ruleSummary.SetResourceReference(TextBlock.ForegroundProperty, errors > 0 ? "WarningBrush" : "MutedTextBrush");
            _badge.BorderThickness = new Thickness(selectedMonitor ? 2 : 1);
            _badge.SetResourceReference(Border.BorderBrushProperty, selectedMonitor ? "MonitorBorderBrush" : "BorderBrush");
            _ruleSummary.Text = _summary;
            _tunnels.Update(monitorIndex, Monitor, rules, segments, gaps, selected);
        }

        #endregion

        #region -- Private Methods --

        /// <summary>標籤無法全部容納時顯示數量，所有實際通道線仍保留。</summary>
        /// <param name="count">因畫面擁擠而略過的文字標籤數量。</param>
        private void UpdateOmittedSummary(int count) => _ruleSummary.Text = _summary + (count > 0 ? "\n" + count + " 個標籤過密，請在編輯器查看" : string.Empty);

        /// <summary>建立跟隨系統主題的置中文字。</summary>
        /// <param name="text">顯示文字。</param>
        /// <param name="size">DIP 字體大小。</param>
        /// <param name="brush">色盤前景資源名稱。</param>
        /// <returns>供辨識卡使用的文字控制項。</returns>
        private static TextBlock Label(string text, double size, string brush)
        {
            TextBlock label = new TextBlock { Text = text, FontSize = size, TextAlignment = TextAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, brush);
            return label;
        }

        /// <summary>在顯示前設定 layered 視窗的穿透與不啟用旗標，不修改主視窗樣式。</summary>
        /// <param name="sender">已建立 HWND 的覆蓋視窗。</param>
        /// <param name="e">來源建立事件。</param>
        private void InitializeSource(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            const int flags = 0x08000000 | 0x00000080 | 0x00000020; // NOACTIVATE、TOOLWINDOW、TRANSPARENT。
            SetWindowLong(handle, -20, (GetWindowLong(handle, -20) | flags) & ~0x00040000);
            if ((GetWindowLong(handle, -20) & flags) != flags) throw new Win32Exception(Marshal.GetLastWin32Error());
            _source = HwndSource.FromHwnd(handle);
            _source.AddHook(_messageHook);
        }

        /// <summary>覆蓋完整實體螢幕，包含負座標及工作列；呼叫端需處於每螢幕 DPI 感知環境。</summary>
        /// <param name="handle">本視窗的 HWND。</param>
        private void PlaceWindow(IntPtr handle)
        {
            if (!SetWindowPos(handle, new IntPtr(-1), Monitor.left, Monitor.top,
                checked((int)((long)Monitor.right - Monitor.left + 1)), checked((int)((long)Monitor.bottom - Monitor.top + 1)), 0x0030))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        /// <summary>拒絕原生命中與啟用；layered 的 TRANSPARENT 同時讓其他執行緒的桌面視窗可被點擊。</summary>
        /// <param name="handle">本視窗 HWND。</param>
        /// <param name="message">原生訊息編號。</param>
        /// <param name="wParam">未使用的訊息參數。</param>
        /// <param name="lParam">未使用的訊息參數。</param>
        /// <param name="handled">命中或啟用訊息時設為 true，其餘交給 WPF。</param>
        /// <returns>HTTRANSPARENT 或 MA_NOACTIVATE，其餘為零。</returns>
        private IntPtr WindowMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != 0x0084 && message != 0x0021) return IntPtr.Zero;
            handled = true;
            return new IntPtr(message == 0x0084 ? -1 : 3);
        }

        /// <summary>關閉後解除借用來源與視窗事件，不保留全域 hook 或顯示器設定。</summary>
        /// <param name="sender">已關閉的覆蓋視窗。</param>
        /// <param name="e">關閉事件。</param>
        private void WindowClosed(object sender, EventArgs e)
        {
            _tunnels.StopTrackingLayout();
            _tunnels.LabelsOmitted -= UpdateOmittedSummary;
            if (_source != null && !_source.IsDisposed) _source.RemoveHook(_messageHook);
            _source = null;
            SourceInitialized -= InitializeSource;
            Closed -= WindowClosed;
        }

        /// <summary>暫時切換 UI 執行緒的 DPI 感知，只用於覆蓋視窗建立與定位。</summary>
        /// <param name="context">DPI 感知環境。</param>
        /// <returns>原先環境，失敗為零；成功後由呼叫端 finally 還原。</returns>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

        /// <summary>讀取 HWND 的 32 位元樣式旗標，不讀取指標欄位。</summary>
        /// <param name="handle">覆蓋視窗 HWND。</param>
        /// <param name="index">GWL_EXSTYLE。</param>
        /// <returns>延伸樣式旗標。</returns>
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr handle, int index);

        /// <summary>設定 HWND 的 32 位元樣式旗標，不修改指標欄位。</summary>
        /// <param name="handle">覆蓋視窗 HWND。</param>
        /// <param name="index">GWL_EXSTYLE。</param>
        /// <param name="value">完整延伸樣式。</param>
        /// <returns>原先的樣式。</returns>
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr handle, int index, int value);

        /// <summary>以實體像素定位覆蓋視窗，保持 Topmost 並避免啟用。</summary>
        /// <param name="handle">覆蓋視窗 HWND。</param>
        /// <param name="insertAfter">HWND_TOPMOST。</param>
        /// <param name="x">左側像素。</param>
        /// <param name="y">上側像素。</param>
        /// <param name="width">包含尾端的寬度。</param>
        /// <param name="height">包含尾端的高度。</param>
        /// <param name="flags">SWP_NOACTIVATE 與 SWP_FRAMECHANGED。</param>
        /// <returns>是否成功。</returns>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        #endregion
    }
}
