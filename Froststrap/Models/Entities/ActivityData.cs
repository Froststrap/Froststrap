using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using System.Web;
using System.Windows.Input;

namespace Froststrap.Models.Entities
{
    internal sealed class ActivityData : IDisposable
    {
        private long _universeId;
        private readonly SemaphoreSlim _serverQuerySemaphore = new(1, 1);
        private bool _disposed;

        /// <summary>
        /// If the current activity stems from an in-universe teleport, then this will be
        /// set to the activity that corresponds to the initial game join
        /// </summary>
        public ActivityData? RootActivity { get; set; }

        public long UniverseId
        {
            get => _universeId;
            set => _universeId = value;
        }

        public long PlaceId { get; set; }

        public string JobId { get; set; } = string.Empty;

        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// This will be empty unless the server joined is a private server
        /// </summary>
        public string AccessCode { get; set; } = string.Empty;

        public long UserId { get; set; }

        public string MachineAddress { get; set; } = string.Empty;

        public bool MachineAddressValid => !string.IsNullOrEmpty(MachineAddress) && !MachineAddress.StartsWith("10.", StringComparison.Ordinal);

        public bool IsTeleport { get; set; }

        public ServerType ServerType { get; set; } = ServerType.Public;

        public DateTime TimeJoined { get; set; }

        public DateTime? TimeLeft { get; set; }

        public DateTime? StartTime { get; set; }

        // everything below here is optional strictly for bloxstraprpc, discord rich presence, or game history

        /// <summary>
        /// This is intended only for other people to use, i.e. context menu invite link, rich presence joining
        /// </summary>
        public string RPCLaunchData { get; set; } = string.Empty;

        public UniverseDetails? UniverseDetails { get; set; }

        public string? RootJobId { get; set; }

        public Bitmap? ThumbnailBitmap { get; set; }

        public event EventHandler<string>? OnDeleteRequested;

        public ICommand RejoinServerCommand => new RelayCommand(() => RejoinServer(true));
        public ICommand CopyDeeplinkCommand => new RelayCommand<Visual>(CopyDeeplink);
        public ICommand CopyServerIdCommand => new RelayCommand<Visual>(CopyServerId);
        public ICommand DeleteHistoryCommand => new RelayCommand(DeleteHistory);

        // Removed the separate SemaphoreSlim field; using the one defined above.

        public string GetInviteDeeplink(bool launchData = true, DeeplinkType type = DeeplinkType.RobloxProtocol)
        {
            string baseUrl = type switch
            {
                DeeplinkType.Froststrap => "http://froststrap.github.io/invite",
                DeeplinkType.RobloxWeb => "https://www.roblox.com/games/start",
                _ => "roblox://experiences/start"
            };

            string deeplink = $"{baseUrl}?placeId={PlaceId}";

            if (ServerType == ServerType.Private)
            {
                if (!string.IsNullOrEmpty(AccessCode))
                    deeplink += "&accessCode=" + AccessCode;
            }
            else
            {
                deeplink += "&gameInstanceId=" + JobId;
            }

            if (launchData && !string.IsNullOrEmpty(RPCLaunchData))
            {
                deeplink += "&launchData=" + HttpUtility.UrlEncode(RPCLaunchData);
            }

            return deeplink;
        }

        public async Task<string?> QueryServerLocation()
        {
            if (!MachineAddressValid)
                throw new InvalidOperationException($"Machine address is invalid ({MachineAddress})");

            await _serverQuerySemaphore.WaitAsync();

            if (GlobalCache.ServerLocation.TryGetValue(MachineAddress, out string? location))
            {
                _serverQuerySemaphore.Release();
                return location;
            }

            try
            {
                Uri ipInfoUrl = new($"https://ipinfo.io/{MachineAddress}/json");
                var ipInfo = await Http.GetJson<IPInfoResponse>(ipInfoUrl);

                if (string.IsNullOrEmpty(ipInfo.City))
                    throw new InvalidHTTPResponseException("Reported city was blank");

                if (ipInfo.City == ipInfo.Region)
                    location = $"{ipInfo.Region}, {ipInfo.Country}";
                else
                    location = $"{ipInfo.City}, {ipInfo.Region}, {ipInfo.Country}";

                GlobalCache.ServerLocation[MachineAddress] = location;
                _serverQuerySemaphore.Release();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to get server location for {MachineAddress}: {ex.Message}");

                GlobalCache.ServerLocation[MachineAddress] = location;
                _serverQuerySemaphore.Release();
            }

            return location;
        }

        public void RejoinServer(bool CloseRoblox = true)
        {
            try
            {
                App.Logger.Info($"Rejoining server: {PlaceId}/{JobId}");

                string robloxUri = GetInviteDeeplink(true);

                Process.Start(new ProcessStartInfo
                {
                    FileName = robloxUri,
                    UseShellExecute = true
                });

                if (CloseRoblox)
                    CloseRobloxProcesses();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to rejoin server: {ex.Message}");
                _ = Frontend.ShowMessageBox($"Failed to rejoin server: {ex.Message}", MessageBoxImage.Error);
            }
        }

        public static void CloseRobloxProcesses()
        {
            try
            {
                var process = Process.GetProcessesByName("RobloxPlayerBeta");

                if (process.Length == 0)
                {
                    App.Logger.Info($"Roblox not found");
                    return;
                }

                foreach (var proc in process)
                {
                    if ((DateTime.Now - proc.StartTime).TotalSeconds < 3)
                    {
                        App.Logger.Info($"Skipping new process");
                        continue;
                    }

                    proc.Kill();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Roblox could not be closed: {ex.Message}");
            }
        }

        private async void CopyDeeplink(Visual? visual)
        {
            var topLevel = TopLevel.GetTopLevel(visual);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(GetInviteDeeplink(true, DeeplinkType.RobloxWeb));
            }
        }

        private async void CopyServerId(Visual? visual)
        {
            var topLevel = TopLevel.GetTopLevel(visual);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(JobId);
            }
        }

        private void DeleteHistory()
        {
            string jobIdToDelete = !string.IsNullOrEmpty(RootJobId) ? RootJobId : JobId;

            if (!string.IsNullOrEmpty(jobIdToDelete))
            {
                OnDeleteRequested?.Invoke(this, jobIdToDelete);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _serverQuerySemaphore?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}