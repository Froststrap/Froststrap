using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Froststrap.UI.Elements.Controls
{
    public partial class StepIndicator : UserControl
    {
        public static readonly StyledProperty<int> PageCountProperty =
            AvaloniaProperty.Register<StepIndicator, int>(nameof(PageCount), 3);

        public static readonly StyledProperty<int> CurrentIndexProperty =
            AvaloniaProperty.Register<StepIndicator, int>(nameof(CurrentIndex));

        public int PageCount
        {
            get => GetValue(PageCountProperty);
            set => SetValue(PageCountProperty, value);
        }

        public int CurrentIndex
        {
            get => GetValue(CurrentIndexProperty);
            set => SetValue(CurrentIndexProperty, value);
        }

        private const double DotSize = 8;
        private const double CurrentDotSize = 10;
        private const double AdjacentOpacity = 0.4;

        static StepIndicator()
        {
            PageCountProperty.Changed.AddClassHandler<StepIndicator>((c, _) => c.Rebuild());
            CurrentIndexProperty.Changed.AddClassHandler<StepIndicator>((c, _) => c.UpdateDots());
        }

        public StepIndicator()
        {
            InitializeComponent();
            Rebuild();
        }

        private void Rebuild()
        {
            if (DotsHost is null)
                return;

            DotsHost.Children.Clear();

            for (int i = 0; i < PageCount; i++)
            {
                var dot = new Ellipse
                {
                    Width = DotSize,
                    Height = DotSize,
                    Transitions =
                    [
                        new DoubleTransition { Property = Layoutable.WidthProperty, Duration = TimeSpan.FromMilliseconds(200), Easing = new CubicEaseOut() },
                        new DoubleTransition { Property = Layoutable.HeightProperty, Duration = TimeSpan.FromMilliseconds(200), Easing = new CubicEaseOut() },
                        new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) }
                    ]
                };

                DotsHost.Children.Add(dot);
            }

            UpdateDots();
        }

        private void UpdateDots()
        {
            if (DotsHost is null)
                return;

            for (int i = 0; i < DotsHost.Children.Count; i++)
            {
                if (DotsHost.Children[i] is not Ellipse dot)
                    continue;

                var distance = Math.Abs(i - CurrentIndex);

                if (distance == 0)
                {
                    dot.Width = CurrentDotSize;
                    dot.Height = CurrentDotSize;
                    dot.Opacity = 1.0;
                    dot.Bind(Shape.FillProperty, new DynamicResourceExtension("AccentFillColorDefaultBrush"));
                }
                else if (distance == 1)
                {
                    dot.Width = DotSize;
                    dot.Height = DotSize;
                    dot.Opacity = AdjacentOpacity;
                    dot.Bind(Shape.FillProperty, new DynamicResourceExtension("AccentFillColorDefaultBrush"));
                }
                else
                {
                    dot.Width = DotSize;
                    dot.Height = DotSize;
                    dot.Opacity = 1.0;
                    dot.Bind(Shape.FillProperty, new DynamicResourceExtension("TextFillColorTertiaryBrush"));
                }
            }
        }
    }
}