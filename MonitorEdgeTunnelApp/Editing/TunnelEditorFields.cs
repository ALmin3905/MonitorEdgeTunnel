using System.ComponentModel;
using System.Globalization;

namespace MonitorEdgeTunnelApp
{
    /// <summary>提供符合範圍類型的文字欄位，原生 from/to 仍是唯一可套用的數值來源。</summary>
    public partial class TunnelInfo_Notify
    {
        /// <summary>保留尚未完成或無效的起點文字，避免關窗時遺失編輯。</summary>
        private string _startText;
        /// <summary>保留尚未完成或無效的長度或內縮文字。</summary>
        private string _extentText;
        /// <summary>由文字欄位更新原生數值時避免清除使用者正在輸入的字串。</summary>
        private bool _updatingText;
        /// <summary>完整邊緣不需要編輯起點及長度。</summary>
        public bool IsRangeEditable => rangeType != RangeType.Full;
        /// <summary>明確的動作選項；-2 表示已選傳送但尚未指定目標。</summary>
        public bool IsTransport
        {
            get => relativeID != -1;
            set { if (value != IsTransport) relativeID = value ? -2 : -1; }
        }
        /// <summary>選單顯示的通道及螢幕位置，原始 ID 不變。</summary>
        public string TargetCaption => id < 0 ? "選擇目標通道" : "通道 " + id + " · 螢幕 " + (displayID + 1) + " " + Editing.EditorOptionLabelConverter.EdgeLabel(edgeType);
        /// <summary>依模式說明第一個欄位的意義；表格標頭統一標示像素單位。</summary>
        public string StartLabel => rangeType == RangeType.Relative ? "起點內縮" : "起點偏移";
        /// <summary>依模式區分終點內縮與含尾端像素的長度。</summary>
        public string ExtentLabel => rangeType == RangeType.Relative ? "終點內縮" : "長度";
        /// <summary>起點的編輯文字；解析成功才同步原生值，無效內容仍保留在草稿。</summary>
        public string StartText
        {
            get => _startText ?? from.ToString(CultureInfo.CurrentCulture);
            set
            {
                _startText = value;
                _updatingText = true;
                try { if (TryNumber(value, false, out int number)) from = number; }
                finally { _updatingText = false; }
                NotifyPropertyChanged(nameof(StartText));
                NotifyEditorFields();
            }
        }
        /// <summary>相對模式為終點內縮；自訂模式為實際像素長度，換算為原生 to=長度-1。</summary>
        public string ExtentText
        {
            get => _extentText ?? (rangeType == RangeType.Customize ? (long)to + 1 : to).ToString(CultureInfo.CurrentCulture);
            set
            {
                _extentText = value;
                _updatingText = true;
                try { if (TryNumber(value, rangeType == RangeType.Customize, out int number)) to = number; }
                finally { _updatingText = false; }
                NotifyPropertyChanged(nameof(ExtentText));
                NotifyEditorFields();
            }
        }
        /// <summary>起點文字格式錯誤；完整邊緣模式不使用此欄位。</summary>
        public string StartInputError => IsRangeEditable && !TryNumber(StartText, false, out _) ? "請輸入 0 到 2147483647 的整數" : string.Empty;
        /// <summary>長度或內縮文字格式錯誤，長度需至少一個像素。</summary>
        public string ExtentInputError => IsRangeEditable && !TryNumber(ExtentText, rangeType == RangeType.Customize, out _) ?
            rangeType == RangeType.Customize ? "請輸入 1 到 2147483648 的像素長度" : "請輸入 0 到 2147483647 的整數" : string.Empty;
        /// <summary>是否有無法換成原生值的編輯文字，供草稿保護與套用檢查使用。</summary>
        public bool HasInputErrors => StartInputError.Length > 0 || ExtentInputError.Length > 0;

        #region -- Private Methods --

        /// <summary>依整數或含尾端長度語意解析文字，不使用浮點數，不修改編輯物件。</summary>
        /// <param name="text">目前顯示的文字。</param>
        /// <param name="length">true 代表需轉為長度減一的原生欄位。</param>
        /// <param name="value">成功時輸出可安全轉型的原生值。</param>
        /// <returns>格式與可表達的範圍都合法時為 true。</returns>
        private static bool TryNumber(string text, bool length, out int value)
        {
            value = 0;
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out long number) ||
                number < (length ? 1 : 0) || number > (long)int.MaxValue + (length ? 1 : 0)) return false;
            value = (int)(length ? number - 1 : number);
            return true;
        }

        /// <summary>原生欄位或模式被程式修改時同步顯示，保留正在輸入的文字與原生語意。</summary>
        /// <param name="property">原生欄位或編輯文字的名稱。</param>
        private void EditorValueChanged(string property)
        {
            if (!_updatingText)
            {
                if (property == nameof(from) || property == nameof(rangeType)) _startText = null;
                if (property == nameof(to) || property == nameof(rangeType)) _extentText = null;
            }
            if (property != nameof(StartText) && property != nameof(ExtentText)) NotifyEditorFields();
        }

        /// <summary>在 UI 執行緒更新所有衍生欄位與提示，不改變原生設定。</summary>
        private void NotifyEditorFields()
        {
            foreach (string property in new[] { nameof(IsRangeEditable), nameof(IsTransport), nameof(TargetCaption), nameof(StartLabel),
                nameof(ExtentLabel), nameof(StartText), nameof(ExtentText), nameof(StartInputError), nameof(ExtentInputError), nameof(HasInputErrors) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        #endregion
    }
}
