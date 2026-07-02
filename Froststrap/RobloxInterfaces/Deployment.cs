namespace Froststrap.RobloxInterfaces
{
    public static class Deployment
    {
        public const string DefaultRobloxDomain = "roblox.com";

        public const string DefaultChannel = "production";

        public static event EventHandler<string>? ChannelChanged;

        private static string _channel = DefaultChannel;
        public static string Channel
        {
            get => _channel;
            set
            {
                if (_channel == value) return;
                _channel = value;
                ChannelChanged?.Invoke(null, value);
            }
        }

        public static string ChannelToken { get; set; } = string.Empty;

        public static string BinaryType => OperatingSystem.IsMacOS() ? "MacPlayer" : "WindowsPlayer";

        public static string RobloxDomain => App.Settings.Prop.RobloxDomain;

        public static bool IsDefaultChannel => Channel.Equals(DefaultChannel, StringComparison.OrdinalIgnoreCase) || Channel.Equals("live", StringComparison.OrdinalIgnoreCase);

        public static bool IsDefaultRobloxDomain => RobloxDomain.Equals(DefaultRobloxDomain, StringComparison.OrdinalIgnoreCase);

        public static string BaseUrl { get; private set; } = null!;

        public static readonly List<HttpStatusCode?> BadChannelCodes = [
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound
        ];

        private static readonly Dictionary<string, ClientVersion> ClientVersionCache = [];

        private static readonly List<string> BaseUrls =
        [
            "https://setup.rbxcdn.com",
            "https://setup-ak.rbxcdn.com",
            "https://setup-aws.rbxcdn.com",
            "https://setup-cfly.rbxcdn.com",
            "https://s3.amazonaws.com/setup.roblox.com"
        ];

        private static async Task<(string url, long latency)> GetLatency(string url, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, $"{url}/versionStudio");
                using var response = await App.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                stopwatch.Stop();
                return (url, stopwatch.ElapsedMilliseconds);
            }
            catch
            {
                return (url, long.MaxValue);
            }
        }

        /// <summary>
        /// This function serves double duty as the setup mirror enumerator, and as our connectivity check.
        /// Returns null for success.
        /// </summary>
        public static async Task<Exception?> InitializeConnectivity()
        {
            const string LOG_IDENT = "Deployment::InitializeConnectivity";
            const string FALLBACK_URL = "https://setup.rbxcdn.com";
            var tokenSource = new CancellationTokenSource();

            var tasks = BaseUrls.Select(url => GetLatency(url, tokenSource.Token)).ToList();

            App.Logger.WriteLine(LOG_IDENT, "Testing for best regional download mirror...");

            try
            {
                var results = await Task.WhenAll(tasks);

                var (url, latency) = results
                    .Where(r => r.latency != long.MaxValue)
                    .OrderBy(r => r.latency)
                    .FirstOrDefault();

                if (url != null)
                {
                    BaseUrl = url;
                    App.Logger.WriteLine(LOG_IDENT, $"Optimal BaseUrl: {BaseUrl} ({latency}ms)");
                    tokenSource.Cancel();
                    return null;
                }

                BaseUrl = FALLBACK_URL;
                App.Logger.WriteLine(LOG_IDENT, $"No mirrors responded. Falling back to default: {BaseUrl}");
                return new Exception("No regional mirrors were responsive.");
            }
            catch (Exception ex)
            {
                BaseUrl = FALLBACK_URL;
                App.Logger.WriteException(LOG_IDENT, ex);
                return ex;
            }
        }

        public static string GetLocation(string resource)
        {
            string location = BaseUrl;
            if (!IsDefaultChannel)
                location += "/channel/common";
            location += resource;
            return location;
        }

        public async static Task<UserChannel?> GetUserChannel(string binaryType)
        {
            const string LOG_IDENT = "Deployment::GetUserChannel";
            try
            {
                Uri apiUrl = UrlBuilder.BuildApiUrl("clientsettings", "v2/user-channel?binaryType=" + binaryType);
                HttpResponseMessage response = await App.Cookies.AuthGet(apiUrl);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                UserChannel channelInfo = JsonSerializer.Deserialize<UserChannel>(content)!;
                return channelInfo;
            }
            catch (HttpRequestException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to get user channel");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
            return null;
        }

        public static async Task<bool> IsChannelPrivate(string channel)
        {
            if (channel == "production")
                channel = "live";
            if (channel == "live")
                return false;

            try
            {
                Uri apiUrl = UrlBuilder.BuildApiUrl("clientsettingscdn", $"v2/client-version/{BinaryType}/channel/{channel}");
                var response = await App.HttpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                if (BadChannelCodes.Contains(ex.StatusCode))
                    return true;
            }
            return false;
        }

        public static async Task<DateTime?> GetVersionTimestamp(string version)
        {
            const string LOG_IDENT = "Deployment::GetVersionTimestamp";
            const string header = "last-modified";

            if (string.IsNullOrEmpty(BaseUrl))
                await InitializeConnectivity();

            try
            {
                string location;

                if (OperatingSystem.IsMacOS())
                {
                    location = GetLocation($"/mac/{version}-RobloxPlayer.zip");

                    using var request = new HttpRequestMessage(HttpMethod.Head, location);
                    using var response = await App.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    if (response.Content.Headers.TryGetValues(header, out var values))
                    {
                        string lastModified = values.First();
                        DateTime dateTime = DateTime.Parse(lastModified, CultureInfo.InvariantCulture);
                        return dateTime;
                    }
                }
                else
                {
                    location = GetLocation($"/{version}-rbxPkgManifest.txt");
                    var response = await App.HttpClient.GetAsync(location);
                    response.EnsureSuccessStatusCode();

                    if (response.Content.Headers.TryGetValues(header, out var values))
                    {
                        string lastModified = values.First();
                        DateTime dateTime = DateTime.Parse(lastModified, CultureInfo.InvariantCulture);
                        return dateTime;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to get timestamp for {version}");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
            return null;
        }

        public static async Task<ClientVersion> GetInfo(string? channel = null, bool behindProductionCheck = false, bool includeTimestamp = false, string? binaryTypeOverride = null)
        {
            const string LOG_IDENT = "Deployment::GetInfo";

            if (string.IsNullOrEmpty(channel))
                channel = Channel;

            bool isDefaultChannel = string.Compare(channel, DefaultChannel, StringComparison.OrdinalIgnoreCase) == 0;

            App.Logger.WriteLine(LOG_IDENT, $"Getting deploy info for channel {channel}");

            string activeBinaryType = binaryTypeOverride ?? BinaryType;
            string cacheKey = $"{channel}-{activeBinaryType}";

            HttpRequestMessage request = new() { Method = HttpMethod.Get };

            if (!string.IsNullOrEmpty(ChannelToken))
            {
                App.Logger.WriteLine(LOG_IDENT, "Got Roblox-Channel-Token");
                request.Headers.Add("Roblox-Channel-Token", ChannelToken);
            }

            ClientVersion clientVersion;

            if (ClientVersionCache.TryGetValue(cacheKey, out var value))
            {
                App.Logger.WriteLine(LOG_IDENT, "Deploy information is cached");
                clientVersion = value;
            }
            else
            {
                string path = $"v2/client-version/{activeBinaryType}";
                if (!isDefaultChannel)
                    path += $"/channel/{channel}";

                try
                {
                    request.RequestUri = UrlBuilder.BuildApiUrl("clientsettingscdn", path);
                    clientVersion = await Http.SendJson<ClientVersion>(request);
                }
                catch (HttpRequestException httpEx) when (!isDefaultChannel && BadChannelCodes.Contains(httpEx.StatusCode))
                {
                    throw new InvalidChannelException(httpEx.StatusCode);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to contact clientsettingscdn! Falling back to clientsettings...");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    HttpRequestMessage fallbackRequest = new()
                    {
                        Method = HttpMethod.Get,
                        RequestUri = UrlBuilder.BuildApiUrl("clientsettings", path)
                    };
                    if (!string.IsNullOrEmpty(ChannelToken))
                        fallbackRequest.Headers.Add("Roblox-Channel-Token", ChannelToken);

                    try
                    {
                        clientVersion = await Http.SendJson<ClientVersion>(fallbackRequest);
                    }
                    catch (HttpRequestException httpEx) when (!isDefaultChannel && BadChannelCodes.Contains(httpEx.StatusCode))
                    {
                        throw new InvalidChannelException(httpEx.StatusCode);
                    }
                }

                if (!isDefaultChannel && behindProductionCheck)
                {
                    var defaultClientVersion = await GetInfo(DefaultChannel);
                    if (Utilities.CompareVersions(clientVersion.Version, defaultClientVersion.Version) == VersionComparison.LessThan)
                        clientVersion.IsBehindDefaultChannel = true;
                }
                else
                    clientVersion.IsBehindDefaultChannel = false;

                ClientVersionCache[cacheKey] = clientVersion;
            }

            if (includeTimestamp && clientVersion.Timestamp is null)
                clientVersion.Timestamp = await GetVersionTimestamp(clientVersion.VersionGuid);

            return clientVersion;
        }
    }
}