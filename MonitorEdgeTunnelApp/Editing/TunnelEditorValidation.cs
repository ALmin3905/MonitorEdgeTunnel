using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MonitorEdgeTunnelApp
{
    /// <summary>把輸入與規則錯誤提供給 WPF 欄位，不改變規則數值或服務設定。</summary>
    public partial class TunnelInfo_Notify : INotifyDataErrorInfo
    {
        /// <summary>以原生或編輯欄位名稱定位的語意錯誤。</summary>
        private Dictionary<string, string> _validationErrors = new Dictionary<string, string>();
        /// <summary>WPF 欄位驗證通知；在 UI 執行緒觸發。</summary>
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;
        /// <summary>目前有無輸入或規則錯誤，警告不列為錯誤。</summary>
        public bool HasErrors => HasInputErrors || _validationErrors.Count > 0;
        /// <summary>可顯示在目前規則旁的完整錯誤原因。</summary>
        public string ValidationMessage => string.Join("；", GetErrors(null).Cast<string>().Distinct());
        /// <summary>僅提醒優先順序的重疊警告，不阻止編輯。</summary>
        public string WarningMessage { get; private set; } = string.Empty;
        /// <summary>以目前螢幕邊緣計算的合法範圍提示。</summary>
        public string RangeHint { get; private set; } = string.Empty;
        /// <summary>欄位工具提示同時提供錯誤原因與合法範圍。</summary>
        public string FieldHelp => ValidationMessage.Length > 0 ? ValidationMessage : RangeHint;

        #region -- Public Methods --

        /// <summary>回傳指定欄位或所有錯誤，供 WPF 顯示欄位邊框與提示。</summary>
        /// <param name="propertyName">欄位名稱；null 或空字串表示全部欄位。</param>
        /// <returns>不重複的錯誤文字快照。</returns>
        public IEnumerable GetErrors(string propertyName)
        {
            List<string> errors = _validationErrors.Where(pair => string.IsNullOrEmpty(propertyName) || pair.Key == propertyName).Select(pair => pair.Value).ToList();
            if ((string.IsNullOrEmpty(propertyName) || propertyName == nameof(StartText)) && StartInputError.Length > 0 && !_validationErrors.ContainsKey(nameof(StartText))) errors.Add(StartInputError);
            if ((string.IsNullOrEmpty(propertyName) || propertyName == nameof(ExtentText)) && ExtentInputError.Length > 0 && !_validationErrors.ContainsKey(nameof(ExtentText))) errors.Add(ExtentInputError);
            return errors.Distinct().ToArray();
        }

        /// <summary>於 UI 執行緒套入共用驗證結果，只有變更的欄位會發出驗證通知。</summary>
        /// <param name="property">錯誤欄位，無錯誤時為空字串。</param>
        /// <param name="message">錯誤原因，無錯誤時為空字串。</param>
        /// <param name="warning">可繼續操作的重疊優先權說明。</param>
        /// <param name="hint">目前邊緣的合法輸入範圍。</param>
        internal void SetValidation(string property, string message, string warning, string hint)
        {
            Dictionary<string, string> next = new Dictionary<string, string>();
            if (property.Length > 0 && message.Length > 0) next[property] = message;
            string[] changed = _validationErrors.Keys.Union(next.Keys).Where(key => !_validationErrors.TryGetValue(key, out string old) ||
                !next.TryGetValue(key, out string value) || old != value).ToArray();
            bool warningChanged = WarningMessage != warning || RangeHint != hint;
            _validationErrors = next;
            WarningMessage = warning;
            RangeHint = hint;
            foreach (string key in changed) ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(key));
            if (changed.Length > 0 || warningChanged)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasErrors)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationMessage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WarningMessage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RangeHint)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FieldHelp)));
            }
        }

        #endregion

        #region -- Private Methods --

        /// <summary>數字輸入格式變更後重新通知兩個文字欄位，未套用的原始字串仍保留。</summary>
        private void NotifyInputErrors()
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(StartText)));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(ExtentText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasErrors)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationMessage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FieldHelp)));
        }

        #endregion
    }
}
