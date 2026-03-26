using Froststrap.AppData;
using Froststrap.Integrations;
using Froststrap.UI;
using System.Text.Json;
using System.Text;
using System.Diagnostics;

namespace Froststrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;

        private readonly NotifyIconWrapper? _notifyIcon;

        public ActivityWatcher? ActivityWatcher;

        public readonly IntegrationWatcher? IntegrationWatcher;

        public PlayerDiscordRichPresence? PlayerRichPresence;
        public StudioDiscordRichPresence? StudioRichPresence;

        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _isDisposed = false;

        public Watcher()
        {
            const string LOG_IDENT = "Watcher";

            if (!_lock.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher instance already exists");
                return;
            }

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (String.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new ApplicationException("Roblox player is not been installed");

                using var gameClientProcess = Process.Start(path);

                _watcherData = new() { ProcessId = gameClientProcess.Id, LaunchMode = LaunchMode.Player };
#else
                throw new Exception("Watcher data not specified");
#endif
            }
            else
            {
                _watcherData = JsonSerializer.Deserialize<WatcherData>(Encoding.UTF8.GetString(Convert.FromBase64String(watcherDataArg)));
            }

            if (_watcherData is null)
                throw new Exception("Watcher data is invalid");

            if (App.Settings.Prop.EnableActivityTracking)
            {
                string rbxLogDir = Path.Combine(Paths.Roblox, "logs");
                string? detectedLogFile = null;

                App.Logger.WriteLine(LOG_IDENT, "Searching for Roblox log file...");

                for (int i = 0; i < 20; i++)
                {
                    if (Directory.Exists(rbxLogDir))
                    {
                        detectedLogFile = Directory.GetFiles(rbxLogDir, "*.log")
                            .Select(f => new FileInfo(f))
                            .Where(f => f.CreationTimeUtc > DateTime.UtcNow.AddMinutes(-2))
                            .OrderByDescending(f => f.CreationTimeUtc)
                            .FirstOrDefault()?.FullName;
                    }

                    if (detectedLogFile != null) break;
                    Thread.Sleep(500);
                }

                if (detectedLogFile != null)
                {
                    ActivityWatcher = new(detectedLogFile, _watcherData.LaunchMode, _watcherData.ProcessId);

                    if (App.Settings.Prop.UseDisableAppPatch)
                    {
                        ActivityWatcher.OnAppClose += delegate
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                            try
                            {
                                using var process = Process.GetProcessById(_watcherData.ProcessId);
                                process.CloseMainWindow();
                            }
                            catch { }
                        };
                    }

                    if ((_watcherData.LaunchMode == LaunchMode.Studio || _watcherData.LaunchMode == LaunchMode.StudioAuth) && App.Settings.Prop.StudioRPC)
                        StudioRichPresence = new(ActivityWatcher);
                    else if (_watcherData.LaunchMode == LaunchMode.Player && App.Settings.Prop.UseDiscordRichPresence)
                        PlayerRichPresence = new(ActivityWatcher);

                    _notifyIcon = new(this);
                    IntegrationWatcher = new IntegrationWatcher(ActivityWatcher);
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "No log file found. Skipping initialization of UI and RPC.");
                }
            }
        }

        public void KillRobloxProcess() => CloseProcess(_watcherData!.ProcessId, true);

        public void CloseProcess(int pid, bool force = false)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.WriteLine(LOG_IDENT, $"Killing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                if (force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public async Task Run()
        {
            if (!_lock.IsAcquired || _watcherData is null)
                return;

            ActivityWatcher?.Start();

            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    bool running = false;
                    try
                    {
                        using var process = Process.GetProcessById(_watcherData.ProcessId);
                        running = !process.HasExited;
                    }
                    catch { running = false; }

                    if (!running) break;

                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine("Watcher::Run", "Watcher was cancelled");
                return;
            }

            if (_cancellationTokenSource.Token.IsCancellationRequested)
                return;

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
            if (_isDisposed)
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

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}