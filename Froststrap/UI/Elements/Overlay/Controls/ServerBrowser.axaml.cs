﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class ServerBrowser : UserControl, IDisposable
    {
        private ServerBrowserViewModel? _viewModel;
        private IDisposable? _visibilitySub;
        private bool _disposed;

        public ServerBrowser()
        {
            InitializeComponent();

            _visibilitySub = PublicView.GetObservable(IsVisibleProperty)
                .Subscribe(new AnonymousObserver<bool>(v => _viewModel?.SetVisible(v)));
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