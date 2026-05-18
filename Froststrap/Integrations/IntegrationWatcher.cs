using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Froststrap.Utility;
using Froststrap.Enums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Froststrap.Integrations
{
    [SupportedOSPlatform("windows")]
    public class IntegrationWatcher : IDisposable
    {
        private readonly ActivityWatcher _activityWatcher;
        private readonly Dictionary<int, CustomIntegration> _activeIntegrations = [];

        private HWND _robloxWindowHandle = default;
        private readonly uint _robloxPID;
        private readonly int _robloxProcessId;

        private HICON _customGameIconHandle = default;

        private const uint WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateIconFromResourceEx(byte[] pbIconBits, uint cbIconBits, bool fIcon, uint dwVersion, int cxDesired, int cyDesired, uint uFlags);

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

            Task.Run(async () =>
            {
                EnsureWindowHandleCached();

                if (App.Settings.Prop.AutoChangeIcon)
                {
                    await UpdateIconToGameIcon();
                }

                if (App.Settings.Prop.AutoChangeTitle)
                {
                    await UpdateTitleToGameName();
                }
            });

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

            if (_robloxWindowHandle.Value != IntPtr.Zero)
            {
                try
                {
                    if (App.Settings.Prop.AutoChangeTitle)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Resetting window title back to 'Roblox'");
                        PInvoke.SetWindowText(_robloxWindowHandle, "Roblox");
                    }

                    if (App.Settings.Prop.AutoChangeIcon)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Resetting window icons back to default");

                        PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_SMALL, IntPtr.Zero);
                        PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_BIG, IntPtr.Zero);

                        if (_customGameIconHandle.Value != IntPtr.Zero)
                        {
                            PInvoke.DestroyIcon(_customGameIconHandle);
                            _customGameIconHandle = default;
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to reset window modifications: {ex.Message}");
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

        private void EnsureWindowHandleCached()
        {
            if (_robloxWindowHandle.Value != IntPtr.Zero) return;

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

        private async Task UpdateIconToGameIcon()
        {
            const string LOG_IDENT = "IntegrationWatcher::UpdateIconToGameIcon";

            if (_robloxWindowHandle.Value == IntPtr.Zero) return;

            try
            {
                var activity = _activityWatcher.Data;
                if (activity == null || activity.UniverseId == 0) return;

                App.Logger.WriteLine(LOG_IDENT, $"Fetching icon layout for Universe ID: {activity.UniverseId}");

                var request = new ThumbnailRequest
                {
                    TargetId = (ulong)activity.UniverseId,
                    Size = "150x150",
                    Type = ThumbnailType.GameIcon,
                    Format = ThumbnailFormat.Png
                };

                string? iconUrl = await Thumbnails.GetThumbnailUrlAsync(request, CancellationToken.None);

                if (string.IsNullOrEmpty(iconUrl))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to resolve valid asset thumbnail address.");
                    return;
                }

                using var response = await App.HttpClient.GetAsync(iconUrl);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();

                using var image = await Image.LoadAsync<Bgra32>(stream);
                using var ms = new MemoryStream();
                await image.SaveAsPngAsync(ms);
                byte[] pngBytes = ms.ToArray();

                IntPtr hIconRaw = CreateIconFromResourceEx(pngBytes, (uint)pngBytes.Length, true, 0x00030000, image.Width, image.Height, 0);
                HICON copiedHIcon = PInvoke.CopyIcon((HICON)hIconRaw);

                if (copiedHIcon.Value != IntPtr.Zero)
                {
                    _customGameIconHandle = copiedHIcon;

                    PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_SMALL, _customGameIconHandle.Value);
                    PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_BIG, _customGameIconHandle.Value);

                    App.Logger.WriteLine(LOG_IDENT, "Game icon transformation injected successfully via ImageSharp compressed png bits.");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to process game icon adjustment: {ex.Message}");
            }
        }

        private async Task UpdateTitleToGameName()
        {
            const string LOG_IDENT = "IntegrationWatcher::UpdateTitleToGameName";

            if (_robloxWindowHandle.Value == IntPtr.Zero) return;

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

                if (!string.IsNullOrEmpty(gameName))
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

            if (_customGameIconHandle.Value != IntPtr.Zero)
            {
                PInvoke.DestroyIcon(_customGameIconHandle);
                _customGameIconHandle = default;
            }

            _activityWatcher.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}