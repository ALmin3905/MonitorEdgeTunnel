using System.Collections.ObjectModel;

namespace MonitorEdgeTunnelApp
{
    public static class ObservableCollectionExtensions
    {
        public static void RemoveAll<T>(this ObservableCollection<T> collection)
        {
            // 從後面刪除
            for (int i = collection.Count - 1; i >= 0; --i)
            {
                collection.RemoveAt(i);
            }
        }
    }
}
