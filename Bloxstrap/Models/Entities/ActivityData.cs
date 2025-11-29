using Bloxstrap.Models.APIs;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Web;
using System.Windows;
using System.Windows.Input;

namespace Bloxstrap.Models.Entities
{
    public class ActivityData
    {
        private long _universeId = 0;

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

        public long PlaceId { get; set; } = 0;

        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// This will be empty unless the server joined is a private server
        /// </summary>
        public string AccessCode { get; set; } = string.Empty;

        public long UserId { get; set; } = 0;

        public string MachineAddress { get; set; } = string.Empty;

        public bool MachineAddressValid => !string.IsNullOrEmpty(MachineAddress) && !MachineAddress.StartsWith("10.");

        public bool IsTeleport { get; set; } = false;

        public ServerType ServerType { get; set; } = ServerType.Public;

        public DateTime TimeJoined { get; set; }

        public DateTime? TimeLeft { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        // everything below here is optional strictly for bloxstraprpc, discord rich presence, or game history

        /// <summary>
        /// This is intended only for other people to use, i.e. context menu invite link, rich presence joining
        /// </summary>
        public string RPCLaunchData { get; set; } = string.Empty;

        public UniverseDetails? UniverseDetails { get; set; }

        public string GameHistoryDescription
        {
            get
            {
                string desc = string.Format(
                    "{0} • {1} {2} {3}",
                    UniverseDetails?.Data.Creator.Name ?? "Unknown",
                    TimeJoined.ToString("t"),
                    Locale.CurrentCulture.Name.StartsWith("ja") ? '~' : '-',
                    TimeLeft?.ToString("t") ?? "?"
                );

                if (ServerType != ServerType.Public)
                    desc += " • " + ServerType.ToTranslatedString();

                return desc;
            }
        }

        public ICommand RejoinServerCommand => new RelayCommand(RejoinServer);
        public ICommand CopyDeeplinkCommand => new RelayCommand(CopyDeeplink);
        public ICommand CopyServerIdCommand => new RelayCommand(CopyServerId);

        private SemaphoreSlim serverQuerySemaphore = new(1, 1);
        private SemaphoreSlim serverTimeSemaphore = new(1, 1);

        public string GetInviteDeeplink(bool launchData = true)
        {
            const string baseUrl = "https://froststrap.github.io/invite";

            var queryParts = new List<string>
            {
                $"placeId={PlaceId}"
            };

            if (ServerType == ServerType.Private && !string.IsNullOrEmpty(AccessCode))
            {
                queryParts.Add("accessCode=" + HttpUtility.UrlEncode(AccessCode));
            }
            else
            {
                if (!string.IsNullOrEmpty(JobId))
                    queryParts.Add("gameInstanceId=" + HttpUtility.UrlEncode(JobId));
                else
                    queryParts.Add("gameInstanceId=");
            }

            if (launchData && !string.IsNullOrEmpty(RPCLaunchData))
            {
                queryParts.Add("launchData=" + HttpUtility.UrlEncode(RPCLaunchData));
            }

            string query = string.Join("&", queryParts);
            return $"{baseUrl}?{query}";
        }

        public async Task<DateTime?> QueryServerTime()
        {
            const string LOG_IDENT = "ActivityData::QueryServerTime";

            if (string.IsNullOrEmpty(JobId))
                throw new InvalidOperationException("JobId is null");

            if (PlaceId == 0)
                throw new InvalidOperationException("PlaceId is null");

            await serverTimeSemaphore.WaitAsync();

            if (GlobalCache.ServerTime.TryGetValue(JobId, out DateTime? time))
            {
                serverTimeSemaphore.Release();
                return time;
            }

            DateTime? firstSeen = DateTime.UtcNow;
            try
            {
                var serverTimeRaw = await Http.GetJson<RoValraTimeResponse>($"https://apis.rovalra.com/v1/server_details?place_id={PlaceId}&server_ids={JobId}");

                var serverBody = new RoValraProcessServerBody
                {
                    PlaceId = PlaceId,
                    ServerIds = new() { JobId }
                };

                string json = JsonSerializer.Serialize(serverBody);
                HttpContent postContent = new StringContent(json, Encoding.UTF8, "application/json");

                // we dont need to await it since its not as important
                // we want to return uptime quickly
                _ = App.HttpClient.PostAsync("https://apis.rovalra.com/process_servers", postContent);


                RoValraServer? server = null;

                if (serverTimeRaw?.Servers != null && serverTimeRaw.Servers.Count > 0)
                    server = serverTimeRaw.Servers[0];

                // if the server hasnt been registered we will simply return UtcNow
                // since firstSeen is UtcNow by default we dont have to check anything else
                if (server?.FirstSeen != null)
                    firstSeen = server.FirstSeen;

                GlobalCache.ServerTime[JobId] = firstSeen;
                serverTimeSemaphore.Release();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to get server time for {PlaceId}/{JobId}");
                App.Logger.WriteException(LOG_IDENT, ex);

                GlobalCache.ServerTime[JobId] = firstSeen;
                serverTimeSemaphore.Release();

                Frontend.ShowConnectivityDialog(
                    string.Format(Strings.Dialog_Connectivity_UnableToConnect, "rovalra.com"),
                    Strings.ActivityWatcher_LocationQueryFailed,
                    MessageBoxImage.Warning,
                    ex
                );
            }

            return firstSeen;
        }

        public async Task<string?> QueryServerLocation()
        {
            const string LOG_IDENT = "ActivityData::QueryServerLocation";

            if (!MachineAddressValid)
                throw new InvalidOperationException($"Machine address is invalid ({MachineAddress})");

            await serverQuerySemaphore.WaitAsync();

            if (GlobalCache.ServerLocation.TryGetValue(MachineAddress, out string? location))
            {
                serverQuerySemaphore.Release();
                return location;
            }

            try
            {
                // Try RoValra API first
                try
                {
                    var response = await Http.GetJson<RoValraGeolocation>($"https://apis.rovalra.com/v1/geolocation?ip={MachineAddress}");
                    var geolocation = response.Location;

                    if (geolocation is not null)
                    {
                        if (geolocation.City == geolocation.Region && geolocation.City == geolocation.Country)
                            location = geolocation.Country;
                        else if (geolocation.City == geolocation.Region)
                            location = $"{geolocation.Region}, {geolocation.Country}";
                        else
                            location = $"{geolocation.City}, {geolocation.Region}, {geolocation.Country}";

                        App.Logger.WriteLine(LOG_IDENT, $"Got location from RoValra: {location}");
                        GlobalCache.ServerLocation[MachineAddress] = location;
                        serverQuerySemaphore.Release();
                        return location;
                    }
                }
                catch (Exception rovalraEx)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"RoValra API failed, falling back to ipinfo.io: {rovalraEx.Message}");
                }

                // Fallback to ipinfo.io
                var ipInfo = await Http.GetJson<IPInfoResponse>($"https://ipinfo.io/{MachineAddress}/json");

                if (string.IsNullOrEmpty(ipInfo.City))
                    throw new InvalidHTTPResponseException("Reported city was blank");

                if (ipInfo.City == ipInfo.Region)
                    location = $"{ipInfo.Region}, {ipInfo.Country}";
                else
                    location = $"{ipInfo.City}, {ipInfo.Region}, {ipInfo.Country}";

                App.Logger.WriteLine(LOG_IDENT, $"Got location from ipinfo.io: {location}");
                GlobalCache.ServerLocation[MachineAddress] = location;
                serverQuerySemaphore.Release();
                return location;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to get server location for {MachineAddress}");
                App.Logger.WriteException(LOG_IDENT, ex);

                GlobalCache.ServerLocation[MachineAddress] = location;
                serverQuerySemaphore.Release();

                Frontend.ShowConnectivityDialog(
                    string.Format(Strings.Dialog_Connectivity_UnableToConnect, "rovalra.com/ipinfo.io"),
                    Strings.ActivityWatcher_LocationQueryFailed,
                    MessageBoxImage.Warning,
                    ex
                );

                return location;
            }
        }

        public override string ToString() => $"{PlaceId}/{JobId}";

        public void RejoinServer()
        {
            try
            {
                App.Logger.WriteLine("ActivityData::RejoinServer", $"Rejoining server: {PlaceId}/{JobId}");

                string robloxUri = $"roblox://experiences/start?placeId={PlaceId}&gameInstanceId={JobId}";

                if (ServerType == ServerType.Private && !string.IsNullOrEmpty(AccessCode))
                {
                    robloxUri += $"&accessCode={AccessCode}";
                }

                if (!string.IsNullOrEmpty(RPCLaunchData))
                {
                    robloxUri += $"&launchData={HttpUtility.UrlEncode(RPCLaunchData)}";
                }

                App.Logger.WriteLine("ActivityData::RejoinServer", $"Launching Roblox URI: {robloxUri}");

                Process.Start(new ProcessStartInfo
                {
                    FileName = robloxUri,
                    UseShellExecute = true
                });

                App.Logger.WriteLine("ActivityData::RejoinServer", "Successfully launched new Roblox instance");

                CloseExistingRobloxInstances();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ActivityData::RejoinServer", ex);
                Frontend.ShowMessageBox($"Failed to rejoin server: {ex.Message}", MessageBoxImage.Error);
            }
        }

        private void CloseExistingRobloxInstances()
        {
            try
            {
                var processes = Process.GetProcessesByName("RobloxPlayerBeta");
                int closedCount = 0;

                foreach (var process in processes)
                {
                    try
                    {
                        if ((DateTime.Now - process.StartTime).TotalSeconds < 3)
                        {
                            App.Logger.WriteLine("ActivityData::CloseExistingRobloxInstances", $"Skipping new process (PID: {process.Id})");
                            continue;
                        }

                        App.Logger.WriteLine("ActivityData::CloseExistingRobloxInstances", $"Instantly closing old Roblox process (PID: {process.Id})");
                        process.Kill(); // Kill instantly, don't wait for exit
                        closedCount++;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("ActivityData::CloseExistingRobloxInstances", $"Failed to close process {process.Id}: {ex.Message}");
                    }
                }

                App.Logger.WriteLine("ActivityData::CloseExistingRobloxInstances", $"Instantly closed {closedCount} old Roblox instances");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ActivityData::CloseExistingRobloxInstances", $"Error closing processes: {ex.Message}");
            }
        }

        private async void CopyDeeplink()
        {
            try
            {
                string deeplink = GetInviteDeeplink();
                Clipboard.SetText(deeplink);

                App.Logger.WriteLine("ActivityData::CopyDeeplink", $"Copied deeplink to clipboard: {deeplink}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ActivityData::CopyDeeplink", ex);
                Frontend.ShowMessageBox($"Failed to copy deeplink: {ex.Message}", MessageBoxImage.Error);
            }
        }

        private async void CopyServerId()
        {
            try
            {
                if (string.IsNullOrEmpty(JobId))
                {
                    Frontend.ShowMessageBox("No server ID available to copy", MessageBoxImage.Information);
                    return;
                }

                Clipboard.SetText(JobId);
                App.Logger.WriteLine("ActivityData::CopyServerId", $"Copied server ID to clipboard: {JobId}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ActivityData::CopyServerId", ex);
                Frontend.ShowMessageBox($"Failed to copy server ID: {ex.Message}", MessageBoxImage.Error);
            }
        }
    }
}
