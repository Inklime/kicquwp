using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace kicquwp
{
    public sealed partial class DebugLogPage : Page
    {
        public DebugLogPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RefreshLog();
            DebugLogService.LogUpdated += OnLogUpdated;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            DebugLogService.LogUpdated -= OnLogUpdated;
        }

        private async void OnLogUpdated()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, RefreshLog);
        }

        private void RefreshLog()
        {
            LogText.Text = DebugLogService.GetFullLog();
            LogScroll.ChangeView(null, LogScroll.ScrollableHeight, null);
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var dp = new DataPackage();
            dp.SetText(DebugLogService.GetFullLog());
            Clipboard.SetContent(dp);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            DebugLogService.Clear();
            RefreshLog();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}