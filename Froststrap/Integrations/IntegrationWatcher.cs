using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Froststrap.Integrations
{
    [SupportedOSPlatform("windows")]
    internal class IntegrationWatcher : IDisposable
    {
        private static unsafe bool IsHandleValid(HWND hwnd) => hwnd.Value != null;

        private readonly ActivityWatcher _activityWatcher;
        private readonly Dictionary<int, CustomIntegration> _activeIntegrations = [];

        private HWND _robloxWindowHandle;

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
                App.Logger.Error($"Failed to load multi-size default asset icon: {ex.Message}");
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
                if (!integration.SpecifyGame || integration.GameID != currentGameId.ToString(CultureInfo.InvariantCulture))
                    continue;

                LaunchIntegration(integration);
            }
        }

        private unsafe void OnGameLeave(object? sender, EventArgs e)
        {

            if (!IsHandleValid(_robloxWindowHandle) || OperatingSystem.IsWindows())
            {
                try
                {
                    if (App.Settings.Prop.AutoChangeIcon)
                    {
                        App.Logger.Info("Resetting window icons back to default");

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
                        App.Logger.Info("Resetting window title back to 'Roblox'");
                        PInvoke.SetWindowText(_robloxWindowHandle, "Roblox");
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to reset window modifications: {ex.Message}");
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
            if (!IsHandleValid(_robloxWindowHandle)) return;

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
            if (!IsHandleValid(_robloxWindowHandle)) return;

            try
            {
                var activity = _activityWatcher.Data;
                if (activity == null || activity.UniverseId == 0) return;

                App.Logger.Info($"Fetching icon layout for Universe ID: {activity.UniverseId}");

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
                    App.Logger.Info("Failed to resolve valid asset thumbnail address.");
                    return;
                }

                using var response = await App.HttpClient.GetAsync(new Uri(iconUrl));
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

                if (!_customGameIconSmallHandle.IsInvalid && !_customGameIconBigHandle.IsInvalid)
                {
                    PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_SMALL, _customGameIconSmallHandle.DangerousGetHandle());
                    PInvoke.SendMessage(_robloxWindowHandle, WM_SETICON, (WPARAM)ICON_BIG, _customGameIconBigHandle.DangerousGetHandle());

                    App.Logger.Info("Game icon transformation injected successfully across both small and large sizing frames.");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to process game icon adjustment: {ex.Message}");
            }
        }

        [SupportedOSPlatform("windows")]
        private async Task UpdateTitleToGameName()
        {
            if (!IsHandleValid(_robloxWindowHandle)) return;

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
                        App.Logger.Error("Unhandled exception: ", ex);
                    }
                    activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
                }

                if (activity.UniverseDetails?.Data == null) return;

                string gameName = activity.UniverseDetails.Data.Name;
                if (string.IsNullOrEmpty(gameName)) return;

                string title = gameName;

                if (App.Settings.Prop.AutoChangeTitleWithPlayerCount)
                {
                    long playing = activity.UniverseDetails.Data.Playing;
                    var converter = new UI.Converters.NumberAbbreviationConverter();
                    string abbreviated = converter.Convert(playing, typeof(string), null, CultureInfo.CurrentCulture) as string ?? playing.ToString(CultureInfo.InvariantCulture);
                    title = $"{gameName} ({abbreviated} playing)";
                }

                PInvoke.SetWindowText(_robloxWindowHandle, title);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to update title: {ex.Message}");
            }
        }

        private void LaunchIntegration(CustomIntegration integration)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = integration.Location,
                    Arguments = integration.LaunchArgs.Replace("\r\n", " ", StringComparison.Ordinal),
                    WorkingDirectory = Path.GetDirectoryName(integration.Location),
                    UseShellExecute = true
                });

                if (process != null)
                {
                    App.Logger.Info($"Integration '{integration.Name}' launched for game ID '{integration.GameID}' (PID {process.Id}).");
                    _activeIntegrations[process.Id] = integration;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to launch integration '{integration.Name}': {ex.Message}");
            }
        }

        private static void TerminateProcess(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                process.Kill();

                App.Logger.Info($"Terminated integration process (PID {pid}).");
            }
            catch (Exception)
            {
                App.Logger.Error($"Failed to terminate process (PID {pid}), likely already exited.");
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
