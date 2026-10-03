using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Froststrap.Integrations;
using Froststrap.Models.Overlay;
using Froststrap.UI.ViewModels.Overlay.Controls;
using System.ComponentModel;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class FriendActivity : UserControl
    {
        private FriendActivityViewModel _viewModel;

        private ChatTab? _dragTab;
        private Point _dragStart;
        private bool _dragActive;

        public FriendActivity()
        {
            InitializeComponent();

            DraftBox.AddHandler(KeyDownEvent, DraftKeyDown, RoutingStrategies.Tunnel);

            TabScroller.AddHandler(PointerPressedEvent, TabPointerPressed, RoutingStrategies.Tunnel);
            TabScroller.AddHandler(PointerMovedEvent, TabPointerMoved, RoutingStrategies.Tunnel);
            TabScroller.AddHandler(PointerReleasedEvent, TabPointerReleased, RoutingStrategies.Tunnel);

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

        private void TabPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            ChatTab? tab = FindTabFromSource(e.Source as Visual);

            if (tab is null)
                return;

            _dragTab = tab;
            _dragStart = e.GetPosition(TabScroller);
            _dragActive = false;
        }

        private void TabPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragTab is null)
                return;

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                ResetDrag();
                return;
            }

            Point pos = e.GetPosition(TabScroller);

            if (!_dragActive)
            {
                if (Math.Abs(pos.X - _dragStart.X) < 5 && Math.Abs(pos.Y - _dragStart.Y) < 5)
                    return;

                _dragActive = true;
                e.Pointer.Capture(TabScroller);
            }

            int target = FindTabIndexAt(pos);

            if (target < 0)
                return;

            int current = _viewModel.Tabs.IndexOf(_dragTab);

            if (current >= 0 && current != target)
                _viewModel.Tabs.Move(current, target);
        }

        private void TabPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_dragActive)
                e.Handled = true;

            ResetDrag();
        }

        private void ResetDrag()
        {
            _dragTab = null;
            _dragActive = false;
        }

        private static ChatTab? FindTabFromSource(Visual? source)
        {
            Visual? current = source;

            while (current is not null)
            {
                if (current is Control control && control.DataContext is ChatTab tab)
                    return tab;

                current = current.GetVisualParent();
            }

            return null;
        }

        private int FindTabIndexAt(Point point)
        {
            var itemsControl = TabScroller.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault();

            if (itemsControl is null)
                return -1;

            var panel = itemsControl.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault();

            if (panel is null)
                return -1;

            for (int i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is not Visual child)
                    continue;

                Point? topLeft = child.TranslatePoint(new Point(0, 0), TabScroller);

                if (topLeft is null)
                    continue;

                double left = topLeft.Value.X;
                double right = left + child.Bounds.Width;

                if (point.X >= left && point.X <= right)
                    return i;
            }

            return -1;
        }
    }
}