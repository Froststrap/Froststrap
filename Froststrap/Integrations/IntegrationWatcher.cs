using System.Runtime.InteropServices;

namespace Froststrap.Integrations
{
    public class IntegrationWatcher : IDisposable
    {
        private readonly ActivityWatcher _activityWatcher;
        private readonly Dictionary<int, CustomIntegration> _activeIntegrations = [];

        private const uint WM_SETTEXT = 0x000C;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        public IntegrationWatcher(ActivityWatcher activityWatcher)
        {
            _activityWatcher = activityWatcher;

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
                IntPtr windowHandle = IntPtr.Zero;

                try
                {
                    Process? processById = Watcher.ProcessId != null ? Process.GetProcessById((int)Watcher.ProcessId) : null;
                    if (processById != null)
                        windowHandle = processById.MainWindowHandle;
                }
                catch { }

                if (windowHandle == IntPtr.Zero)
                {
                    foreach (Process proc in Process.GetProcesses())
                    {
                        if (proc.MainWindowTitle == "Roblox")
                        {
                            windowHandle = proc.MainWindowHandle;
                            break;
                        }
                    }
                }

                if (windowHandle != IntPtr.Zero && !string.IsNullOrEmpty(gameName))
                {
                    SendMessage(windowHandle, WM_SETTEXT, IntPtr.Zero, gameName);
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