// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Interactivity;
using Avalonia.Media;
using Froststrap.UI.Elements.Base;

namespace Froststrap.UI.Elements.Dialogs
{
    internal partial class ColorPickerDialog : AvaloniaWindow
    {
        public ColorPickerDialog()
        {
            InitializeComponent();
        }

        public ColorPickerDialog(string initialHex) : this()
        {
            if (!string.IsNullOrEmpty(initialHex) && Color.TryParse(initialHex, out var color))
            {
                Part_ColorView.Color = color;
            }
        }

        private void OnConfirm_Click(object? sender, RoutedEventArgs e)
        {
            var color = Part_ColorView.Color;

            string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

            Close(hex);
        }

        private void OnCancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
