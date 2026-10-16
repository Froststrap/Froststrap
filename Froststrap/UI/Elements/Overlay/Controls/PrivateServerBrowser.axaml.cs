using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class PrivateServerBrowser : UserControl
    {
        private const double LoadMoreThreshold = 240;

        private PrivateServersViewModel? _viewModel;

        public PrivateServerBrowser()
        {
            InitializeComponent();

            ServerScroll.AddHandler(ScrollViewer.ScrollChangedEvent, OnServersScrolled);

            this.GetObservable(IsVisibleProperty)
                .Subscribe(new AnonymousObserver<bool>(v => _viewModel?.SetVisible(v)));
        }

        private void OnServersScrolled(object? sender, ScrollChangedEventArgs e)
        {
            if (sender is not ScrollViewer viewer || viewer.Extent.Height <= 0)
                return;

            if (viewer.Offset.Y + viewer.Viewport.Height < viewer.Extent.Height - LoadMoreThreshold)
                return;

            if (_viewModel is { IsBusy: false, HasMore: true })
                _viewModel.LoadMoreCommand.Execute(null);
        }

        public void Attach(ActivityWatcher? activityWatcher)
        {
            _viewModel?.SetVisible(false);

            _viewModel = new PrivateServersViewModel(activityWatcher);
            DataContext = _viewModel;

            _viewModel.SetVisible(IsVisible);
        }
    }

    internal sealed class AnonymousObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;

        public AnonymousObserver(Action<T> onNext) => _onNext = onNext;

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => _onNext(value);
    }
}