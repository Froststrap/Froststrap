using Avalonia.Controls;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class GameBrowser : UserControl
    {
        private GameBrowserViewModel _viewModel;

        public GameBrowser()
        {
            _viewModel = new GameBrowserViewModel(null);

            DataContext = _viewModel;

            InitializeComponent();
        }

        public void Attach(ActivityWatcher? activityWatcher)
        {
            _viewModel = new GameBrowserViewModel(activityWatcher);

            DataContext = _viewModel;
        }
    }
}
