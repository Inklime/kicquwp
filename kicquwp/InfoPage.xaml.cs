using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace kicquwp
{

    public sealed partial class InfoPage : Page
    {
        private int _versionTapCount = 0;
        private DateTime _lastTapTime = DateTime.MinValue;
        public InfoPage()
        {
            this.InitializeComponent();

        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Получаем версию из манифеста
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            VersionText.Text = string.Format("{0}.{1}.{2}.{3}",
                version.Major, version.Minor, version.Build, version.Revision);
            VersionText.Tapped += VersionText_Tapped;
        }

        private void BackButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.GoBack();
        }
        private async void VersionText_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var now = DateTime.Now;
            if ((now - _lastTapTime).TotalMilliseconds > 800)
                _versionTapCount = 0;

            _versionTapCount++;
            _lastTapTime = now;

            if (_versionTapCount >= 3)
            {
                _versionTapCount = 0;
                await ShowLogDialog();
            }
        }

        private async System.Threading.Tasks.Task ShowLogDialog()
        {
            Frame.Navigate(typeof(DebugMenuPage));
        }
    }
}