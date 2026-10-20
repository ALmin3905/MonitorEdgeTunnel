using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MonitorEdgeTunnelApp.Themes
{
    /// <summary>以共用深淺色樣式顯示目前介面使用的單一確認錯誤訊息。</summary>
    internal static class ThemedMessageBox
    {
        #region -- Public Methods --

        /// <summary>在 UI 執行緒顯示確認對話框並等待關閉，不更改服務或設定。</summary>
        /// <param name="message">完整錯誤內容，可包含換行。</param>
        /// <param name="caption">視窗標題。</param>
        /// <param name="buttons">目前只支援呼叫端使用的 OK。</param>
        /// <param name="image">用於選擇錯誤或一般提示文字色。</param>
        /// <returns>使用者確認或關閉對話框後回傳 OK。</returns>
        internal static MessageBoxResult Show(string message, string caption, MessageBoxButton buttons, MessageBoxImage image)
        {
            if (buttons != MessageBoxButton.OK) throw new ArgumentOutOfRangeException(nameof(buttons));
            Window dialog = CreateDialog(message, caption, image);
            Window owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
            if (owner != null) dialog.Owner = owner;
            dialog.ShowDialog();
            return MessageBoxResult.OK;
        }

        /// <summary>在 UI 執行緒建立尚未顯示的對話框；供正式流程與無互動視覺驗證共用。</summary>
        /// <param name="message">要呈現並可捲動閱讀的內容。</param>
        /// <param name="caption">原生標題與內文標題。</param>
        /// <param name="image">Error 時使用錯誤狀態色，其餘使用一般文字色。</param>
        /// <returns>由呼叫端顯示或關閉的 WPF Window。</returns>
        internal static Window CreateDialog(string message, string caption, MessageBoxImage image)
        {
            Window dialog = new Window { Title = caption, Width = 440, SizeToContent = SizeToContent.Height,
                MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 80), ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
            dialog.SourceInitialized += ThemeManager.WindowSourceInitialized;
            Grid body = new Grid { Margin = new Thickness(24) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock title = new TextBlock { Text = caption, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) };
            title.SetResourceReference(TextBlock.ForegroundProperty, image == MessageBoxImage.Error ? "DangerBrush" : "TextBrush");
            ScrollViewer content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap } };
            Grid.SetRow(content, 1);
            Button confirm = new Button { Content = "知道了", IsDefault = true, IsCancel = true, MinWidth = 88,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
            confirm.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
            Grid.SetRow(confirm, 2);
            body.Children.Add(title);
            body.Children.Add(content);
            body.Children.Add(confirm);
            dialog.Content = body;
            return dialog;
        }

        #endregion
    }
}
