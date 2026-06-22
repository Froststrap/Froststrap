using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Froststrap.Integrations
{
    [SupportedOSPlatform("windows")]
    public class IntegrationWatcher : IDisposable
    {
        private readonly ActivityWatcher _activityWatcher;
        private readonly Dictionary<int, CustomIntegration> _activeIntegrations = [];

        private HWND _robloxWindowHandle = default;

        private DestroyIconSafeHandle? _customGameIconSmallHandle;
        private DestroyIconSafeHandle? _customGameIconBigHandle;
        private DestroyIconSafeHandle? _defaultRobloxIconSmallHandle;
        private DestroyIconSafeHandle? _defaultRobloxIconBigHandle;

        private const uint WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        public IntegrationWatcher(ActivityWatcher activityWatcher, int robloxProcessId)
        {
            _activityWatcher = activityWatcher;

#if DEBUG
            if (OperatingSystem.IsWindows())
            {
                var robloxProcesses = Process.GetProcessesByName("RobloxPlayerBeta");
                if (robloxProcesses.Length == 0)
                {
                    robloxProcesses = [.. Process.GetProcesses()
                    .Where(p => p.ProcessName.Contains("Roblox", StringComparison.OrdinalIgnoreCase))];
                }

                if (robloxProcesses.Length > 0)
                {
                    _ = robloxProcesses[0].Id;
                }
            }
#endif

            _activityWatcher.OnGameJoin += OnGameJoin;
            _activityWatcher.OnGameLeave += OnGameLeave;

            if (OperatingSystem.IsWindows())
                LoadDefaultIcon();
        }

        [SupportedOSPlatform("windows")]
        private void LoadDefaultIcon()
        {
            try
            {
                using var stream = Resource.GetStream("Icon2025.ico");
                if (stream == null) return;

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                byte[] icoBytes = ms.ToArray();

                int smallWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSMICON);
                int smallHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSMICON);
                int bigWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXICON);
                int bigHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYICON);

                unsafe
                {
                    fixed (byte* pBytes = icoBytes)
                    {
                        int smallOffset = PInvoke.LookupIconIdFromDirectoryEx(pBytes, true, smallWidth, smallHeight, 0);
                        if (smallOffset > 0)
                        {
                            byte[] smallBits = new byte[icoBytes.Length - smallOffset];
                            Buffer.BlockCopy(icoBytes, smallOffset, smallBits, 0, smallBits.Length);

                            _defaultRobloxIconSmallHandle = PInvoke.CreateIconFromResourceEx(smallBits, true, 0x00030000, smallWidth, smallHeight, IMAGE_FLAGS.LR_DEFAULTCOLOR);
                        }

                        int bigOffset = PInvoke.LookupIconIdFromDirectoryEx(pBytes, true, bigWidth, bigHeight, 0);
                        if (bigOffset > 0)
                        {
                            byte[] bigBits = new byte[icoBytes.Length - bigOffset];
                            Buffer.BlockCopy(icoBytes, bigOffset, bigBits, 0, bigBits.Length);

                            _defaultRobloxIconBigHandle = PInvoke.CreateIconFromResourceEx(bigBits, true, 0x00030000, bigWidth, bigHeight, IMAGE_FLAGS.LR_DEFAULTCOLOR);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("IntegrationWatcher::LoadDefaultIcon", $"Failed to load multi-size default asset icon: {ex.Message}");
            }
        }

        private void OnGameJoin(object? sender, EventArgs e)
        {
            if (!_activityWatcher.InGame)
                return;

            if (OperatingSystem.IsWindows())
            {
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

            if (_robloxWindowHandle.Value != IntPtr.Zero || OperatingSystem.IsWindows())
            {
                try
                {
                    if (App.Settings.Prop.AutoChangeIcon)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Resetting window icons back to default");

                        if (_defaultRobloxIconSmallHandle != null && !_defaultRobloxIconSmallHandle.IsInvalid &&
                            _defaultRobloxIconBigHandle != null && !_defaultRobloxIconBigHandle.IsInvalid)
                        {
                            PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_SMALL, _defaultRobloxIconSmallHandle.DangerousGetHandle());
                            PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_BIG, _defaultRobloxIconBigHandle.DangerousGetHandle());
                        }
                        else
                        {
                            PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_SMALL, IntPtr.Zero);
                            PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_BIG, IntPtr.Zero);
                        }

                        _customGameIconSmallHandle?.Dispose();
                        _customGameIconSmallHandle = null;

                        _customGameIconBigHandle?.Dispose();
                        _customGameIconBigHandle = null;
                    }

                    if (App.Settings.Prop.AutoChangeTitle)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Resetting window title back to 'Roblox'");
                        PInvoke.SetWindowText(_robloxWindowHandle, "Roblox");
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

        [SupportedOSPlatform("windows")]
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

        [SupportedOSPlatform("windows")]
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
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                byte[] pngBytes = ms.ToArray();

                int smallWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSMICON);
                int smallHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSMICON);
                int bigWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXICON);
                int bigHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYICON);

                _customGameIconSmallHandle = PInvoke.CreateIconFromResourceEx(pngBytes, true, 0x00030000, smallWidth, smallHeight, IMAGE_FLAGS.LR_DEFAULTCOLOR);
                _customGameIconBigHandle = PInvoke.CreateIconFromResourceEx(pngBytes, true, 0x00030000, bigWidth, bigHeight, IMAGE_FLAGS.LR_DEFAULTCOLOR);

                if (_customGameIconSmallHandle != null && !_customGameIconSmallHandle.IsInvalid &&
                    _customGameIconBigHandle != null && !_customGameIconBigHandle.IsInvalid)
                {
                    PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_SMALL, _customGameIconSmallHandle.DangerousGetHandle());
                    PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_BIG, _customGameIconBigHandle.DangerousGetHandle());

                    App.Logger.WriteLine(LOG_IDENT, "Game icon transformation injected successfully across both small and large sizing frames.");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to process game icon adjustment: {ex.Message}");
            }
        }

        [SupportedOSPlatform("windows")]
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

            _customGameIconSmallHandle?.Dispose();
            _customGameIconBigHandle?.Dispose();
            _defaultRobloxIconSmallHandle?.Dispose();
            _defaultRobloxIconBigHandle?.Dispose();

            _activityWatcher.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}