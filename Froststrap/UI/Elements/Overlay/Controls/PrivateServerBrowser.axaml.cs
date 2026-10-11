using System;
using Avalonia;
using Avalonia.Controls;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class PrivateServerBrowser : UserControl
    {
        private PrivateServersViewModel? _viewModel;

        public PrivateServerBrowser()
        {
            InitializeComponent();

            PropertyChanged += OnSelfPropertyChanged;
        }

        public void Attach(ActivityWatcher? activityWatcher)
        {
            _viewModel?.SetVisible(false);

            _viewModel = new PrivateServersViewModel(activityWatcher);
            DataContext = _viewModel;

            _viewModel.SetVisible(IsEffectivelyVisible);
        }

        private void OnSelfPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(IsEffectivelyVisible))
                _viewModel?.SetVisible(IsEffectivelyVisible);
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