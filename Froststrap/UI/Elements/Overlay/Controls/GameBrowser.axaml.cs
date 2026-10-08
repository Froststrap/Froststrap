using Avalonia;
using Avalonia.Controls;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class GameBrowser : UserControl
    {
        private GameBrowserViewModel _viewModel;
        private bool _attached;

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

            _attached = true;

            LoadContinue();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsVisibleProperty && change.GetNewValue<bool>())
                LoadContinue();
        }

        private void LoadContinue()
        {
            if (_attached && IsVisible)
                _ = _viewModel.LoadContinueAsync();
        }
    }
}