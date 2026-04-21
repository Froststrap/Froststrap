using Froststrap.AppData;
using Froststrap.Integrations;

namespace Froststrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;

        private readonly NotifyIconWrapper? _notifyIcon;

        public ActivityWatcher? ActivityWatcher { get; }
        public IntegrationWatcher? IntegrationWatcher { get; }
        public PlayerDiscordRichPresence? PlayerRichPresence { get; }
        public StudioDiscordRichPresence? StudioRichPresence { get; }

        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private int _disposeState = 0;

        public Watcher()
        {
            const string LOG_IDENT = "Watcher";

            if (!_lock.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher instance already exists");
                return;
            }

            _watcherData = LoadWatcherData();

            if (_watcherData is null)
                throw new InvalidOperationException("Watcher data is invalid");

            App.Logger.WriteLine(
                LOG_IDENT,
                $"Watcher data parsed: pid={_watcherData.ProcessId}, launchMode={_watcherData.LaunchMode}, logFile={_watcherData.LogFile ?? "(null)"}, autocloseCount={_watcherData.AutoclosePids?.Count ?? 0}"
            );

            if (_watcherData.ProcessId <= 0)
                throw new InvalidOperationException("Watcher data has invalid process id");

            if (!Enum.IsDefined(_watcherData.LaunchMode) || _watcherData.LaunchMode == LaunchMode.None || _watcherData.LaunchMode == LaunchMode.Unknown)
                throw new InvalidOperationException($"Watcher data has invalid launch mode: {_watcherData.LaunchMode}");

            if (!App.Settings.Prop.EnableActivityTracking)
                return;

            ActivityWatcher = new(_watcherData.LogFile, _watcherData.LaunchMode, _watcherData.ProcessId);

            if (App.Settings.Prop.UseDisableAppPatch)
            {
                ActivityWatcher.OnAppClose += (_, _) =>
                {
                    App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                    CloseProcess(_watcherData.ProcessId, false);
                };
            }

            bool isStudio = _watcherData.LaunchMode == LaunchMode.Studio || _watcherData.LaunchMode == LaunchMode.StudioAuth;

            if (isStudio && App.Settings.Prop.StudioRPC)
                StudioRichPresence = new(ActivityWatcher);
            else if (_watcherData.LaunchMode == LaunchMode.Player && App.Settings.Prop.UseDiscordRichPresence)
                PlayerRichPresence = new(ActivityWatcher);

            _notifyIcon = new(this);
        }

        private static WatcherData? LoadWatcherData()
        {
            const string LOG_IDENT = "Watcher::LoadWatcherData";

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (string.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new ApplicationException("Roblox player has not been installed");

                using var gameClientProcess = Process.Start(path);
                if (gameClientProcess is null)
                    throw new InvalidOperationException("Failed to start Roblox player in debug mode");

                return new WatcherData
                {
                    ProcessId = gameClientProcess.Id,
                    LaunchMode = LaunchMode.Player
                };
#else
                throw new InvalidOperationException("Watcher data not specified");
#endif
            }

            try
            {
                byte[] dataBytes = Convert.FromBase64String(watcherDataArg);
                string json = Encoding.UTF8.GetString(dataBytes);
                WatcherData? data = JsonSerializer.Deserialize<WatcherData>(json);

                if (data is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Deserialized watcher data is null");
                    return null;
                }

                return data;
            }
            catch (FormatException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher data is not valid base64");
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
            catch (JsonException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher data JSON is invalid");
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        public void KillRobloxProcess()
        {
            if (_watcherData is null)
                return;

            CloseProcess(_watcherData.ProcessId, true);
        }

        public void CloseProcess(int pid, bool force = false)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.WriteLine(LOG_IDENT, $"Closing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                if (force)
                {
                    process.Kill(true);
                    return;
                }

                bool closed = process.CloseMainWindow();
                if (!closed)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has no main window, forcing close");
                    process.Kill(true);
                    return;
                }

                if (!process.WaitForExit(5000))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} did not exit after graceful close, forcing close");
                    process.Kill(true);
                }
            }
            catch (ArgumentException)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} is no longer running");
            }
            catch (InvalidOperationException)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} is no longer available");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private async Task WaitForRobloxExitAsync(int pid, CancellationToken cancellationToken)
        {
            const string LOG_IDENT = "Watcher::WaitForRobloxExitAsync";

            try
            {
                using var process = Process.GetProcessById(pid);

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} already exited");
                    return;
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (process.HasExited)
                        return;

                    await Task.Delay(500, cancellationToken);
                    process.Refresh();
                }
            }
            catch (ArgumentException)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} no longer exists");
            }
            catch (InvalidOperationException)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} is unavailable");
            }
        }

        public async Task Run()
        {
            if (!_lock.IsAcquired || _watcherData is null)
                return;

            ActivityWatcher?.Start();

            try
            {
                await WaitForRobloxExitAsync(_watcherData.ProcessId, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_watcherData.AutoclosePids is not null)
            {
                foreach (int pid in _watcherData.AutoclosePids)
                    CloseProcess(pid);
            }

            if (App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Process, "-settings -testmode");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) == 1)
                return;

            App.Logger.WriteLine("Watcher::Dispose", "Disposing Watcher");

            _cancellationTokenSource.Cancel();

            if (App.Settings.Prop.MultiInstanceLaunching)
            {
                App.Logger.WriteLine("Watcher::Dispose", "Starting multi-instance cleanup");
                App.Bootstrapper?.CleanupMultiInstanceResources();
            }

            IntegrationWatcher?.Dispose();
            _notifyIcon?.Dispose();
            PlayerRichPresence?.Dispose();
            StudioRichPresence?.Dispose();
            ActivityWatcher?.Dispose();
            _cancellationTokenSource.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
