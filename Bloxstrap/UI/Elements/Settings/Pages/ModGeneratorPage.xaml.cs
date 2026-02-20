using Bloxstrap.UI.ViewModels.Settings;
using Wpf.Ui.Mvvm.Contracts;
using System.Windows;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class ModGeneratorPage
    {
        private ModGeneratorViewModel _viewModel = null!;

        public ModGeneratorPage()
        {
            _viewModel = new ModGeneratorViewModel();
            DataContext = _viewModel;

            _viewModel.OpenModsEvent += OpenMods;
            _viewModel.OpenCommunityModsEvent += OpenCommunityMods;
            _viewModel.OpenPresetModsEvent += OpenPresetMods;

            InitializeComponent();
            App.FrostRPC?.SetPage("Mod Generator");
        }

        private void OpenMods(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
            {
                window.Navigate(typeof(ModsPage));
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