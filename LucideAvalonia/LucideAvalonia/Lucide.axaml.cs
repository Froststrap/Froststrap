using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using LucideAvalonia.Enum;

namespace LucideAvalonia;

public partial class Lucide : UserControl
{
    public Lucide()
    {
        InitializeComponent();
    }

    // Initialize components
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // Property for setting the icon source
    public static readonly StyledProperty<object?> IconSourceProperty =
        AvaloniaProperty.Register<Lucide, object?>("IconSource");

    public object? IconSource
    {
        get => GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    // Property for setting the icon name
    public static readonly StyledProperty<LucideIconNames> IconProperty =
        AvaloniaProperty.Register<Lucide, LucideIconNames>("Icon");

    public LucideIconNames Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    // Property for setting the icon stroke brush
    public static readonly StyledProperty<IBrush?> StrokeBrushProperty =
        AvaloniaProperty.Register<Lucide, IBrush?>(
            nameof(StrokeBrush),
            defaultBindingMode: BindingMode.TwoWay);

    public IBrush? StrokeBrush
    {
        get => GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    // Define a dependency property for the stroke thickness
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Lucide, double>("StrokeThickness");

    // Property to get or set the stroke thickness
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    // Override the OnPropertyChanged method to handle property changes
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty ||
            change.Property == StrokeBrushProperty ||
            change.Property == StrokeThicknessProperty)
        {
            UpdateIcon();
        }
    }

    private void UpdateIcon()
    {
        var key = Icon.ToString();
        if (Application.Current?.TryGetResource(key, ThemeVariant.Default, out var value) == true &&
            value is DrawingImage drawing)
        {
            IconSource = CloneDrawing(drawing);
        }
        else
        {
            IconSource = null;
        }
    }

    private DrawingImage CloneDrawing(DrawingImage original)
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