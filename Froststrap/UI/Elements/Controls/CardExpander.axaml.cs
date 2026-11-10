// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

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
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (_container == null) return;

            _container.MaxHeight = IsExpanded ? double.PositiveInfinity : 0;
        }
    }
}
