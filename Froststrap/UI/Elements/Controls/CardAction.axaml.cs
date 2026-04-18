using Avalonia;
using Avalonia.Controls;
using FluentIcons.Common;

namespace Froststrap.UI.Elements.Controls
{
    public class CardAction : Button
    {
        public static readonly StyledProperty<string> HeaderProperty =
            AvaloniaProperty.Register<CardAction, string>(nameof(Header));

        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<CardAction, string>(nameof(Description));

        public static readonly StyledProperty<object> IconProperty =
            AvaloniaProperty.Register<CardAction, object>(nameof(Icon));

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

        public object Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
    }
}