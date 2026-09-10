using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace Froststrap.Integrations
{
    internal class RobloxServerFetcher : IDisposable
    {
        private readonly HttpClient _client;
        private Dictionary<int, string>? _datacenterIdToRegion;
        private List<string>? _regionList;
        private List<DatacenterEntry>? _datacenterEntries;

        private IPInfoResponse? _ipinfoCache;
        private DateTime _ipinfoCachedAtUtc;

        private readonly string _serverCacheFilePath = Path.Combine(Paths.Cache, "server_cache.json");
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, ServerInstance>> _serverCache = [];

        private const string DatacenterUrl = "https://apis.rovalra.com/v1/datacenters/list";
        private const string RegionServersUrl = "https://apis.rovalra.com/v1/servers/region";
        private const string IpInfoUrl = "https://ipinfo.io/json";

        private static readonly TimeSpan IpInfoCacheDuration = TimeSpan.FromHours(6);

        private bool _disposed;

        internal class ServerSelectionResult
        {
            public string? ServerId { get; set; }
            public string? Region { get; set; }
            public int Rank { get; set; }
            public DateTime? FirstSeen { get; set; }
            public bool Found => !string.IsNullOrEmpty(ServerId);
        }

        private static readonly char[] _regionSeparators = [','];

        public RobloxServerFetcher()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 20
            };

            _client = new HttpClient(handler);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Roblox/Froststrap");

            try
            {
                Directory.CreateDirectory(Paths.Cache);

                if (File.Exists(_serverCacheFilePath))
                {
                    using FileStream fs = File.OpenRead(_serverCacheFilePath);
                    var loadedCache = JsonSerializer.Deserialize<ConcurrentDictionary<long, ConcurrentDictionary<string, ServerInstance>>>(fs);

                    if (loadedCache != null)
                    {
                        _serverCache = loadedCache;
                        App.Logger.Info($"Loaded {_serverCache.Count} games from disk.");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception:", ex);
            }
        }

        private static string BuildRegionKey(string? city, string? country)
        {
            if (string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(country))
                return "Unknown";

            return $"{city}, {country}".Trim().Trim(',', ' ');
        }

        public async Task<(List<string> regions, Dictionary<int, string> datacenterMap)?> GetDatacentersAsync(CancellationToken cancellationToken = default)
        {
            if (_datacenterIdToRegion != null && _regionList != null)
                return (_regionList, _datacenterIdToRegion);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var json = await _client.GetStringAsync(new Uri(DatacenterUrl), cancellationToken);
                var datacenterEntries = JsonSerializer.Deserialize<List<DatacenterEntry>>(json);

                if (datacenterEntries == null) return null;

                var map = new Dictionary<int, string>();
                var regions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in datacenterEntries)
                {
                    string regionKey = BuildRegionKey(entry.Location?.City, entry.Location?.Country);
                    regions.Add(regionKey);

                    foreach (var id in entry.DataCenterIds)
                    {
                        map[id] = regionKey;
                    }
                }

                _regionList = [.. regions.OrderBy(r => r, StringComparer.OrdinalIgnoreCase)];
                _datacenterIdToRegion = map;
                _datacenterEntries = datacenterEntries;

                return (_regionList, _datacenterIdToRegion);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception:", ex);
                return null;
            }
        }

        private static (string? city, string? country) ParseRegionString(string region)
        {
            if (string.IsNullOrEmpty(region))
                return (null, null);

            var parts = region.Split(_regionSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                string city = parts[0].Trim();
                string country = parts[1].Trim();
                return (city, country);
            }

            if (region.Length == 2 && region.All(char.IsLetter))
                return (null, region.ToUpperInvariant());

            return (null, null);
        }

        public async Task<(List<ServerInstance> Servers, int? NextCursor)> FetchServersByRegionAsync(
            long placeId, string region, int? cursor = null, CancellationToken cancellationToken = default)
        {
            const int limit = 100;
            var results = new List<ServerInstance>();

            try
            {
                var (city, country) = ParseRegionString(region);
                var query = $"place_id={placeId}&limit={limit}";

                if (!string.IsNullOrEmpty(country))
                    query += $"&country={country}";
                if (!string.IsNullOrEmpty(city))
                    query += $"&city={Uri.EscapeDataString(city)}";

                if (string.IsNullOrEmpty(country) && string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(region))
                    query += $"&region={Uri.EscapeDataString(region)}";

                if (cursor.HasValue)
                    query += $"&cursor={cursor.Value}";

                var url = new UriBuilder(RegionServersUrl) { Query = query }.Uri;

                var response = await _client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode) return (results, null);

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var regionResponse = JsonSerializer.Deserialize<RoValraRegionResponse>(content);

                if (regionResponse?.Servers == null) return (results, null);

                foreach (var s in regionResponse.Servers)
                {
                    string displayRegion = ResolveRegionDisplay(s.DatacenterId, s.City, s.RegionCode, s.Country);

                    var server = new ServerInstance
                    {
                        Id = s.ServerId!,
                        Region = displayRegion,
                        DataCenterId = s.DatacenterId,
                        FirstSeen = s.FirstSeen,
                        Playing = 0,
                        MaxPlayers = 0,
                        PlayerTokens = []
                    };
                    results.Add(server);
                }

                var placeCache = _serverCache.GetOrAdd(placeId, _ => []);
                foreach (var server in results)
                {
                    if (server.Region != "Unknown")
                        placeCache[server.Id] = server;
                }

                return (results, regionResponse.NextCursor);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Error fetching servers for region {region}: {ex}");
                return (results, null);
            }
        }

        private string ResolveRegionDisplay(int datacenterId, string? city, string? regionCode, string? country)
        {
            if (_datacenterIdToRegion != null && _datacenterIdToRegion.TryGetValue(datacenterId, out var cachedRegion))
                return cachedRegion;

            if (!string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(regionCode))
                return $"{city}, {regionCode}";

            if (!string.IsNullOrEmpty(country))
                return country;

            return "Unknown";
        }

        public async Task<List<ServerInstance>> FetchServersForRegionsAsync(long placeId, List<string> regions, CancellationToken cancellationToken = default)
        {
            var allServers = new List<ServerInstance>();
            var seenIds = new HashSet<string>();

            foreach (var region in regions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (servers, _) = await FetchServersByRegionAsync(placeId, region, null, cancellationToken);

                foreach (var s in servers)
                {
                    if (seenIds.Add(s.Id))
                        allServers.Add(s);
                }
            }

            return allServers;
        }

        public async Task<FetchResult> FetchServerInstancesAsync(long placeId, string cursor = "", string? optionalCookie = null, CancellationToken cancellationToken = default)
        {
            string? roblosecurity = !string.IsNullOrWhiteSpace(optionalCookie) ? optionalCookie : await ResolveCookieAsync();
            if (string.IsNullOrWhiteSpace(roblosecurity)) return new FetchResult();

            if (_datacenterIdToRegion == null) await GetDatacentersAsync(cancellationToken);

            var baseUri = UrlBuilder.BuildApiUrl("games", $"v2/games/{placeId}/servers/Public", secure: true);
            var url = new UriBuilder(baseUri)
            {
                Query = $"sortOrder=Desc&excludeFullGames=true&limit=100&orderBy=BestLatency&cursor={cursor}"
            }.Uri;

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Cookie", $".ROBLOSECURITY={roblosecurity}");

            var response = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new FetchResult();

            using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement)) return new FetchResult();

            string nextCursor = jsonDoc.RootElement.TryGetProperty("nextPageCursor", out var cElem) ? cElem.GetString() ?? "" : "";

            var instances = new ConcurrentBag<ServerInstance>();
            var placeCache = _serverCache.GetOrAdd(placeId, _ => []);

            var serverInfos = new List<(string jobId, int playing, int maxPlayers, List<string> playerTokens)>();
            var serverIdsToFetch = new List<string>();

            foreach (var serverElem in dataElement.EnumerateArray())
            {
                string jobId = serverElem.GetProperty("id").GetString() ?? "";
                int playing = serverElem.GetProperty("playing").GetInt32();
                int maxPlayers = serverElem.GetProperty("maxPlayers").GetInt32();

                var playerTokensElement = serverElem.GetProperty("playerTokens");
                var playerTokens = playerTokensElement.EnumerateArray()
                                      .Select(x => x.GetString() ?? "")
                                      .Where(s => !string.IsNullOrEmpty(s))
                                      .ToList();

                if (playing >= maxPlayers) continue;

                if (placeCache.TryGetValue(jobId, out var cached) && cached.Region != "Unknown")
                {
                    var refreshed = new ServerInstance
                    {
                        Id = jobId,
                        Playing = playing,
                        MaxPlayers = maxPlayers,
                        Region = cached.Region,
                        DataCenterId = cached.DataCenterId,
                        FirstSeen = cached.FirstSeen,
                        PlayerTokens = playerTokens
                    };

                    placeCache[jobId] = refreshed;
                    instances.Add(refreshed);
                    continue;
                }

                serverInfos.Add((jobId, playing, maxPlayers, playerTokens));
                serverIdsToFetch.Add(jobId);
            }

            var regionTasks = new List<Task<(string jobId, int? dcId)>>();
            foreach (var (jobId, playing, maxPlayers, _) in serverInfos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var regionTask = FetchServerRegionAsync(placeId, jobId, roblosecurity, cancellationToken);
                regionTasks.Add(regionTask);
            }

            var regionResults = await Task.WhenAll(regionTasks);

            Dictionary<string, DateTime?> uptimeResults = null!;
            if (serverIdsToFetch.Count > 0)
            {
                uptimeResults = await GetServerUptimesBatchAsync(serverIdsToFetch, placeId, cancellationToken);
            }

            for (int i = 0; i < serverInfos.Count; i++)
            {
                var (jobId, playing, maxPlayers, playerTokens) = serverInfos[i];
                var (_, dcId) = regionResults[i];

                string region = (dcId.HasValue &&
                                 _datacenterIdToRegion != null &&
                                 _datacenterIdToRegion.TryGetValue(dcId.Value, out var mapped))
                    ? mapped
                    : "Unknown";

                DateTime? uptime = uptimeResults != null && uptimeResults.TryGetValue(jobId, out var up) ? up : null;

                var server = new ServerInstance
                {
                    Id = jobId,
                    Playing = playing,
                    MaxPlayers = maxPlayers,
                    Region = region,
                    DataCenterId = dcId,
                    FirstSeen = uptime,
                    PlayerTokens = playerTokens
                };

                if (region != "Unknown") placeCache[jobId] = server;
                instances.Add(server);
            }

            return new FetchResult
            {
                Servers = [.. instances],
                NextCursor = nextCursor
            };
        }

        public async Task<Dictionary<string, DateTime?>> GetServerUptimesBatchAsync(List<string> jobIds, long placeId, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, DateTime?>();

            if (jobIds == null || jobIds.Count == 0)
                return result;

            try
            {
                const int batchSize = 50;
                var batches = jobIds
                    .Select((id, index) => new { id, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(g => g.Select(x => x.id).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var serverIdsParam = string.Join(",", batch);
                    var response = await _client.GetAsync(new Uri(
                        $"https://apis.rovalra.com/v1/servers/details?place_id={placeId}&server_ids={Uri.EscapeDataString(serverIdsParam)}"),
                        cancellationToken
                    );

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        var serverTimeRaw = JsonSerializer.Deserialize<RoValraTimeResponse>(content);

                        if (serverTimeRaw?.Servers != null)
                        {
                            foreach (var server in serverTimeRaw.Servers)
                            {
                                result[server.ServerId!] = server.FirstSeen;
                            }
                        }
                    }
                    else
                    {
                        foreach (var id in batch)
                        {
                            result[id] = null;
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception:", ex);
                return result;
            }
        }

        private async Task<(string jobId, int? dcId)> FetchServerRegionAsync(long placeId, string jobId, string roblosecurity, CancellationToken cancellationToken)
        {
            try
            {
                var joinResp = await SendJoinRequestWithRetriesAsync(placeId, jobId, roblosecurity, cancellationToken);
                using var parsed = JsonDocument.Parse(await joinResp.Content.ReadAsStringAsync(cancellationToken));

                int? dcId = null;
                if (TryExtractDataCenterId(parsed.RootElement, out int extracted))
                    dcId = extracted;

                return (jobId, dcId);
            }
            catch
            {
                return (jobId, null);
            }
        }

        private async Task<HttpResponseMessage> SendJoinRequestWithRetriesAsync(long placeId, string jobId, string roblosecurity, CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            const int maxAttempts = 3;

            while (true)
            {
                attempt++;
                using var joinReq = new HttpRequestMessage(HttpMethod.Post, UrlBuilder.BuildApiUrl("gamejoin", "v1/join-game-instance", secure: true));
                joinReq.Headers.Add("Referer", $"https://roblox.com/games/{placeId}");
                joinReq.Headers.Add("Origin", "https://roblox.com");
                joinReq.Headers.Add("Cookie", $".ROBLOSECURITY={roblosecurity}");

                joinReq.Content = new StringContent(JsonSerializer.Serialize(new
                {
                    placeId,
                    isTeleport = false,
                    gameId = jobId,
                    gameJoinAttemptId = jobId
                }), Encoding.UTF8, "application/json");

                try
                {
                    var resp = await _client.SendAsync(joinReq, cancellationToken).ConfigureAwait(false);

                    if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden)
                        return resp;

                    if (((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500) && attempt < maxAttempts)
                    {
                        await Task.Delay(500 * attempt, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return resp;
                }
                catch (HttpRequestException) when (attempt < maxAttempts)
                {
                    await Task.Delay(250 * attempt, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static bool TryExtractDataCenterId(JsonElement elem, out int dcId)
        {
            dcId = 0;
            if (elem.ValueKind != JsonValueKind.Object) return false;

            if (elem.TryGetProperty("DataCenterId", out var dcProp) && dcProp.TryGetInt32(out dcId))
                return true;

            foreach (var prop in elem.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number && (prop.Name.Contains("DataCenterId", StringComparison.Ordinal) || prop.Name.Equals("dc", StringComparison.Ordinal)))
                {
                    if (prop.Value.TryGetInt32(out dcId)) return true;
                }
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    if (TryExtractDataCenterId(prop.Value, out dcId)) return true;
                }
            }
            return false;
        }

        public async Task<bool> ValidateCookieAsync(string roblosecurityCookie)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roblosecurityCookie)) return false;

                using var request = new HttpRequestMessage(HttpMethod.Get, UrlBuilder.BuildApiUrl("users", "v1/users/authenticated", secure: true));
                request.Headers.Add("Cookie", $".ROBLOSECURITY={roblosecurityCookie}");

                var response = await _client.SendAsync(request);
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch
            {
                return false;
            }
        }

        private static Task<string?> GetCookieFromAccountManagerAsync()
        {
            try
            {
                if (AccountManager.Shared?.ActiveAccount != null)
                {
                    return Task.FromResult<string?>(AccountManager.Shared.ActiveAccount.SecurityToken);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception:", ex);
            }
            return Task.FromResult<string?>(null);
        }

        private static Task<string?> GetCookieFromCookiesManagerAsync()
        {
            try
            {
                if (App.Cookies != null)
                {
                    var field = typeof(CookiesManager).GetField("AuthCookie", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        return Task.FromResult(field.GetValue(App.Cookies) as string);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception:", ex);
            }
            return Task.FromResult<string?>(null);
        }

        public async Task<string?> ResolveCookieAsync()
        {
            var accountManagerCookie = await GetCookieFromAccountManagerAsync();
            if (!string.IsNullOrWhiteSpace(accountManagerCookie) && await ValidateCookieAsync(accountManagerCookie))
            {
                App.Logger.Info("Using valid cookie from Account Manager.");
                return accountManagerCookie;
            }

            var cookiesManagerCookie = await GetCookieFromCookiesManagerAsync();
            if (!string.IsNullOrWhiteSpace(cookiesManagerCookie) && await ValidateCookieAsync(cookiesManagerCookie))
            {
                App.Logger.Info("Using valid cookie from Cookies Manager.");
                return cookiesManagerCookie;
            }

            App.Logger.Warn("No valid .ROBLOSECURITY cookie could be resolved.");
            return null;
        }

        private async Task<bool> IsServerAliveAsync(long placeId, string jobId, string roblosecurity, CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(2));

                var response = await SendJoinRequestWithRetriesAsync(placeId, jobId, roblosecurity, cts.Token);
                return response.StatusCode != HttpStatusCode.Unauthorized &&
                       response.StatusCode != HttpStatusCode.Forbidden &&
                       (response.IsSuccessStatusCode || (int)response.StatusCode == 429);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static double Deg2Rad(double deg) => deg * (Math.PI / 180.0);

        private static double GetDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            double dLat = Deg2Rad(lat2 - lat1);
            double dLon = Deg2Rad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                      Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
                      Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private async Task<IPInfoResponse?> GetIpInfoCachedAsync(CancellationToken cancellationToken)
        {
            if (_ipinfoCache != null && DateTime.UtcNow - _ipinfoCachedAtUtc < IpInfoCacheDuration)
                return _ipinfoCache;

            try
            {
                var ipinfoJson = await _client.GetStringAsync(new Uri(IpInfoUrl), cancellationToken);
                _ipinfoCache = JsonSerializer.Deserialize<IPInfoResponse>(ipinfoJson);
                _ipinfoCachedAtUtc = DateTime.UtcNow;
                return _ipinfoCache;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to resolve IP info:", ex);
                return _ipinfoCache;
            }
        }

        public async Task<List<string>> GetClosestRegionsForAutoModeAsync(int topCount, CancellationToken cancellationToken = default)
        {
            try
            {
                var datacentersResult = await GetDatacentersAsync(cancellationToken);
                if (datacentersResult == null || _datacenterEntries == null || _datacenterEntries.Count == 0)
                    return [];

                var ipinfo = await GetIpInfoCachedAsync(cancellationToken);
                if (string.IsNullOrEmpty(ipinfo?.Loc))
                    return [];

                string[] location = ipinfo.Loc.Split(',');
                if (location.Length < 2) return [];

                if (!double.TryParse(location[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double userLat) ||
                    !double.TryParse(location[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double userLon))
                    return [];

                var regionDistances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                foreach (var dc in _datacenterEntries)
                {
                    if (dc.Location == null || dc.Location.LatLong == null || dc.Location.LatLong.Length < 2)
                        continue;

                    if (!double.TryParse(dc.Location.LatLong[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                        !double.TryParse(dc.Location.LatLong[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                        continue;

                    double distance = GetDistance(userLat, userLon, lat, lon);

                    string regionKey = BuildRegionKey(dc.Location.City, dc.Location.Country);
                    if (string.IsNullOrEmpty(regionKey) || regionKey.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!regionDistances.TryGetValue(regionKey, out double existingDistance) || distance < existingDistance)
                        regionDistances[regionKey] = distance;
                }

                var topRegions = regionDistances
                    .OrderBy(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .Take(topCount)
                    .ToList();

                App.Logger.Info($"Top {topRegions.Count} regions (by distance): {string.Join(", ", topRegions)}");
                return topRegions;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception:", ex);
                return [];
            }
        }

        public async Task<ServerSelectionResult> FindBestServerInRegionAsync(
            long placeId,
            List<string> topRegions,
            CancellationToken cancellationToken = default)
        {
            try
            {
                App.Logger.Info($"Searching in top {topRegions.Count} regions: {string.Join(", ", topRegions)}");

                string? cookie = await ResolveCookieAsync();
                if (string.IsNullOrEmpty(cookie))
                {
                    App.Logger.Warn("No valid cookie for server liveliness checks.");
                }

                var allServers = await FetchServersForRegionsAsync(placeId, topRegions, cancellationToken);
                var valid = allServers.Where(s => !string.IsNullOrEmpty(s.Region) && s.Region != "Unknown").ToList();

                if (valid.Count == 0)
                    return new ServerSelectionResult();

                var regionRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < topRegions.Count; i++)
                    regionRank[topRegions[i]] = i + 1;

                var sorted = valid
                    .OrderBy(s => regionRank.TryGetValue(s.Region, out int rank) ? rank : int.MaxValue)
                    .ThenBy(s => s.FirstSeen)
                    .ToList();

                if (!string.IsNullOrEmpty(cookie))
                {
                    const int maxCheck = 10;
                    int checkedCount = 0;
                    foreach (var server in sorted)
                    {
                        if (checkedCount >= maxCheck) break;
                        bool alive = await IsServerAliveAsync(placeId, server.Id, cookie, cancellationToken);
                        if (alive)
                        {
                            int bestRank = regionRank.TryGetValue(server.Region, out int r) ? r : int.MaxValue;
                            return new ServerSelectionResult
                            {
                                ServerId = server.Id,
                                Region = server.Region,
                                Rank = bestRank,
                                FirstSeen = server.FirstSeen
                            };
                        }
                        checkedCount++;
                    }
                    App.Logger.Warn("No alive server found among checked candidates.");
                    return new ServerSelectionResult();
                }
                else
                {
                    var best = sorted.First();
                    int bestRank = regionRank.TryGetValue(best.Region, out int r) ? r : int.MaxValue;
                    return new ServerSelectionResult
                    {
                        ServerId = best.Id,
                        Region = best.Region,
                        Rank = bestRank,
                        FirstSeen = best.FirstSeen
                    };
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception:", ex);
                return new ServerSelectionResult();
            }
        }

        public async Task<ServerSelectionResult> FindBestServerInSelectedRegionAsync(
            long placeId,
            string selectedRegion,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(selectedRegion))
                    return new ServerSelectionResult();

                App.Logger.Info($"Searching for servers in selected region: {selectedRegion}");

                string? cookie = await ResolveCookieAsync();
                if (string.IsNullOrEmpty(cookie))
                {
                    App.Logger.Warn("No valid cookie for server liveliness checks.");
                }

                var (servers, _) = await FetchServersByRegionAsync(placeId, selectedRegion, null, cancellationToken);

                if (servers.Count == 0)
                    return new ServerSelectionResult();

                var sorted = servers.OrderBy(s => s.FirstSeen).ToList();

                if (!string.IsNullOrEmpty(cookie))
                {
                    const int maxCheck = 10;
                    int checkedCount = 0;
                    foreach (var server in sorted)
                    {
                        if (checkedCount >= maxCheck) break;
                        bool alive = await IsServerAliveAsync(placeId, server.Id, cookie, cancellationToken);
                        if (alive)
                        {
                            return new ServerSelectionResult
                            {
                                ServerId = server.Id,
                                Region = server.Region,
                                Rank = 1,
                                FirstSeen = server.FirstSeen
                            };
                        }
                        checkedCount++;
                    }
                    App.Logger.Warn("No alive server found in selected region.");
                    return new ServerSelectionResult();
                }
                else
                {
                    var best = sorted.First();
                    return new ServerSelectionResult
                    {
                        ServerId = best.Id,
                        Region = best.Region,
                        Rank = 1,
                        FirstSeen = best.FirstSeen
                    };
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception:", ex);
                return new ServerSelectionResult();
            }
        }

        public async Task<bool> JoinBestServerAsync(
            long placeId,
            int bestRegionAmounts = 3,
            bool showConfirmation = true,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string selectedRegion = App.Settings.Prop.SelectedRegion ?? "";

                if (!string.IsNullOrEmpty(selectedRegion) &&
                    !selectedRegion.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await FindBestServerInSelectedRegionAsync(
                        placeId,
                        selectedRegion,
                        cancellationToken);

                    if (result.Found)
                    {
                        if (showConfirmation)
                        {
                            var confirmResult = await Frontend.ShowMessageBox(
                                $"Found server in {result.Region}.\nDo you want to join?",
                                MessageBoxImage.Question,
                                MessageBoxButton.YesNo);
                            if (confirmResult != MessageBoxResult.Yes)
                                return false;
                        }

                        string robloxUri = $"roblox://experiences/start?placeId={placeId}&gameInstanceId={result.ServerId}";
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = robloxUri,
                            UseShellExecute = true
                        });
                        return true;
                    }
                }

                var topRegions = await GetClosestRegionsForAutoModeAsync(bestRegionAmounts, cancellationToken);
                if (topRegions.Count == 0)
                {
                    await Frontend.ShowMessageBox("Could not determine your location for Auto mode. Please try again later.", MessageBoxImage.Warning);
                    return false;
                }

                var autoResult = await FindBestServerInRegionAsync(
                    placeId,
                    topRegions,
                    cancellationToken);

                if (!autoResult.Found)
                {
                    await Frontend.ShowMessageBox($"Could not find a suitable server after checking servers in {topRegions.Count} regions.", MessageBoxImage.Information);
                    return false;
                }

                if (showConfirmation)
                {
                    var confirmResult = await Frontend.ShowMessageBox(
                        $"Found server in {autoResult.Region}.\nDo you want to join?",
                        MessageBoxImage.Question,
                        MessageBoxButton.YesNo);
                    if (confirmResult != MessageBoxResult.Yes)
                        return false;
                }

                string robloxUriAuto = $"roblox://experiences/start?placeId={placeId}&gameInstanceId={autoResult.ServerId}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = robloxUriAuto,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception:", ex);
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _client?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}