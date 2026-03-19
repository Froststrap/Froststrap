using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages;

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
}