using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Froststrap.UI.Elements.Base;
using System;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class ColorPickerDialog : AvaloniaWindow
    {
        public ColorPickerDialog()
        {
            InitializeComponent();
        }

        public ColorPickerDialog(string initialHex) : this()
        {
            if (!string.IsNullOrEmpty(initialHex) && Color.TryParse(initialHex, out var color))
            {
                var model = Part_SquarePicker.Color;
                model.A = color.A;
                model.RGB_R = color.R;
                model.RGB_G = color.G;
                model.RGB_B = color.B;

                Part_HexBox.Text = initialHex.ToUpper();
            }
        }

        private void Part_HexBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (Part_HexBox == null || Part_SquarePicker == null) return;

            if (!string.IsNullOrWhiteSpace(Part_HexBox.Text) && Color.TryParse(Part_HexBox.Text, out var color))
            {
                var model = Part_SquarePicker.Color;
                model.A = color.A;
                model.RGB_R = color.R;
                model.RGB_G = color.G;
                model.RGB_B = color.B;
            }
        }

        private void OnConfirm_Click(object? sender, RoutedEventArgs e)
        {
            var model = Part_SquarePicker.Color;

            string hex = $"#{(byte)model.A:X2}{(byte)model.RGB_R:X2}{(byte)model.RGB_G:X2}{(byte)model.RGB_B:X2}";

            Close(hex);
        }

        private void OnCancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}