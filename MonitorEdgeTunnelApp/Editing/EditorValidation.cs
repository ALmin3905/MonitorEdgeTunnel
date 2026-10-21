using MonitorEdgeTunnelApp.Controls;
using System.Collections.Generic;
using System.Linq;

namespace MonitorEdgeTunnelApp.Editing
{
    /// <summary>共用預覽與套用前檢查，補上尚未能轉成原生值的文字錯誤，不修改編輯資料。</summary>
    internal static class EditorValidation
    {
        #region -- Public Methods --

        /// <summary>以相同原生規則計算範圍與目標，再加入文字編輯狀態；限 UI 執行緒讀取模型。</summary>
        /// <param name="monitors">目前螢幕快照。</param>
        /// <param name="rules">編輯中規則的穩定順序快照。</param>
        /// <returns>與 rules 相同順序的結果，無效文字不繪製舊數值的範圍。</returns>
        internal static IReadOnlyList<TunnelPreviewSegment> Evaluate(IReadOnlyList<MonitorInfo> monitors, IReadOnlyList<TunnelInfo_Notify> rules)
        {
            IReadOnlyList<TunnelPreviewSegment> segments = TunnelPreviewCalculator.Calculate(monitors, rules.Select(TunnelInfo_Notify.Cast).ToList());
            for (int i = 0; i < rules.Count; ++i)
            {
                if (!rules[i].HasInputErrors)
                {
                    if (rules[i].relativeID == -2)
                    {
                        segments[i].Error = "請選擇要傳送到的目標通道";
                        segments[i].ErrorField = TunnelValidationField.Target;
                    }
                    continue;
                }
                segments[i].Error = rules[i].StartInputError.Length > 0 ? rules[i].StartInputError : rules[i].ExtentInputError;
                segments[i].ErrorField = rules[i].StartInputError.Length > 0 ? TunnelValidationField.Start : TunnelValidationField.Extent;
                segments[i].HasGeometry = false;
                segments[i].TargetIndex = -1;
            }
            foreach (TunnelPreviewSegment segment in segments)
                if (segment.TargetIndex >= 0 && segments[segment.TargetIndex].Error.Length > 0)
                {
                    segment.Error = "目標通道的設定無效";
                    segment.ErrorField = TunnelValidationField.Target;
                    segment.TargetIndex = -1;
                }
            return segments;
        }

        /// <summary>將型別化錯誤位置對應到 WPF 編輯欄位，不比對提示字串。</summary>
        /// <param name="field">計算結果中的錯誤位置。</param>
        /// <returns>綁定欄位名稱；沒有錯誤時為空字串。</returns>
        internal static string PropertyName(TunnelValidationField field)
        {
            switch (field)
            {
                case TunnelValidationField.Id: return nameof(TunnelInfo_Notify.id);
                case TunnelValidationField.Monitor: return nameof(TunnelInfo_Notify.displayID);
                case TunnelValidationField.Edge: return nameof(TunnelInfo_Notify.edgeType);
                case TunnelValidationField.RangeMode: return nameof(TunnelInfo_Notify.rangeType);
                case TunnelValidationField.Start: return nameof(TunnelInfo_Notify.StartText);
                case TunnelValidationField.Extent: return nameof(TunnelInfo_Notify.ExtentText);
                case TunnelValidationField.Target: return nameof(TunnelInfo_Notify.relativeID);
                default: return string.Empty;
            }
        }

        #endregion
    }
}
