using Froststrap.AppData;
using Froststrap.Integrations;

namespace Froststrap
{
    internal class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;

        private readonly NotifyIconWrapper? _notifyIcon;

        public readonly ActivityWatcher? ActivityWatcher;

        public readonly IntegrationWatcher? IntegrationWatcher;

        public readonly Softkey? Softkey;

        public readonly PlayerDiscordRichPresence? PlayerRichPresence;
        public readonly StudioDiscordRichPresence? StudioRichPresence;

        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _isDisposed;
        private int _gameModeHandle = -1;

        public static int? ProcessId { get; private set; }

        public Watcher()
        {
            if (!_lock.IsAcquired)
            {
                App.Logger.Error("Watcher instance already exists");
                return;
            }

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (String.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new InvalidOperationException("Roblox player has not been installed");

                using var gameClientProcess = Process.Start(path);

                _watcherData = new() { ProcessId = gameClientProcess.Id, LaunchMode = LaunchMode.Player };
#else
                throw new InvalidOperationException("Watcher data not specified");
#endif
            }
            else
            {
                _watcherData = JsonSerializer.Deserialize<WatcherData>(Encoding.UTF8.GetString(Convert.FromBase64String(watcherDataArg)));
            }

            if (_watcherData is null)
                throw new InvalidOperationException("Watcher data is invalid");

            ProcessId = _watcherData.ProcessId;

            if (OperatingSystem.IsLinux() && App.Settings.Prop.StudioGameMode && (_watcherData.LaunchMode == LaunchMode.Studio || _watcherData.LaunchMode == LaunchMode.StudioAuth))
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    _gameModeHandle = await RegisterGameModeWithDbusSendAsync(_watcherData.ProcessId, _cancellationTokenSource.Token);
                });
            }

            if (App.Settings.Prop.EnableActivityTracking)
            {
                ActivityWatcher = new(_watcherData.LogFile, _watcherData.LaunchMode, _watcherData.ProcessId);

                if (!string.IsNullOrEmpty(_watcherData.AccessCode))
                {
                    ActivityWatcher.Data.AccessCode = _watcherData.AccessCode;
                    ActivityWatcher.Data.ServerType = ServerType.Private;
                    App.Logger.Info($"Set access code from launch data: {_watcherData.AccessCode}");
                }

                if (App.Settings.Prop.UseDisableAppPatch)
                {
                    ActivityWatcher.OnAppClose += delegate
                    {
                        App.Logger.Info("Received desktop app exit, closing Roblox");
                        using var process = Process.GetProcessById(_watcherData.ProcessId);
                        process.CloseMainWindow();
                    };
                }

                if ((_watcherData.LaunchMode == LaunchMode.Studio || _watcherData.LaunchMode == LaunchMode.StudioAuth) && App.Settings.Prop.StudioRPC)
                    StudioRichPresence = new(ActivityWatcher);
                else if (_watcherData.LaunchMode == LaunchMode.Player && App.Settings.Prop.UseDiscordRichPresence)
                    PlayerRichPresence = new(ActivityWatcher);

                if (_watcherData.LaunchMode == LaunchMode.Player)
                    IntegrationWatcher = new IntegrationWatcher(ActivityWatcher, _watcherData.ProcessId);

                if (_watcherData.LaunchMode == LaunchMode.Player && App.Settings.Prop.SoftKeyEnabled)
                    Softkey = new Softkey(_watcherData.ProcessId);

                _notifyIcon = new(this);
            }

            if (_watcherData.LaunchMode == LaunchMode.Player)
            {
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
                                App.Logger.Info($"Set priority for {proc.Id} to {priorityClass}");
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
                        App.Logger.Error($"Post-launch task error: {ex.Message}");
                    }
                });
            }
        }

        private static async Task<int> RegisterGameModeWithDbusSendAsync(int pid, CancellationToken cancellationToken)
        {
            const string GameModePortalService = "org.freedesktop.portal.Desktop";
            const string GameModePortalPath = "/org/freedesktop/portal/desktop";
            const string GameModePortalInterface = "org.freedesktop.portal.GameMode";

            if (pid <= 0)
            {
                App.Logger.Error($"Invalid PID: {pid}");
                return -1;
            }

            try
            {
                var whichPsi = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "dbus-send",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var whichProcess = Process.Start(whichPsi);
                if (whichProcess == null)
                {
                    App.Logger.Info("Failed to start 'which' command, GameMode registration skipped");
                    return -1;
                }

                var exitTask = whichProcess.WaitForExitAsync(cancellationToken);
                var timeoutTask = Task.Delay(1000, cancellationToken);
                var completedTask = await Task.WhenAny(exitTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    App.Logger.Info("'which' command timed out, GameMode registration skipped");
                    return -1;
                }

                if (whichProcess.ExitCode != 0)
                {
                    App.Logger.Info("dbus-send not found, GameMode registration skipped");
                    return -1;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "dbus-send",
                    Arguments = $"--session --print-reply --dest={GameModePortalService} {GameModePortalPath} {GameModePortalInterface}.RegisterGame int32:{pid}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    App.Logger.Info("Failed to start dbus-send");
                    return -1;
                }

#pragma warning disable CA2016 // ReadToEndAsync doesn't support cancellation tokens
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
#pragma warning restore CA2016
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    App.Logger.Error($"dbus-send failed: {error}");
                    return -1;
                }

                var match = Regex.Match(output, @"int32\s+(\d+)");
                if (!match.Success)
                {
                    App.Logger.Error($"Failed to parse GameMode response: {output}");
                    return -1;
                }

                int handle = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

                if (handle < 0)
                {
                    App.Logger.Error($"GameMode registration rejected with handle {handle}");
                    return -1;
                }

                App.Logger.Info($"Registered with GameMode via dbus-send, handle: {handle}");
                return handle;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to register with GameMode: {ex.Message}");
                return -1;
            }
        }

        private static async Task UnregisterGameModeWithDbusSendAsync(int handle)
        {
            const string GameModePortalService = "org.freedesktop.portal.Desktop";
            const string GameModePortalPath = "/org/freedesktop/portal/desktop";
            const string GameModePortalInterface = "org.freedesktop.portal.GameMode";

            if (handle <= 0)
                return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dbus-send",
                    Arguments = $"--session --print-reply --dest={GameModePortalService} {GameModePortalPath} {GameModePortalInterface}.UnregisterGame int32:{handle}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        App.Logger.Info($"Unregistered from GameMode, handle: {handle}");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to unregister from GameMode: {ex.Message}");
            }
        }

        public void KillRobloxProcess() => CloseProcess(_watcherData!.ProcessId, true);
        public static void CloseProcess(int pid, bool force = false)
        {
            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.Info($"Killing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.Info($"PID {pid} has already exited");
                    return;
                }

                if (force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"PID {pid} could not be closed");
                App.Logger.Error(ex);
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

            if (_gameModeHandle > 0)
            {
                await UnregisterGameModeWithDbusSendAsync(_gameModeHandle);
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
            if (_isDisposed)
                return;

            App.Logger.Info("Disposing Watcher");

            _cancellationTokenSource.Cancel();

            if (_gameModeHandle > 0)
            {
                _ = UnregisterGameModeWithDbusSendAsync(_gameModeHandle);
                _gameModeHandle = -1;
            }

            IntegrationWatcher?.Dispose();
            Softkey?.Dispose();
            _notifyIcon?.Dispose();
            PlayerRichPresence?.Dispose();
            StudioRichPresence?.Dispose();
            ActivityWatcher?.Dispose();
            _cancellationTokenSource.Dispose();
            _lock.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
