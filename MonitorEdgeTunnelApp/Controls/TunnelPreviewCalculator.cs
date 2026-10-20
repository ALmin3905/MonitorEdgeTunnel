using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitorEdgeTunnelApp.Controls
{
    /// <summary>保存一筆編輯規則的實體範圍、投影比例及驗證結果，不持有 UI 或原生服務。</summary>
    internal sealed class TunnelPreviewSegment
    {
        /// <summary>規則在來源快照中的位置；-1 代表強制禁行的剩餘區段。</summary>
        public int RuleIndex { get; internal set; } = -1;
        /// <summary>所屬螢幕在來源快照中的位置；-1 代表找不到螢幕。</summary>
        public int MonitorIndex { get; internal set; } = -1;
        /// <summary>區段所在的螢幕邊緣。</summary>
        public EdgeType Edge { get; internal set; }
        /// <summary>包含在範圍內的第一個桌面像素座標。</summary>
        public long From { get; internal set; }
        /// <summary>包含在範圍內的最後一個桌面像素座標。</summary>
        public long To { get; internal set; }
        /// <summary>起點在完整螢幕邊緣上的比例。</summary>
        public double StartFraction { get; internal set; }
        /// <summary>最後一個像素之後的比例，確保單點禁行仍有一個像素寬。</summary>
        public double EndFraction { get; internal set; }
        /// <summary>是否具有完整且未超出螢幕的來源幾何。</summary>
        public bool HasGeometry { get; internal set; }
        /// <summary>是否為禁止通行規則。</summary>
        public bool IsForbidden { get; internal set; }
        /// <summary>目標規則在來源快照中的位置；-1 代表沒有可用目標。</summary>
        public int TargetIndex { get; internal set; } = -1;
        /// <summary>重疊區段的顯示分層，不改變規則的實際優先權。</summary>
        public int Lane { get; internal set; }
        /// <summary>無法套用的原因；空字串代表沒有發現錯誤。</summary>
        public string Error { get; internal set; } = string.Empty;
        /// <summary>重疊時的優先權說明；空字串代表沒有重疊警告。</summary>
        public string Warning { get; internal set; } = string.Empty;
    }

    /// <summary>依原生通道語意計算預覽區段；只處理值快照，可在任意執行緒使用。</summary>
    internal static class TunnelPreviewCalculator
    {
        #region -- Public Methods --

        /// <summary>計算各規則的來源範圍、目標及重疊順序，不修改輸入或套用設定。</summary>
        /// <param name="monitors">目前螢幕的實體像素邊界快照。</param>
        /// <param name="rules">依目前清單順序排列的規則快照。</param>
        /// <returns>與 rules 順序一致的計算結果；無效規則保留錯誤供介面提示。</returns>
        public static IReadOnlyList<TunnelPreviewSegment> Calculate(IReadOnlyList<MonitorInfo> monitors, IReadOnlyList<TunnelInfo> rules)
        {
            if (monitors == null) throw new ArgumentNullException(nameof(monitors));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            List<TunnelPreviewSegment> result = new List<TunnelPreviewSegment>(rules.Count);
            Dictionary<int, int> ids = new Dictionary<int, int>();
            HashSet<int> duplicates = new HashSet<int>();
            for (int i = 0; i < rules.Count; ++i)
            {
                if (ids.ContainsKey(rules[i].id)) duplicates.Add(rules[i].id);
                else ids.Add(rules[i].id, i);
                result.Add(CalculateRange(monitors, rules[i], i));
            }
            for (int i = 0; i < rules.Count; ++i)
            {
                TunnelInfo rule = rules[i];
                TunnelPreviewSegment segment = result[i];
                if (duplicates.Contains(rule.id)) segment.Error = "通道 ID 重複";
                if (segment.Error.Length > 0 || rule.relativeID == -1) continue;
                if (rule.relativeID < -1 || rule.relativeID == rule.id || !ids.TryGetValue(rule.relativeID, out int target) ||
                    duplicates.Contains(rule.relativeID))
                    segment.Error = "目標通道不存在、重複或指向自己";
                else
                    segment.TargetIndex = target;
            }
            string[] errors = result.Select(segment => segment.Error).ToArray();
            for (int i = 0; i < result.Count; ++i)
            {
                TunnelPreviewSegment segment = result[i];
                if (segment.TargetIndex >= 0 && errors[segment.TargetIndex].Length > 0)
                {
                    segment.Error = "目標通道的設定無效";
                    segment.TargetIndex = -1;
                }
                HashSet<int> occupied = new HashSet<int>();
                for (int earlier = 0; earlier < i; ++earlier)
                {
                    TunnelPreviewSegment previous = result[earlier];
                    if (!Overlaps(previous, segment)) continue;
                    occupied.Add(previous.Lane);
                    if (segment.Error.Length == 0 && previous.Error.Length == 0 && segment.Warning.Length == 0)
                        segment.Warning = "與通道 " + rules[earlier].id + " 重疊；重疊部分由前面的規則優先匹配";
                }
                while (occupied.Contains(segment.Lane)) ++segment.Lane;
            }
            return result;
        }

        /// <summary>計算明確規則以外的剩餘邊緣，供強制禁行的背景標示使用。</summary>
        /// <param name="monitors">與 segments 使用相同順序的螢幕快照。</param>
        /// <param name="segments">已完成驗證的明確規則區段。</param>
        /// <returns>未被有效明確規則覆蓋的禁行區段，不建立實際通道 ID。</returns>
        public static IReadOnlyList<TunnelPreviewSegment> CalculateForbiddenGaps(IReadOnlyList<MonitorInfo> monitors,
            IReadOnlyList<TunnelPreviewSegment> segments)
        {
            List<TunnelPreviewSegment> result = new List<TunnelPreviewSegment>();
            for (int index = 0; index < monitors.Count; ++index)
            {
                MonitorInfo monitor = monitors[index];
                if (monitor.right < monitor.left || monitor.bottom < monitor.top) continue;
                foreach (EdgeType edge in new[] { EdgeType.Left, EdgeType.Right, EdgeType.Top, EdgeType.Bottom })
                {
                    bool vertical = edge == EdgeType.Left || edge == EdgeType.Right;
                    long first = vertical ? monitor.top : monitor.left;
                    long last = vertical ? monitor.bottom : monitor.right;
                    long cursor = first;
                    foreach (TunnelPreviewSegment segment in segments.Where(item => item.MonitorIndex == index && item.Edge == edge &&
                        item.HasGeometry && item.Error.Length == 0).OrderBy(item => item.From))
                    {
                        if (segment.From > cursor) result.Add(CreateGap(index, edge, cursor, segment.From - 1, first, last));
                        cursor = Math.Max(cursor, segment.To + 1);
                    }
                    if (cursor <= last) result.Add(CreateGap(index, edge, cursor, last, first, last));
                }
            }
            return result;
        }

        #endregion

        #region -- Private Methods --

        /// <summary>依 Full、Relative 或 Customize 語意計算單筆規則的含尾端範圍。</summary>
        /// <param name="monitors">螢幕快照。</param>
        /// <param name="rule">待驗證的規則值。</param>
        /// <param name="index">規則在清單內的位置。</param>
        /// <returns>含幾何與來源驗證結果的區段；全程使用 long 避免整數溢位。</returns>
        private static TunnelPreviewSegment CalculateRange(IReadOnlyList<MonitorInfo> monitors, TunnelInfo rule, int index)
        {
            TunnelPreviewSegment result = new TunnelPreviewSegment { RuleIndex = index, Edge = rule.edgeType, IsForbidden = rule.relativeID == -1 };
            for (int i = 0; i < monitors.Count; ++i)
                if (monitors[i].id == rule.displayID) { result.MonitorIndex = i; break; }
            if (result.MonitorIndex < 0) { result.Error = "找不到所屬螢幕"; return result; }
            MonitorInfo monitor = monitors[result.MonitorIndex];
            if (monitor.right < monitor.left || monitor.bottom < monitor.top || (int)rule.edgeType < 0 || (int)rule.edgeType > 3)
            { result.Error = "螢幕邊界或邊緣類型無效"; return result; }
            bool vertical = rule.edgeType == EdgeType.Left || rule.edgeType == EdgeType.Right;
            long first = vertical ? monitor.top : monitor.left;
            long last = vertical ? monitor.bottom : monitor.right;
            long from = first;
            long to = last;
            switch (rule.rangeType)
            {
                case RangeType.Full: break;
                case RangeType.Relative: from += rule.from; to -= rule.to; break;
                case RangeType.Customize: from += rule.from; to = from + rule.to; break;
                default: result.Error = "通道範圍類型無效"; return result;
            }
            if (from < first || to > last || from > to)
            { result.Error = "範圍超出螢幕邊緣或起點大於終點"; return result; }
            result.From = from;
            result.To = to;
            result.StartFraction = (double)(from - first) / (last - first + 1);
            result.EndFraction = (double)(to - first + 1) / (last - first + 1);
            result.HasGeometry = true;
            if (rule.id < 0 || rule.id > 100) result.Error = "通道 ID 必須介於 0 與 100";
            else if (rule.relativeID >= 0 && from == to) result.Error = "傳送通道不能只有一個點";
            return result;
        }

        /// <summary>判斷兩筆可定位區段是否在同一邊緣共享至少一個像素。</summary>
        /// <param name="first">較早的規則區段。</param>
        /// <param name="second">較晚的規則區段。</param>
        /// <returns>兩者幾何有效且含尾端範圍交疊時回傳 true。</returns>
        private static bool Overlaps(TunnelPreviewSegment first, TunnelPreviewSegment second)
        {
            return first.HasGeometry && second.HasGeometry && first.MonitorIndex == second.MonitorIndex && first.Edge == second.Edge &&
                first.From <= second.To && second.From <= first.To;
        }

        /// <summary>建立無使用者規則 ID 的剩餘禁行區段。</summary>
        /// <param name="index">螢幕在快照中的位置。</param>
        /// <param name="edge">邊緣類型。</param>
        /// <param name="from">含尾端範圍起點。</param>
        /// <param name="to">含尾端範圍終點。</param>
        /// <param name="first">整條邊緣的第一個像素。</param>
        /// <param name="last">整條邊緣的最後一個像素。</param>
        /// <returns>已正規化並標為禁行的剩餘區段。</returns>
        private static TunnelPreviewSegment CreateGap(int index, EdgeType edge, long from, long to, long first, long last)
        {
            return new TunnelPreviewSegment { MonitorIndex = index, Edge = edge, From = from, To = to, IsForbidden = true,
                HasGeometry = true, StartFraction = (double)(from - first) / (last - first + 1), EndFraction = (double)(to - first + 1) / (last - first + 1) };
        }

        #endregion
    }
}
