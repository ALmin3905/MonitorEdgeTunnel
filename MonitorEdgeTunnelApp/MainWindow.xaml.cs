using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace MonitorEdgeTunnelApp
{
    public class TunnelInfo_Notify : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        private int _id = 0;
        private int _from = 0;
        private int _to = 0;
        private int _relativeID = -1;
        private int _displayID = 0;
        private EdgeType _edgeType = EdgeType.Left;
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

        public TunnelInfo_Notify()
        {
            
        }

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

        public static ObservableCollection<TunnelInfo_Notify> DefaultTunnelInfos { get; set; }

        static TunnelInfo_Notify()
        {
            DefaultTunnelInfos = new ObservableCollection<TunnelInfo_Notify>()
            {
                new TunnelInfo_Notify{ id = -1 }
            };
        }

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
    }

    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string v)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(v));
        }

        public ObservableCollection<MonitorInfo> monitorInfos { get; set; } = new ObservableCollection<MonitorInfo>();

        public ObservableCollection<TunnelInfo_Notify> tunnelInfos { get; set; } = new ObservableCollection<TunnelInfo_Notify>();

        private bool _forceForbidEdgeChecked = false;

        public bool forceForbidEdgeChecked
        {
            get => _forceForbidEdgeChecked;
            set
            {
                _forceForbidEdgeChecked = value;
                OnPropertyChanged("forceForbidEdgeChecked");
            }
        }

        private bool _isStart = false;

        public bool isStart
        {
            get => _isStart;
            set
            {
                _isStart = value;
                OnPropertyChanged("isStart");
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // update data
            UpdateData();

            // bind data
            MonitorInfoListView.DataContext = this;
            TunnelInfoListView.DataContext = this;
            ForceForbidEdgeCheckbox.DataContext = this;
            IsStartButton.DataContext = this;
        }

        private void UpdateData()
        {
            // MonitorEdgeTunnel Instance
            MonitorEdgeTunnel monitorEdgeTunnel = MonitorEdgeTunnel.Instance;

            // 重置設定
            monitorEdgeTunnel.LoadSetting();

            // MonitorInfo
            monitorInfos.RemoveAll();
            monitorEdgeTunnel.GetMonitorInfoList().ForEach(monitorInfo =>
            {
                monitorInfos.Add(monitorInfo);
            });

            // TunnelInfo
            tunnelInfos.RemoveAll();
            monitorEdgeTunnel.GetCurrentTunnelInfoList().ForEach(tunnelInfo =>
            {
                tunnelInfos.Add(new TunnelInfo_Notify(tunnelInfo));
            });
            ReorderTunnelInfosID();

            // ForceForbidEdge
            forceForbidEdgeChecked = monitorEdgeTunnel.IsForceForbidEdge();

            // isStart
            isStart = monitorEdgeTunnel.IsStart();
        }

        private void ReorderTunnelInfosID()
        {
            for (int i = 0; i < tunnelInfos.Count; ++i)
            {
                // 紀錄當前id
                int currID = tunnelInfos[i].id;

                // id與index一樣就跳過
                if (currID == i)
                {
                    continue;
                }

                // 修改id
                tunnelInfos[i].id = i;

                // 找到相符的relativeID並修改
                for (int j = 0; j < tunnelInfos.Count; ++j)
                {
                    if (tunnelInfos[j].relativeID == currID)
                    {
                        tunnelInfos[j].relativeID = i;
                    }
                }
            }
        }

        private void TunnelInfoRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // 找到index
            int index = TunnelInfoListView.Items.IndexOf((sender as FrameworkElement).DataContext);

            // 記錄它的id
            int currID = tunnelInfos[index].id;

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
        }

        private void ApplyTunnelInfosSetting()
        {
            // MonitorEdgeTunnel Instance
            MonitorEdgeTunnel monitorEdgeTunnel = MonitorEdgeTunnel.Instance;

            // 如果運行中就先停止
            bool tmpIsStart = isStart;
            if (tmpIsStart)
            {
                StartOrStop();
            }

            // TunnelInfos
            List<TunnelInfo> tunnelInfoList = new List<TunnelInfo>();
            foreach (TunnelInfo_Notify tunnelInfo_Notify in tunnelInfos)
            {
                tunnelInfoList.Add(TunnelInfo_Notify.Cast(tunnelInfo_Notify));
            }
            monitorEdgeTunnel.SetCurrentTunnelInfoList(tunnelInfoList);

            // IsForbidEdge
            monitorEdgeTunnel.SetForceForbidEdge(forceForbidEdgeChecked);

            // 如果本來是運行中就恢復運行
            if (tmpIsStart)
            {
                StartOrStop();
            }
        }

        private void StartOrStop()
        {
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

        private void ShowMonitorEdgeTunnelErrorMessage()
        {
            switch (MonitorEdgeTunnel.Instance.GetErrorMsgCode())
            {
                case MonitorEdgeTunnelErrorMsg.Null:
                    // 沒事，不顯示
                    break;
                case MonitorEdgeTunnelErrorMsg.NoSettingFile:
                    _ = MessageBox.Show("沒有設定檔", "錯誤訊息", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                case MonitorEdgeTunnelErrorMsg.HookFail:
                    _ = MessageBox.Show("應用程式嚴重錯誤，請重啟試試", "錯誤訊息", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                case MonitorEdgeTunnelErrorMsg.GetMonitorInfoFailed:
                    _ = MessageBox.Show("取得螢幕資訊失敗", "錯誤訊息", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                case MonitorEdgeTunnelErrorMsg.NoMonitorInfo:
                    _ = MessageBox.Show("沒有螢幕資訊", "錯誤訊息", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                case MonitorEdgeTunnelErrorMsg.AppendTunnelInfoFailed:
                    _ = MessageBox.Show("通道資訊設定失敗，請確認是否符合規則", "錯誤訊息", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                case MonitorEdgeTunnelErrorMsg.TunnelInfoError:
                    _ = MessageBox.Show("通道資訊錯誤，請確認是否符合規則", "錯誤訊息", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                default:
                    throw new NotImplementedException("ShowMonitorEdgeTunnelErrorMessage Failed");
            }
        }

        private void AddTunnelInfoButton_Click(object sender, RoutedEventArgs e)
        {
            tunnelInfos.Add(new TunnelInfo_Notify { id = tunnelInfos.Count });
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTunnelInfosSetting();
        }

        private void SaveSettingButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTunnelInfosSetting();
            MonitorEdgeTunnel.Instance.SaveSetting();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateData();
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            StartOrStop();
        }

        private void CloseWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseAppMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
