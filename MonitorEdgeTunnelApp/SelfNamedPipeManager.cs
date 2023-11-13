using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace MonitorEdgeTunnelApp
{
    public delegate void OpenWindowEventHandler();

    public enum SelfNamedPipeAction : int
    {
        None,
        Disconnect,
        Close,
        OpenWindow
    }

    public class SelfNamedPipeManager
    {
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
