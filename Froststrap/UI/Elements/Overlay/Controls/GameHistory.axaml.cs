using Avalonia;
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

            PropertyChanged += OnSelfPropertyChanged;
        }

        public void Attach(ActivityWatcher? activityWatcher)
        {
            _viewModel?.SetVisible(false);

            _viewModel = new GameHistoryViewModel(activityWatcher);
            DataContext = _viewModel;

            _viewModel.SetVisible(IsEffectivelyVisible);
        }

        private void OnSelfPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(IsEffectivelyVisible))
                _viewModel?.SetVisible(IsEffectivelyVisible);
        }
    }
}