using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Froststrap.Integrations;
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

        public void Attach(IOverlayHost? overlay)
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

        private void TabScroller_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.Handled || sender is not ScrollViewer scroller)
                return;

            if (scroller.ScrollBarMaximum.X <= 0)
                return;

            double next = scroller.Offset.X - (e.Delta.Y * 60);

            next = Math.Clamp(next, 0, scroller.ScrollBarMaximum.X);

            if (Math.Abs(next - scroller.Offset.X) < 0.5)
                return;

            scroller.Offset = new Vector(next, scroller.Offset.Y);
            e.Handled = true;
        }
    }
}