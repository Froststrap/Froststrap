using Avalonia;
using Avalonia.Controls;
using IconPacks.Avalonia.Material;

namespace Froststrap.UI.Elements.Controls
{
    public class CardAction : Button
    {
        public static readonly StyledProperty<string> HeaderProperty =
            AvaloniaProperty.Register<CardAction, string>(nameof(Header));

        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<CardAction, string>(nameof(Description));

        public static readonly StyledProperty<PackIconMaterialKind> IconProperty =
            AvaloniaProperty.Register<CardAction, PackIconMaterialKind>(nameof(Icon));

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

        public PackIconMaterialKind Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
    }
}