using MonitorEdgeTunnelApp.Editing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Linq;
using MessageBox = MonitorEdgeTunnelApp.Themes.ThemedMessageBox;

namespace MonitorEdgeTunnelApp
{
    /// <summary>提供通道編輯欄位與變更通知，供 WPF 清單雙向綁定。</summary>
    public partial class TunnelInfo_Notify : INotifyPropertyChanged
    {
        /// <summary>通知 UI 綁定來源的欄位已變更。</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>通道識別碼。</summary>
        private int _id = 0;
        /// <summary>起點或相對起點位移。</summary>
        private int _from = 0;
        /// <summary>終點位移或自訂範圍長度。</summary>
        private int _to = 0;
        /// <summary>相依通道 ID；-1 代表禁止通行。</summary>
        private int _relativeID = -1;
        /// <summary>所屬螢幕 ID。</summary>
        private int _displayID = 0;
        /// <summary>通道所在邊緣。</summary>
        private EdgeType _edgeType = EdgeType.Left;
        /// <summary>範圍的解讀方式。</summary>
        private RangeType _rangeType = RangeType.Full;

        public int id
        {
            get => _id;
            set
            {
                _id = value;
                NotifyPropertyChanged("id");
            }
        }
        public int from
        {
            get => _from;
            set
            {
                _from = value;
                NotifyPropertyChanged("from");
            }
        }
        public int to
        {
            get => _to;
            set
            {
                _to = value;
                NotifyPropertyChanged("to");
            }
        }
        public int relativeID
        {
            get => _relativeID;
            set
            {
                _relativeID = value;
                NotifyPropertyChanged("relativeID");
            }
        }
        public int displayID
        {
            get => _displayID;
            set
            {
                _displayID = value;
                NotifyPropertyChanged("displayID");
            }
        }
        public EdgeType edgeType
        {
            get => _edgeType;
            set
            {
                _edgeType = value;
                NotifyPropertyChanged("edgeType");
            }
        }
        public RangeType rangeType
        {
            get => _rangeType;
            set
            {
                _rangeType = value;
                NotifyPropertyChanged("rangeType");
            }
        }

        #region -- Public Methods --

        /// <summary>以預設禁止通行的欄位建立新的編輯項目。</summary>
        public TunnelInfo_Notify()
        {
            
        }

        /// <summary>複製通道編輯資料，不複製事件訂閱者。</summary>
        /// <param name="tunnelInfo_Notify">要複製的來源項目。</param>
        public TunnelInfo_Notify(TunnelInfo_Notify tunnelInfo_Notify)
        {
            id = tunnelInfo_Notify.id;
            from = tunnelInfo_Notify.from;
            to = tunnelInfo_Notify.to;
            relativeID = tunnelInfo_Notify.relativeID;
            displayID = tunnelInfo_Notify.displayID;
            edgeType = tunnelInfo_Notify.edgeType;
            rangeType = tunnelInfo_Notify.rangeType;
        }

        /// <summary>由原生通道值建立可通知變更的編輯項目。</summary>
        /// <param name="tunnelInfo">來源通道設定。</param>
        public TunnelInfo_Notify(TunnelInfo tunnelInfo)
        {
            id = tunnelInfo.id;
            from = tunnelInfo.from;
            to = tunnelInfo.to;
            relativeID = tunnelInfo.relativeID;
            displayID = tunnelInfo.displayID;
            edgeType = tunnelInfo.edgeType;
            rangeType = tunnelInfo.rangeType;
        }

        /// <summary>相依通道選單內代表禁止通行的預設項目。</summary>
        public static ObservableCollection<TunnelInfo_Notify> DefaultTunnelInfos { get; set; }

        /// <summary>將 UI 編輯模型轉成原生介面使用的值型別。</summary>
        /// <param name="tunnelInfo_Notify">要轉換的通道編輯資料。</param>
        /// <returns>不含 UI 事件的通道設定。</returns>
        public static TunnelInfo Cast(TunnelInfo_Notify tunnelInfo_Notify)
        {
            TunnelInfo tunnelInfo = new TunnelInfo();
            tunnelInfo.id = tunnelInfo_Notify.id;
            tunnelInfo.from = tunnelInfo_Notify.from;
            tunnelInfo.to = tunnelInfo_Notify.to;
            tunnelInfo.relativeID = tunnelInfo_Notify.relativeID;
            tunnelInfo.displayID = tunnelInfo_Notify.displayID;
            tunnelInfo.edgeType = tunnelInfo_Notify.edgeType;
            tunnelInfo.rangeType = tunnelInfo_Notify.rangeType;

            return tunnelInfo;
        }
        #endregion

        #region -- Private Methods --

        /// <summary>建立相依通道選單的禁止通行項目與尚未選擇目標的顯示項目。</summary>
        static TunnelInfo_Notify()
        {
            DefaultTunnelInfos = new ObservableCollection<TunnelInfo_Notify>()
            {
                new TunnelInfo_Notify{ id = -1 }, new TunnelInfo_Notify{ id = -2 }
            };
        }

        /// <summary>發送通道欄位的變更通知。</summary>
        /// <param name="info">已變更的屬性名稱。</param>
        private void NotifyPropertyChanged(string info)
        {
            EditorValueChanged(info);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        #endregion
    }

    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>通知 UI 綁定來源的欄位已變更。</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<MonitorInfo> monitorInfos { get; set; } = new ObservableCollection<MonitorInfo>();

        /// <summary>目前配置的可編輯規則；物件由工作區保留至程式結束。</summary>
        public ObservableCollection<TunnelInfo_Notify> tunnelInfos => _session.Rules;

        /// <summary>左右兩側共用的原始螢幕選取 ID。</summary>
        private int? _selectedMonitorId;

        /// <summary>保留被選取的規則物件，避免清單重新編號後變成另一筆規則。</summary>
        private TunnelInfo_Notify _selectedTunnel;

        /// <summary>與螢幕預覽雙向同步的規則物件，選取本身不套用原生設定。</summary>
        public TunnelInfo_Notify selectedTunnel
        {
            get => _selectedTunnel;
            set
            {
                if (ReferenceEquals(_selectedTunnel, value)) return;
                _selectedTunnel = value;
                OnPropertyChanged(nameof(selectedTunnel));
                UpdateIdentificationPreview();
            }
        }

        /// <summary>目前選取的螢幕 ID；選取僅影響介面，不變更通道設定。</summary>
        public int? selectedMonitorId
        {
            get => _selectedMonitorId;
            set
            {
                if (_selectedMonitorId == value)
                    return;
                _selectedMonitorId = value;
                OnPropertyChanged(nameof(selectedMonitorId));
                UpdateIdentificationPreview();
            }
        }

        /// <summary>介面編輯中的強制邊界禁止狀態；變更只更新草稿。</summary>
        public bool forceForbidEdgeChecked
        {
            get => _session?.ForceForbid ?? false;
            set { if (_session != null) _session.ForceForbid = value; }
        }

        /// <summary>介面顯示的原生執行狀態。</summary>
        private bool _isStart = false;

        public bool isStart
        {
            get => _isStart;
            set
            {
                _isStart = value;
                OnPropertyChanged("isStart");
                OnPropertyChanged(nameof(CanStartStop));
            }
        }

        /// <summary>介面顯示的開機自動啟動狀態。</summary>
        private bool _isAutorun = false;

        public bool isAutorun
        {
            get => _isAutorun;
            set
            {
                _isAutorun = value;
                OnPropertyChanged("isAutorun");
            }
        }

        #region -- Public Methods --

        /// <summary>在 UI 執行緒建立視窗、載入設定並建立資料綁定。</summary>
        public MainWindow() : this(new EditorWorkspace()) { }

        /// <summary>在 UI 執行緒建立視窗並恢復工作區草稿，不重新載入設定檔。</summary>
        /// <param name="workspace">由應用程式持有的各配置編輯內容。</param>
        /// <param name="requestExit">選用的安全結束流程；null 時由本視窗確認後結束。</param>
        /// <param name="createIdentification">選用的辨識控制器工廠，null 使用正式的非啟用覆蓋視窗。</param>
        internal MainWindow(EditorWorkspace workspace, Action requestExit = null, Func<Window, Controls.MonitorIdentification> createIdentification = null)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _requestExit = requestExit;
            _createIdentification = createIdentification ?? (owner => new Controls.MonitorIdentification(owner));
            InitializeComponent();
            Closed += EditorWindowClosed;

            // update data
            RefreshDisplayData();

            // isAutorun
            isAutorun = AutoStartManager.IsAutorun();

            // bind data
            if (Content is FrameworkElement root) root.DataContext = this;
            MonitorInfoListView.DataContext = this;
            MonitorLayout.DataContext = this;
            TunnelInfoListView.DataContext = this;
            ForceForbidEdgeCheckbox.DataContext = this;
            IsStartButton.DataContext = this;
            AutorunMenuItem.DataContext = this;
        }

        /// <summary>在 UI 執行緒重新載入設定並更新螢幕、通道及功能狀態；尚無檔案時清空規則及邊緣封鎖選項。</summary>
        public void UpdateData()
        {
            Dispatcher.VerifyAccess();
            RefreshDisplayData();
            MonitorEdgeTunnel service = MonitorEdgeTunnel.Instance;
            bool wasRunning = service.IsStart();
            try
            {
                if (wasRunning && !service.Stop()) { ShowMonitorEdgeTunnelErrorMessage(); return; }
                if (!service.LoadSetting())
                {
                    ShowMonitorEdgeTunnelErrorMessage();
                    if (wasRunning && !service.Start()) ShowMonitorEdgeTunnelErrorMessage();
                    return;
                }
                _workspace.Reload(_session, ReadNativeSettings());
                selectedTunnel = null;
                _workspaceNotice = string.Empty;
                if (wasRunning && !service.Start()) ShowMonitorEdgeTunnelErrorMessage();
                NotifySession();
            }
            finally { isStart = service.IsStart(); }
        }

        #endregion

        #region -- Private Methods --

        /// <summary>在 UI 執行緒捲到選取規則，忽略內部 ComboBox 冒泡的選取事件。</summary>
        /// <param name="sender">通道清單。</param>
        /// <param name="e">可區分原始來源的選取路由事件。</param>
        private void TunnelInfoListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ListView list && ReferenceEquals(e.OriginalSource, list) && list.SelectedItem != null)
                list.ScrollIntoView(list.SelectedItem);
        }

        /// <summary>發送主視窗狀態的變更通知。</summary>
        /// <param name="v">已變更的屬性名稱。</param>
        private void OnPropertyChanged(string v)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(v));
        }

        /// <summary>依 UI 清單順序重排通道 ID 並同步相依參照。</summary>
        private void ReorderTunnelInfosID()
        {
            // 先紀錄全部原始 ID 的新位置，避免 ID 互換時二次改寫其他規則的相依目標。
            Dictionary<int, int> newIds = new Dictionary<int, int>();
            int[] originalTargets = tunnelInfos.Select(rule => rule.relativeID).ToArray();
            for (int i = 0; i < tunnelInfos.Count; ++i) newIds[tunnelInfos[i].id] = i;
            for (int i = 0; i < tunnelInfos.Count; ++i)
            {
                TunnelInfo_Notify rule = tunnelInfos[i];
                int originalTarget = originalTargets[i];
                rule.id = i;
                rule.relativeID = originalTarget >= 0 && newIds.TryGetValue(originalTarget, out int target) ? target : originalTarget;
            }
        }

        /// <summary>刪除目前列、清除相關相依參照並重排 ID；刪除選取規則時改選相鄰規則。</summary>
        /// <param name="sender">觸發事件的 UI 控制項。</param>
        /// <param name="e">路由事件資料。</param>
        private void TunnelInfoRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // 找到index
            int index = tunnelInfos.IndexOf((sender as FrameworkElement)?.DataContext as TunnelInfo_Notify);
            if (index < 0) return;

            // 記錄它的id
            int currID = tunnelInfos[index].id;
            bool wasSelected = ReferenceEquals(selectedTunnel, tunnelInfos[index]);

            // 刪除
            tunnelInfos.RemoveAt(index);

            // 有RelativeID的改為 -1
            foreach (TunnelInfo_Notify tunnelInfo in tunnelInfos)
            {
                if (tunnelInfo.relativeID == currID)
                {
                    tunnelInfo.relativeID = -1;
                }
            }

            ReorderTunnelInfosID();
            if (wasSelected) selectedTunnel = tunnelInfos.Count > 0 ? tunnelInfos[Math.Min(index, tunnelInfos.Count - 1)] : null;
        }

        /// <summary>在 UI 執行緒套用編輯內容並恢復原本執行狀態；失敗時提示錯誤。</summary>
        /// <returns>是否全部套用成功；false 時不應繼續存檔。</returns>
        private bool ApplyTunnelInfosSetting()
        {
            if (!PrepareApply()) return false;
            try
            {
                // MonitorEdgeTunnel Instance
                MonitorEdgeTunnel monitorEdgeTunnel = MonitorEdgeTunnel.Instance;

                // 如果運行中就先停止，以原生實際狀態為準。
                bool tmpIsStart = monitorEdgeTunnel.IsStart();
                isStart = tmpIsStart;
                if (tmpIsStart)
                {
                    if (!monitorEdgeTunnel.Stop())
                    {
                        ShowMonitorEdgeTunnelErrorMessage();
                        return false;
                    }
                    isStart = false;
                }

                // TunnelInfos
                List<TunnelInfo> tunnelInfoList = new List<TunnelInfo>();
                foreach (TunnelInfo_Notify tunnelInfo_Notify in tunnelInfos)
                    tunnelInfoList.Add(TunnelInfo_Notify.Cast(tunnelInfo_Notify));

                int result = monitorEdgeTunnel.SetCurrentTunnelInfoList(tunnelInfoList);
                if (result != 0)
                {
                    if (result == -3)
                        ShowMonitorEdgeTunnelErrorMessage();
                    else
                        MessageBox.Show($"更新通道設定失敗，錯誤碼：{result}", "錯誤訊息", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // IsForbidEdge
                if (!monitorEdgeTunnel.SetCurrentForceForbidEdge(forceForbidEdgeChecked))
                {
                    ShowMonitorEdgeTunnelErrorMessage();
                    return false;
                }

                // 如果本來是運行中就恢復運行。
                if (tmpIsStart)
                {
                    if (!monitorEdgeTunnel.Start())
                    {
                        ShowMonitorEdgeTunnelErrorMessage();
                        return false;
                    }
                    isStart = true;
                }
                return true;
            }
            finally
            {
                isStart = MonitorEdgeTunnel.Instance.IsStart();
                _session.ObserveApplied(ReadNativeSettings());
            }
        }

        /// <summary>依 UI 狀態切換原生功能，成功後更新狀態，失敗時顯示錯誤。</summary>
        private void StartOrStop()
        {
            RevalidateSession();
            if (!isStart && !CanApply) return;
            if (!isStart && HasUnappliedChanges)
            {
                EditorSession pending = _session;
                TunnelSettingsSnapshot settings = pending.Capture();
                string layout = EditorWorkspace.LayoutKey(monitorInfos);
                if (!MessageBox.Confirm("目前有尚未套用的草稿。要先套用，再啟動通道嗎？", "套用並啟動", "套用並啟動") ||
                    !OperationStillCurrent(pending, settings, layout) || !ApplyTunnelInfosSetting()) return;
            }
            if (isStart)
            {
                if (MonitorEdgeTunnel.Instance.Stop())
                {
                    isStart = false;
                }
                else
                {
                    ShowMonitorEdgeTunnelErrorMessage();
                }
            }
            else
            {
                if (MonitorEdgeTunnel.Instance.Start())
                {
                    isStart = true;
                }
                else
                {
                    ShowMonitorEdgeTunnelErrorMessage();
                }
            }
        }

        /// <summary>在 UI 執行緒顯示目前原生呼叫的錯誤訊息。</summary>
        private void ShowMonitorEdgeTunnelErrorMessage()
        {
            _ = MessageBox.Show(MonitorEdgeTunnelErrorMsgConvertor.ToErrorMsgString(MonitorEdgeTunnel.Instance.GetErrorMsgCode()), "錯誤訊息", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>在 UI 執行緒加入目前螢幕的預設規則並選取供詳細編輯，不套用設定。</summary>
        /// <param name="sender">觸發事件的 UI 控制項。</param>
        /// <param name="e">路由事件資料。</param>
        private void AddTunnelInfoButton_Click(object sender, RoutedEventArgs e)
        {
            int nextId = Enumerable.Range(0, 101).Where(id => tunnelInfos.All(rule => rule.id != id)).DefaultIfEmpty(-1).First();
            if (nextId >= 0)
            {
                TunnelInfo_Notify rule = new TunnelInfo_Notify { id = nextId, displayID = selectedMonitorId ?? 0 };
                tunnelInfos.Add(rule);
                selectedTunnel = rule;
            }
        }

        /// <summary>套用目前編輯內容，錯誤由套用流程提示。</summary>
        /// <param name="sender">觸發事件的 UI 控制項。</param>
        /// <param name="e">路由事件資料。</param>
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTunnelInfosSetting();
        }

        /// <summary>套用成功後存檔；任何失敗都提示並停止後續步驟。</summary>
        /// <param name="sender">觸發事件的 UI 控制項。</param>
        /// <param name="e">路由事件資料。</param>
        private void SaveSettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ApplyTunnelInfosSetting())
                return;

            if (!MonitorEdgeTunnel.Instance.SaveSetting())
                ShowMonitorEdgeTunnelErrorMessage();
            else _workspace.MarkSaved();
        }

        /// <summary>從設定檔重新整理 UI 編輯內容。</summary>
        /// <param name="sender">觸發事件的 UI 控制項。</param>
        /// <param name="e">路由事件資料。</param>
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            EditorSession pending = _session;
            TunnelSettingsSnapshot settings = pending.Capture();
            string layout = EditorWorkspace.LayoutKey(monitorInfos);
            if (HasUnsavedChanges && !MessageBox.Confirm("重新載入會捨棄這組配置尚未儲存的編輯與套用內容；若尚無設定檔，將清空規則並關閉邊緣封鎖。其他配置的草稿會保留。", "重新載入設定？", "捨棄並重新載入")) return;
            if (OperationStillCurrent(pending, settings, layout)) UpdateData();
        }

        /// <summary>切換原生通道功能。</summary>
        /// <param name="sender">觸發事件的 UI 控制項。</param>
        /// <param name="e">路由事件資料。</param>
        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            StartOrStop();
        }

        /// <summary>關閉設定視窗，保留通知區應用程式。</summary>
        /// <param name="sender">觸發事件的 UI 控制項。</param>
        /// <param name="e">路由事件資料。</param>
        private void CloseWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>要求應用程式結束並執行服務清理。</summary>
        /// <param name="sender">觸發事件的 UI 控制項。</param>
        /// <param name="e">路由事件資料。</param>
        private void CloseAppMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_requestExit != null) _requestExit();
            else if (!_workspace.HasUnsavedChanges || MessageBox.Confirm("仍有未儲存內容，結束後將捨棄這些草稿與尚未儲存的設定。", "結束程式？", "結束並捨棄"))
                Application.Current.Shutdown();
        }

        /// <summary>寫入開機自動啟動選項，失敗時提示。</summary>
        /// <param name="sender">觸發事件的 UI 控制項。</param>
        /// <param name="e">路由事件資料。</param>
        private void AutorunMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!AutoStartManager.SetAutorun(isAutorun))
            {
                _ = MessageBox.Show("開機啟動設定失敗", "錯誤訊息", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}
