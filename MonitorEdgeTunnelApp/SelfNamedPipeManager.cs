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

        private static readonly Lazy<SelfNamedPipeManager> lazy = new Lazy<SelfNamedPipeManager>(() => new SelfNamedPipeManager());

        private SelfNamedPipeManager()
        {

        }

        public static SelfNamedPipeManager Instance
        {
            get
            {
                return lazy.Value;
            }
        }

        /// <summary>
        /// 開啟視窗事件
        /// </summary>
        public OpenWindowEventHandler OpenWindowEvent { get; set; }

        private Thread thread;

        private bool isThreadRunning = false;

        private void ListenThreadFunc()
        {
            isThreadRunning = true;

            while (isThreadRunning)
            {
                using (NamedPipeServerStream server = new NamedPipeServerStream(NamedPipeName))
                {
                    server.WaitForConnection();

                    using (StreamReader sr = new StreamReader(server))
                    {
                        while (ProcessCmd(sr.ReadLine()))
                        {
                            Thread.Sleep(1);
                        }
                    }
                }
            }
        }

        private bool ProcessCmd(string cmd)
        {
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
                    throw new NotImplementedException("ProcessCmd Failed");
            }
        }

        private void SendCmd(SelfNamedPipeAction action)
        {
            using (NamedPipeClientStream client = new NamedPipeClientStream(NamedPipeName))
            {
                // 只catch超時，其他exception就讓它爆開
                try
                {
                    client.Connect(1000);
                }
                catch (TimeoutException)
                {
                    return;
                }

                using (StreamWriter sw = new StreamWriter(client))
                {
                    sw.WriteLine(Enum.GetName(typeof(SelfNamedPipeAction), action));
                    sw.WriteLine(Enum.GetName(typeof(SelfNamedPipeAction), SelfNamedPipeAction.Disconnect));
                }
            }
        }

        public void Listen()
        {
            if (isThreadRunning)
            {
                return;
            }

            thread = new Thread(ListenThreadFunc);
            thread.Start();
        }

        public void Close()
        {
            if (!isThreadRunning)
            {
                return;
            }

            SendCmd(SelfNamedPipeAction.Close);
            thread.Join();
        }

        public void Send(SelfNamedPipeAction action)
        {
            SendCmd(action);
        }
    }
}
