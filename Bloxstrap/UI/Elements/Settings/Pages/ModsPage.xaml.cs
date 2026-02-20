using Bloxstrap.UI.ViewModels.Settings;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Interfaces;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class ModsPage
    {
        private string _originalName = "";
        private ModsViewModel _viewModel = null!;

        public ModsPage()
        {
            _viewModel = new ModsViewModel();
            DataContext = _viewModel;

            _viewModel.OpenModGeneratorEvent += OpenModGenerator;
            _viewModel.OpenCommunityModsEvent += OpenCommunityMods;
            _viewModel.OpenPresetModsEvent += OpenPresetMods;

            InitializeComponent();
            App.FrostRPC?.SetPage("Mods");
        }

        private void ModName_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
                _originalName = textBox.Text;
        }

        private void ModName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && DataContext is ModsViewModel viewModel)
            {
                if (_originalName != textBox.Text)
                {
                    bool success = viewModel.RenameMod(_originalName, textBox.Text);

                    if (success)
                    {
                        var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                        binding?.UpdateSource();
                    }
                    else
                    {
                        textBox.Text = _originalName;
                    }
                }
            }
        }

        private void Page_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is ModsViewModel vm)
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                Task.Run(() => vm.ProcessDroppedFiles(files));
            }
        }

        private void OpenModGenerator(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
            {
                window.Navigate(typeof(ModGeneratorPage));
            }
        }

        private void OpenCommunityMods(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
            {
                window.Navigate(typeof(CommunityModsPage));
            }
        }

        private void OpenPresetMods(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
            {
                window.Navigate(typeof(ModsPresetsPage));
            }
        }
    }
}