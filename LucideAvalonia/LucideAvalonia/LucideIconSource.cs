using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;

namespace LucideAvalonia
{
    public class LucideIconSource : ImageIconSource
    {
        public static readonly StyledProperty<LucideIconNames> IconProperty =
            AvaloniaProperty.Register<LucideIconSource, LucideIconNames>(nameof(Icon));

        public static readonly StyledProperty<IBrush?> StrokeBrushProperty =
            AvaloniaProperty.Register<LucideIconSource, IBrush?>(nameof(StrokeBrush));

        public static readonly StyledProperty<double> StrokeThicknessProperty =
            AvaloniaProperty.Register<LucideIconSource, double>(nameof(StrokeThickness), 2.0);

        public LucideIconNames Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public IBrush? StrokeBrush
        {
            get => GetValue(StrokeBrushProperty);
            set => SetValue(StrokeBrushProperty, value);
        }

        public double StrokeThickness
        {
            get => GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IconProperty ||
                change.Property == StrokeBrushProperty ||
                change.Property == StrokeThicknessProperty)
            {
                UpdateSource();
            }
        }

        private void UpdateSource()
        {
            var key = Icon.ToString();
            if (Application.Current?.TryGetResource(key, ThemeVariant.Default, out var value) == true &&
                value is DrawingImage drawing)
            {
                Source = ApplyStrokeToDrawing(drawing);
            }
            else
            {
                Source = null;
            }
        }

        private DrawingImage ApplyStrokeToDrawing(DrawingImage original)
        {
            if (original.Drawing is not DrawingGroup originalGroup)
                return original;

            var newGroup = new DrawingGroup();
            foreach (var child in originalGroup.Children)
            {
                if (child is GeometryDrawing geo)
                {
                    var newGeo = geo.Geometry?.Clone();
                    if (newGeo == null) continue;

                    var brush = StrokeBrush ?? Brushes.Black;
                    var newPen = new Pen(brush, StrokeThickness);

                    var newDrawing = new GeometryDrawing
                    {
                        Geometry = newGeo,
                        Pen = newPen,
                        Brush = geo.Brush
                    };
                    newGroup.Children.Add(newDrawing);
                }
                else
                {
                    newGroup.Children.Add(child);
                }
            }
            return new DrawingImage(newGroup);
        }
    }
}