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
using System.Net.Http.Headers;

namespace Bloxstrap.Integrations
{
    public class RobloxServerFetcher
    {
        private const string LOG_IDENT = "RobloxServerFetcher";
        private readonly HttpClient _client;
        private Dictionary<int, string>? _datacenterIdToRegion;
        private List<string>? _regionList;

        private readonly string _serverCacheFilePath = Path.Combine(Paths.Cache, "server_cache.json");
        private ConcurrentDictionary<long, ConcurrentDictionary<string, ServerInstance>> _serverCache = new();

        private const string DatacenterUrl = "https://apis.rovalra.com/v1/datacenters/list";

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
                var response = await _client.GetAsync($"https://apis.rovalra.com/v1/server_details?place_id={placeId}&server_ids={jobId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var serverTimeRaw = JsonSerializer.Deserialize<RoValraTimeResponse>(content);

                    if (serverTimeRaw?.Servers != null && serverTimeRaw.Servers.Count > 0)
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

        public async Task<(List<string> regions, Dictionary<int, string> datacenterMap)?> GetDatacentersAsync()
        {
            if (_datacenterIdToRegion != null && _regionList != null)
                return (_regionList, _datacenterIdToRegion);

            try
            {
                var json = await _client.GetStringAsync(DatacenterUrl);
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

                _regionList = regions.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
                _datacenterIdToRegion = map;

                return (_regionList, _datacenterIdToRegion);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"{LOG_IDENT}::Datacenters", ex);
                return null;
            }
        }

        private async Task<HttpResponseMessage> SendJoinRequestWithRetriesAsync(long placeId, string jobId, string roblosecurity)
        {
            int attempt = 0;
            const int maxAttempts = 3;

            while (true)
            {
                attempt++;
                var joinReq = new HttpRequestMessage(HttpMethod.Post, "https://gamejoin.roblox.com/v1/join-game-instance");
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
                    var resp = await _client.SendAsync(joinReq).ConfigureAwait(false);

                    if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden)
                        return resp;

                    if (((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500) && attempt < maxAttempts)
                    {
                        await Task.Delay(500 * attempt).ConfigureAwait(false);
                        continue;
                    }

                    return resp;
                }
                catch (HttpRequestException) when (attempt < maxAttempts)
                {
                    await Task.Delay(250 * attempt).ConfigureAwait(false);
                }
            }
        }

        private bool TryExtractDataCenterId(JsonElement elem, out int dcId)
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

                var request = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
                request.Headers.Add("Cookie", $".ROBLOSECURITY={roblosecurityCookie}");

                var response = await _client.SendAsync(request);
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch
            {
                return false;
            }
        }

        public async Task<FetchResult> FetchServerInstancesAsync(long placeId, string roblosecurity, string cursor = "", int sortOrder = 2)
        {
            if (string.IsNullOrWhiteSpace(roblosecurity)) return new FetchResult();

            if (_datacenterIdToRegion == null) await GetDatacentersAsync();

            string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?sortOrder={sortOrder}&excludeFullGames=true&limit=100&cursor={cursor}";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Cookie", $".ROBLOSECURITY={roblosecurity}");

            var response = await _client.SendAsync(req).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new FetchResult();

            using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement)) return new FetchResult();

            string nextCursor = jsonDoc.RootElement.TryGetProperty("nextPageCursor", out var cElem) ? cElem.GetString() ?? "" : "";

            var instances = new ConcurrentBag<ServerInstance>();
            var placeCache = _serverCache.GetOrAdd(placeId, _ => new ConcurrentDictionary<string, ServerInstance>());

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 8 };
            await Parallel.ForEachAsync(dataElement.EnumerateArray(), parallelOptions, async (serverElem, ct) =>
            {
                string jobId = serverElem.GetProperty("id").GetString() ?? "";
                int playing = serverElem.GetProperty("playing").GetInt32();
                int maxPlayers = serverElem.GetProperty("maxPlayers").GetInt32();

                if (playing >= maxPlayers) return;

                if (placeCache.TryGetValue(jobId, out var cached) && cached.Region != "Unknown")
                {
                    instances.Add(cached);
                    return;
                }

                try
                {
                    var joinResp = await SendJoinRequestWithRetriesAsync(placeId, jobId, roblosecurity);
                    using var parsed = JsonDocument.Parse(await joinResp.Content.ReadAsStringAsync());

                    int? dcId = null;
                    if (TryExtractDataCenterId(parsed.RootElement, out int extracted)) dcId = extracted;

                    string region = (dcId.HasValue && _datacenterIdToRegion!.TryGetValue(dcId.Value, out var mapped)) ? mapped : "Unknown";
                    DateTime? uptime = region != "Unknown" ? await GetServerUptime(jobId, placeId) : null;

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
                catch { }
            });

            return new FetchResult
            {
                Servers = instances.ToList(),
                NextCursor = nextCursor
            };
        }
    }
}