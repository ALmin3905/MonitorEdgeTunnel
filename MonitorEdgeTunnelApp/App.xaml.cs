using Microsoft.Win32;
using MonitorEdgeTunnelApp.Themes;
using MonitorEdgeTunnelApp.Editing;
using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Application = System.Windows.Application;

namespace MonitorEdgeTunnelApp
{
    /// <summary>
    /// App.xaml 的互動邏輯
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Process互斥鎖名稱
        /// </summary>
        private const string PROCESS_MUTEX_NAME = "_MonitorEdgeTunnelApp_";

        /// <summary>
        /// Process互斥鎖
        /// </summary>
        private Mutex processMutex;

        /// <summary>
        /// NotifyIcon
        /// </summary>
        private NotifyIcon trayIcon;

        /// <summary>
        /// 是否自動啟動 (由執行程式時帶入參數決定)
        /// </summary>
        private bool isAutoRun = false;

        /// <summary>應用程式存續期間共用的系統配色監聽器。</summary>
        private ThemeManager _themeManager;
        /// <summary>視窗關閉與配置切換之間持續保留的草稿。</summary>
        private readonly EditorWorkspace _editorWorkspace = new EditorWorkspace();

        #region -- Application Lifecycle --

        /// <summary>建立 UI 生命週期的服務啟動、回呼解除與停止順序。</summary>
        public App()
        {
            // 事件綁定都寫在這裡

            Startup += new StartupEventHandler(LockAppEvent);
            Startup += InitializeTheme;
            Startup += new StartupEventHandler(ListenNamedPipeEvent);
            Startup += new StartupEventHandler(AddTrayIconEvent);
            Startup += new StartupEventHandler(AddDisplaySettingsChangedEvent);
            Startup += new StartupEventHandler(InitMonitorEdgeTunnelLogger);

            Exit += DisposeTheme;
            Exit += new ExitEventHandler(RemoveDisplaySettingsChangedEvent);
            Exit += new ExitEventHandler(ShutdownMonitorEdgeTunnel);
            Exit += new ExitEventHandler(DeinitMonitorEdgeTunnelLogger);
            Exit += new ExitEventHandler(RemoveTrayIconEvent);
            Exit += new ExitEventHandler(CloseNamedPipeEvent);
        }

        /// <summary>完成啟動事件後，在 UI 執行緒依 autorun 參數啟動功能或顯示視窗。</summary>
        /// <param name="e">啟動參數與事件資料。</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 如果是autorun就啟動功能，但不建立視窗
            if (isAutoRun)
            {
                if (!MonitorEdgeTunnel.Instance.Start())
                {
                    trayIcon.ShowBalloonTip(3000, "自動啟動失敗", "請手動調整參數並啟動", ToolTipIcon.Error);
                }
            }
            else
            {
                CreateAndShowMainWindow();
            }
        }

        #endregion

        #region -- Private Methods --

        /// <summary>於 UI 啟動時套用 Windows 配色，通知區模式也持續追蹤之後的切換。</summary>
        /// <param name="sender">應用程式來源。</param>
        /// <param name="e">啟動參數；不修改。</param>
        private void InitializeTheme(object sender, StartupEventArgs e) => _themeManager = new ThemeManager(this);

        /// <summary>於 UI 結束時解除靜態系統事件，停止尚未執行的主題更新。</summary>
        /// <param name="sender">應用程式來源。</param>
        /// <param name="e">結束資料；不修改。</param>
        private void DisposeTheme(object sender, ExitEventArgs e) => _themeManager?.Dispose();

        /// <summary>建立單一執行個體鎖；已有實例時通知其開窗並結束本行程。</summary>
        /// <param name="sender">啟動事件來源。</param>
        /// <param name="e">應用程式啟動資料。</param>
        private void LockAppEvent(object sender, StartupEventArgs e)
        {
            processMutex = new Mutex(true, PROCESS_MUTEX_NAME, out bool ret);

            if (!ret)
            {
                // 呼叫已存在的應用程式開啟視窗
                SelfNamedPipeManager.Instance.Send(SelfNamedPipeAction.OpenWindow);

                // 退出
                Environment.Exit(0);
            }
        }

        /// <summary>訂閱跨行程開窗指令並啟動背景 IPC 接收。</summary>
        /// <param name="sender">啟動事件來源。</param>
        /// <param name="e">應用程式啟動資料。</param>
        private void ListenNamedPipeEvent(object sender, StartupEventArgs e)
        {
            SelfNamedPipeManager.Instance.OpenWindowEvent += new OpenWindowEventHandler(delegate { CreateAndShowMainWindow(); });
            SelfNamedPipeManager.Instance.Listen();
        }

        /// <summary>結束應用程式時停止背景 IPC 接收。</summary>
        /// <param name="sender">結束事件來源。</param>
        /// <param name="e">應用程式結束資料。</param>
        private void CloseNamedPipeEvent(object sender, ExitEventArgs e)
        {
            SelfNamedPipeManager.Instance.Close();
        }

        /// <summary>在 UI 執行緒建立通知區圖示與開窗、結束選單。</summary>
        /// <param name="sender">啟動事件來源。</param>
        /// <param name="e">應用程式啟動資料。</param>
        private void AddTrayIconEvent(object sender, StartupEventArgs e)
        {
            // 從 WPF Resource 讀取 icon
            var iconUri = new Uri("pack://application:,,,/Resources/monitor.ico", UriKind.Absolute);
            using (var stream = Application.GetResourceStream(iconUri)?.Stream)
            {
                trayIcon = new NotifyIcon
                {
                    // 依通知區的系統小圖示尺寸，載入對應的 ICO 影格。
                    Icon = stream != null ? new System.Drawing.Icon(stream, SystemInformation.SmallIconSize) : System.Drawing.SystemIcons.Application,
                    Text = "螢幕邊界助手",
                    Visible = true
                };
            }

            ContextMenu menu = new ContextMenu();

            // 開啟視窗
            {
                MenuItem item = new MenuItem();
                item.Text = "開啟視窗";
                item.Click += new EventHandler(delegate { CreateAndShowMainWindow(); });
                _ = menu.MenuItems.Add(item);
            }

            // 關閉程式
            {
                MenuItem item = new MenuItem();
                item.Text = "結束";
                item.Click += new EventHandler(delegate { RequestExit(); });
                _ = menu.MenuItems.Add(item);
            }

            // 雙擊開啟視窗
            trayIcon.DoubleClick += new EventHandler(delegate { CreateAndShowMainWindow(); });

            trayIcon.ContextMenu = menu;
        }

        /// <summary>隱藏並釋放通知區圖示。</summary>
        /// <param name="sender">結束事件來源。</param>
        /// <param name="e">應用程式結束資料。</param>
        private void RemoveTrayIconEvent(object sender, ExitEventArgs e)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }

        /// <summary>在 UI 執行緒確認所有配置的未儲存內容，再結束通知區與原生服務。</summary>
        private void RequestExit()
        {
            if (!_editorWorkspace.HasUnsavedChanges || ThemedMessageBox.Confirm(
                "仍有未儲存內容；結束會捨棄草稿與尚未儲存的設定。關閉視窗可保留草稿並留在通知區。", "結束程式？", "結束並捨棄")) Shutdown();
        }

        /// <summary>建立或顯示主視窗；背景呼叫會非同步派送至 UI，結束期間忽略開窗。</summary>
        private void CreateAndShowMainWindow()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            // 不同執行緒需要委託主執行緒；非同步派送，避免停止服務時相互等待。
            if (Dispatcher.CheckAccess())
            {
                // 如果視窗關閉就先建立視窗
                if (MainWindow == null)
                {
                    MainWindow = new MainWindow(_editorWorkspace, RequestExit);
                    MainWindow.SourceInitialized += ThemeManager.WindowSourceInitialized;
                }

                MainWindow.Show();
                _ = MainWindow.Activate();
                MainWindow.WindowState = WindowState.Normal;
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(CreateAndShowMainWindow));
            }
        }

        /// <summary>解析啟動參數並記錄是否以 autorun 模式啟動。</summary>
        /// <param name="sender">啟動事件來源。</param>
        /// <param name="e">包含命令列參數的啟動資料。</param>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 確認是否有"/autorun"指令
            if (e.Args.Length != 0 && e.Args[0] == AutoStartManager.AUTORUN_CMD_STR)
            {
                isAutoRun = true;
            }
        }

        /// <summary>訂閱原生訊息執行緒的螢幕變更回呼。</summary>
        /// <param name="sender">啟動事件來源。</param>
        /// <param name="e">應用程式啟動資料。</param>
        private void AddDisplaySettingsChangedEvent(object sender, StartupEventArgs e)
        {
            if (!MonitorEdgeTunnel.Instance.AddDisplayChangedCallback(DisplaySettingsChangedEvent))
            {
                Logger.Log(3, "Add Display Changed Event Failed");
            }
        }

        /// <summary>連接支援多執行緒的受控日誌接收者。</summary>
        /// <param name="sender">啟動事件來源。</param>
        /// <param name="e">應用程式啟動資料。</param>
        private void InitMonitorEdgeTunnelLogger(object sender, StartupEventArgs e)
        {
            Logger.InitMonitorEdgeTunnelLogger();
        }

        /// <summary>在 UI 結束期間停止全部原生服務，避免 DLL 卸載時仍有工作執行緒。</summary>
        /// <param name="sender">應用程式事件來源。</param>
        /// <param name="e">應用程式結束事件資料。</param>
        private void ShutdownMonitorEdgeTunnel(object sender, ExitEventArgs e)
        {
            if (!MonitorEdgeTunnel.Instance.Shutdown())
                Logger.Log(3, "Native services did not stop within the timeout");
        }

        /// <summary>原生工作執行緒停止後解除日誌回呼。</summary>
        /// <param name="sender">結束事件來源。</param>
        /// <param name="e">應用程式結束資料。</param>
        private void DeinitMonitorEdgeTunnelLogger(object sender, ExitEventArgs e)
        {
            Logger.DeinitMonitorEdgeTunnelLogger();
        }

        /// <summary>解除螢幕變更訂閱，避免 UI 結束後繼續通知。</summary>
        /// <param name="sender">結束事件來源。</param>
        /// <param name="e">應用程式結束資料。</param>
        private void RemoveDisplaySettingsChangedEvent(object sender, ExitEventArgs e)
        {
            if (!MonitorEdgeTunnel.Instance.RemoveDisplayChangedCallback(DisplaySettingsChangedEvent))
            {
                Logger.Log(3, "Remove Display Changed Event Failed");
            }
        }

        /// <summary>以非同步方式派送原生螢幕變更通知，在 UI 執行緒更新提示與資料。</summary>
        private void DisplaySettingsChangedEvent()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            // 不同執行緒需要委託主執行緒；非同步派送，避免停止服務時相互等待。
            if (Dispatcher.CheckAccess())
            {
                // 提示螢幕設定改變事件
                if (MonitorEdgeTunnel.Instance.IsStart())
                {
                    trayIcon.ShowBalloonTip(1000, "螢幕設定改變通知", "通道規則改變", ToolTipIcon.Info);
                }

                // 更新視窗資訊
                UpdateMainWindowData();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(DisplaySettingsChangedEvent));
            }
        }

        /// <summary>在 UI 執行緒重新整理已存在主視窗的螢幕與通道資訊。</summary>
        private void UpdateMainWindowData()
        {
            if (MainWindow != null)
            {
                // 更新資訊
                ((MainWindow)MainWindow).RefreshDisplayData();
            }
        }
        #endregion
    }
}
