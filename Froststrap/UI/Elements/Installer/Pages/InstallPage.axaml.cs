using Avalonia.Controls;
using Froststrap.UI.ViewModels.Installer;

namespace Froststrap.UI.Elements.Installer.Pages
{
    public partial class InstallPage : UserControl
    {
        private readonly InstallViewModel _viewModel = new();

        public InstallPage()
        {
            InitializeComponent();

            _viewModel.SetCanContinueEvent += (_, state) =>
            {
                if (TopLevel.GetTopLevel(this) is MainWindow window)
                    window.SetButtonEnabled("next", state);
            };

            Loaded += (sender, e) =>
            {
                if (TopLevel.GetTopLevel(this) is MainWindow window)
                {
                    window.SetNextButtonText(Strings.Common_Navigation_Install);

                    window.NextPageCallback -= NextPageCallback;
                    window.NextPageCallback += NextPageCallback;
                }
            };

            DataContext = _viewModel;
        }

        public async Task<bool> NextPageCallback() => await _viewModel.DoInstall();
    }
}