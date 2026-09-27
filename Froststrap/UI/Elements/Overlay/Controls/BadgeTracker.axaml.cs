using Avalonia.Controls;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class BadgeTracker : UserControl
    {
        private BadgeTrackerViewModel _viewModel;

        public BadgeTracker()
        {
            _viewModel = new BadgeTrackerViewModel(null);

            DataContext = _viewModel;

            InitializeComponent();
        }

        public void Attach(ActivityWatcher? activityWatcher)
        {
            _viewModel = new BadgeTrackerViewModel(activityWatcher);

            DataContext = _viewModel;

            _ = _viewModel.LoadAsync();
        }
    }
}
