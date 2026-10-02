using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Froststrap.UI.ViewModels.Overlay.Controls;
using System.ComponentModel;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class FriendActivity : UserControl
    {
        private FriendActivityViewModel _viewModel;

        public FriendActivity()
        {
            InitializeComponent();

            DraftBox.AddHandler(KeyDownEvent, DraftKeyDown, RoutingStrategies.Tunnel);

            _viewModel = new FriendActivityViewModel(null);
            DataContext = _viewModel;
        }

        public void Attach(Integrations.Overlay? overlay)
        {
            _viewModel.MessagesAdded -= OnMessagesAdded;
            _viewModel.PropertyChanged -= OnViewModelChanged;

            _viewModel = new FriendActivityViewModel(overlay);
            _viewModel.MessagesAdded += OnMessagesAdded;
            _viewModel.PropertyChanged += OnViewModelChanged;

            DataContext = _viewModel;

            if (IsEffectivelyVisible)
                _ = _viewModel.LoadAsync();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property.Name == nameof(IsEffectivelyVisible) && change.GetNewValue<bool>())
                _ = _viewModel.LoadAsync();
        }

        private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(FriendActivityViewModel.SelectedTab))
                return;

            ScrollToEnd();

            if (_viewModel.HasTab)
                Dispatcher.UIThread.Post(() => DraftBox.Focus(), DispatcherPriority.Input);
        }

        private void OnMessagesAdded(object? sender, EventArgs e) => ScrollToEnd();

        private void ScrollToEnd() =>
            Dispatcher.UIThread.Post(MessageScroller.ScrollToEnd, DispatcherPriority.Loaded);

        private void DraftKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                return;

            e.Handled = true;

            if (_viewModel.SendCommand.CanExecute(null))
                _viewModel.SendCommand.Execute(null);
        }
    }
}