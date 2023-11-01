using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace MonitorEdgeTunnelApp
{
    public class TunnelInfo_Notify : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        private int _displayID;

        public int id { get; set; }
        public int from { get; set; }
        public int to { get; set; }
        public int relativeID { get; set; }
        public int displayID
        {
            get => _displayID;
            set
            {
                _displayID = value;
                NotifyPropertyChanged("displayID");
            }
        }
        public EdgeType edgeType { get; set; }
        public RangeType rangeType { get; set; }

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
    }

    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<MonitorInfo> monitorInfos { get; set; } = new ObservableCollection<MonitorInfo>();

        public ObservableCollection<TunnelInfo_Notify> tunnelInfos { get; set; } = new ObservableCollection<TunnelInfo_Notify>();

        private void UpdateData()
        {
            // MonitorEdgeTunnel Instance
            MonitorEdgeTunnel monitorEdgeTunnel = MonitorEdgeTunnel.Instance;

            // MonitorInfo
            monitorInfos.Clear();
            monitorEdgeTunnel.GetMonitorInfoList().ForEach(monitorInfo =>
            {
                monitorInfos.Add(monitorInfo);
            });

            // TunnelInfo
            tunnelInfos.Clear();
            monitorEdgeTunnel.GetTunnelInfoList().ForEach(tunnelInfo =>
            {
                tunnelInfos.Add(new TunnelInfo_Notify(tunnelInfo));
            });
        }

        public MainWindow()
        {
            InitializeComponent();

            // update data
            UpdateData();

            // bind data
            MonitorInfoListView.DataContext = this;
            TunnelInfoListView.DataContext = this;
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < tunnelInfos.Count; ++i)
            {
                Trace.WriteLine(tunnelInfos[i].displayID);
            }
        }
    }
}
