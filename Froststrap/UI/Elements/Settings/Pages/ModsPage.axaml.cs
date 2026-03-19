using Avalonia.Controls;
using Avalonia.Interactivity;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    public partial class ModsPage : UserControl
    {
        private string _originalName = "";

        public ModsPage()
        {
            InitializeComponent();

            App.FrostRPC?.SetPage("Mods");
        }

        private void ModName_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
                _originalName = textBox.Text ?? "";
        }

        private void ModName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && DataContext is ModsViewModel viewModel)
            {
                if (_originalName != textBox.Text)
                {
                    bool success = viewModel.RenameMod(_originalName, textBox.Text ?? "");

                    if (!success)
                    {
                        textBox.Text = _originalName;
                    }
                }
            }
        }
    }
}