using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal class DockReveal
    {
        private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(200);

        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<DockReveal, Control, bool>("IsEnabled");

        public static bool GetIsEnabled(Control target) => target.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(Control target, bool value) => target.SetValue(IsEnabledProperty, value);

        static DockReveal()
        {
            IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
        }

        private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            control.PointerEntered -= Open;
            control.PointerExited -= Close;

            if (e.NewValue is true)
            {
                control.PointerEntered += Open;
                control.PointerExited += Close;
            }
        }

        private static void Open(object? sender, PointerEventArgs e)
        {
            if (sender is Control control)
                Animate(control, open: true);
        }

        private static void Close(object? sender, PointerEventArgs e)
        {
            if (sender is Control control)
                Animate(control, open: false);
        }

        private static void Animate(Control control, bool open)
        {
            var reveal = control.GetVisualDescendants()
                                .OfType<Decorator>()
                                .FirstOrDefault(d => d.Name == "Reveal");

            if (reveal is not { Child: Control child })
                return;

            child.Measure(Size.Infinity);

            ApplyTransitions(reveal, open);

            reveal.Width = open ? child.DesiredSize.Width : 0;
            reveal.Opacity = open ? 1 : 0;
        }

        private static void ApplyTransitions(Decorator reveal, bool open)
        {
            reveal.Transitions ??= new Transitions();

            var easing = open
                ? (Easing)new CubicEaseOut()
                : new CubicEaseIn();

            UpdateOrAdd(reveal, Layoutable.WidthProperty, easing);
            UpdateOrAdd(reveal, Visual.OpacityProperty, easing);
        }

        private static void UpdateOrAdd(Decorator reveal, AvaloniaProperty property, Easing easing)
        {
            var existing = reveal.Transitions!
                .OfType<DoubleTransition>()
                .FirstOrDefault(t => t.Property == property);

            if (existing is null)
            {
                reveal.Transitions!.Add(new DoubleTransition
                {
                    Property = property,
                    Duration = SlideDuration,
                    Easing = easing
                });
            }
            else
            {
                existing.Easing = easing;
                existing.Duration = SlideDuration;
            }
        }
    }
}