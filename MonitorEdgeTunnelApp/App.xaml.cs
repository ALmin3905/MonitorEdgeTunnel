using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace MonitorEdgeTunnelApp
{
    /// <summary>
    /// App.xaml 的互動邏輯
    /// </summary>
    public partial class App : Application
    {
        private Mutex systemMutex;

        private NotifyIcon trayIcon;

        public App()
        {
            Startup += new StartupEventHandler(LockAppEvent);
            Startup += new StartupEventHandler(AddTrayIconEvent);

            Exit += new ExitEventHandler(RemoveTrayIconEvent);
        }

        private void LockAppEvent(object sender, StartupEventArgs e)
        {
            systemMutex = new Mutex(true, "_MonitorEdgeTunnelApp_", out bool ret);

            if (!ret)
            {
                // TODO : 之後改成顯示已存在process的視窗
                _ = MessageBox.Show("已有程式運行了", "訊息");
                Environment.Exit(0);
            }
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

            trayIcon.ContextMenu = menu;
        }

        private void RemoveTrayIconEvent(object sender, ExitEventArgs e)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }

        private void CreateAndShowMainWindow()
        {
            if (MainWindow == null)
            {
                MainWindow = new MainWindow();
            }

            MainWindow.Show();
        }
    }
}
