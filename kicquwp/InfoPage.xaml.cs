using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace kicquwp
{
    public sealed partial class InfoPage : Page
    {
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
        }

        private void BackButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}