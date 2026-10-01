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

            PublicView.PropertyChanged += (_, e) =>
            {
                if (e.Property == IsVisibleProperty)
                    _viewModel.SetVisible(PublicView.IsVisible);
            };
        }

        public void Attach(ActivityWatcher? activityWatcher)
        {
            _viewModel.SetVisible(false);
            _viewModel = new ServerBrowserViewModel(activityWatcher);
            DataContext = _viewModel;
            _viewModel.SetVisible(PublicView.IsVisible);
            _ = _viewModel.InitialiseAsync();

            PrivateView.Attach(activityWatcher);
        }
    }
}