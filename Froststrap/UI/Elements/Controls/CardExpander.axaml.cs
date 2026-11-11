// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Froststrap.UI.Elements.Controls
{
    internal class CardExpander : ContentControl
    {
        public static readonly StyledProperty<object?> HeaderProperty =
            AvaloniaProperty.Register<CardExpander, object?>(nameof(Header));

        public static readonly StyledProperty<string?> DescriptionProperty =
            AvaloniaProperty.Register<CardExpander, string?>(nameof(Description));

        public static readonly StyledProperty<object?> HeaderContentProperty =
            AvaloniaProperty.Register<CardExpander, object?>(nameof(HeaderContent));

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<CardExpander, bool>(nameof(IsExpanded), defaultValue: false);

        private static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(150);

        public object? Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public string? Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public object? HeaderContent
        {
            get => GetValue(HeaderContentProperty);
            set => SetValue(HeaderContentProperty, value);
        }

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        private Border? _container;
        private ContentPresenter? _presenter;
        private CancellationTokenSource? _animationCts;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsExpandedProperty)
            {
                UpdateVisualState();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _container = e.NameScope.Find<Border>("PART_ContentContainer");
            _presenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");

            UpdateVisualState(animate: false);
        }

        private async void UpdateVisualState(bool animate = true)
        {
            if (_container == null || _presenter == null) return;

            _animationCts?.Cancel();
            _animationCts = null;

            var useAnimation = animate && !App.Settings.Prop.DisableAnimations;

            if (!useAnimation)
            {
                _container.Transitions = null;

                if (IsExpanded)
                {
                    // Let the content size itself so items added later are never clipped.
                    _container.MaxHeight = double.PositiveInfinity;
                }
                else
                {
                    _container.MaxHeight = 0;
                }

                return;
            }

            var cts = new CancellationTokenSource();
            _animationCts = cts;

            try
            {
                if (IsExpanded)
                {
                    await AnimateExpandedAsync(cts.Token);
                }
                else
                {
                    await AnimateCollapsedAsync(cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_animationCts, cts))
                {
                    _animationCts = null;
                }

                cts.Dispose();
            }
        }

        private async Task AnimateExpandedAsync(CancellationToken token)
        {
            if (_container == null || _presenter == null) return;

            var targetHeight = MeasureContentHeight();
            if (!double.IsFinite(targetHeight))
            {
                _container.Transitions = null;
                _container.MaxHeight = double.PositiveInfinity;
                return;
            }

            _container.Transitions = null;
            _container.MaxHeight = 0;

            _container.Transitions = CreateHeightTransition();
            _container.MaxHeight = targetHeight;

            await Task.Delay(ExpandDuration, token);

            if (token.IsCancellationRequested) return;

            _container.Transitions = null;
            // Hand the height back to the layout system so dynamic content keeps fitting.
            _container.MaxHeight = double.PositiveInfinity;
        }

        private async Task AnimateCollapsedAsync(CancellationToken token)
        {
            if (_container == null) return;

            var currentHeight = _container.Bounds.Height;
            if (currentHeight <= 0)
            {
                _container.Transitions = null;
                _container.MaxHeight = 0;
                return;
            }

            // MaxHeight is infinite while expanded, and infinity can't be animated from,
            // so pin it to the current height first.
            _container.Transitions = null;
            _container.MaxHeight = currentHeight;

            _container.Transitions = CreateHeightTransition();
            _container.MaxHeight = 0;

            await Task.Delay(ExpandDuration, token);

            if (token.IsCancellationRequested) return;

            _container.Transitions = null;
            _container.MaxHeight = 0;
        }

        private double MeasureContentHeight()
        {
            if (_container == null || _presenter == null) return double.PositiveInfinity;

            var availableWidth = _container.Bounds.Width;
            if (availableWidth <= 0)
            {
                availableWidth = Bounds.Width;
            }

            if (availableWidth <= 0) return double.PositiveInfinity;

            _presenter.Measure(new Size(availableWidth, double.PositiveInfinity));

            var desiredHeight = _presenter.DesiredSize.Height;
            if (double.IsNaN(desiredHeight) || double.IsInfinity(desiredHeight))
            {
                return double.PositiveInfinity;
            }

            return desiredHeight + _container.BorderThickness.Top + _container.BorderThickness.Bottom;
        }

        private static Transitions CreateHeightTransition()
        {
            return
            [
                new DoubleTransition
                {
                    Property = Layoutable.MaxHeightProperty,
                    Duration = ExpandDuration,
                    Easing = new CubicEaseOut()
                }
            ];
        }
    }
}
