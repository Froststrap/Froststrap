using Bloxstrap.UI.ViewModels.Settings;
using Wpf.Ui.Mvvm.Contracts;
using System.Windows;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class ModsPresetsPage
    {
        private ModsPresetsViewModel _viewModel = null!;

        public ModsPresetsPage()
        {
            _viewModel = new ModsPresetsViewModel();
            DataContext = _viewModel;

            _viewModel.OpenModsEvent += OpenMods;
            _viewModel.OpenCommunityModsEvent += OpenCommunityMods;
            _viewModel.OpenModGeneratorEvent += OpenModGenerator;

            InitializeComponent();
            App.FrostRPC?.SetPage("Preset Mods");
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

        private void OpenModGenerator(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
            {
                window.Navigate(typeof(ModGeneratorPage));
            }
        }
    }
}