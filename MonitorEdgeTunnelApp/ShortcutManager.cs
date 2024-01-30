using System;
using System.IO;
using IWshRuntimeLibrary;

namespace MonitorEdgeTunnelApp
{
    /// <summary>
    /// 快捷相關工具
    /// </summary>
    public static class ShortcutManager
    {
        /// <summary>
        /// 建立快捷
        /// </summary>
        /// <param name="directory">路徑</param>
        /// <param name="shortcutName">快捷名稱</param>
        /// <param name="targetPath">快捷目標路徑</param>
        /// <param name="args">快捷命令</param>
        /// <returns>是否成功</returns>
        public static bool CreateShortcut(string directory, string shortcutName, string targetPath, string args = "")
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    _ = Directory.CreateDirectory(directory);
                }

                string shortcutPath = Path.Combine(directory, string.Format("{0}.lnk", shortcutName));
                WshShell shell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Arguments = args;
                shortcut.IconLocation = targetPath;
                shortcut.Save();

                return true;
            }
            catch (Exception)
            {
                // TODO: 接上Log
            }

            return false;
        }

        /// <summary>
        /// 取得快捷目標路徑(沒找到會回傳null)
        /// </summary>
        /// <param name="shortcutFullPath">快捷完整路徑</param>
        /// <returns>快捷目標路徑</returns>
        public static string GetShrotcutTargetPath(string shortcutFullPath)
        {
            try
            {
                if (!System.IO.File.Exists(shortcutFullPath))
                {
                    throw new FileNotFoundException();
                }

                WshShell shell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutFullPath);

                return shortcut.TargetPath;
            }
            catch (Exception)
            {
                // TODO: 接上Log
            }

            return null;
        }

        /// <summary>
        /// 取得快捷目標路徑(沒找到會回傳null)
        /// </summary>
        /// <param name="directory">路徑</param>
        /// <param name="shortcutName">快捷名稱</param>
        /// <returns>快捷目標路徑</returns>
        public static string GetShrotcutTargetPath(string directory, string shortcutName)
        {
            return GetShrotcutTargetPath(Path.Combine(directory, string.Format("{0}.lnk", shortcutName)));
        }
    }
}
