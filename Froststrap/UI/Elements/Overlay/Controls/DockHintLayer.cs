using System;
using System.Linq;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal class DockHintLayer : Canvas
    {
        private static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan HideGrace = TimeSpan.FromMilliseconds(150);
        private static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(120);

        private const double Gap = 10;
        private const double Edge = 4;

        public static readonly AttachedProperty<string?> TextProperty =
            AvaloniaProperty.RegisterAttached<DockHintLayer, Control, string?>("Text");

        public static string? GetText(Control target) => target.GetValue(TextProperty);
        public static void SetText(Control target, string? value) => target.SetValue(TextProperty, value);

        private readonly Border _bubble;
        private readonly TextBlock _label;
        private readonly DispatcherTimer _showTimer;
        private readonly DispatcherTimer _hideTimer;

        private Control? _target;
        private bool _shown;

        public DockHintLayer()
        {
            IsHitTestVisible = false;

            _label = new TextBlock { FontSize = 13 };
            _label.Bind(TextBlock.ForegroundProperty,
                new DynamicResourceExtension("TextFillColorPrimaryBrush"));

            _bubble = new Border
            {
                Child = _label,
                Padding = new Thickness(10, 5, 10, 6),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Opacity = 0,
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = Visual.OpacityProperty,
                        Duration = Fade
                    }
                },
                Effect = new DropShadowEffect
                {
                    BlurRadius = 12,
                    OffsetX = 0,
                    OffsetY = 2,
                    Opacity = 0.4,
                    Color = Colors.Black
                }
            };

            _bubble.Bind(Border.BackgroundProperty,
                new DynamicResourceExtension("SolidBackgroundFillColorBaseBrush"));
            _bubble.Bind(Border.BorderBrushProperty,
                new DynamicResourceExtension("SurfaceStrokeColorDefaultBrush"));

            Children.Add(_bubble);

            _showTimer = new DispatcherTimer { Interval = ShowDelay };
            _showTimer.Tick += (_, _) => Show();

            _hideTimer = new DispatcherTimer { Interval = HideGrace };
            _hideTimer.Tick += (_, _) => Hide(false);

            PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.IsVisibleProperty && !IsVisible)
                    Hide(true);
            };
        }

        static DockHintLayer()
        {
            TextProperty.Changed.AddClassHandler<Control>(OnTextChanged);
        }

        private static void OnTextChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            control.PointerEntered -= OnEnter;
            control.PointerExited -= OnLeave;
            control.PropertyChanged -= OnTargetPropertyChanged;

            if (e.NewValue is not string text || String.IsNullOrEmpty(text))
                return;

            control.PointerEntered += OnEnter;
            control.PointerExited += OnLeave;
            control.PropertyChanged += OnTargetPropertyChanged;
        }

        private static void OnEnter(object? sender, PointerEventArgs e)
        {
            if (sender is Control control)
                FindLayer(control)?.Hover(control);
        }

        private static void OnLeave(object? sender, PointerEventArgs e)
        {
            if (sender is Control control)
                FindLayer(control)?.Unhover(control, false);
        }

        private static void OnTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != Visual.IsVisibleProperty)
                return;

            if (sender is Control { IsVisible: false } control)
                FindLayer(control)?.Unhover(control, true);
        }

        private static DockHintLayer? FindLayer(Visual element)
        {
            for (Visual? current = element; current is not null; current = current.GetVisualParent())
            {
                if (current is Panel panel
                    && panel.Children.OfType<DockHintLayer>().FirstOrDefault() is DockHintLayer layer)
                {
                    return layer;
                }
            }

            return null;
        }

        private void Hover(Control target)
        {
            _hideTimer.Stop();

            _target = target;
            _label.Text = GetText(target);

            if (_shown)
            {
                Place();
                return;
            }

            _showTimer.Stop();
            _showTimer.Start();
        }

        private void Unhover(Control target, bool instant)
        {
            if (_target != target)
                return;

            _showTimer.Stop();

            if (instant || !_shown)
            {
                Hide(instant);
                return;
            }

            _hideTimer.Stop();
            _hideTimer.Start();
        }

        private void Show()
        {
            _showTimer.Stop();

            if (_target is null || !_target.IsVisible || !IsVisible)
                return;

            Place();

            _shown = true;
            _bubble.Opacity = 1;
        }

        private void Hide(bool instant)
        {
            _showTimer.Stop();
            _hideTimer.Stop();

            _target = null;

            if (!_shown && !instant)
                return;

            _shown = false;

            if (instant)
            {
                Transitions? saved = _bubble.Transitions;
                _bubble.Transitions = null;
                _bubble.Opacity = 0;
                _bubble.Transitions = saved;
            }
            else
            {
                _bubble.Opacity = 0;
            }
        }

        private void Place()
        {
            if (_target is null)
                return;

            _bubble.InvalidateMeasure();
            _bubble.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

            Size size = _bubble.DesiredSize;

            Point? centre = _target.TranslatePoint(new Point(_target.Bounds.Width / 2, 0), this);
            if (centre is null)
                return;

            Visual bar = BarOf(_target);
            Point? barTopLeft = bar.TranslatePoint(default, this);
            if (barTopLeft is null)
                return;

            double left = Math.Clamp(
                centre.Value.X - size.Width / 2,
                Edge,
                Math.Max(Edge, Bounds.Width - size.Width - Edge));

            SetLeft(_bubble, Math.Round(left));
            SetTop(_bubble, Math.Round(barTopLeft.Value.Y - size.Height - Gap));
        }

        private Visual BarOf(Visual target)
        {
            Visual bar = target;
            Visual? current = target;

            while (current is not null)
            {
                Visual? parent = current.GetVisualParent();

                if (parent is null || ReferenceEquals(parent, Parent))
                    break;

                bar = parent;
                current = parent;
            }

            return bar;
        }
    }
}