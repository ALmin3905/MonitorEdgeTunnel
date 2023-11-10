using System;
using System.Collections.ObjectModel;

namespace MonitorEdgeTunnelApp
{
    public static class UtilitiesExtensions
    {
        public static void RemoveAll<T>(this ObservableCollection<T> collection)
        {
            // 從後面刪除
            for (int i = collection.Count - 1; i >= 0; --i)
            {
                collection.RemoveAt(i);
            }
        }

        public static T ToEnum<T>(this string value, T defaultValue) where T : struct, Enum
        {
            return !string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out T result) ? result : defaultValue;
        }
    }
}
