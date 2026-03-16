using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;

namespace Froststrap.UI.Elements.Controls
{
    public class SquareCard : TemplatedControl
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

        [Content]
        public object InnerContent
        {
            get => GetValue(InnerContentProperty);
            set => SetValue(InnerContentProperty, value);
        }
    }
}