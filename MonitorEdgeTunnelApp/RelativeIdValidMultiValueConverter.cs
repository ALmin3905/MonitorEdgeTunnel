using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MonitorEdgeTunnelApp
{
    public class RelativeIdValidMultiValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            {
                return Visibility.Collapsed;
            }

            //     ComboBoxItem id   ListViewItem id
            return (int)values[0] == ((int)values[1]) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
