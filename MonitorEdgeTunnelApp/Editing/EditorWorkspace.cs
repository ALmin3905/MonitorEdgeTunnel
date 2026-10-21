using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MonitorEdgeTunnelApp.Editing
{
    /// <summary>由應用程式持有各螢幕配置的草稿；所有存取限 UI 執行緒。</summary>
    internal sealed class EditorWorkspace
    {
        /// <summary>以完整桌面佈局識別的編輯工作階段。</summary>
        private readonly Dictionary<string, EditorSession> _sessions = new Dictionary<string, EditorSession>();
        /// <summary>任一配置有未儲存內容時，結束應用程式前應提醒。</summary>
        internal bool HasUnsavedChanges => _sessions.Values.Any(session => session.HasUnsavedChanges);

        #region -- Public Methods --

        /// <summary>切換至目前配置並讀取實際服務基準，已存在的草稿保留原物件。</summary>
        /// <param name="monitors">目前螢幕邊界及縮放資料。</param>
        /// <param name="settings">目前服務設定，不重新載入檔案。</param>
        /// <returns>這組配置可繼續編輯的工作階段。</returns>
        internal EditorSession Activate(IEnumerable<MonitorInfo> monitors, TunnelSettingsSnapshot settings)
        {
            string key = string.Join(";", monitors.Select(monitor => string.Join(",", new[] {
                monitor.id.ToString(CultureInfo.InvariantCulture), monitor.left.ToString(CultureInfo.InvariantCulture),
                monitor.top.ToString(CultureInfo.InvariantCulture), monitor.right.ToString(CultureInfo.InvariantCulture),
                monitor.bottom.ToString(CultureInfo.InvariantCulture), monitor.scaling.ToString("R", CultureInfo.InvariantCulture) })));
            if (!_sessions.TryGetValue(key, out EditorSession session))
                _sessions.Add(key, session = new EditorSession(settings));
            else session.ObserveApplied(settings);
            return session;
        }

        /// <summary>成功儲存全部原生設定後，更新每組配置的檔案基準。</summary>
        internal void MarkSaved()
        {
            foreach (EditorSession session in _sessions.Values) session.MarkSaved();
        }

        /// <summary>全域設定檔成功重新載入後，保留其他配置草稿並要求下次重新確認基準。</summary>
        /// <param name="active">正在重新載入的工作階段。</param>
        /// <param name="settings">目前配置重新載入後的原生值。</param>
        internal void Reload(EditorSession active, TunnelSettingsSnapshot settings)
        {
            foreach (EditorSession session in _sessions.Values) session.NeedsReload = true;
            active.Replace(settings);
        }

        #endregion
    }
}
