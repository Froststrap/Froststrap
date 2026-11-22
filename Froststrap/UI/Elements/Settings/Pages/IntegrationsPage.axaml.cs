// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    internal sealed partial class IntegrationsPage : UserControl
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
}