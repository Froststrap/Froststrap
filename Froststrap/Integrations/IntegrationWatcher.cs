using Windows.Win32;
using Windows.Win32.Foundation;

namespace Froststrap.Integrations
{
    public class IntegrationWatcher : IDisposable
    {
        private readonly ActivityWatcher _activityWatcher;
        private readonly Dictionary<int, CustomIntegration> _activeIntegrations = [];

        private HWND _robloxWindowHandle = default;
        private readonly uint _robloxPID;
        private readonly int _robloxProcessId;

        public IntegrationWatcher(ActivityWatcher activityWatcher, int robloxProcessId)
        {
            _activityWatcher = activityWatcher;

#if DEBUG
            var robloxProcesses = Process.GetProcessesByName("RobloxPlayerBeta");
            if (robloxProcesses.Length == 0)
            {
                robloxProcesses = [.. Process.GetProcesses().Where(p => p.ProcessName.Contains("Roblox", StringComparison.OrdinalIgnoreCase))];
            }

            if (robloxProcesses.Length > 0)
            {
                _robloxProcessId = robloxProcesses[0].Id;
                _robloxPID = (uint)_robloxProcessId;
            }
            else
            {
                _robloxProcessId = robloxProcessId;
                _robloxPID = (uint)robloxProcessId;
            }
#else
            _robloxProcessId = robloxProcessId;
            _robloxPID = (uint)robloxProcessId;
#endif

            _activityWatcher.OnGameJoin += OnGameJoin;
            _activityWatcher.OnGameLeave += OnGameLeave;
        }

        private void OnGameJoin(object? sender, EventArgs e)
        {
            if (!_activityWatcher.InGame)
                return;

            if (App.Settings.Prop.AutoChangeTitle)
            {
                Task.Run(() => UpdateTitleToGameName());
            }

            long currentGameId = _activityWatcher.Data.PlaceId;

            foreach (var integration in App.Settings.Prop.CustomIntegrations)
            {
                if (!integration.SpecifyGame || integration.GameID != currentGameId.ToString())
                    continue;

                LaunchIntegration(integration);
            }
        }

        private void OnGameLeave(object? sender, EventArgs e)
        {
            const string LOG_IDENT = "IntegrationWatcher::OnGameLeave";

            if (App.Settings.Prop.AutoChangeTitle && _robloxWindowHandle.Value != IntPtr.Zero)
            {
                try
                {
                    App.Logger.WriteLine(LOG_IDENT, "Resetting window title back to 'Roblox'");
                    PInvoke.SetWindowText(_robloxWindowHandle, "Roblox");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to reset title: {ex.Message}");
                }
            }

            foreach (var pid in _activeIntegrations.Keys.ToList())
            {
                var integration = _activeIntegrations[pid];
                if (integration.AutoCloseOnGame)
                {
                    TerminateProcess(pid);
                    _activeIntegrations.Remove(pid);
                }
            }
        }

        private async Task UpdateTitleToGameName()
        {
            const string LOG_IDENT = "IntegrationWatcher::UpdateTitleToGameName";

            try
            {
                var activity = _activityWatcher.Data;
                if (activity == null) return;

                if (activity.UniverseDetails is null)
                {
                    try
                    {
                        await UniverseDetails.FetchSingle(activity.UniverseId);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                    activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
                }

                if (activity.UniverseDetails?.Data == null) return;

                string gameName = activity.UniverseDetails.Data.Name;

                if (_robloxWindowHandle.Value == IntPtr.Zero)
                {
                    IntPtr nativeHandle = IntPtr.Zero;
                    try
                    {
                        Process? processById = Watcher.ProcessId != null ? Process.GetProcessById((int)Watcher.ProcessId) : null;
                        if (processById != null)
                            nativeHandle = processById.MainWindowHandle;
                    }
                    catch { }

                    if (nativeHandle == IntPtr.Zero)
                    {
                        foreach (Process proc in Process.GetProcesses())
                        {
                            if (proc.MainWindowTitle == "Roblox")
                            {
                                nativeHandle = proc.MainWindowHandle;
                                break;
                            }
                        }
                    }

                    _robloxWindowHandle = (HWND)nativeHandle;
                }

                if (_robloxWindowHandle.Value != IntPtr.Zero && !string.IsNullOrEmpty(gameName))
                {
                    PInvoke.SetWindowText(_robloxWindowHandle, gameName);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to update title: {ex.Message}");
            }
        }

        private void LaunchIntegration(CustomIntegration integration)
        {
            const string LOG_IDENT = "IntegrationWatcher::LaunchIntegration";

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = integration.Location,
                    Arguments = integration.LaunchArgs.Replace("\r\n", " "),
                    WorkingDirectory = Path.GetDirectoryName(integration.Location),
                    UseShellExecute = true
                });

                if (process != null)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Integration '{integration.Name}' launched for game ID '{integration.GameID}' (PID {process.Id}).");
                    _activeIntegrations[process.Id] = integration;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to launch integration '{integration.Name}': {ex.Message}");
            }
        }

        private static void TerminateProcess(int pid)
        {
            const string LOG_IDENT = "IntegrationWatcher::TerminateProcess";

            try
            {
                var process = Process.GetProcessById(pid);
                process.Kill();

                App.Logger.WriteLine(LOG_IDENT, $"Terminated integration process (PID {pid}).");
            }
            catch (Exception)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to terminate process (PID {pid}), likely already exited.");
            }
        }

        public void Dispose()
        {
            foreach (var pid in _activeIntegrations.Keys)
            {
                TerminateProcess(pid);
            }

            _activeIntegrations.Clear();
            _activityWatcher.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}