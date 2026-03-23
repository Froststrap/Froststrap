using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Froststrap.UI.Elements.Controls
{
    public class CardExpander : ContentControl
    {
        public static readonly StyledProperty<object?> HeaderProperty =
            AvaloniaProperty.Register<CardExpander, object?>(nameof(Header));

        public static readonly StyledProperty<string?> DescriptionProperty =
            AvaloniaProperty.Register<CardExpander, string?>(nameof(Description));

        public static readonly StyledProperty<object?> HeaderContentProperty =
            AvaloniaProperty.Register<CardExpander, object?>(nameof(HeaderContent));

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<CardExpander, bool>(nameof(IsExpanded), defaultValue: false);

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

            this.PropertyChanged += (sender, args) =>
            {
                if (args.Property == BoundsProperty)
                {
                    UpdateVisualState();
                }
            };

            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (_container == null || _presenter == null) return;

            if (IsExpanded)
            {
                _container.ClearValue(Layoutable.HeightProperty);
                _container.Opacity = 1;
            }
            else
            {
                _container.Height = 0;
                _container.Opacity = 0;
            }
        }
    }
}