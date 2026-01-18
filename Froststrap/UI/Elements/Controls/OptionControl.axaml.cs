using Avalonia;
using Avalonia.Controls;

namespace Froststrap.UI.Elements.Controls
{
    public partial class OptionControl : UserControl
    {
        public static readonly StyledProperty<string> HeaderProperty =
            AvaloniaProperty.Register<OptionControl, string>(nameof(Header));

        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<OptionControl, string>(nameof(Description));

        public static readonly StyledProperty<string> HelpLinkProperty =
            AvaloniaProperty.Register<OptionControl, string>(nameof(HelpLink));

        public static readonly StyledProperty<object> InnerContentProperty =
            AvaloniaProperty.Register<OptionControl, object>(nameof(InnerContent));

        public string Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public string Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public string HelpLink
        {
            get => GetValue(HelpLinkProperty);
            set => SetValue(HelpLinkProperty, value);
        }

        public object InnerContent
        {
            get => GetValue(InnerContentProperty);
            set => SetValue(InnerContentProperty, value);
        }

        public OptionControl()
        {
            InitializeComponent();
        }
    }
}