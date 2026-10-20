using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using MonitorEdgeTunnelApp;

namespace MonitorEdgeTunnelApp
{
    /// <summary>接收隔離測試的日誌，不啟動其他應用程式服務。</summary>
    public static class Logger
    {
        #region -- Public Methods --
        /// <summary>將等級與訊息寫到測試輸出。</summary>
        /// <param name="level">原生日誌等級。</param>
        /// <param name="message">要顯示的內容。</param>
        public static void Log(int level, string message) { Console.WriteLine(message); }
        #endregion
    }
}

/// <summary>以專用名稱的真實管線驗證 EOF、取消讀取與快速啟停。</summary>
public static class PipeTests
{
    #region -- Public Methods --
    /// <summary>執行全部 IPC 情境，不接觸實際 App 的管線；回傳零代表通過。</summary>
    /// <returns>程序結束碼。</returns>
    public static int Main()
    {
        SelfNamedPipeManager manager = SelfNamedPipeManager.Instance;
        using (AutoResetEvent opened = new AutoResetEvent(false))
        {
            manager.OpenWindowEvent = () => opened.Set();
            manager.Listen();
            // EOF without the Disconnect command must permit the next connection.
            using (NamedPipeClientStream client = Connect()) { }
            manager.Send(SelfNamedPipeAction.OpenWindow);
            Require(opened.WaitOne(2000));

            // Closing while a connected client sends no data must cancel ReadLine.
            using (NamedPipeClientStream client = Connect())
            {
                Stopwatch timer = Stopwatch.StartNew();
                manager.Close();
                Require(timer.ElapsedMilliseconds < 2000);
            }

            // Closing while waiting for a connection must also complete.
            manager.Listen();
            Thread.Sleep(30);
            manager.Close();
            for (int index = 0; index < 20; ++index) { manager.Listen(); manager.Close(); }
            manager.Listen();
            using (NamedPipeClientStream client = Connect())
            using (StreamWriter writer = new StreamWriter(client)) { writer.WriteLine("999"); }
            manager.Send(SelfNamedPipeAction.OpenWindow);
            Require(opened.WaitOne(2000));
            manager.Close();
        }
        Console.WriteLine("Pipe EOF reconnects, blocked I/O cancels, and rapid restart completes");
        return 0;
    }
    #endregion

    #region -- Private Methods --
    /// <summary>連線至本次測試專用的伺服器，回傳由呼叫者釋放的用戶端。</summary>
    /// <returns>已連線的輸出管線。</returns>
    private static NamedPipeClientStream Connect()
    {
        NamedPipeClientStream client = new NamedPipeClientStream(".", "_MonitorEdgeTunnelAppNamedPipe_", PipeDirection.Out);
        client.Connect(2000);
        return client;
    }

    /// <summary>確保情境條件成立，失敗時中止測試。</summary>
    /// <param name="condition">待驗證的條件。</param>
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Pipe regression failed");
    }
    #endregion
}
