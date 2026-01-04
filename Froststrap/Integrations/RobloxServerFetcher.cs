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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bloxstrap.Integrations;
using Froststrap.AvaloniaUI;
using Froststrap.Models.APIs.Roblox;
using Froststrap.Models.APIs.RoValra;

namespace Froststrap.Integrations
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

        private const string DatacenterUrl = "https://apis.rovalra.com/v1/datacenters/list";

        public void SetChannelToken(string? token)
        {
            _channelToken = string.IsNullOrWhiteSpace(token) ? null : token;
            App.Logger.WriteLine(LOG_IDENT, $"Channel token {(string.IsNullOrEmpty(token) ? "cleared" : "set")}");
        }

        public RobloxServerFetcher()
        {
            _client.Timeout = TimeSpan.FromSeconds(60);

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
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
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

            try
            {
                var shared = AccountManager.Shared;
                var active = shared?.ActiveAccount;
                if (active != null && !string.IsNullOrWhiteSpace(active.SecurityToken))
                {
                    _roblosecurityCached = active.SecurityToken;
                    App.Logger.WriteLine(LOG_IDENT_GET_TOKEN, $"Using roblosecurity from AccountManager active account '{active.Username}'.");
                    return _roblosecurityCached;
                }

                App.Logger.WriteLine(LOG_IDENT_GET_TOKEN, "No active account roblosecurity available in AccountManager. Region selector will not fetch servers.");
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_GET_TOKEN, ex);
                return null;
            }
        }

        private string MaskToken(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            if (s.Length <= 12) return new string('*', s.Length);
            return s.Substring(0, 6) + "..." + s.Substring(s.Length - 4);
        }

        private string DescribeTokenSource(string token, AltAccount? activeSnapshot = null)
        {
            if (string.IsNullOrWhiteSpace(token)) return "none";

            try
            {
                var shared = AccountManager.Shared;
                var active = activeSnapshot ?? shared?.ActiveAccount;
                if (active != null && !string.IsNullOrWhiteSpace(active.SecurityToken) && active.SecurityToken == token)
                    return $"active account '{active.Username}' token={MaskToken(token)}";

                var match = shared?.Accounts.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.SecurityToken) && a.SecurityToken == token);
                if (match != null)
                    return $"alt-account '{match.Username}' token={MaskToken(token)}";

                return $"unknown source (token len={token.Length}) token={MaskToken(token)}";
            }
            catch
            {
                return $"unknown (error inspecting AltManager) token len={(token?.Length ?? 0)}";
            }
        }

        public void ClearCache()
        {
            const string LOG_IDENT_CLEAR_CACHE = $"{LOG_IDENT}::ClearCache";

            _serverCache.Clear();
            App.Logger.WriteLine(LOG_IDENT_CLEAR_CACHE, "In-memory server cache has been cleared.");

            try
            {
                if (File.Exists(_serverCacheFilePath))
                {
                    File.Delete(_serverCacheFilePath);
                    App.Logger.WriteLine(LOG_IDENT_CLEAR_CACHE, $"Successfully deleted persistent cache file at: {_serverCacheFilePath}");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_CLEAR_CACHE, ex);
            }
        }

        private async Task<HttpResponseMessage> SendJoinRequestWithRetriesAsync(long placeId, string jobId, string roblosecurity, int maxAttempts = 3)
        {
            const string LOG_IDENT_JOIN_REQUEST = $"{LOG_IDENT}::SendJoinRequestWithRetries";

            int attempt = 0;
            while (true)
            {
                attempt++;
                var joinReq = new HttpRequestMessage(HttpMethod.Post, "https://gamejoin.roblox.com/v1/join-game-instance");
                joinReq.Headers.Add("User-Agent", "Roblox/Froststrap");
                joinReq.Headers.Add("Referer", $"https://roblox.com/games/{placeId}");
                joinReq.Headers.Add("Origin", "https://roblox.com");

                joinReq.Headers.Remove("Cookie");
                joinReq.Headers.Add("Cookie", $".ROBLOSECURITY={roblosecurity}");

                // Add channel token header if available
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
                        InvalidateCachedRoblosecurity();
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
                return new FetchResult();

            App.Logger.WriteLine(LOG_IDENT_FETCH, $"Starting server fetch for place {placeId} with cursor: '{cursor}' and sortOrder: {sortOrder}");

            AltAccount? activeSnapshot = null;
            try
            {
                activeSnapshot = AccountManager.Shared?.ActiveAccount;
                string desc = DescribeTokenSource(roblosecurity, activeSnapshot);
                App.Logger.WriteLine(LOG_IDENT_FETCH, $"Fetching servers for place {placeId}.");

                if (!string.IsNullOrEmpty(_channelToken))
                {
                    App.Logger.WriteLine(LOG_IDENT_FETCH, $"Using channel token for authenticated API access");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_FETCH, ex);
            }

            string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?sortOrder={sortOrder}&excludeFullGames=true&limit=100&cursor={cursor}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Remove("Cookie");
            req.Headers.Add("Cookie", $".ROBLOSECURITY={roblosecurity}");

            if (!string.IsNullOrEmpty(_channelToken))
            {
                req.Headers.Add("Roblox-Channel-Token", _channelToken);
            }

            HttpResponseMessage response;
            try
            {
                response = await _client.SendAsync(req).ConfigureAwait(false);
            }
            catch
            {
                return new FetchResult();
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                App.Logger.WriteLine(LOG_IDENT_FETCH, $"Server list request returned {(int)response.StatusCode}; invalidating cached roblosecurity.");
                InvalidateCachedRoblosecurity();
                return new FetchResult();
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await Task.Delay(10000).ConfigureAwait(false);
                return new FetchResult();
            }

            if (!response.IsSuccessStatusCode)
                return new FetchResult();

            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var jsonDoc = JsonDocument.Parse(responseBody);
            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
                return new FetchResult();

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
                            catch
                            {
                                // Ignore uptime errors
                            }
                        }
                    }
                    catch
                    {
                        // Ignore join info errors
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

            App.Logger.WriteLine(LOG_IDENT_FETCH, $"Returning {validServers.Count} servers for Place ID {placeId}. NextCursor: {nextCursor}");

            return new FetchResult
            {
                Servers = validServers,
                NewlyFetchedCount = validServers.Count,
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
    }
}