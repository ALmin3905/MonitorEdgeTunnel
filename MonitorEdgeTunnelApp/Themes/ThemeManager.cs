using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MonitorEdgeTunnelApp.Themes
{
    /// <summary>應用程式採用的系統配色；高對比優先於一般深淺色設定。</summary>
    internal enum AppTheme
    {
        /// <summary>暖白背景與深色文字。</summary>
        Light,
        /// <summary>石墨灰背景與淺色文字。</summary>
        Dark,
        /// <summary>使用 Windows 的高對比系統色。</summary>
        HighContrast
    }

    /// <summary>在 UI 執行緒更新共用色盤，將系統背景通知非同步派送回 WPF。</summary>
    internal class ThemeManager : IDisposable
    {
        /// <summary>包含所有視窗共用資源及 UI Dispatcher 的應用程式。</summary>
        private readonly Application _application;
        /// <summary>於 UI 執行緒讀取偏好的來源，測試可提供不修改系統的替身。</summary>
        private readonly Func<AppTheme> _readTheme;
        /// <summary>最近套用的色盤，用於避免重複載入資源。</summary>
        private AppTheme? _currentTheme;
        /// <summary>目前擁有的色盤字典；不替換其他樣式或呼叫端資源。</summary>
        private ResourceDictionary _palette;
        /// <summary>合併連續系統通知的 UI 工作。</summary>
        private DispatcherOperation _pendingRefresh;

        #region -- Public Methods --

        /// <summary>在 application 的 UI 執行緒套用初始配色並訂閱系統通知。</summary>
        /// <param name="application">資源及視窗的擁有者，不可為 null。</param>
        /// <param name="readTheme">選用的 UI 配色讀取器；null 時讀取 Windows 設定。</param>
        internal ThemeManager(Application application, Func<AppTheme> readTheme = null)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _application.Dispatcher.VerifyAccess();
            _readTheme = readTheme ?? ReadSystemTheme;
            _palette = application.Resources.MergedDictionaries.FirstOrDefault(dictionary =>
                dictionary.Source != null && Enum.GetNames(typeof(AppTheme)).Any(name =>
                    dictionary.Source.OriginalString.EndsWith("Themes/" + name + ".xaml", StringComparison.OrdinalIgnoreCase)));
            Refresh();
            SystemEvents.UserPreferenceChanged += UserPreferenceChanged;
            SystemParameters.StaticPropertyChanged += SystemParameterChanged;
        }

        /// <summary>要求重新讀取配色；背景呼叫只排入 UI 工作，結束或 Dispose 後忽略。</summary>
        internal void Refresh()
        {
            Dispatcher dispatcher = _application.Dispatcher;
            if (disposedValue || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Refresh));
                return;
            }
            AppTheme theme = _readTheme();
            if (_currentTheme == theme) return;
            ResourceDictionary palette = new ResourceDictionary { Source = ResourceUri(theme + ".xaml") };
            int index = _palette == null ? 0 : _application.Resources.MergedDictionaries.IndexOf(_palette);
            if (index < 0) index = 0;
            _application.Resources.MergedDictionaries.Insert(index, palette);
            if (_palette != null) _application.Resources.MergedDictionaries.Remove(_palette);
            _palette = palette;
            _currentTheme = theme;
            _application.Resources["IsDarkTheme"] = theme == AppTheme.Dark;
            foreach (Window window in _application.Windows) UpdateTitleBar(window, theme == AppTheme.Dark);
        }

        /// <summary>取得目前組件內的主題資源路徑，供應用程式與隔離的 WPF 測試共用。</summary>
        /// <param name="file">Themes 目錄內的 XAML 檔名。</param>
        /// <returns>包含組件名稱的相對 pack 資源 URI。</returns>
        internal static Uri ResourceUri(string file) => new Uri("/" + typeof(ThemeManager).Assembly.GetName().Name + ";component/Themes/" + file, UriKind.Relative);

        /// <summary>在新視窗建立 HWND 後套用目前標題列配色，不主動建立其他視窗。</summary>
        /// <param name="sender">已建立來源的 WPF Window。</param>
        /// <param name="e">來源建立通知；不修改。</param>
        internal static void WindowSourceInitialized(object sender, EventArgs e)
        {
            UpdateTitleBar((Window)sender, Application.Current?.TryFindResource("IsDarkTheme") is bool dark && dark);
        }

        #endregion

        #region -- Private Methods --

        /// <summary>於 UI 執行緒讀取應用程式色彩偏好；設定不存在或無權讀取時使用淺色。</summary>
        /// <returns>高對比優先，否則依 AppsUseLightTheme 回傳深色或淺色。</returns>
        private static AppTheme ReadSystemTheme()
        {
            if (SystemParameters.HighContrast) return AppTheme.HighContrast;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return key?.GetValue("AppsUseLightTheme") is int value && value == 0 ? AppTheme.Dark : AppTheme.Light;
            }
            catch (SecurityException) { return AppTheme.Light; }
            catch (UnauthorizedAccessException) { return AppTheme.Light; }
            catch (IOException) { return AppTheme.Light; }
        }

        /// <summary>接收可能來自系統執行緒的個人化通知，只排入 UI 更新。</summary>
        /// <param name="sender">系統事件來源。</param>
        /// <param name="e">偏好變更類別；各類都重新讀取，避免依賴 Windows 版本的分類。</param>
        private void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => QueueRefresh();

        /// <summary>系統高對比切換時更新色盤；其他 WPF 參數不觸發重載。</summary>
        /// <param name="sender">WPF 系統參數來源。</param>
        /// <param name="e">包含變更的屬性名稱。</param>
        private void SystemParameterChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast)) QueueRefresh();
        }

        /// <summary>將任意執行緒的通知非同步派送；UI 端合併尚未執行的刷新。</summary>
        private void QueueRefresh()
        {
            Dispatcher dispatcher = _application.Dispatcher;
            if (disposedValue || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (disposedValue || _pendingRefresh?.Status == DispatcherOperationStatus.Pending) return;
                _pendingRefresh = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Refresh));
            }));
        }

        /// <summary>於 UI 執行緒請求 DWM 的深淺標題列；舊系統不支援時維持原生外觀。</summary>
        /// <param name="window">已存在或尚未顯示的視窗；不為它建立 HWND。</param>
        /// <param name="dark">是否請求深色標題列。</param>
        private static void UpdateTitleBar(Window window, bool dark)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            int value = dark ? 1 : 0;
            try { DwmSetWindowAttribute(handle, 20, ref value, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        /// <summary>呼叫 DWM 設定視窗屬性，僅供 UI 執行緒的標題列更新使用。</summary>
        /// <param name="handle">既有 HWND。</param>
        /// <param name="attribute">DWM 屬性編號；20 為沉浸式深色。</param>
        /// <param name="value">傳入 Windows BOOL 的整數儲存位置；不依賴輸出值。</param>
        /// <param name="size">value 的位元組數。</param>
        /// <returns>HRESULT；不支援此屬性時允許系統沿用原生標題列。</returns>
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);

        #endregion

        #region -- Dispose Pattern --

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _application.Dispatcher.VerifyAccess();
                    SystemEvents.UserPreferenceChanged -= UserPreferenceChanged;
                    SystemParameters.StaticPropertyChanged -= SystemParameterChanged;
                    _pendingRefresh?.Abort();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
