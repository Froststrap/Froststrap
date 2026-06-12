using Avalonia.Controls;
using Avalonia.Input;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    public partial class IntegrationsPage : UserControl
    {
        public IntegrationsPage()
        {
            InitializeComponent();

            App.FrostRPC?.SetPage("Integration");
        }

        public void CustomIntegrationSelection(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is IntegrationsViewModel viewModel && sender is ListBox listBox)
            {
                viewModel.SelectedCustomIntegration = listBox.SelectedItem as CustomIntegration;
            }
        }

        private void ValidateInt32(object sender, TextInputEventArgs e) => e.Handled = !Int32.TryParse(e.Text, out int _);
    }
}