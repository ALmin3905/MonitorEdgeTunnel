using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace MonitorEdgeTunnelApp
{
    public static class Logger
    {
        private static readonly string LogFilePath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Log", "log.txt");

        // 建立 Serilog logger 實例
        private static readonly ILogger logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(LogFilePath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        public static void InitMonitorEdgeTunnelLogger()
        {
            MonitorEdgeTunnel.Instance.SetLogCallback((level, message) =>
            {
                Log((int)level, message);
            });
        }

        public static void DeinitMonitorEdgeTunnelLogger()
        {
            MonitorEdgeTunnel.Instance.SetLogCallback(null);
        }

        public static void Log(int level, string message)
        {
            // 根據 level 決定 log 級別
            switch (level)
            {
                case 0: // Debug
                    logger.Debug(message);
                    break;
                case 1: // Info
                    logger.Information(message);
                    break;
                case 2: // Warning
                    logger.Warning(message);
                    break;
                case 3: // Error
                    logger.Error(message);
                    break;
                case 4: // Fatal
                    logger.Fatal(message);
                    break;
                default:
                    break;
            }
        }
    }
}
