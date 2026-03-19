using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using IconPacks.Avalonia.Material;

namespace Froststrap.UI.Elements.Controls
{
    public class IconButton : Button
    {
        public static readonly StyledProperty<Geometry?> IconDataProperty =
            AvaloniaProperty.Register<IconButton, Geometry?>(nameof(IconData));

        public static readonly StyledProperty<double> IconSizeProperty =
            AvaloniaProperty.Register<IconButton, double>(nameof(IconSize), 12);

        public static readonly StyledProperty<PackIconMaterialKind> IconProperty =
            AvaloniaProperty.Register<IconButton, PackIconMaterialKind>(nameof(Icon));

        public Geometry? IconData
        {
            get => GetValue(IconDataProperty);
            set => SetValue(IconDataProperty, value);
        }

        public double IconSize
        {
            get => GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        public PackIconMaterialKind Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
    }
}