// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;

namespace Froststrap.UI.Elements.Controls
{
    internal class CardAction : Button
    {
        public static readonly StyledProperty<string> HeaderProperty =
            AvaloniaProperty.Register<CardAction, string>(nameof(Header));

        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<CardAction, string>(nameof(Description));

        public static readonly StyledProperty<object?> IconProperty =
            AvaloniaProperty.Register<CardAction, object?>(nameof(Icon));

        public static readonly StyledProperty<double> IconSizeProperty =
            AvaloniaProperty.Register<CardAction, double>(nameof(IconSize), 24);

        public string Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public string Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public object? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public double IconSize
        {
            get => GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }
    }
}