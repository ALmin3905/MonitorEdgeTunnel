using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace MonitorEdgeTunnelApp
{
    /// <summary>
    /// 開啟視窗事件delegate
    /// </summary>
    public delegate void OpenWindowEventHandler();

    /// <summary>
    /// 指令
    /// </summary>
    public enum SelfNamedPipeAction : int
    {
        /// <summary>
        /// Nothing
        /// </summary>
        None,
        /// <summary>
        /// 中斷連線
        /// </summary>
        Disconnect,
        /// <summary>
        /// 關閉SelfNamedPipe
        /// </summary>
        Close,
        /// <summary>
        /// 開啟視窗
        /// </summary>
        OpenWindow
    }

    /// <summary>
    /// 與自己(同一應用程式)溝通的行程間通訊工具，Client以指令的方式來使Server做出動作。
    /// <para>TODO: Server只能一個，目前沒處理多個Process監聽的風險，外部須自行處理(EX: 使用系統鎖保護僅能一個Process監聽)</para>
    /// </summary>
    public class SelfNamedPipeManager
    {
        /// <summary>
        /// NamedPipe名稱
        /// </summary>
        private const string NamedPipeName = "_MonitorEdgeTunnelAppNamedPipe_";

        /// <summary>延遲建立共用 IPC 管理員。</summary>
        private static readonly Lazy<SelfNamedPipeManager> lazy = new Lazy<SelfNamedPipeManager>(() => new SelfNamedPipeManager());

        /// <summary>保護監聽執行緒及目前管線的生命週期。</summary>
        private readonly object syncRoot = new object();

        /// <summary>接收指令的背景執行緒。</summary>
        private Thread thread;

        /// <summary>跨執行緒可見的監聽意圖，Listen 在啟動執行緒前設定。</summary>
        private volatile bool isThreadRunning = false;

        /// <summary>供 Close 取消等待連線或讀取的目前伺服器。</summary>
        private NamedPipeServerStream activeServer;

        /// <summary>取得共用 IPC 管理員。</summary>
        public static SelfNamedPipeManager Instance => lazy.Value;

        /// <summary>
        /// 開啟視窗事件
        /// <para>由背景執行緒呼叫；訂閱者應非同步派送 UI 工作，不可同步等待 UI 結束流程。</para>
        /// </summary>
        public OpenWindowEventHandler OpenWindowEvent { get; set; }

        #region -- Public Methods --

        /// <summary>啟動背景監聽；重複呼叫不會建立第二個接收執行緒。</summary>
        public void Listen()
        {
            lock (syncRoot)
            {
                if (thread != null && thread.IsAlive)
                    return;

                isThreadRunning = true;
                thread = new Thread(ListenThreadFunc) { IsBackground = true, Name = "MonitorEdgeTunnel IPC" };
                try
                {
                    thread.Start();
                }
                catch
                {
                    isThreadRunning = false;
                    thread = null;
                    throw;
                }
            }
        }

        /// <summary>取消等待連線或讀取，並等待接收執行緒結束；不需透過管線向自己傳送停止指令。</summary>
        public void Close()
        {
            Thread listener;
            NamedPipeServerStream server;
            lock (syncRoot)
            {
                isThreadRunning = false;
                listener = thread;
                server = activeServer;
            }

            server?.Dispose();
            if (listener != null && listener != Thread.CurrentThread)
                listener.Join();

            lock (syncRoot)
            {
                if (thread == listener && (listener == null || !listener.IsAlive))
                    thread = null;
            }
        }

        /// <summary>連線至現有應用程式並傳送一個指令，完成後要求結束該次連線。</summary>
        /// <param name="action">要傳送的指令；連線逾時或對端斷線時不產生未處理例外。</param>
        public void Send(SelfNamedPipeAction action)
        {
            SendCmd(action);
        }

        #endregion

        #region -- Private Methods --

        /// <summary>建立單一實例；實際監聽由 Listen 啟動。</summary>
        private SelfNamedPipeManager()
        {
        }

        /// <summary>在背景執行緒接收指令；EOF 與管線中斷只結束目前連線，Close 則取消整個監聽。</summary>
        private void ListenThreadFunc()
        {
            try
            {
                while (isThreadRunning)
                {
                    using (NamedPipeServerStream server = new NamedPipeServerStream(
                        NamedPipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        lock (syncRoot)
                        {
                            if (!isThreadRunning)
                                return;
                            activeServer = server;
                        }

                        try
                        {
                            server.WaitForConnection();
                            using (StreamReader reader = new StreamReader(server))
                            {
                                while (isThreadRunning && ProcessCmd(reader.ReadLine()))
                                {
                                }
                            }
                        }
                        catch (IOException)
                        {
                            // 對端非正常斷線或 Close 取消 I/O，下一輪依監聽意圖決定是否重連。
                        }
                        catch (ObjectDisposedException) when (!isThreadRunning)
                        {
                            // Close 會釋放非同步管線，取消等待連線或讀取。
                        }
                        finally
                        {
                            lock (syncRoot)
                                activeServer = null;
                        }
                    }
                }
            }
            catch (IOException exception)
            {
                Logger.Log(3, "Named pipe listener failed: " + exception.Message);
            }
            finally
            {
                isThreadRunning = false;
            }
        }

        /// <summary>執行收到的指令；EOF、斷線或未知數值指令會結束目前連線。</summary>
        /// <param name="cmd">接收到的一行文字，null 代表 EOF。</param>
        /// <returns>是否繼續讀取目前連線。</returns>
        private bool ProcessCmd(string cmd)
        {
            if (cmd == null)
                return false;

            switch (cmd.ToEnum(SelfNamedPipeAction.None))
            {
                case SelfNamedPipeAction.None:
                    return true;
                case SelfNamedPipeAction.Disconnect:
                    return false;
                case SelfNamedPipeAction.Close:
                    isThreadRunning = false;
                    return false;
                case SelfNamedPipeAction.OpenWindow:
                    OpenWindowEvent?.Invoke();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>以短連線傳送指令；允許對端在連線或寫入期間結束。</summary>
        /// <param name="action">要傳送的單一指令，後續會自動附加 Disconnect。</param>
        private void SendCmd(SelfNamedPipeAction action)
        {
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", NamedPipeName, PipeDirection.Out))
                {
                    client.Connect(1000);
                    using (StreamWriter writer = new StreamWriter(client))
                    {
                        writer.WriteLine(Enum.GetName(typeof(SelfNamedPipeAction), action));
                        writer.WriteLine(Enum.GetName(typeof(SelfNamedPipeAction), SelfNamedPipeAction.Disconnect));
                    }
                }
            }
            catch (TimeoutException)
            {
                // 已有實例可能正在結束，連線逾時時不再傳送。
            }
            catch (IOException)
            {
                // 對端可能在寫入前結束或取消連線。
            }
        }

        #endregion
    }
}
