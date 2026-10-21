using System;
using System.Collections.Generic;
using System.Diagnostics;
using MonitorEdgeTunnelApp.Editing;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>在 UI 執行緒管理短暫的實體螢幕標記，隨擁有者關閉並清除計時器。</summary>
    internal sealed class MonitorIdentification
    {
        /// <summary>接收 Esc 並持有標記視窗的主視窗。</summary>
        private readonly Window _owner;
        /// <summary>建立覆蓋視窗的工廠，測試可使用透明視窗而不干擾桌面。</summary>
        private readonly Func<MonitorInfo, MonitorIdentificationWindow> _createWindow;
        /// <summary>辨識持續時間，預設十秒。</summary>
        private readonly TimeSpan _duration;
        /// <summary>不受系統時間調整或 UI 暫時忙碌影響的經過時間。</summary>
        private readonly Stopwatch _elapsed = new Stopwatch();
        /// <summary>只在辨識期間啟動的 UI 計時器。</summary>
        private readonly DispatcherTimer _timer;
        /// <summary>目前顯示的覆蓋視窗，不保留靜態視窗參考。</summary>
        private readonly List<MonitorIdentificationWindow> _windows = new List<MonitorIdentificationWindow>();
        /// <summary>擁有者關閉後禁止再次顯示。</summary>
        private bool _closed;
        /// <summary>顯示時的配置鍵，更新期間配置不一致就清除標記。</summary>
        private string _layout;
        /// <summary>開始、倒數或結束時通知操作文字更新；限 UI 執行緒。</summary>
        internal event EventHandler Changed;
        /// <summary>是否有正在顯示的實體螢幕標記。</summary>
        internal bool IsVisible => _windows.Count > 0;
        /// <summary>距離自動關閉的秒數，沒有標記時為零。</summary>
        internal int SecondsRemaining { get; private set; }
        /// <summary>目前標記的唯讀清單，外部不得關閉其中的個別視窗。</summary>
        internal IReadOnlyList<MonitorIdentificationWindow> Windows => _windows.AsReadOnly();

        #region -- Public Methods --

        /// <summary>建立隨擁有者清理的辨識控制器，不建立 HWND 或啟動計時；限 UI 執行緒。</summary>
        /// <param name="owner">接收 Esc 的主視窗。</param>
        /// <param name="createWindow">選用視窗工廠；null 使用正式覆蓋視窗。</param>
        /// <param name="duration">選用正值持續時間；null 使用十秒。</param>
        internal MonitorIdentification(Window owner, Func<MonitorInfo, MonitorIdentificationWindow> createWindow = null, TimeSpan? duration = null)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _owner.Dispatcher.VerifyAccess();
            _duration = duration ?? TimeSpan.FromSeconds(10);
            if (_duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
            _createWindow = createWindow ?? (monitor => new MonitorIdentificationWindow(monitor));
            _timer = new DispatcherTimer(DispatcherPriority.Background, owner.Dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += TimerTick;
            _owner.PreviewKeyDown += OwnerKeyDown;
            _owner.Closed += OwnerClosed;
        }

        /// <summary>顯示指定配置的螢幕標記，重新開始前先關閉舊視窗；不改動設定，限 UI 執行緒。</summary>
        /// <param name="monitors">目前配置的實體像素快照，無效大小會略過。</param>
        /// <returns>至少顯示一個視窗時為 true；擁有者不可見或已關閉時為 false。</returns>
        internal bool Show(IReadOnlyList<MonitorInfo> monitors)
        {
            _owner.Dispatcher.VerifyAccess();
            Stop();
            if (_closed || !_owner.IsVisible) return false;
            try
            {
                SecondsRemaining = (int)Math.Ceiling(_duration.TotalSeconds);
                foreach (MonitorInfo monitor in monitors.Where(IsUsable))
                {
                    MonitorIdentificationWindow window = _createWindow(monitor);
                    _windows.Add(window);
                    window.Owner = _owner;
                    window.UpdateCountdown(SecondsRemaining);
                    window.ShowOverlay();
                }
                if (!IsVisible) { SecondsRemaining = 0; return false; }
                _layout = EditorWorkspace.LayoutKey(monitors);
                _elapsed.Restart();
                _timer.Start();
                Changed?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch { Stop(); throw; }
        }

        /// <summary>同步目前編輯規則與選取，不重新開始倒數、不套用服務；配置變動時結束辨識，限 UI 執行緒。</summary>
        /// <param name="monitors">目前配置的穩定快照。</param>
        /// <param name="rules">包含未完成文字狀態的編輯規則。</param>
        /// <param name="forceForbid">是否標示其餘未設定的禁行邊緣。</param>
        /// <param name="selected">目前選取的規則物件，沒有選取為 null。</param>
        /// <param name="selectedMonitor">目前選取的原始螢幕 ID。</param>
        internal void UpdateRules(IReadOnlyList<MonitorInfo> monitors, IReadOnlyList<TunnelInfo_Notify> rules, bool forceForbid,
            TunnelInfo_Notify selected, int? selectedMonitor)
        {
            _owner.Dispatcher.VerifyAccess();
            if (!IsVisible) return;
            if (_layout != EditorWorkspace.LayoutKey(monitors)) { Stop(); return; }
            IReadOnlyList<TunnelPreviewSegment> segments = EditorValidation.Evaluate(monitors, rules);
            IReadOnlyList<TunnelPreviewSegment> gaps = forceForbid ? TunnelPreviewCalculator.CalculateForbiddenGaps(monitors, segments) : new TunnelPreviewSegment[0];
            TunnelInfo[] values = rules.Select(TunnelInfo_Notify.Cast).ToArray();
            int selectedIndex = -1;
            for (int i = 0; i < rules.Count; ++i) if (ReferenceEquals(rules[i], selected)) selectedIndex = i;
            foreach (MonitorIdentificationWindow window in _windows)
            {
                int index = -1;
                for (int i = 0; i < monitors.Count; ++i) if (monitors[i].id == window.Monitor.id) index = i;
                window.UpdateRules(index, values, segments, gaps, selectedIndex, selectedMonitor == window.Monitor.id, forceForbid);
            }
        }

        /// <summary>關閉全部標記並停止計時，不套用、儲存或更動原生通道服務；限 UI 執行緒。</summary>
        internal void Stop()
        {
            _owner.Dispatcher.VerifyAccess();
            _timer.Stop();
            _elapsed.Stop();
            bool visible = IsVisible;
            MonitorIdentificationWindow[] windows = _windows.ToArray();
            _windows.Clear();
            SecondsRemaining = 0;
            foreach (MonitorIdentificationWindow window in windows) window.Close();
            if (visible) Changed?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region -- Private Methods --

        /// <summary>以 long 檢查含尾端邊界，排除不可表示為 Win32 視窗尺寸的資料。</summary>
        /// <param name="monitor">實體像素資訊。</param>
        /// <returns>編號與尺寸有效時為 true。</returns>
        private static bool IsUsable(MonitorInfo monitor)
        {
            long width = (long)monitor.right - monitor.left + 1;
            long height = (long)monitor.bottom - monitor.top + 1;
            return monitor.id >= 0 && width > 0 && width <= int.MaxValue && height > 0 && height <= int.MaxValue;
        }

        /// <summary>按經過時間更新倒數，到期關閉；UI 忙碌後不會重新延長辨識時間。</summary>
        /// <param name="sender">目前計時器。</param>
        /// <param name="e">UI 計時事件。</param>
        private void TimerTick(object sender, EventArgs e)
        {
            int seconds = Math.Max(0, (int)Math.Ceiling((_duration - _elapsed.Elapsed).TotalSeconds));
            if (seconds == 0) { Stop(); return; }
            if (seconds == SecondsRemaining) return;
            SecondsRemaining = seconds;
            foreach (MonitorIdentificationWindow window in _windows) window.UpdateCountdown(seconds);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>主視窗保有焦點時以 Esc 結束，不建立全域鍵盤 hook。</summary>
        /// <param name="sender">擁有者視窗。</param>
        /// <param name="e">輸入按鍵，辨識中的 Esc 會標為已處理。</param>
        private void OwnerKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || !IsVisible) return;
            Stop();
            e.Handled = true;
        }

        /// <summary>關閉全部標記並解除事件，不讓計時器保留已關閉的擁有者。</summary>
        /// <param name="sender">已關閉的主視窗。</param>
        /// <param name="e">關閉事件。</param>
        private void OwnerClosed(object sender, EventArgs e)
        {
            _closed = true;
            Stop();
            _timer.Tick -= TimerTick;
            _owner.PreviewKeyDown -= OwnerKeyDown;
            _owner.Closed -= OwnerClosed;
        }

        #endregion
    }
}
