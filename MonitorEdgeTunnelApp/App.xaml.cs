using Microsoft.Win32;
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

        public App()
        {
            // 事件綁定都寫在這裡

            Startup += new StartupEventHandler(LockAppEvent);
            Startup += new StartupEventHandler(ListenNamedPipeEvent);
            Startup += new StartupEventHandler(AddTrayIconEvent);
            Startup += new StartupEventHandler(AddDisplaySettingsChangedEvent);
            Startup += new StartupEventHandler(InitMonitorEdgeTunnelLogger);

            Exit += new ExitEventHandler(RemoveDisplaySettingsChangedEvent);
            Exit += new ExitEventHandler(RemoveTrayIconEvent);
            Exit += new ExitEventHandler(CloseNamedPipeEvent);
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            throw new NotImplementedException();
        }

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

        private void ListenNamedPipeEvent(object sender, StartupEventArgs e)
        {
            SelfNamedPipeManager.Instance.OpenWindowEvent += new OpenWindowEventHandler(delegate { CreateAndShowMainWindow(); });
            SelfNamedPipeManager.Instance.Listen();
        }

        private void CloseNamedPipeEvent(object sender, ExitEventArgs e)
        {
            SelfNamedPipeManager.Instance.Close();
        }

        private void AddTrayIconEvent(object sender, StartupEventArgs e)
        {
            trayIcon = new NotifyIcon
            {
                Icon = new Icon("monitor.ico"),
                Text = "螢幕邊界助手",
                Visible = true
            };

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
                item.Click += new EventHandler(delegate { Shutdown(); });
                _ = menu.MenuItems.Add(item);
            }

            // 雙擊開啟視窗
            trayIcon.DoubleClick += new EventHandler(delegate { CreateAndShowMainWindow(); });

            trayIcon.ContextMenu = menu;
        }

        private void RemoveTrayIconEvent(object sender, ExitEventArgs e)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }

        private void CreateAndShowMainWindow()
        {
            // 不同執行緒需要委託主執行緒
            if (Dispatcher.CheckAccess())
            {
                // 如果視窗關閉就先建立視窗
                if (MainWindow == null)
                {
                    MainWindow = new MainWindow();
                }

                MainWindow.Show();
                _ = MainWindow.Activate();
                MainWindow.WindowState = WindowState.Normal;
            }
            else
            {
                Dispatcher.Invoke(CreateAndShowMainWindow);
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 確認是否有"/autorun"指令
            if (e.Args.Length != 0 && e.Args[0] == AutoStartManager.AUTORUN_CMD_STR)
            {
                isAutoRun = true;
            }
        }

        private void AddDisplaySettingsChangedEvent(object sender, StartupEventArgs e)
        {
            try
            {
                SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            }
            catch (Exception)
            {
                trayIcon.ShowBalloonTip(3000, "異常", "無法監聽螢幕設定", ToolTipIcon.Error);
            }
        }

        private void InitMonitorEdgeTunnelLogger(object sender, StartupEventArgs e)
        {
            Logger.InitMonitorEdgeTunnelLogger();
        }

        private void RemoveDisplaySettingsChangedEvent(object sender, ExitEventArgs e)
        {
            try
            {
                SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            }
            catch (Exception)
            {
                // 不做事...
            }
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            // 套用當前螢幕設定的通道規則
            if (MonitorEdgeTunnel.Instance.IsStart())
            {
                // 提示螢幕設定改變事件
                if (MonitorEdgeTunnel.Instance.Start())
                {
                    trayIcon.ShowBalloonTip(1000, "螢幕設定改變通知", "通道規則改變", ToolTipIcon.Info);
                }
                else
                {
                    trayIcon.ShowBalloonTip(3000, "螢幕設定改變通知", MonitorEdgeTunnelErrorMsgConvertor.ToErrorMsgString(MonitorEdgeTunnel.Instance.GetErrorMsgCode()), ToolTipIcon.Error);
                }
            }

            // 更新視窗資訊
            UpdateMainWindowData();
        }

        private void UpdateMainWindowData()
        {
            // 不同執行緒需要委託主執行緒
            if (Dispatcher.CheckAccess())
            {
                if (MainWindow != null)
                {
                    // 更新資訊
                    ((MainWindow)MainWindow).UpdateData();
                }
            }
            else
            {
                Dispatcher.Invoke(UpdateMainWindowData);
            }
        }
    }
}
