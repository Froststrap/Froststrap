using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using LucideAvalonia.Enum;
using System;
using System.Windows.Input;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class OverlayPanel : UserControl
    {
        public const double MinPanelWidth = 380;
        public const double MinPanelHeight = 260;

        private static int _topmostZIndex;

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<OverlayPanel, string>(nameof(Title), String.Empty);

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly StyledProperty<LucideIconNames> IconProperty =
            AvaloniaProperty.Register<OverlayPanel, LucideIconNames>(nameof(Icon), LucideIconNames.Circle);

        public LucideIconNames Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly StyledProperty<object?> PanelContentProperty =
            AvaloniaProperty.Register<OverlayPanel, object?>(nameof(PanelContent));

        public object? PanelContent
        {
            get => GetValue(PanelContentProperty);
            set => SetValue(PanelContentProperty, value);
        }

        public static readonly StyledProperty<Thickness> ContentPaddingProperty =
            AvaloniaProperty.Register<OverlayPanel, Thickness>(nameof(ContentPadding), new Thickness(12));

        public Thickness ContentPadding
        {
            get => GetValue(ContentPaddingProperty);
            set => SetValue(ContentPaddingProperty, value);
        }

        public static readonly StyledProperty<ICommand?> CloseCommandProperty =
            AvaloniaProperty.Register<OverlayPanel, ICommand?>(nameof(CloseCommand));

        public ICommand? CloseCommand
        {
            get => GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }

        public static readonly StyledProperty<object?> CloseCommandParameterProperty =
            AvaloniaProperty.Register<OverlayPanel, object?>(nameof(CloseCommandParameter));

        public object? CloseCommandParameter
        {
            get => GetValue(CloseCommandParameterProperty);
            set => SetValue(CloseCommandParameterProperty, value);
        }

        private Point _dragStart;
        private Point _dragOrigin;
        private bool _dragging;
        private IPointer? _capturedPointer;

        public OverlayPanel()
        {
            InitializeComponent();
            AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        }

        private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
        {
            Raise();
        }

        private Canvas? Surface => Parent as Canvas;

        public Point Position => new(
            Or(Canvas.GetLeft(this), 0),
            Or(Canvas.GetTop(this), 0));

        public Size PanelSize => new(
            Or(Width, Bounds.Width),
            Or(Height, Bounds.Height));

        public int Depth => ZIndex;

        public Size SurfaceSize => Surface is null
            ? default
            : new Size(Surface.Bounds.Width, Surface.Bounds.Height);

        private static double Or(double value, double fallback) =>
            Double.IsNaN(value) ? fallback : value;

        public void Raise() => ZIndex = ++_topmostZIndex;

        public void PlaceAt(double left, double top, double width, double height)
        {
            Width = width;
            Height = height;

            Canvas.SetLeft(this, left);
            Canvas.SetTop(this, top);

            Clamp();
            Raise();
        }

        #region Dragging

        private void TitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (Surface is null)
                return;

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            _dragging = true;
            _dragStart = e.GetPosition(Surface);
            _dragOrigin = new Point(
                Canvas.GetLeft(this),
                Canvas.GetTop(this));

            _capturedPointer = e.Pointer;
            e.Pointer.Capture(TitleBar);
            e.Handled = true;
        }

        private void TitleBarPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_dragging || Surface is null)
                return;

            Vector delta = e.GetPosition(Surface) - _dragStart;

            Canvas.SetLeft(this, _dragOrigin.X + delta.X);
            Canvas.SetTop(this, _dragOrigin.Y + delta.Y);

            Clamp();
        }

        private void TitleBarPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_dragging)
                return;

            _dragging = false;

            _capturedPointer?.Capture(null);
            _capturedPointer = null;
        }

        #endregion

        #region Resizing

        private void ResizeDragDelta(object? sender, VectorEventArgs e)
        {
            if (sender is not Thumb thumb || thumb.Tag is not string edge || Surface is null)
                return;

            if (edge.Contains("Left", StringComparison.Ordinal))
            {
                double applied = Grow(Bounds.Width, -e.Vector.X, MinPanelWidth, out double width);

                Width = width;

                Canvas.SetLeft(this, Canvas.GetLeft(this) - applied);
            }
            else if (edge.Contains("Right", StringComparison.Ordinal))
            {
                Grow(Bounds.Width, e.Vector.X, MinPanelWidth, out double width);

                Width = width;
            }

            if (edge.Contains("Top", StringComparison.Ordinal))
            {
                double applied = Grow(Bounds.Height, -e.Vector.Y, MinPanelHeight, out double height);

                Height = height;

                Canvas.SetTop(this, Canvas.GetTop(this) - applied);
            }
            else if (edge.Contains("Bottom", StringComparison.Ordinal))
            {
                Grow(Bounds.Height, e.Vector.Y, MinPanelHeight, out double height);

                Height = height;
            }

            Clamp();
        }

        private static double Grow(double current, double delta, double min, out double result)
        {
            result = Math.Max(current + delta, min);

            return result - current;
        }

        #endregion

        public void Clamp()
        {
            if (Surface is null || Surface.Bounds.Width == 0)
                return;

            double width = Math.Min(Double.IsNaN(Width) ? Bounds.Width : Width, Surface.Bounds.Width);
            double height = Math.Min(Double.IsNaN(Height) ? Bounds.Height : Height, Surface.Bounds.Height);

            if (width > 0)
                Width = Math.Max(width, MinPanelWidth);

            if (height > 0)
                Height = Math.Max(height, MinPanelHeight);

            Canvas.SetLeft(this, Math.Clamp(Canvas.GetLeft(this), 0, Math.Max(Surface.Bounds.Width - Width, 0)));
            Canvas.SetTop(this, Math.Clamp(Canvas.GetTop(this), 0, Math.Max(Surface.Bounds.Height - Height, 0)));
        }

        private void BodySizeChanged(object? sender, SizeChangedEventArgs e) =>
            Body.Clip = new RectangleGeometry(new Rect(e.NewSize), 7, 7);
    }
}