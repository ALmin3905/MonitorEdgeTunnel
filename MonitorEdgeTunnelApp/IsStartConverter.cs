using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MonitorEdgeTunnelApp
{
    [ValueConversion(typeof(bool), typeof(string))]
    public class IsStartConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != DependencyProperty.UnsetValue ? (bool)value ? "停止" : "啟動" : throw new SystemException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
