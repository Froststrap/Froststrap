using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LucideAvalonia.Enum;

namespace Froststrap.UI.Elements.Controls
{
    public class CardAction : Button
    {
        public static readonly StyledProperty<string> HeaderProperty =
            AvaloniaProperty.Register<CardAction, string>(nameof(Header));

        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<CardAction, string>(nameof(Description));

        public static readonly StyledProperty<LucideIconNames> IconProperty =
            AvaloniaProperty.Register<CardAction, LucideIconNames>(nameof(Icon));

        public static readonly StyledProperty<double> IconSizeProperty =
            AvaloniaProperty.Register<CardAction, double>(nameof(IconSize), 24);

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

        public LucideIconNames Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public double IconSize
        {
            get => GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }
    }
}