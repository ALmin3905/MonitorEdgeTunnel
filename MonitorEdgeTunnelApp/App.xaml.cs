using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace MonitorEdgeTunnelApp
{
    /// <summary>
    /// App.xaml 的互動邏輯
    /// </summary>
    public partial class App : Application
    {
        private const string PROCESS_MUTEX_NAME = "_MonitorEdgeTunnelApp_";

        private bool isAutoRun = false;

        private Mutex processMutex;

        private NotifyIcon trayIcon;

        public App()
        {
            Startup += new StartupEventHandler(LockAppEvent);
            Startup += new StartupEventHandler(ListenNamedPipeEvent);
            Startup += new StartupEventHandler(AddTrayIconEvent);

            Exit += new ExitEventHandler(CloseNamedPipeEvent);
            Exit += new ExitEventHandler(RemoveTrayIconEvent);
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
    }
}
