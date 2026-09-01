using Avalonia.Controls;
using Avalonia.Interactivity;
using Froststrap.UI.ViewModels.Onboarding;
using LucideAvalonia.Enum;

namespace Froststrap.UI.Elements.Onboarding.Pages
{
    internal partial class Page1 : UserControl, IDisposable
    {
        private static readonly LucideIconNames[] IconCycle = [LucideIconNames.Languages, LucideIconNames.Globe];
        private static readonly TimeSpan HoldDuration = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan FadeDuration = TimeSpan.FromSeconds(1);

        private CancellationTokenSource? _cts;
        private bool _disposed;

        public Page1()
        {
            DataContext = new Page1ViewModel();
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            _ = RunIconCycleAsync(_cts.Token);
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private async Task RunIconCycleAsync(CancellationToken token)
        {
            var index = 0;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(HoldDuration, token);

                    HeaderIcon.Opacity = 0;
                    await Task.Delay(FadeDuration, token);

                    index = (index + 1) % IconCycle.Length;
                    HeaderIcon.Icon = IconCycle[index];

                    HeaderIcon.Opacity = 1;
                    await Task.Delay(FadeDuration, token);
                }
            }
            catch (TaskCanceledException) { }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;
                }
            }

            _disposed = true;
        }
    }
}