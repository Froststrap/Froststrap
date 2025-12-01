using Bloxstrap.UI.ViewModels.Settings;
using System.Windows;
using Wpf.Ui.Mvvm.Contracts;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class FastFlagsPage
    {
        public FastFlagsPage()
        {
            SetupViewModel();
            InitializeComponent();
            App.FrostRPC?.SetPage("FastFlags Settings");
        }

        private bool _initialLoad = false;

        private FastFlagsViewModel _viewModel = null!;

        private void SetupViewModel()
        {
            _viewModel = new FastFlagsViewModel();

            _viewModel.OpenFlagEditorEvent += OpenFlagEditor;
            _viewModel.RequestPageReloadEvent += (_, _) => SetupViewModel();

            DataContext = _viewModel;
        }

        private void OpenFlagEditor(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
            {
                window.Navigate(typeof(FastFlagEditorPage));
            }
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
    }
}