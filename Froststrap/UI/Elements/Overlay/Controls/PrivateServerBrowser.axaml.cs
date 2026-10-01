using System;
using Avalonia;
using Avalonia.Controls;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class PrivateServerBrowser : UserControl
    {
        private PrivateServersViewModel _viewModel;

        public PrivateServerBrowser()
        {
            _viewModel = new PrivateServersViewModel(null);
            DataContext = _viewModel;
            InitializeComponent();

            this.GetObservable(IsVisibleProperty)
                .Subscribe(new AnonymousObserver<bool>(v => _viewModel.SetVisible(v)));
        }

        public void Attach(ActivityWatcher? activityWatcher)
        {
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