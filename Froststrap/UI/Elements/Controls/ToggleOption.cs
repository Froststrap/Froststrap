// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Froststrap.UI.Elements.Controls
{
    internal sealed class ToggleOption : OptionControl
    {
        public static readonly StyledProperty<bool> IsCheckedProperty =
            AvaloniaProperty.Register<ToggleOption, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

        private readonly ToggleSwitch _toggle = new();

        public bool IsChecked
        {
            get => GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(OptionControl);

        public ToggleOption()
        {
            _toggle.IsCheckedChanged += (_, _) => IsChecked = _toggle.IsChecked == true;
            InnerContent = _toggle;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsCheckedProperty)
                _toggle.IsChecked = IsChecked;
        }
    }
}
