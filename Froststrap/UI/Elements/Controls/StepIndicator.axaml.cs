using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Froststrap.UI.Elements.Controls
{
    internal partial class StepIndicator : UserControl
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

        static StepIndicator()
        {
            PageCountProperty.Changed.AddClassHandler<StepIndicator>((c, _) => c.UpdateProgress());
            CurrentIndexProperty.Changed.AddClassHandler<StepIndicator>((c, _) => c.UpdateProgress());
        }

        public StepIndicator()
        {
            InitializeComponent();

            FillBar.Transitions =
            [
                new DoubleTransition
                {
                    Property = Layoutable.WidthProperty,
                    Duration = TimeSpan.FromMilliseconds(250),
                    Easing = new CubicEaseOut()
                }
            ];

            Track.SizeChanged += (_, _) => UpdateProgress();

            UpdateProgress();
        }

        private void UpdateProgress()
        {
            if (Track is null || FillBar is null)
                return;

            var trackWidth = Track.Bounds.Width;
            if (trackWidth <= 0)
                return;

            var fraction = PageCount <= 0
                ? 0d
                : Math.Clamp((CurrentIndex + 1) / (double)PageCount, 0d, 1d);

            FillBar.Width = trackWidth * fraction;
        }
    }
}
