using Avalonia.Controls;
using Avalonia.Input;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    public partial class RobloxSettingsPage : UserControl
    {
        private readonly RobloxSettingsViewModel _viewModel;

        public RobloxSettingsPage()
        {
            _viewModel = new RobloxSettingsViewModel();
            DataContext = _viewModel;
            InitializeComponent();
        }

        private void ValidateUInt32(object? sender, TextInputEventArgs e)
        {
            if (sender is TextBox textBox && !string.IsNullOrEmpty(e.Text))
            {
                string currentText = textBox.Text ?? string.Empty;
                int caretIndex = textBox.CaretIndex;
                string newText = currentText.Insert(caretIndex, e.Text);

                e.Handled = !uint.TryParse(newText, out _);
            }
        }

        private void ValidateFloat(object? sender, TextInputEventArgs e)
        {
            if (sender is TextBox textBox && !string.IsNullOrEmpty(e.Text))
            {
                string currentText = textBox.Text ?? string.Empty;
                int caretIndex = textBox.CaretIndex;
                string newText = currentText.Insert(caretIndex, e.Text);

                e.Handled = !Regex.IsMatch(newText, @"^-?\d*\.?\d*$");
            }
        }
    }
}