using System.Collections.Generic;
using System.Linq;

namespace MonitorEdgeTunnelApp.Editing
{
    /// <summary>保存與 UI 物件分離的設定值，用來比較草稿、已套用與已儲存內容。</summary>
    internal sealed class TunnelSettingsSnapshot
    {
        /// <summary>依優先順序複製的通道值；不得由呼叫端修改。</summary>
        internal TunnelInfo[] Rules { get; }
        /// <summary>是否封鎖未設定的邊緣。</summary>
        internal bool ForceForbid { get; }

        #region -- Public Methods --

        /// <summary>複製規則與邊緣選項，不持有編輯事件，可於任意執行緒使用獨立快照。</summary>
        /// <param name="rules">依目前優先順序排列的規則。</param>
        /// <param name="forceForbid">是否封鎖其他邊緣。</param>
        internal TunnelSettingsSnapshot(IEnumerable<TunnelInfo> rules, bool forceForbid)
        {
            Rules = rules.ToArray();
            ForceForbid = forceForbid;
        }

        /// <summary>比較所有原生設定欄位與順序，不把執行或 UI 選取狀態視為編輯。</summary>
        /// <param name="other">另一份設定；null 永遠不同。</param>
        /// <returns>規則及邊緣設定相同時為 true。</returns>
        internal bool Matches(TunnelSettingsSnapshot other) => other != null && ForceForbid == other.ForceForbid && Rules.SequenceEqual(other.Rules);

        #endregion
    }
}
