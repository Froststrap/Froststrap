using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

        public static new readonly StyledProperty<FlyoutBase?> FlyoutProperty =
            AvaloniaProperty.Register<IconButton, FlyoutBase?>(nameof(Flyout), null);

        private Button? _secondaryButton;

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

        public new FlyoutBase? Flyout
        {
            get => GetValue(FlyoutProperty);
            set => SetValue(FlyoutProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _secondaryButton?.Click -= OnSecondaryButtonClick;

            _secondaryButton = e.NameScope.Find<Button>("PART_SecondaryButton");

            _secondaryButton?.Click += OnSecondaryButtonClick;
        }

        private void OnSecondaryButtonClick(object? sender, EventArgs e)
        {
            if (Flyout is not null)
            {
                Flyout.ShowAt(this);
                PseudoClasses.Set(":flyout-open", true);
                Flyout.Closed += OnFlyoutClosed;
            }
        }

        private void OnFlyoutClosed(object? sender, EventArgs e)
        {
            PseudoClasses.Set(":flyout-open", false);
            Flyout?.Closed -= OnFlyoutClosed;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == FlyoutProperty)
            {
                if (change.OldValue is FlyoutBase oldFlyout)
                    oldFlyout.Closed -= OnFlyoutClosed;
            }
        }
    }
}