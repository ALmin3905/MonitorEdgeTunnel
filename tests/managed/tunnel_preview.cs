using MonitorEdgeTunnelApp;
using MonitorEdgeTunnelApp.Controls;
using System;
using System.Linq;

namespace MonitorEdgeTunnelApp
{
    /// <summary>純計算測試的日誌替身，不建立原生服務。</summary>
    public static class Logger
    {
        #region -- Public Methods --
        /// <summary>接受 level 與 message，測試中不輸出日誌或產生副作用。</summary>
        /// <param name="level">日誌等級。</param>
        /// <param name="message">日誌內容。</param>
        public static void Log(int level, string message) { }
        #endregion
    }
}

/// <summary>使用正式資料合約驗證原生範圍語意及預覽專用的驗證與禁行區段。</summary>
public static class TunnelPreviewTests
{
    #region -- Public Methods --

    /// <summary>執行不建立視窗、不載入 DLL 的純值測試。</summary>
    /// <returns>全部斷言通過時回傳 0；失敗時由例外終止程序。</returns>
    public static int Main()
    {
        TestNativeRangeSemantics();
        TestInvalidRules();
        TestTargets();
        TestOverlapPriority();
        TestForbiddenGaps();
        TestExtremeCoordinates();
        Console.WriteLine("6/6 tunnel preview calculation cases passed");
        return 0;
    }

    #endregion

    #region -- Private Methods --

    /// <summary>驗證四邊及三種範圍模式符合原生包含尾端的算法，不受 DPI metadata 影響。</summary>
    private static void TestNativeRangeSemantics()
    {
        MonitorInfo monitor = Monitor();
        foreach (EdgeType edge in new[] { EdgeType.Left, EdgeType.Right, EdgeType.Top, EdgeType.Bottom })
        {
            foreach (RangeType type in new[] { RangeType.Full, RangeType.Relative, RangeType.Customize })
            {
                TunnelInfo rule = Rule(0, edge, type, 10, type == RangeType.Relative ? 20 : 70);
                TunnelPreviewSegment result = TunnelPreviewCalculator.Calculate(new[] { monitor }, new[] { rule })[0];
                Require(result.Error.Length == 0 && result.HasGeometry, "valid native range");
                Require(result.From == (type == RangeType.Full ? -100 : -90) && result.To == (type == RangeType.Full ? 0 : -20), "native inclusive endpoints");
                Require(result.EndFraction > result.StartFraction && result.EndFraction <= 1, "normalized geometry");
                monitor.scaling = 2;
                TunnelPreviewSegment changed = TunnelPreviewCalculator.Calculate(new[] { monitor }, new[] { rule })[0];
                Require(changed.StartFraction == result.StartFraction && changed.EndFraction == result.EndFraction, "no extra DPI scaling");
            }
        }
        TunnelInfo point = Rule(0, EdgeType.Left, RangeType.Customize, 100, 0);
        TunnelPreviewSegment single = TunnelPreviewCalculator.Calculate(new[] { monitor }, new[] { point })[0];
        Require(single.From == 0 && single.To == 0 && single.EndFraction == 1 && single.StartFraction < 1, "one-pixel blocking remains visible");
    }

    /// <summary>驗證無效資料保留原因、不夾成有效範圍，也不修改輸入。</summary>
    private static void TestInvalidRules()
    {
        TunnelInfo[] invalid = {
            Rule(-1), Rule(101), Rule(0, (EdgeType)4), Rule(0, EdgeType.Left, (RangeType)8),
            Rule(0, EdgeType.Left, RangeType.Relative, -1, 0), Rule(0, EdgeType.Left, RangeType.Customize, 99, 3),
            Rule(0, EdgeType.Left, RangeType.Customize, 10, -1)
        };
        foreach (TunnelInfo rule in invalid)
            Require(TunnelPreviewCalculator.Calculate(new[] { Monitor() }, new[] { rule })[0].Error.Length > 0, "invalid source reported");
        TunnelInfo missing = Rule(0);
        missing.displayID = 7;
        Require(!TunnelPreviewCalculator.Calculate(new[] { Monitor() }, new[] { missing })[0].HasGeometry, "missing display has no invented location");
        TunnelInfo full = Rule(0, EdgeType.Left, RangeType.Full, int.MinValue, int.MaxValue);
        Require(TunnelPreviewCalculator.Calculate(new[] { Monitor() }, new[] { full })[0].Error.Length == 0 && full.from == int.MinValue,
            "full range ignores unused offsets and preserves input");
        Require(TunnelPreviewCalculator.Calculate(new MonitorInfo[0], new TunnelInfo[0]).Count == 0, "empty layout");
    }

    /// <summary>驗證方向由相依 ID 決定，支援非連續 ID 並拒絕遺失、自指、重複與無效目標。</summary>
    private static void TestTargets()
    {
        TunnelInfo source = Rule(7);
        source.relativeID = 3;
        TunnelInfo target = Rule(3, EdgeType.Top);
        var result = TunnelPreviewCalculator.Calculate(new[] { Monitor() }, new[] { source, target });
        Require(result[0].TargetIndex == 1 && result[1].TargetIndex == -1 && result[1].IsForbidden, "direction does not imply reverse rule");
        foreach (int id in new[] { 7, 99, -2 })
        {
            source.relativeID = id;
            Require(TunnelPreviewCalculator.Calculate(new[] { Monitor() }, new[] { source, target })[0].Error.Length > 0, "invalid target");
        }
        source.relativeID = 3;
        Require(TunnelPreviewCalculator.Calculate(new[] { Monitor() }, new[] { source, target, target }).All(item => item.Error.Length > 0), "duplicate target IDs");
        target.from = 200; target.rangeType = RangeType.Relative;
        Require(TunnelPreviewCalculator.Calculate(new[] { Monitor() }, new[] { source, target })[0].Error.Length > 0, "target geometry invalid");
        source.from = 100; source.to = 0; source.rangeType = RangeType.Customize;
        Require(TunnelPreviewCalculator.Calculate(new[] { Monitor() }, new[] { source, Rule(3) })[0].Error.Length > 0, "transport cannot use one point");
    }

    /// <summary>驗證重疊優先權依清單順序，圖形分層不改變原始規則 ID 或範圍。</summary>
    private static void TestOverlapPriority()
    {
        var result = TunnelPreviewCalculator.Calculate(new[] { Monitor() }, new[] {
            Rule(7, EdgeType.Left, RangeType.Customize, 10, 20), Rule(3, EdgeType.Left, RangeType.Customize, 20, 20),
            Rule(9, EdgeType.Left, RangeType.Customize, 60, 10), Rule(4, EdgeType.Top, RangeType.Full)
        });
        Require(result[0].Lane == 0 && result[1].Lane == 1 && result[2].Lane == 0 && result[3].Lane == 0, "only intersecting same-edge intervals need another lane");
        Require(result[0].Warning.Length == 0 && result[1].Warning.Contains("7") && result[2].Warning.Length == 0, "earlier list item wins regardless of ID");
    }

    /// <summary>驗證強制禁行只覆蓋明確有效區段以外的像素，並保留未設定的其他邊緣。</summary>
    private static void TestForbiddenGaps()
    {
        var monitors = new[] { Monitor() };
        var segments = TunnelPreviewCalculator.Calculate(monitors, new[] {
            Rule(0, EdgeType.Left, RangeType.Customize, 10, 20), Rule(1, EdgeType.Left, RangeType.Customize, 20, 20),
            Rule(2, EdgeType.Left, RangeType.Customize, 60, 10)
        });
        var gaps = TunnelPreviewCalculator.CalculateForbiddenGaps(monitors, segments);
        var left = gaps.Where(item => item.Edge == EdgeType.Left).ToArray();
        Require(left.Length == 3 && left[0].From == -100 && left[0].To == -91 && left[1].From == -59 && left[1].To == -41 &&
            left[2].From == -29 && left[2].To == 0, "inclusive interval union and complement");
        Require(gaps.Count == 6 && gaps.All(item => item.RuleIndex == -1 && item.IsForbidden), "implicit edges have no editable IDs");
        var full = TunnelPreviewCalculator.Calculate(monitors, new[] { Rule(0) });
        Require(TunnelPreviewCalculator.CalculateForbiddenGaps(monitors, full).All(item => item.Edge != EdgeType.Left), "full explicit edge overrides fallback");
    }

    /// <summary>驗證超大與負桌面座標不會溢位或產生無限投影比例。</summary>
    private static void TestExtremeCoordinates()
    {
        MonitorInfo monitor = Monitor();
        monitor.left = int.MinValue; monitor.right = int.MaxValue;
        TunnelInfo rule = Rule(0, EdgeType.Top);
        TunnelPreviewSegment result = TunnelPreviewCalculator.Calculate(new[] { monitor }, new[] { rule })[0];
        Require(result.HasGeometry && result.StartFraction == 0 && result.EndFraction == 1, "full int coordinate span");
        rule.rangeType = RangeType.Customize; rule.from = int.MaxValue; rule.to = int.MaxValue;
        result = TunnelPreviewCalculator.Calculate(new[] { Monitor() }, new[] { rule })[0];
        Require(!result.HasGeometry && result.Error.Length > 0, "offset addition does not wrap into a valid range");
    }

    /// <summary>建立含負座標及不同顯示比例的固定螢幕。</summary>
    /// <returns>兩軸均為 -100 至 0 的螢幕值。</returns>
    private static MonitorInfo Monitor() => new MonitorInfo { id = 0, left = -100, top = -100, right = 0, bottom = 0, scaling = 0.57142857 };

    /// <summary>建立預設禁行規則，供各範圍與方向案例修改。</summary>
    /// <param name="id">規則識別碼。</param>
    /// <param name="edge">螢幕邊緣。</param>
    /// <param name="type">範圍模式。</param>
    /// <param name="from">起點位移。</param>
    /// <param name="to">終點縮排或自訂延伸距離。</param>
    /// <returns>位於螢幕 0 且沒有傳送目標的規則。</returns>
    private static TunnelInfo Rule(int id, EdgeType edge = EdgeType.Left, RangeType type = RangeType.Full, int from = 0, int to = 0)
        => new TunnelInfo { id = id, displayID = 0, edgeType = edge, rangeType = type, from = from, to = to, relativeID = -1 };

    /// <summary>檢查具名條件，失敗時拋出可定位原因的例外。</summary>
    /// <param name="condition">必須成立的條件。</param>
    /// <param name="message">失敗時顯示的行為說明。</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    #endregion
}
