using Froststrap.AppData;
using Froststrap.Integrations;

namespace Froststrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;

        public readonly NotifyIconWrapper? _notifyIcon;

        public static string? RobloxPath { get; private set; }

        public static int? ProcessId { get; private set; }

        public readonly ActivityWatcher? ActivityWatcher;

        public readonly IntegrationWatcher? IntegrationWatcher;

        public readonly PlayerDiscordRichPresence? PlayerRichPresence;
        public readonly StudioDiscordRichPresence? StudioRichPresence;

        public readonly WindowController? WindowController;

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
                    throw new ApplicationException("Roblox player has not been installed");

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

            RobloxPath = _watcherData.RobloxDirectory;
            ProcessId = _watcherData.ProcessId;

            if (App.Settings.Prop.EnableActivityTracking)
            {
                ActivityWatcher = new(this, _watcherData.LogFile, _watcherData.LaunchMode, _watcherData.ProcessId);

                if (App.Settings.Prop.UseDisableAppPatch)
                {
                    ActivityWatcher.OnAppClose += delegate
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                        using var process = Process.GetProcessById(_watcherData.ProcessId);
                        process.CloseMainWindow();
                    };
                }

                if ((_watcherData.LaunchMode == LaunchMode.Studio || _watcherData.LaunchMode == LaunchMode.StudioAuth) && App.Settings.Prop.StudioRPC)
                    StudioRichPresence = new(ActivityWatcher);
                else if (_watcherData.LaunchMode == LaunchMode.Player && App.Settings.Prop.UseDiscordRichPresence)
                    PlayerRichPresence = new(ActivityWatcher);

                if (App.Settings.Prop.UseWindowControl)
                    WindowController = new(ActivityWatcher);

                IntegrationWatcher = new IntegrationWatcher(ActivityWatcher, _watcherData.ProcessId);

                _notifyIcon = new(this);
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(20000);

                try
                {
                    var processes = Process.GetProcessesByName("RobloxPlayerBeta");

                    foreach (var proc in processes)
                    {
                        if (proc.HasExited) continue;

                        if (App.Settings.Prop.SelectedProcessPriority != ProcessPriorityOption.Normal)
                        {
                            ProcessPriorityClass priorityClass = App.Settings.Prop.SelectedProcessPriority switch
                            {
                                ProcessPriorityOption.Low => ProcessPriorityClass.Idle,
                                ProcessPriorityOption.BelowNormal => ProcessPriorityClass.BelowNormal,
                                ProcessPriorityOption.AboveNormal => ProcessPriorityClass.AboveNormal,
                                ProcessPriorityOption.High => ProcessPriorityClass.High,
                                ProcessPriorityOption.RealTime => ProcessPriorityClass.RealTime,
                                _ => ProcessPriorityClass.Normal
                            };

                            proc.PriorityClass = priorityClass;
                            App.Logger.WriteLine(LOG_IDENT, $"Set priority for {proc.Id} to {priorityClass}");
                        }
                    }

                    if (App.Settings.Prop.AutoCloseCrashHandler)
                    {
                        foreach (var crashProc in Process.GetProcessesByName("RobloxCrashHandler"))
                        {
                            try { crashProc.Kill(); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Post-launch task error: {ex.Message}");
                }
            });
        }

        public void KillRobloxProcess() => CloseProcess(_watcherData!.ProcessId, true);

        public static void CloseProcess(int pid, bool force = false)
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
                    var processExists = Utilities.GetProcessesSafe().Any(x => x.Id == _watcherData.ProcessId);

                    if (!processExists && _watcherData.LaunchMode == LaunchMode.Player)
                    {
                        if (OperatingSystem.IsLinux())
                            processExists = Utilities.GetProcessesSafe().Any(x => x.ProcessName == "sober");
                        else if (OperatingSystem.IsMacOS())
                            processExists = Utilities.GetProcessesSafe().Any(x => x.ProcessName == "RobloxPlayer");
                    }

                    if (!processExists && (_watcherData.LaunchMode == LaunchMode.Studio || _watcherData.LaunchMode == LaunchMode.StudioAuth) && OperatingSystem.IsMacOS())
                        processExists = Utilities.GetProcessesSafe().Any(x => x.ProcessName == "RobloxStudio");

                    if (!processExists) break;

                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException) { return; }

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
            WindowController?.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}