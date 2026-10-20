using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;

namespace MonitorEdgeTunnelApp
{
    /// <summary>以可控制的服務結果測試真正的視窗存檔事件，不顯示 UI。</summary>
    public static class SaveUiTests
    {
        #region -- Public Methods --
        /// <summary>在 STA 建立未顯示的視窗，驗證失敗提示及停止存檔；回傳零代表通過。</summary>
        /// <returns>程序結束碼。</returns>
        [STAThread]
        public static int Main()
        {
            MainWindow window = new MainWindow();
            MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
            ClickSave(window);
            Require(service.SaveCalls == 1 && TestMessageBox.Calls == 1);
            service.ListResult = -3;
            ClickSave(window);
            Require(service.SaveCalls == 1 && TestMessageBox.Calls == 2);
            service.ListResult = 0;
            service.Running = true;
            service.StopSucceeds = false;
            ClickSave(window);
            Require(service.SaveCalls == 1 && TestMessageBox.Calls == 3);
            service.StopSucceeds = true;
            service.StartSucceeds = false;
            ClickSave(window);
            Require(service.SaveCalls == 1 && TestMessageBox.Calls == 4);
            service.Running = true;
            service.StartSucceeds = true;
            service.SaveSucceeds = true;
            ClickSave(window);
            Require(service.SaveCalls == 2 && TestMessageBox.Calls == 4 && window.isStart);
            window.Close();
            Console.WriteLine("UI reports save failure and never persists a failed apply or restart");
            return 0;
        }
        #endregion

        #region -- Private Methods --
        /// <summary>呼叫 window 的真正存檔事件，使用空 sender 與事件參數。</summary>
        /// <param name="window">未顯示的受測視窗。</param>
        private static void ClickSave(MainWindow window)
        {
            typeof(MainWindow).GetMethod("SaveSettingButton_Click", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(window, new object[] { null, null });
        }
        /// <summary>驗證 condition；失敗時中止測試。</summary>
        /// <param name="condition">待驗證的條件。</param>
        private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Save UI regression failed"); }
        #endregion
    }

    /// <summary>提供測試 XAML 欄位所需的簡單資料容器。</summary>
    public class TestControl
    {
        /// <summary>綁定來源。</summary>
        public object DataContext { get; set; }
        /// <summary>編輯清單的項目容器。</summary>
        public ArrayList Items { get; } = new ArrayList();
    }

    /// <summary>提供受測 code-behind 的 XAML 欄位，不載入實際 UI 配置。</summary>
    public partial class MainWindow
    {
        /// <summary>螢幕清單的替代控制項。</summary>
        private readonly TestControl MonitorInfoListView = new TestControl();
        /// <summary>螢幕佈局控制項的替代控制項。</summary>
        private readonly TestControl MonitorLayout = new TestControl();
        /// <summary>通道清單的替代控制項。</summary>
        private readonly TestControl TunnelInfoListView = new TestControl();
        /// <summary>邊界選項的替代控制項。</summary>
        private readonly TestControl ForceForbidEdgeCheckbox = new TestControl();
        /// <summary>啟停按鈕的替代控制項。</summary>
        private readonly TestControl IsStartButton = new TestControl();
        /// <summary>自動啟動選單的替代控制項。</summary>
        private readonly TestControl AutorunMenuItem = new TestControl();
        #region -- Private Methods --
        /// <summary>測試中不載入或顯示 XAML。</summary>
        private void InitializeComponent() { }
        #endregion
    }

}
