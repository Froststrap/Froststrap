using Bloxstrap.Models.APIs;
using CommunityToolkit.Mvvm.Input;
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

        // everything below here is optional strictly for bloxstraprpc, discord rich presence, or game history

        /// <summary>
        /// This is intended only for other people to use, i.e. context menu invite link, rich presence joining
        /// </summary>
        public string RPCLaunchData { get; set; } = string.Empty;

        public UniverseDetails? UniverseDetails { get; set; }

        public string? RootJobId { get; set; }

        public event EventHandler<string>? OnDeleteRequested;

        public ICommand RejoinServerCommand => new RelayCommand(() => RejoinServer(true));
        public ICommand CopyDeeplinkCommand => new RelayCommand(CopyDeeplink);
        public ICommand CopyServerIdCommand => new RelayCommand(CopyServerId);
        public ICommand DeleteHistoryCommand => new RelayCommand(DeleteHistory);

        private SemaphoreSlim serverQuerySemaphore = new(1, 1);
        private SemaphoreSlim serverTimeSemaphore = new(1, 1);

        public string GetInviteDeeplink(bool launchData = true)
        {
            string deeplink = $"roblox://experiences/start?placeId={PlaceId}";

            if (ServerType == ServerType.Private) // thats not going to work
                deeplink += "&accessCode=" + AccessCode;
            else
                deeplink += "&gameInstanceId=" + JobId;

            if (launchData && !string.IsNullOrEmpty(RPCLaunchData))
                deeplink += "&launchData=" + HttpUtility.UrlEncode(RPCLaunchData);

            return deeplink;
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

        public void RejoinServer(bool CloseRoblox = true)
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
                App.Logger.WriteException("ActivityData::RejoinServer", ex);
                Frontend.ShowMessageBox($"Failed to rejoin server: {ex.Message}", MessageBoxImage.Error);
            }
        }

        public void CloseRobloxProcesses()
        {
            const string LOG_IDENT = "ActivityData::CloseProcess";

            try
            {
                var process = Process.GetProcessesByName("RobloxPlayerBeta");

                if (process.Length == 0)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Roblox not found");
                    return;
                }

                foreach (var proc in process)
                {
                    if ((DateTime.Now - proc.StartTime).TotalSeconds < 3)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Skipping new process");
                        continue;
                    }

                    proc.Kill();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Roblox could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private async void CopyDeeplink()
        {
            string deeplink = GetInviteDeeplink();
            Clipboard.SetText(deeplink);
        }

        private async void CopyServerId() => Clipboard.SetText(JobId);

        private void DeleteHistory()
        {
            string jobIdToDelete = !string.IsNullOrEmpty(RootJobId) ? RootJobId : JobId;

            if (!string.IsNullOrEmpty(jobIdToDelete))
            {
                OnDeleteRequested?.Invoke(this, jobIdToDelete);
            }
        }
    }
}