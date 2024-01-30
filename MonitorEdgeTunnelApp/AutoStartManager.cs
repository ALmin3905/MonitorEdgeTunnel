using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MonitorEdgeTunnelApp
{
    /// <summary>
    /// 開機自動啟動管理工具
    /// </summary>
    public static class AutoStartManager
    {
        /// <summary>
        /// 自動啟動指令字串
        /// </summary>
        public const string AUTORUN_CMD_STR = "/autorun";

        /// <summary>
        /// 快捷名稱
        /// </summary>
        private const string SHORTCUT_NAME = "MonitorEdgeHelper";

        /// <summary>
        /// System Startup 路徑
        /// </summary>
        private static string SystemStartupFolderPath => Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        /// <summary>
        /// 應用程式路徑
        /// </summary>
        private static string AppFullPath => Process.GetCurrentProcess().MainModule.FileName;

        /// <summary>
        /// 設定開機自動啟動
        /// </summary>
        /// <param name="onOff">是否自動啟動</param>
        /// <returns>是否成功</returns>
        public static bool SetAutorun(bool onOff)
        {
            // 刪掉所有Autorun Shortcut
            RemoveAllAutorunShortcuts();

            // Autorun false就返回true，否則建立新的shortcut並返回結果
            return !onOff || ShortcutManager.CreateShortcut(SystemStartupFolderPath, SHORTCUT_NAME, AppFullPath, AUTORUN_CMD_STR);
        }

        /// <summary>
        /// 是否開機自動啟動
        /// </summary>
        /// <returns>是否開機自動啟動</returns>
        public static bool IsAutorun()
        {
            return GetAutorunShortcuts().Count != 0;
        }

        /// <summary>
        /// 取得所有應用程式開機自動啟動的shortcut清單 (通常只有1個或沒有)
        /// </summary>
        /// <returns>應用程式開機自動啟動shortcut清單</returns>
        private static List<string> GetAutorunShortcuts()
        {
            // 返回的結果
            List<string> retList = new List<string>();

            // 取得Startup Folder Files
            string[] files = Directory.GetFiles(SystemStartupFolderPath, "*.lnk");

            if (files == null || files.Length == 0)
            {
                return retList;
            }

            // 確認target path是否正確
            foreach (string file in files)
            {
                string targetPath = ShortcutManager.GetShrotcutTargetPath(file);
                if (targetPath != null && targetPath == AppFullPath)
                {
                    retList.Add(file);
                }
            }

            return retList;
        }

        /// <summary>
        /// 移除所有應用程式開機自動啟動的shortcuts
        /// </summary>
        private static void RemoveAllAutorunShortcuts()
        {
            foreach (string shortcut in GetAutorunShortcuts())
            {
                File.Delete(shortcut);
            }
        }
    }
}
