using System;
using Avalonia.Controls;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class BadgeTracker : UserControl
    {
        private BadgeTrackerViewModel? _viewModel;

        public event EventHandler<Badge>? BadgeEarned;

        public BadgeTracker()
        {
            InitializeComponent();
        }

        public void Attach(ActivityWatcher? activityWatcher)
        {
            if (_viewModel is not null)
                _viewModel.BadgeEarned -= OnBadgeEarned;

            _viewModel = new BadgeTrackerViewModel(activityWatcher);
            _viewModel.BadgeEarned += OnBadgeEarned;

            DataContext = _viewModel;

            _ = _viewModel.LoadAsync();
        }

        private void OnBadgeEarned(object? sender, Badge badge) => BadgeEarned?.Invoke(this, badge);
    }
}