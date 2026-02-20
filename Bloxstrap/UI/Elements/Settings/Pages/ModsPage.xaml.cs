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
        private bool _initialLoad = false;
        private ModsViewModel _viewModel = null!;
        private ModGeneratorViewModel _gviewModel = null!;
        private CommunityModsViewModel _cviewModel = null!;

        public ModsPage()
        {
            _gviewModel = new ModGeneratorViewModel();
            _gviewModel.ReloadModListEvent += (s, e) => _initialLoad = true;

            _cviewModel = new CommunityModsViewModel();
            _cviewModel.ReloadModListEvent += (s, e) => _initialLoad = true;

            SetupViewModel();

            InitializeComponent();
            App.FrostRPC?.SetPage("Mods");
        }

        private void SetupViewModel()
        {
            _viewModel = new ModsViewModel();
            DataContext = _viewModel;

            _viewModel.OpenModGeneratorEvent += OpenModGenerator;
            _viewModel.OpenCommunityModsEvent += OpenCommunityMods;
            _viewModel.OpenPresetModsEvent += OpenPresetMods;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            SetupViewModel();
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