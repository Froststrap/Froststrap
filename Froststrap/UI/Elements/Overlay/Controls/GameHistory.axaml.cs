using Avalonia.Controls;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class GameHistory : UserControl
    {
        private GameHistoryViewModel? _viewModel;

        public GameHistory()
        {
            InitializeComponent();
        }

        public void Attach(ActivityWatcher? activityWatcher)
        {
            _viewModel = new GameHistoryViewModel(activityWatcher);
            DataContext = _viewModel;
        }
    }
}