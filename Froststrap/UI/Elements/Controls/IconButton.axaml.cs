using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentIcons.Common;

namespace Froststrap.UI.Elements.Controls
{
    public class IconButton : Button
    {
        public static readonly StyledProperty<Geometry?> IconDataProperty =
            AvaloniaProperty.Register<IconButton, Geometry?>(nameof(IconData));

        public static readonly StyledProperty<double> IconSizeProperty =
            AvaloniaProperty.Register<IconButton, double>(nameof(IconSize), 12);

        public static readonly StyledProperty<Symbol?> IconProperty =
            AvaloniaProperty.Register<IconButton, Symbol?>(nameof(Icon), null);

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

        public Symbol? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
    }
}