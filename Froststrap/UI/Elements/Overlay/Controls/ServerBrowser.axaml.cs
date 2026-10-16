using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class ServerBrowser : UserControl, IDisposable
    {
        private const double LoadMoreThreshold = 240;

        private ServerBrowserViewModel? _viewModel;
        private IDisposable? _visibilitySub;
        private bool _disposed;

        public ServerBrowser()
        {
            InitializeComponent();

            _visibilitySub = PublicView.GetObservable(IsVisibleProperty)
                .Subscribe(new AnonymousObserver<bool>(v => _viewModel?.SetVisible(v)));

            ServerScroll.AddHandler(ScrollViewer.ScrollChangedEvent, OnServersScrolled);
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
            if (_viewModel is not null)
            {
                _viewModel.SetVisible(false);
                _viewModel.Dispose();
                _viewModel = null;
            }

            _viewModel = new ServerBrowserViewModel(activityWatcher);
            DataContext = _viewModel;

            PrivateView.Attach(activityWatcher);

            _viewModel.SetVisible(PublicView.IsVisible);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _visibilitySub?.Dispose();
            _visibilitySub = null;

            if (_viewModel is not null)
            {
                _viewModel.SetVisible(false);
                _viewModel.Dispose();
                _viewModel = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}