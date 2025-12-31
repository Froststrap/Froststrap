/*
 *  Froststrap
 *  Copyright (c) Froststrap Team
 *
 *  This file is part of Froststrap and is distributed under the terms of the
 *  GNU Affero General Public License, version 3 or later.
 *
 *  SPDX-License-Identifier: AGPL-3.0-or-later
 *
 *  Description: Nix flake for shipping for Nix-darwin, Nix, NixOS, and modules
 *               of the Nix ecosystem. 
 */

using System.Collections.Concurrent;

namespace Bloxstrap.Integrations
{
    public class RobloxServerFetcher
    {
        private const string LOG_IDENT = "RobloxServerFetcher";
        private readonly HttpClient _client = new();
        private Dictionary<int, string>? _datacenterIdToRegion;
        private List<string>? _regionList;

        private string? _roblosecurityCached;
        private string? _channelToken;
        private readonly string _serverCacheFilePath = Path.Combine(Paths.Cache, "server_cache.json");
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, ServerInstance>> _serverCache = new();

        private readonly CookiesManager _cookiesManager;

        private const string DatacenterUrl = "https://apis.rovalra.com/v1/datacenters/list";

        public RobloxServerFetcher()
        {
            _cookiesManager = new CookiesManager();

            try
            {
                Directory.CreateDirectory(Paths.Cache);

                if (File.Exists(_serverCacheFilePath))
                {
                    string json = File.ReadAllText(_serverCacheFilePath);
                    var loadedCache = JsonSerializer.Deserialize<ConcurrentDictionary<long, ConcurrentDictionary<string, ServerInstance>>>(json);

                    if (loadedCache != null)
                    {
                        _serverCache = loadedCache;
                        App.Logger.WriteLine(LOG_IDENT, $"Successfully loaded server cache for {_serverCache.Count} games from disk.");
                    }
                }

                _ = InitializeCookiesAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private async Task InitializeCookiesAsync()
        {
            const string LOG_IDENT_INIT_COOKIES = $"{LOG_IDENT}::InitializeCookies";

            try
            {
                await _cookiesManager.LoadCookies();

                if (_cookiesManager.Loaded)
                {
                    _roblosecurityCached = _cookiesManager.GetAuthCookie();

                    var user = await _cookiesManager.GetAuthenticated();
                    if (user != null)
                    {
                        App.Logger.WriteLine(LOG_IDENT_INIT_COOKIES, $"Authenticated as: {user.Username} (ID: {user.Id})");
                    }
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_INIT_COOKIES, $"Cookies not loaded. State: {_cookiesManager.State}");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_INIT_COOKIES, ex);
            }
        }

        public async Task<DateTime?> GetServerUptime(string jobId, long placeId)
        {
            const string LOG_IDENT_UPTIME = $"{LOG_IDENT}::GetServerUptime";

            try
            {
                var response = await _client.GetAsync($"https://apis.rovalra.com/v1/server_details?place_id={placeId}&server_ids={jobId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var serverTimeRaw = JsonSerializer.Deserialize<RoValraTimeResponse>(content);

                    if (serverTimeRaw?.Servers != null && serverTimeRaw.Servers.Count > 0)
                    {
                        var server = serverTimeRaw.Servers[0];
                        return server.FirstSeen;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_UPTIME, ex);
            }

            return null;
        }

        public async Task<(List<string> regions, Dictionary<int, string> datacenterMap)?> GetDatacentersAsync()
        {
            const string LOG_IDENT_DATACENTERS = $"{LOG_IDENT}::GetDatacenters";

            if (_datacenterIdToRegion != null && _regionList != null)
                return (_regionList, _datacenterIdToRegion);

            string json;
            try
            {
                App.Logger.WriteLine(LOG_IDENT_DATACENTERS, "Fetching datacenters...");
                json = await _client.GetStringAsync(DatacenterUrl).ConfigureAwait(false);
                App.Logger.WriteLine(LOG_IDENT_DATACENTERS, "Successfully fetched datacenter list from remote API.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_DATACENTERS, ex);
                return null;
            }

            try
            {
                var datacenterEntries = JsonSerializer.Deserialize<List<DatacenterEntry>>(json);

                if (datacenterEntries == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_DATACENTERS, "Failed to deserialize datacenter JSON.");
                    return null;
                }

                var map = new Dictionary<int, string>();
                var regions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in datacenterEntries)
                {
                    string city = entry.Location?.City ?? "";
                    string country = entry.Location?.Country ?? "";

                    string regionKey = string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(country)
                        ? "Unknown"
                        : $"{city}, {country}".Trim().Trim(',', ' ');

                    regions.Add(regionKey);

                    foreach (var id in entry.DataCenterIds)
                    {
                        map[id] = regionKey;
                    }
                }

                var orderedRegions = regions.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
                _datacenterIdToRegion = map;
                _regionList = orderedRegions;

                return (_regionList, _datacenterIdToRegion);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_DATACENTERS, ex);
                return null;
            }
        }

        private string GetCachePath()
        {
            string dir = Path.Combine(Paths.Cache);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "regionselector.cache");
        }

        private void InvalidateCachedRoblosecurity()
        {
            _roblosecurityCached = null;
            try
            {
                var p = GetCachePath();
                if (File.Exists(p)) File.Delete(p);
                App.Logger.WriteLine(LOG_IDENT, "Roblosecurity cache invalidated.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private string? GetCachedRoblosecurity()
        {
            const string LOG_IDENT_GET_TOKEN = $"{LOG_IDENT}::GetCachedRoblosecurity";

            if (_cookiesManager.Loaded)
            {
                var cookie = _cookiesManager.GetAuthCookie();
                if (!string.IsNullOrWhiteSpace(cookie))
                {
                    App.Logger.WriteLine(LOG_IDENT_GET_TOKEN, "Using roblosecurity from CookiesManager");
                    return cookie;
                }
            }

            if (!string.IsNullOrWhiteSpace(_roblosecurityCached))
            {
                App.Logger.WriteLine(LOG_IDENT_GET_TOKEN, "Using cached roblosecurity");
                return _roblosecurityCached;
            }

            App.Logger.WriteLine(LOG_IDENT_GET_TOKEN, "No roblosecurity available. Region selector will not fetch servers.");
            return null;
        }

        private string MaskToken(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            if (s.Length <= 12) return new string('*', s.Length);
            return s.Substring(0, 6) + "..." + s.Substring(s.Length - 4);
        }

        private string DescribeTokenSource(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return "none";

            if (_cookiesManager.Loaded)
            {
                return "Cookie loadded";
            }

            return $"cached token={MaskToken(token)}";
        }

        private async Task<HttpResponseMessage> SendJoinRequestWithRetriesAsync(long placeId, string jobId, string roblosecurity, int maxAttempts = 3)
        {
            const string LOG_IDENT_JOIN_REQUEST = $"{LOG_IDENT}::SendJoinRequestWithRetries";

            int attempt = 0;
            while (true)
            {
                attempt++;

                if (_cookiesManager.Loaded)
                {
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, "https://gamejoin.roblox.com/v1/join-game-instance");
                        request.Headers.Add("User-Agent", $"Roblox/Froststrap");
                        request.Headers.Add("Referer", $"https://roblox.com/games/{placeId}");
                        request.Headers.Add("Origin", "https://roblox.com");

                        // Add channel token header if available
                        if (!string.IsNullOrEmpty(_channelToken))
                        {
                            request.Headers.Add("Roblox-Channel-Token", _channelToken);
                        }

                        request.Content = new StringContent(JsonSerializer.Serialize(new
                        {
                            placeId,
                            isTeleport = false,
                            gameId = jobId,
                            gameJoinAttemptId = jobId
                        }), Encoding.UTF8, "application/json");

                        var response = await _cookiesManager.AuthRequest(request);

                        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            App.Logger.WriteLine(LOG_IDENT_JOIN_REQUEST, $"Join request returned {(int)response.StatusCode}; cookies may be invalid.");
                            _roblosecurityCached = null;
                            return response;
                        }

                        if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                        {
                            if (attempt >= maxAttempts)
                                return response;
                            await Task.Delay(1000 * attempt).ConfigureAwait(false);
                            continue;
                        }
                        return response;
                    }
                    catch (Exception ex) when (attempt < maxAttempts)
                    {
                        App.Logger.WriteException(LOG_IDENT_JOIN_REQUEST, ex);
                        await Task.Delay(500 * attempt).ConfigureAwait(false);
                        continue;
                    }
                }
                else
                {
                    var joinReq = new HttpRequestMessage(HttpMethod.Post, "https://gamejoin.roblox.com/v1/join-game-instance");
                    joinReq.Headers.Add("User-Agent", $"Roblox/Froststrap");
                    joinReq.Headers.Add("Referer", $"https://roblox.com/games/{placeId}");
                    joinReq.Headers.Add("Origin", "https://roblox.com");

                    joinReq.Headers.Remove("Cookie");
                    joinReq.Headers.Add("Cookie", $".ROBLOSECURITY={roblosecurity}");

                    if (!string.IsNullOrEmpty(_channelToken))
                    {
                        joinReq.Headers.Add("Roblox-Channel-Token", _channelToken);
                    }

                    joinReq.Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        placeId,
                        isTeleport = false,
                        gameId = jobId,
                        gameJoinAttemptId = jobId
                    }), Encoding.UTF8, "application/json");

                    try
                    {
                        var resp = await _client.SendAsync(joinReq).ConfigureAwait(false);

                        if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden)
                        {
                            App.Logger.WriteLine(LOG_IDENT_JOIN_REQUEST, $"Join request returned {(int)resp.StatusCode}; invalidating cached roblosecurity.");
                            _roblosecurityCached = null;
                            return resp;
                        }

                        if ((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500)
                        {
                            if (attempt >= maxAttempts)
                                return resp;
                            await Task.Delay(1000 * attempt).ConfigureAwait(false);
                            continue;
                        }
                        return resp;
                    }
                    catch (HttpRequestException) when (attempt < maxAttempts)
                    {
                        await Task.Delay(500 * attempt).ConfigureAwait(false);
                        continue;
                    }
                }
            }
        }

        private bool TryExtractDataCenterId(JsonElement elem, out int dcId)
        {
            dcId = 0;
            if (elem.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in elem.EnumerateObject())
                {
                    if (prop.NameEquals("DataCenterId") && prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out dcId))
                        return true;
                    if (prop.Value.ValueKind == JsonValueKind.Number &&
                        (prop.Name.IndexOf("data", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         prop.Name.IndexOf("center", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         prop.Name.Equals("dc", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (prop.Value.TryGetInt32(out dcId))
                            return true;
                    }
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (TryExtractDataCenterId(prop.Value, out dcId)) return true;
                    }
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var s = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            if (int.TryParse(s, out var parsedInt))
                            {
                                dcId = parsedInt;
                                return true;
                            }
                            if (s.TrimStart().StartsWith("{") || s.TrimStart().StartsWith("["))
                            {
                                try
                                {
                                    using var nested = JsonDocument.Parse(s);
                                    if (TryExtractDataCenterId(nested.RootElement, out dcId)) return true;
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            return false;
        }

        public async Task<FetchResult> FetchServerInstancesAsync(long placeId, string cursor = "", int sortOrder = 2)
        {
            const string LOG_IDENT_FETCH = $"{LOG_IDENT}::FetchServerInstances";

            var roblosecurity = GetCachedRoblosecurity();
            if (string.IsNullOrWhiteSpace(roblosecurity))
            {
                App.Logger.WriteLine(LOG_IDENT_FETCH, $"No roblosecurity available for place {placeId}");
                return new FetchResult();
            }

            App.Logger.WriteLine(LOG_IDENT_FETCH, $"Starting server fetch for place {placeId} with cursor: '{cursor}' and sortOrder: {sortOrder}");

            string desc = DescribeTokenSource(roblosecurity);
            App.Logger.WriteLine(LOG_IDENT_FETCH, $"Fetching servers for place {placeId} using {desc}");

            if (!string.IsNullOrEmpty(_channelToken))
            {
                App.Logger.WriteLine(LOG_IDENT_FETCH, $"Using channel token for authenticated API access");
            }

            string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?sortOrder={sortOrder}&excludeFullGames=true&limit=100&cursor={cursor}";

            HttpResponseMessage response;
            try
            {
                if (_cookiesManager.Loaded)
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    if (!string.IsNullOrEmpty(_channelToken))
                    {
                        request.Headers.Add("Roblox-Channel-Token", _channelToken);
                    }
                    response = await _cookiesManager.AuthRequest(request);
                }
                else
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Remove("Cookie");
                    req.Headers.Add("Cookie", $".ROBLOSECURITY={roblosecurity}");

                    if (!string.IsNullOrEmpty(_channelToken))
                    {
                        req.Headers.Add("Roblox-Channel-Token", _channelToken);
                    }

                    response = await _client.SendAsync(req).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                return new FetchResult();
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                App.Logger.WriteLine(LOG_IDENT_FETCH, $"Server list request returned {(int)response.StatusCode}; cookies may be invalid.");
                _roblosecurityCached = null;
                return new FetchResult();
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                App.Logger.WriteLine(LOG_IDENT_FETCH, $"Rate limited for place {placeId}, waiting...");
                await Task.Delay(10000).ConfigureAwait(false);
                return new FetchResult();
            }

            if (!response.IsSuccessStatusCode)
            {
                App.Logger.WriteLine(LOG_IDENT_FETCH, $"Request failed with status: {(int)response.StatusCode} for place {placeId}");
                return new FetchResult();
            }

            string responseBody;
            try
            {
                responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                return new FetchResult();
            }

            using var jsonDoc = JsonDocument.Parse(responseBody);
            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
            {
                App.Logger.WriteLine(LOG_IDENT_FETCH, $"No 'data' array found in response for place {placeId}");
                return new FetchResult();
            }

            string nextCursor = "";
            if (jsonDoc.RootElement.TryGetProperty("nextPageCursor", out var cursorElem) && cursorElem.ValueKind == JsonValueKind.String)
            {
                nextCursor = cursorElem.GetString() ?? "";
            }

            var instances = new List<ServerInstance>();
            int maxConcurrency = 10;
            var semaphore = new SemaphoreSlim(maxConcurrency);

            var serverTasks = dataElement.EnumerateArray().Select(async serverElem =>
            {
                await semaphore.WaitAsync();
                try
                {
                    if (!serverElem.TryGetProperty("id", out var idElem) ||
                        !serverElem.TryGetProperty("playing", out var playingElem) ||
                        !serverElem.TryGetProperty("maxPlayers", out var maxElem))
                    {
                        return null;
                    }

                    string jobId = idElem.GetString() ?? "";
                    int playing = playingElem.GetInt32();
                    int maxPlayers = maxElem.GetInt32();

                    if (playing >= maxPlayers)
                    {
                        return null;
                    }

                    ServerInstance? cachedServer = null;
                    if (string.IsNullOrEmpty(cursor))
                    {
                        var cacheForPlace = _serverCache.GetOrAdd(placeId, new ConcurrentDictionary<string, ServerInstance>());
                        cacheForPlace.TryGetValue(jobId, out cachedServer);
                    }

                    if (cachedServer != null && cachedServer.DataCenterId.HasValue && cachedServer.Region != "Unknown")
                    {
                        return cachedServer;
                    }

                    int? dataCenterId = null;
                    string region = "Unknown";
                    DateTime? firstSeen = null;

                    try
                    {
                        var joinResp = await SendJoinRequestWithRetriesAsync(placeId, jobId, roblosecurity).ConfigureAwait(false);
                        string joinContent = await joinResp.Content.ReadAsStringAsync().ConfigureAwait(false);

                        using var parsed = JsonDocument.Parse(joinContent);

                        if (TryExtractDataCenterId(parsed.RootElement, out var extracted))
                        {
                            dataCenterId = extracted;
                        }
                        else if (parsed.RootElement.TryGetProperty("joinScript", out var joinScriptElem))
                        {
                            if (TryExtractDataCenterId(joinScriptElem, out extracted))
                                dataCenterId = extracted;
                        }

                        if (dataCenterId == null && parsed.RootElement.TryGetProperty("DataCenterId", out var topDc) && topDc.ValueKind == JsonValueKind.Number && topDc.TryGetInt32(out var tdc))
                            dataCenterId = tdc;

                        if (dataCenterId.HasValue)
                        {
                            if (_datacenterIdToRegion == null)
                            {
                                var loaded = await GetDatacentersAsync().ConfigureAwait(false);
                            }

                            if (_datacenterIdToRegion != null && _datacenterIdToRegion.TryGetValue(dataCenterId.Value, out var mappedRegion))
                                region = mappedRegion;
                        }

                        if (region != "Unknown")
                        {
                            try
                            {
                                firstSeen = await GetServerUptime(jobId, placeId);
                            }
                            catch (Exception uptimeEx)
                            {
                                App.Logger.WriteLine(LOG_IDENT_FETCH, $"Failed to get uptime for server {jobId}: {uptimeEx.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT_FETCH, $"Failed to get join info for server {jobId}: {ex.Message}");
                    }

                    var serverInstance = new ServerInstance
                    {
                        Id = jobId,
                        Playing = playing,
                        MaxPlayers = maxPlayers,
                        Region = region,
                        DataCenterId = dataCenterId,
                        FirstSeen = firstSeen
                    };

                    if (dataCenterId.HasValue && region != "Unknown")
                    {
                        var cacheForPlace = _serverCache.GetOrAdd(placeId, new ConcurrentDictionary<string, ServerInstance>());
                        cacheForPlace[jobId] = serverInstance;
                    }

                    return serverInstance;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var processedServers = await Task.WhenAll(serverTasks);
            var validServers = processedServers.OfType<ServerInstance>().ToList();

            int newlyFetchedCount = validServers.Count(s =>
                !string.IsNullOrEmpty(cursor) ||
                (_serverCache.TryGetValue(placeId, out var placeCache) && !placeCache.ContainsKey(s.Id)));

            App.Logger.WriteLine(LOG_IDENT_FETCH, $"Returning {validServers.Count} servers for Place ID {placeId} ({newlyFetchedCount} newly fetched). NextCursor: {nextCursor}");

            return new FetchResult
            {
                Servers = validServers,
                NewlyFetchedCount = newlyFetchedCount,
                NextCursor = nextCursor
            };
        }

        public void SetRoblosecurity(string? token)
        {
            const string LOG_IDENT_SET_TOKEN = $"{LOG_IDENT}::SetRoblosecurity";

            _roblosecurityCached = string.IsNullOrWhiteSpace(token) ? null : token;

            if (!string.IsNullOrWhiteSpace(_roblosecurityCached))
            {
                App.Logger.WriteLine(LOG_IDENT_SET_TOKEN, "RobloxServerFetcher: roblosecurity set from external source (in-memory only).");
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT_SET_TOKEN, "RobloxServerFetcher: roblosecurity cleared (in-memory).");
            }
        }

        public bool HasValidCookies()
        {
            return _cookiesManager.Loaded || !string.IsNullOrWhiteSpace(_roblosecurityCached);
        }
    }
}