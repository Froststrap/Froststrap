using Avalonia;
using Avalonia.Controls;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class ServerBrowser : UserControl
    {
        private ServerBrowserViewModel? _viewModel;

        public ServerBrowser()
        {
            InitializeComponent();

            PublicView.PropertyChanged += OnPublicViewPropertyChanged;
        }

        public void Attach(ActivityWatcher? activityWatcher)
        {
            _viewModel?.SetVisible(false);

            _viewModel = new ServerBrowserViewModel(activityWatcher);
            DataContext = _viewModel;

            PrivateView.Attach(activityWatcher);

            _viewModel.SetVisible(PublicView.IsEffectivelyVisible);
        }

        private void OnPublicViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(PublicView.IsEffectivelyVisible))
                _viewModel?.SetVisible(PublicView.IsEffectivelyVisible);
        }
    }
}