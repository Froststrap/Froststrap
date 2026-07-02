/*
 *  Froststrap
 *  Copyright (c) Froststrap Team
 *
 *  This file is part of Froststrap and is distributed under the terms of the
 *  GNU Affero General Public License, version 3 or later.
 *
 *  SPDX-License-Identifier: AGPL-3.0-or-later
 *
 */

using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace Froststrap.Integrations
{
    public class RobloxServerFetcher
    {
        private const string LOG_IDENT = "RobloxServerFetcher";
        private readonly HttpClient _client;
        private Dictionary<int, string>? _datacenterIdToRegion;
        private List<string>? _regionList;

        private readonly string _serverCacheFilePath = Path.Combine(Paths.Cache, "server_cache.json");
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, ServerInstance>> _serverCache = [];

        private const string DatacenterUrl = "https://apis.rovalra.com/v1/datacenters/list";

        public class RegionDistance
        {
            public string Region { get; set; } = "";
            public double DistanceKm { get; set; }
        }

        public class ServerSelectionResult
        {
            public string? ServerId { get; set; }
            public string? Region { get; set; }
            public int Rank { get; set; }
            public int Players { get; set; }
            public int MaxPlayers { get; set; }
            public bool Found => !string.IsNullOrEmpty(ServerId);
        }

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
                        App.Logger.WriteLine(LOG_IDENT, $"Loaded {_serverCache.Count} games from disk.");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public async Task<DateTime?> GetServerUptime(string jobId, long placeId)
        {
            try
            {
                var response = await _client.GetAsync($"https://apis.rovalra.com/v1/servers/details?place_id={placeId}&server_ids={jobId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var serverTimeRaw = JsonSerializer.Deserialize<RoValraTimeResponse>(content);

                    if (serverTimeRaw?.Servers is { Count: > 0 })
                    {
                        return serverTimeRaw.Servers[0].FirstSeen;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"{LOG_IDENT}::Uptime", ex);
            }

            return null;
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
                    var response = await _client.GetAsync(
                        $"https://apis.rovalra.com/v1/servers/details?place_id={placeId}&server_ids={Uri.EscapeDataString(serverIdsParam)}",
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
                App.Logger.WriteException($"{LOG_IDENT}::UptimesBatch", ex);
                return result;
            }
        }

        public async Task<(List<string> regions, Dictionary<int, string> datacenterMap)?> GetDatacentersAsync(CancellationToken cancellationToken = default)
        {
            if (_datacenterIdToRegion != null && _regionList != null)
                return (_regionList, _datacenterIdToRegion);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var json = await _client.GetStringAsync(DatacenterUrl, cancellationToken);
                var datacenterEntries = JsonSerializer.Deserialize<List<DatacenterEntry>>(json);

                if (datacenterEntries == null) return null;

                var map = new Dictionary<int, string>();
                var regions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in datacenterEntries)
                {
                    string regionKey = string.IsNullOrWhiteSpace(entry.Location?.City) && string.IsNullOrWhiteSpace(entry.Location?.Country)
                        ? "Unknown"
                        : $"{entry.Location.City}, {entry.Location.Country}".Trim().Trim(',', ' ');

                    regions.Add(regionKey);

                    foreach (var id in entry.DataCenterIds)
                    {
                        map[id] = regionKey;
                    }
                }

                _regionList = [.. regions.OrderBy(r => r, StringComparer.OrdinalIgnoreCase)];
                _datacenterIdToRegion = map;

                return (_regionList, _datacenterIdToRegion);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"{LOG_IDENT}::Datacenters", ex);
                return null;
            }
        }

        private async Task<HttpResponseMessage> SendJoinRequestWithRetriesAsync(long placeId, string jobId, string roblosecurity, CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            const int maxAttempts = 3;

            while (true)
            {
                attempt++;
                var joinReq = new HttpRequestMessage(HttpMethod.Post, UrlBuilder.BuildApiUrl("gamejoin", "v1/join-game-instance", secure: true));
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
                if (prop.Value.ValueKind == JsonValueKind.Number && (prop.Name.Contains("DataCenterId") || prop.Name.Equals("dc")))
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

                var request = new HttpRequestMessage(HttpMethod.Get, UrlBuilder.BuildApiUrl("users", "v1/users/authenticated", secure: true));
                request.Headers.Add("Cookie", $".ROBLOSECURITY={roblosecurityCookie}");

                var response = await _client.SendAsync(request);
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string?> GetCookieFromAccountManagerAsync()
        {
            try
            {
                if (AccountManager.Shared?.ActiveAccount != null)
                {
                    return AccountManager.Shared.ActiveAccount.SecurityToken;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"{LOG_IDENT}::GetCookieFromAccountManager", ex);
            }
            return null;
        }

        private static async Task<string?> GetCookieFromCookiesManagerAsync()
        {
            try
            {
                if (App.Cookies != null)
                {
                    var field = typeof(CookiesManager).GetField("AuthCookie", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        return field.GetValue(App.Cookies) as string;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"{LOG_IDENT}::GetCookieFromCookiesManager", ex);
            }
            return null;
        }

        private static async Task<string?> GetCookieFromRemoteDataAsync()
        {
            try
            {
                if (App.RemoteData != null)
                {
                    await App.RemoteData.WaitUntilDataFetched();
                    return App.RemoteData.Prop?.Dummy;
                }
            }
            catch (Exception ex) { App.Logger.WriteException($"{LOG_IDENT}::GetCookieFromRemoteData", ex); }
            return null;
        }

        public async Task<string?> ResolveCookieAsync()
        {
            var accountManagerCookie = await GetCookieFromAccountManagerAsync();
            if (!string.IsNullOrWhiteSpace(accountManagerCookie) && await ValidateCookieAsync(accountManagerCookie))
            {
                App.Logger.WriteLine(LOG_IDENT, "Using valid cookie from Account Manager.");
                return accountManagerCookie;
            }

            var cookiesManagerCookie = await GetCookieFromCookiesManagerAsync();
            if (!string.IsNullOrWhiteSpace(cookiesManagerCookie) && await ValidateCookieAsync(cookiesManagerCookie))
            {
                App.Logger.WriteLine(LOG_IDENT, "Account Manager cookie failed or missing. Using valid cookie from Cookies Manager.");
                return cookiesManagerCookie;
            }

            var remoteDataCookie = await GetCookieFromRemoteDataAsync();
            if (!string.IsNullOrWhiteSpace(remoteDataCookie) && await ValidateCookieAsync(remoteDataCookie))
            {
                App.Logger.WriteLine(LOG_IDENT, "Cookies Manager cookie failed or missing. Using valid cookie from Remote Data.");
                return remoteDataCookie;
            }

            App.Logger.WriteLine(LOG_IDENT, "Failed to resolve any valid .ROBLOSECURITY cookie.");
            return null;
        }

        public async Task<FetchResult> FetchServerInstancesAsync(long placeId, string cursor = "", int sortOrder = 2, string? optionalCookie = null, CancellationToken cancellationToken = default)
        {
            string? roblosecurity = !string.IsNullOrWhiteSpace(optionalCookie) ? optionalCookie : await ResolveCookieAsync();
            if (string.IsNullOrWhiteSpace(roblosecurity)) return new FetchResult();

            if (_datacenterIdToRegion == null) await GetDatacentersAsync(cancellationToken);

            var baseUri = UrlBuilder.BuildApiUrl("games", $"v1/games/{placeId}/servers/Public", secure: true);
            var url = new UriBuilder(baseUri)
            {
                Query = $"sortOrder={sortOrder}&excludeFullGames=true&limit=100&cursor={cursor}"
            }.Uri;

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Cookie", $".ROBLOSECURITY={roblosecurity}");

            var response = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new FetchResult();

            using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement)) return new FetchResult();

            string nextCursor = jsonDoc.RootElement.TryGetProperty("nextPageCursor", out var cElem) ? cElem.GetString() ?? "" : "";

            var instances = new ConcurrentBag<ServerInstance>();
            var placeCache = _serverCache.GetOrAdd(placeId, _ => []);

            var serverInfos = new List<(string jobId, int playing, int maxPlayers, JsonElement serverElem)>();
            var serverIdsToFetch = new List<string>();

            foreach (var serverElem in dataElement.EnumerateArray())
            {
                string jobId = serverElem.GetProperty("id").GetString() ?? "";
                int playing = serverElem.GetProperty("playing").GetInt32();
                int maxPlayers = serverElem.GetProperty("maxPlayers").GetInt32();

                if (playing >= maxPlayers) continue;

                if (placeCache.TryGetValue(jobId, out var cached) && cached.Region != "Unknown")
                {
                    instances.Add(cached);
                    continue;
                }

                serverInfos.Add((jobId, playing, maxPlayers, serverElem));
                serverIdsToFetch.Add(jobId);
            }

            var regionTasks = new List<Task<(string jobId, int? dcId)>>();
            var uptimeTasks = new Dictionary<string, Task<DateTime?>>();

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
                var (jobId, playing, maxPlayers, _) = serverInfos[i];
                var (_, dcId) = regionResults[i];

                string region = (dcId.HasValue && _datacenterIdToRegion!.TryGetValue(dcId.Value, out var mapped)) ? mapped : "Unknown";
                DateTime? uptime = uptimeResults != null && uptimeResults.TryGetValue(jobId, out var up) ? up : null;

                var server = new ServerInstance
                {
                    Id = jobId,
                    Playing = playing,
                    MaxPlayers = maxPlayers,
                    Region = region,
                    DataCenterId = dcId,
                    FirstSeen = uptime
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

        public static double Deg2Rad(double deg) => deg * (Math.PI / 180.0);

        public static double GetDistance(double lat1, double lon1, double lat2, double lon2)
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

        public async Task<List<string>> GetClosestRegionsForAutoModeAsync(int topCount, CancellationToken cancellationToken = default)
        {
            try
            {
                var datacentersResult = await GetDatacentersAsync(cancellationToken);
                if (datacentersResult == null)
                    return [];

                var (_, dcMap) = datacentersResult.Value;

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Froststrap/1.0");
                var ipinfoJson = await httpClient.GetStringAsync("https://ipinfo.io/json", cancellationToken);
                var ipinfo = JsonSerializer.Deserialize<IPInfoResponse>(ipinfoJson);

                if (string.IsNullOrEmpty(ipinfo?.Loc))
                    return [];

                string[] location = ipinfo.Loc.Split(',');
                double userLat = double.Parse(location[0], CultureInfo.InvariantCulture);
                double userLon = double.Parse(location[1], CultureInfo.InvariantCulture);

                var datacentersJson = await httpClient.GetStringAsync("https://apis.rovalra.com/v1/datacenters/list", cancellationToken);
                var datacenters = JsonSerializer.Deserialize<List<DatacenterEntry>>(datacentersJson);

                if (datacenters == null || datacenters.Count == 0)
                    return [];

                var regionDistance = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                foreach (var dc in datacenters)
                {
                    if (dc.Location == null || dc.Location.LatLong == null || dc.Location.LatLong.Length < 2)
                        continue;

                    if (!double.TryParse(dc.Location.LatLong[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                        !double.TryParse(dc.Location.LatLong[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                        continue;

                    double distance = GetDistance(userLat, userLon, lat, lon);

                    string? regionKey = null;
                    foreach (var dcId in dc.DataCenterIds)
                    {
                        if (dcMap.TryGetValue(dcId, out string? region))
                        {
                            regionKey = region;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(regionKey))
                    {
                        regionKey = $"{dc.Location.City}, {dc.Location.Country}".TrimStart(',').Trim();
                        if (string.IsNullOrEmpty(regionKey))
                            regionKey = "Unknown";
                    }

                    if (!regionDistance.TryGetValue(regionKey, out double existingDistance) || distance < existingDistance)
                        regionDistance[regionKey] = distance;
                }

                var closestRegions = regionDistance
                    .OrderBy(kvp => kvp.Value)
                    .Take(topCount)
                    .Select(kvp => kvp.Key)
                    .ToList();

                App.Logger.WriteLine("RobloxServerFetcher", $"Top {closestRegions.Count} regions: {string.Join(", ", closestRegions)}");
                return closestRegions;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("RobloxServerFetcher::GetClosestRegionsForAutoMode", ex);
                return [];
            }
        }

        public async Task<ServerSelectionResult> FindBestServerInRegionAsync(
    long placeId,
    List<string> topRegions,
    bool joinSmallerServer = true,
    int maxServerCheck = 100,
    int maxPages = 5,
    string? cookie = null,
    CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(cookie))
                {
                    cookie = await ResolveCookieAsync();
                    if (string.IsNullOrEmpty(cookie))
                        return new ServerSelectionResult();
                }

                var datacentersResult = await GetDatacentersAsync(cancellationToken);
                if (datacentersResult == null)
                    return new ServerSelectionResult();

                var (_, dcMap) = datacentersResult.Value;

                var regionRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < topRegions.Count; i++)
                    regionRank[topRegions[i]] = i + 1;

                App.Logger.WriteLine("RobloxServerFetcher", $"Searching in top {topRegions.Count} regions: {string.Join(", ", topRegions)}");

                string? nextCursor = null;
                int serversChecked = 0;
                int pagesFetched = 0;

                var allServers = new List<ServerInstance>();

                while (pagesFetched < maxPages && serversChecked < maxServerCheck)
                {
                    int sortOrder = joinSmallerServer ? 1 : 2;
                    var result = await FetchServerInstancesAsync(placeId, nextCursor ?? "", sortOrder, cookie, cancellationToken);

                    if (result?.Servers == null || result.Servers.Count == 0)
                    {
                        if (pagesFetched == 0 && string.IsNullOrEmpty(result?.NextCursor))
                            break;

                        if (!string.IsNullOrEmpty(result?.NextCursor))
                        {
                            await Task.Delay(500, cancellationToken);
                            nextCursor = result.NextCursor;
                            continue;
                        }
                        break;
                    }

                    foreach (var server in result.Servers)
                    {
                        if (serversChecked >= maxServerCheck) break;

                        if (!server.DataCenterId.HasValue) continue;
                        if (!dcMap.TryGetValue(server.DataCenterId.Value, out var serverRegion)) continue;
                        if (server.Playing >= server.MaxPlayers) continue;
                        if (!regionRank.TryGetValue(serverRegion, out _)) continue;

                        allServers.Add(server);
                        serversChecked++;
                    }

                    pagesFetched++;
                    nextCursor = result.NextCursor;
                    if (string.IsNullOrEmpty(nextCursor))
                        break;

                    await Task.Delay(100, cancellationToken);
                }

                App.Logger.WriteLine("RobloxServerFetcher", $"Collected {allServers.Count} servers from {pagesFetched} pages");

                string? bestServerId = null;
                string? bestServerRegion = null;
                int bestRank = int.MaxValue;
                int bestPlayers = int.MaxValue;
                int bestMaxPlayers = 0;

                foreach (var server in allServers)
                {
                    if (!server.DataCenterId.HasValue) continue;
                    if (!dcMap.TryGetValue(server.DataCenterId.Value, out var serverRegion)) continue;
                    if (!regionRank.TryGetValue(serverRegion, out int rank)) continue;

                    bool isBetter = false;
                    if (rank < bestRank)
                    {
                        isBetter = true;
                    }
                    else if (rank == bestRank && joinSmallerServer && server.Playing < bestPlayers)
                    {
                        isBetter = true;
                    }
                    else if (rank == bestRank && !joinSmallerServer && server.Playing > bestPlayers)
                    {
                        isBetter = true;
                    }

                    if (isBetter)
                    {
                        bestRank = rank;
                        bestPlayers = server.Playing;
                        bestMaxPlayers = server.MaxPlayers;
                        bestServerId = server.Id;
                        bestServerRegion = serverRegion;
                        App.Logger.WriteLine("RobloxServerFetcher", $"Found better server in {serverRegion} (rank {rank}, players: {server.Playing}/{server.MaxPlayers})");

                        if (rank == 1)
                        {
                            App.Logger.WriteLine("RobloxServerFetcher", "Found rank 1 server, stopping early");
                            break;
                        }
                    }
                }

                return new ServerSelectionResult
                {
                    ServerId = bestServerId,
                    Region = bestServerRegion,
                    Rank = bestRank,
                    Players = bestPlayers,
                    MaxPlayers = bestMaxPlayers
                };
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("RobloxServerFetcher::FindBestServerInRegionAsync", ex);
                return new ServerSelectionResult();
            }
        }

        public async Task<ServerSelectionResult> FindBestServerInSelectedRegionAsync(
            long placeId,
            string selectedRegion,
            bool joinSmallerServer = true,
            int maxServerCheck = 100,
            int maxPages = 3,
            string? cookie = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(selectedRegion))
                    return new ServerSelectionResult();

                if (string.IsNullOrEmpty(cookie))
                {
                    cookie = await ResolveCookieAsync();
                    if (string.IsNullOrEmpty(cookie))
                        return new ServerSelectionResult();
                }

                var datacentersResult = await GetDatacentersAsync(cancellationToken);
                if (datacentersResult == null)
                    return new ServerSelectionResult();

                var (_, dcMap) = datacentersResult.Value;

                App.Logger.WriteLine("RobloxServerFetcher", $"Searching for servers in selected region: {selectedRegion}");

                string? nextCursor = "";
                int serversChecked = 0;
                int pagesFetched = 0;

                var allServers = new List<ServerInstance>();

                while (!string.IsNullOrEmpty(nextCursor) && pagesFetched < maxPages && serversChecked < maxServerCheck)
                {
                    int sortOrder = joinSmallerServer ? 1 : 2;
                    var result = await FetchServerInstancesAsync(placeId, nextCursor ?? "", sortOrder, cookie, cancellationToken);

                    if (result?.Servers == null || result.Servers.Count == 0)
                    {
                        if (string.IsNullOrEmpty(nextCursor)) break;
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    foreach (var server in result.Servers)
                    {
                        if (serversChecked >= maxServerCheck) break;

                        if (!server.DataCenterId.HasValue) continue;
                        if (!dcMap.TryGetValue(server.DataCenterId.Value, out var serverRegion)) continue;
                        if (server.Playing >= server.MaxPlayers) continue;

                        if (!serverRegion.Equals(selectedRegion, StringComparison.OrdinalIgnoreCase))
                            continue;

                        allServers.Add(server);
                        serversChecked++;
                    }

                    pagesFetched++;

                    if (!string.IsNullOrEmpty(result.NextCursor))
                        nextCursor = result.NextCursor;
                    else
                        break;

                    if (!string.IsNullOrEmpty(nextCursor))
                        await Task.Delay(100, cancellationToken);
                }

                App.Logger.WriteLine("RobloxServerFetcher", $"Found {allServers.Count} servers in selected region from {pagesFetched} pages");

                var bestServer = joinSmallerServer
                    ? allServers.OrderBy(s => s.Playing).FirstOrDefault()
                    : allServers.FirstOrDefault();

                if (bestServer != null)
                {
                    return new ServerSelectionResult
                    {
                        ServerId = bestServer.Id,
                        Region = selectedRegion,
                        Rank = 1,
                        Players = bestServer.Playing,
                        MaxPlayers = bestServer.MaxPlayers
                    };
                }

                return new ServerSelectionResult();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("RobloxServerFetcher::FindBestServerInSelectedRegionAsync", ex);
                return new ServerSelectionResult();
            }
        }

        public async Task<bool> JoinBestServerAsync(
            long placeId,
            bool joinSmallerServer = true,
            int bestRegionAmounts = 3,
            int maxServerCheck = 100,
            bool showConfirmation = true,
            string? cookie = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(cookie))
                {
                    cookie = await ResolveCookieAsync();
                    if (string.IsNullOrEmpty(cookie))
                    {
                        await Frontend.ShowMessageBox("No valid cookie found. Log in using account manager or turn on 'Froststrap Account Permission' to use this feature.", MessageBoxImage.Error);
                        return false;
                    }
                }

                var topRegions = await GetClosestRegionsForAutoModeAsync(bestRegionAmounts, cancellationToken);
                if (topRegions.Count == 0)
                {
                    await Frontend.ShowMessageBox("Could not determine your location for Auto mode. Please try again later.", MessageBoxImage.Warning);
                    return false;
                }

                var result = await FindBestServerInRegionAsync(placeId, topRegions, joinSmallerServer, maxServerCheck, cookie: cookie, cancellationToken: cancellationToken);

                if (!result.Found)
                {
                    await Frontend.ShowMessageBox($"Could not find a suitable server after checking servers in {topRegions.Count} regions.", MessageBoxImage.Information);
                    return false;
                }

                if (showConfirmation)
                {
                    string playerCount = $"{result.Players}/{result.MaxPlayers}";
                    var confirmResult = await Frontend.ShowMessageBox(
                        $"Found server in {result.Region} with {playerCount} players.\nDo you want to join?",
                        MessageBoxImage.Question,
                        MessageBoxButton.YesNo
                    );

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
            catch (Exception ex)
            {
                App.Logger.WriteException("RobloxServerFetcher::JoinBestServerAsync", ex);
                return false;
            }
        }
    }
}