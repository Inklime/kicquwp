using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace kicquwp
{
    public sealed partial class FeatureItem : UserControl
    {
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(string),
                typeof(FeatureItem), new PropertyMetadata("&#xE8D4;",
                    (d, e) => ((FeatureItem)d).IconText.Glyph = (string)e.NewValue));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string),
                typeof(FeatureItem), new PropertyMetadata("",
                    (d, e) => ((FeatureItem)d).FeatureText.Text = (string)e.NewValue));

        public string Icon
        {
            get { return (string)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public FeatureItem()
        {
            this.InitializeComponent();
        }
    }
}
