using System;
using System.Reflection;
using System.Runtime.InteropServices;
using MonitorEdgeTunnelApp;

namespace MonitorEdgeTunnelApp
{
    /// <summary>隔離測試使用的日誌接收者，不啟動其他服務。</summary>
    public static class Logger
    {
        #region -- Public Methods --
        /// <summary>接受日誌輸入，測試中不產生額外輸出。</summary>
        /// <param name="level">日誌等級。</param>
        /// <param name="message">日誌內容。</param>
        public static void Log(int level, string message) { }
        #endregion
    }
}

/// <summary>驗證受控代理對原生一位元組 bool 的解讀。</summary>
public static class InteropTests
{
    #region -- Public Methods --
    /// <summary>用受控原生回傳暫存器驗證 false，並檢查所有 bool 入口宣告；回傳零代表通過。</summary>
    /// <returns>程序結束碼。</returns>
    public static int Main()
    {
        MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
        MonitorEdgeTunnel.DisplayChangedCallbackDel callback = () => { };
        Require(!service.SaveSetting());
        Require(!service.AddDisplayChangedCallback(callback));
        Require(!service.RemoveDisplayChangedCallback(callback));
        foreach (MethodInfo method in typeof(MonitorEdgeTunnel).GetMethods(BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (method.ReturnType == typeof(bool) && method.GetCustomAttribute<DllImportAttribute>() != null)
            {
                MarshalAsAttribute attribute = method.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>();
                Require(attribute != null && attribute.Value == UnmanagedType.I1);
            }
        }
        Console.WriteLine("Native false remains false; all native bool returns marshal as I1");
        return 0;
    }
    #endregion

    #region -- Private Methods --
    /// <summary>確認原生結果符合預期，失敗時中止測試。</summary>
    /// <param name="condition">待驗證的條件。</param>
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Native bool regression failed");
    }
    #endregion
}
