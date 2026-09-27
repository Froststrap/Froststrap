using Avalonia.Controls;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class ServerBrowser : UserControl
    {
        private ServerBrowserViewModel _viewModel;

        public ServerBrowser()
        {
            _viewModel = new ServerBrowserViewModel(null);

            DataContext = _viewModel;

            InitializeComponent();
        }

        public void Attach(ActivityWatcher? activityWatcher)
        {
            _viewModel = new ServerBrowserViewModel(activityWatcher);

            DataContext = _viewModel;

            _ = _viewModel.InitialiseAsync();
        }
    }
}
