// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

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

        public readonly WindowManipulation? WindowManipulation;

        public readonly Softkey? Softkey;

        public readonly PlayerDiscordRichPresence? PlayerRichPresence;
        public readonly StudioDiscordRichPresence? StudioRichPresence;

        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _isDisposed;
        private int _closingRoblox;
        private readonly Lock _closeLock = new();
#pragma warning disable CA2213 // borrowed from CloseRobloxAsync, which disposes it
        private CancellationTokenSource? _closeCancel;
#pragma warning restore CA2213
        private int _gameModeHandle = -1;

        public static int? ProcessId { get; private set; }

        public bool IsActive => _lock.IsAcquired;

        public Watcher(WatcherData watcherData)
        {
            if (!_lock.IsAcquired)
            {
                App.Logger.Error("Watcher instance already exists");
                return;
            }

            _watcherData = watcherData;

            ProcessId = _watcherData.ProcessId;

            if (OperatingSystem.IsWindows() && App.Settings.Prop.EnableWindowManipulation && App.Settings.Prop.EnableActivityTracking)
                WindowManipulation = new(_watcherData.ProcessId);

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
                    ActivityWatcher.OnAppClose += (_, _) => _ = CloseRobloxAsync(_watcherData.ProcessId);

                    // joining another game during the wait cancels the force-close (the close request was already sent)
                    ActivityWatcher.OnLogEntry += (_, entry) =>
                    {
                        if (!entry.Contains(Integrations.ActivityWatcher.GameJoiningEntry, StringComparison.Ordinal))
                            return;

                        lock (_closeLock)
                            _closeCancel?.Cancel();
                    };
                }

                if ((_watcherData.LaunchMode == LaunchMode.Studio || _watcherData.LaunchMode == LaunchMode.StudioAuth) && App.Settings.Prop.StudioRPC)
                    StudioRichPresence = new(ActivityWatcher);
                else if (_watcherData.LaunchMode == LaunchMode.Player && App.Settings.Prop.UseDiscordRichPresence)
                    PlayerRichPresence = new(ActivityWatcher);

                if (_watcherData.LaunchMode == LaunchMode.Player)
                    IntegrationWatcher = new IntegrationWatcher(ActivityWatcher, WindowManipulation);

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
                        if (App.Settings.Prop.AutoCloseCrashHandler)
                        {
                            foreach (var crashProc in Process.GetProcessesByName("RobloxCrashHandler"))
                            {
                                try
                                {
                                    App.Logger.Info($"Found crash handler: {crashProc.ProcessName} (PID {crashProc.Id})");

                                    crashProc.Kill();
                                    await Task.WhenAny(
                                crashProc.WaitForExitAsync(),
                                Task.Delay(3000)
                            );

                                    App.Logger.Info(
                                        crashProc.HasExited
                                            ? $"Crash handler {crashProc.Id} exited successfully"
                                            : $"Crash handler {crashProc.Id} is still running"
                                    );
                                }
                                catch (Exception ex)
                                {
                                    App.Logger.Error($"Failed to close crash handler {crashProc.Id}: {ex}");
                                }
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

        private static bool IsStudioProcess(Process process)
        {
            try
            {
                string expected = Path.GetFileNameWithoutExtension(App.StudioData.ProcessName);
                string actual = Path.GetFileNameWithoutExtension(process.ProcessName);

                if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (OperatingSystem.IsMacOS() && string.Equals(actual, "RobloxStudio", StringComparison.OrdinalIgnoreCase))
                    return true;

                return OperatingSystem.IsLinux()
                    && actual.StartsWith("RobloxStudio", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static int GetNewestStudioProcessId(IEnumerable<Process> processes)
        {
            int newestPid = 0;
            DateTime newestStartTime = DateTime.MinValue;

            foreach (var process in processes)
            {
                if (!IsStudioProcess(process))
                    continue;

                try
                {
                    if (process.StartTime > newestStartTime)
                    {
                        newestStartTime = process.StartTime;
                        newestPid = process.Id;
                    }
                }
                catch (Exception) { }
            }

            return newestPid;
        }

        public async Task Run()
        {
            if (!_lock.IsAcquired || _watcherData is null)
                return;

            ActivityWatcher?.Start();
            WindowManipulation?.Start();

            bool isStudio = _watcherData.LaunchMode == LaunchMode.Studio || _watcherData.LaunchMode == LaunchMode.StudioAuth;

            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var processes = Utility.Processes.GetProcessesSafe();
                    var processExists = processes.Any(x => x.Id == _watcherData.ProcessId);

                    if (!processExists && _watcherData.LaunchMode == LaunchMode.Player)
                    {
                        if (OperatingSystem.IsLinux())
                            processExists = processes.Any(x => x.ProcessName == "sober");
                        else if (OperatingSystem.IsMacOS())
                            processExists = processes.Any(x => x.ProcessName == "RobloxPlayer");
                    }

                    if (isStudio)
                    {
                        var studioProcesses = processes.Where(IsStudioProcess).ToList();

                        if (studioProcesses.Count > 0)
                        {
                            processExists = true;

                            if (!studioProcesses.Any(x => x.Id == _watcherData.ProcessId))
                            {
                                int newestStudioPid = GetNewestStudioProcessId(studioProcesses);

                                if (newestStudioPid != 0)
                                {
                                    App.Logger.Info($"Studio process {_watcherData.ProcessId} is no longer running, now tracking PID {newestStudioPid}");
                                    _watcherData.ProcessId = newestStudioPid;
                                    ProcessId = newestStudioPid;
                                }
                            }
                        }
                    }

                    if (!processExists) break;

                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException) { return; }

            if (_gameModeHandle > 0)
            {
                await UnregisterGameModeWithDbusSendAsync(_gameModeHandle);
            }

            AppStorageManager.Apply();

            if (_watcherData.AutoclosePids is not null)
            {
                foreach (int pid in _watcherData.AutoclosePids)
                    CloseProcess(pid);
            }

            if (App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Process, "--settings --test-mode");
        }

        // "Close To Desktop": asks Roblox to close, then ends it if it's still running
        // (newer Roblox builds can keep running in the system tray when their window is closed)
        private async Task CloseRobloxAsync(int pid)
        {
            // one attempt at a time; a repeat leave line while closing is ignored
            if (Interlocked.Exchange(ref _closingRoblox, 1) == 1)
            {
                App.Logger.Info("Already closing Roblox, ignoring repeat desktop app exit");
                return;
            }

            App.Logger.Info("Received desktop app exit, closing Roblox");
            using var joined = new CancellationTokenSource();

            lock (_closeLock)
                _closeCancel = joined;

            try
            {
                using var process = Process.GetProcessById(pid);

                if (!process.CloseMainWindow())
                    App.Logger.Warn($"Roblox (PID {pid}) has no main window to close");

                using (var wait = CancellationTokenSource.CreateLinkedTokenSource(joined.Token))
                {
                    wait.CancelAfter(TimeSpan.FromSeconds(5));

                    try
                    {
                        await process.WaitForExitAsync(wait.Token);
                        return;
                    }
                    catch (OperationCanceledException) when (joined.IsCancellationRequested)
                    {
                        App.Logger.Info("Joined another game, leaving Roblox open");
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        // still running after 5 seconds
                    }
                }

                // a join can land right as the wait times out
                if (joined.IsCancellationRequested)
                {
                    App.Logger.Info("Joined another game, leaving Roblox open");
                    return;
                }

                // forced on purpose: this setting means "don't keep Roblox running after leaving"
                App.Logger.Warn($"Roblox (PID {pid}) didn't close, ending it");

                try
                {
                    process.Kill();
                }
                catch (Exception) when (HasExited(process))
                {
                    // exited on its own just before Kill
                    return;
                }

                // Kill only starts termination; stay "closing" until it's gone
                if (!await Task.Run(() => process.WaitForExit(TimeSpan.FromSeconds(3))))
                    App.Logger.Warn($"Roblox (PID {pid}) still hasn't exited");
            }
            catch (ArgumentException)
            {
                // already gone
            }
            catch (InvalidOperationException)
            {
                // exited between being found and being asked to close
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"Couldn't close Roblox (PID {pid})");
            }
            finally
            {
                // cleared under the lock so the log reader can't cancel it after it's disposed
                lock (_closeLock)
                    _closeCancel = null;

                Interlocked.Exchange(ref _closingRoblox, 0);
            }
        }

        private static bool HasExited(Process process)
        {
            try
            {
                return process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
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
            WindowManipulation?.Dispose();
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
