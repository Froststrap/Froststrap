using Avalonia;
using Avalonia.Controls;

namespace Froststrap.UI.Elements.Controls
{
    public partial class SquareCard : UserControl
    {
        public static readonly StyledProperty<string> HeaderProperty =
            AvaloniaProperty.Register<SquareCard, string>(nameof(Header));

        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<SquareCard, string>(nameof(Description));

        public static readonly StyledProperty<object> InnerContentProperty =
            AvaloniaProperty.Register<SquareCard, object>(nameof(InnerContent));

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

        public object InnerContent
        {
            get => GetValue(InnerContentProperty);
            set => SetValue(InnerContentProperty, value);
        }

        public SquareCard()
        {
            InitializeComponent();
        }
    }
}