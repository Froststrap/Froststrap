using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public class MultibloxViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private CancellationTokenSource? _launchCts;
        private bool _isLaunching;
        private string _statusText = "Ready";

        public MultibloxViewModel()
        {
            // keep settings defaults reasonable
            if (App.Settings.Prop.MultibloxInstanceCount < 1)
                App.Settings.Prop.MultibloxInstanceCount = 1;
            if (App.Settings.Prop.MultibloxDelayMs < 0)
                App.Settings.Prop.MultibloxDelayMs = 0;
        }

        public int InstanceCount
        {
            get => App.Settings.Prop.MultibloxInstanceCount;
            set
            {
                int clamped = Math.Clamp(value, 1, 10);

                if (clamped != App.Settings.Prop.MultibloxInstanceCount)
                {
                    App.Settings.Prop.MultibloxInstanceCount = clamped;
                    OnPropertyChanged(nameof(InstanceCount));
                }
            }
        }

        public int DelayMs
        {
            get => App.Settings.Prop.MultibloxDelayMs;
            set
            {
                int clamped = Math.Clamp(value, 0, 15000);

                if (clamped != App.Settings.Prop.MultibloxDelayMs)
                {
                    App.Settings.Prop.MultibloxDelayMs = clamped;
                    OnPropertyChanged(nameof(DelayMs));
                }
            }
        }

        public bool IsLaunching
        {
            get => _isLaunching;
            private set
            {
                _isLaunching = value;
                OnPropertyChanged(nameof(IsLaunching));
                OnPropertyChanged(nameof(IsIdle));
            }
        }

        public bool IsIdle => !IsLaunching;

        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public IAsyncRelayCommand LaunchCommand => new AsyncRelayCommand(StartLaunchingAsync);

        public IRelayCommand CancelCommand => new RelayCommand(CancelLaunches);

        private async Task StartLaunchingAsync()
        {
            if (IsLaunching)
                return;

            _launchCts = new CancellationTokenSource();

            IsLaunching = true;
            StatusText = "Starting launch...";

            try
            {
                for (int i = 1; i <= InstanceCount; i++)
                {
                    _launchCts.Token.ThrowIfCancellationRequested();

                    StatusText = $"Launching instance {i} of {InstanceCount}...";

                    if (!LaunchRoblox())
                    {
                        StatusText = "Failed to start Roblox. Check logs for details.";
                        break;
                    }

                    if (i < InstanceCount && DelayMs > 0)
                        await Task.Delay(DelayMs, _launchCts.Token);
                }

                StatusText = "Done. You can close this window.";
            }
            catch (TaskCanceledException)
            {
                StatusText = "Launch cancelled.";
            }
            catch (Exception ex)
            {
                StatusText = "Launch failed. Check logs for details.";
                App.Logger.WriteException("Multiblox::StartLaunchingAsync", ex);
            }
            finally
            {
                IsLaunching = false;
                _launchCts?.Dispose();
                _launchCts = null;
                App.Settings.Save();
            }
        }

        private static bool LaunchRoblox()
        {
            try
            {
                Process.Start(Paths.Application, "-player");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("Multiblox::LaunchRobloxFail", ex);
            }

            return false;
        }

        private void CancelLaunches()
        {
            _launchCts?.Cancel();
        }

        public void Dispose()
        {
            CancelLaunches();
            _launchCts?.Dispose();
        }
    }
}