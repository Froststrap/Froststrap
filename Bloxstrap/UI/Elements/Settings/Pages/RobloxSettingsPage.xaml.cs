using Bloxstrap.Models;
using Bloxstrap.Models.APIs.Config;
using Bloxstrap.UI.ViewModels.Settings;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class RobloxSettingsPage : UiPage
    {
        private RobloxSettingsViewModel? _viewModel;

        public RobloxSettingsPage()
        {
            _viewModel = new RobloxSettingsViewModel();
            DataContext = _viewModel;
            InitializeComponent();
        }

        private void ValidateUInt32(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
                e.Handled = !uint.TryParse(newText, out _);
            }
        }

        private void ValidateFloat(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
                e.Handled = !Regex.IsMatch(newText, @"^-?\d*\.?\d*$");
            }
        }
    }
}
