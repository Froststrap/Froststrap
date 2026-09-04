using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace Froststrap.UI.Utility;

internal class FluentEntranceTransition : IPageTransition
{
    public double VerticalOffset { get; set; } = 150;
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(350);

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        if (from != null)
        {
            var fadeOut = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(100),
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Setters = { new Setter(Visual.OpacityProperty, 0.0) },
                        Cue = new Cue(1d)
                    }
                }
            };

            await fadeOut.RunAsync(from, cancellationToken);
            from.IsVisible = false;
        }

        if (to != null)
        {
            to.IsVisible = true;

            var slideIn = new Animation
            {
                Duration = Duration,
                Easing = new SplineEasing(0.1, 0.9, 0.2, 1.0),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 0.0),
                            new Setter(TranslateTransform.YProperty, VerticalOffset)
                        },
                        Cue = new Cue(0d)
                    },
                    new KeyFrame
                    {
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 1.0),
                            new Setter(TranslateTransform.YProperty, 0.0)
                        },
                        Cue = new Cue(1d)
                    }
                }
            };

            await slideIn.RunAsync(to, cancellationToken);
        }
    }
}

internal enum SlideDirection
{
    Left,
    Right
}

internal class FluentSlideTransition : IPageTransition
{
    public double HorizontalOffset { get; set; } = 150;
    public SlideDirection Direction { get; set; } = SlideDirection.Right;
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(350);

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        double offsetSign = Direction == SlideDirection.Right ? 1 : -1;
        double startOffset = offsetSign * HorizontalOffset;

        if (from != null)
        {
            var fadeOut = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(100),
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Setters = { new Setter(Visual.OpacityProperty, 0.0) },
                        Cue = new Cue(1d)
                    }
                }
            };

            await fadeOut.RunAsync(from, cancellationToken);
            from.IsVisible = false;
        }

        if (to != null)
        {
            to.IsVisible = true;

            var slideIn = new Animation
            {
                Duration = Duration,
                Easing = new SplineEasing(0.1, 0.9, 0.2, 1.0),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 0.0),
                            new Setter(TranslateTransform.XProperty, startOffset)
                        },
                        Cue = new Cue(0d)
                    },
                    new KeyFrame
                    {
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 1.0),
                            new Setter(TranslateTransform.XProperty, 0.0)
                        },
                        Cue = new Cue(1d)
                    }
                }
            };

            await slideIn.RunAsync(to, cancellationToken);
        }
    }
}