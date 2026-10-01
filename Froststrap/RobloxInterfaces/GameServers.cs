using Froststrap.AppData;
using Froststrap.Enums.Overlay;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Froststrap.RobloxInterfaces
{
    internal class GameServers
    {
        private const int PageSize = 100;
        private const int MaxStatsPages = 5;
        private const int MaxRegionPages = 3;
        private const int MaxFaces = 5;
        private const int DetailsBatchSize = 50;
        private const int DetailsReach = 100;

        private static readonly Uri DatacentersUrl = new("https://apis.rovalra.com/v1/datacenters/list");

        private static List<ServerRegion>? _regions;

        public static async Task<List<ServerRegion>> FetchRegionsAsync()
        {
            if (_regions is not null)
                return _regions;

            var regions = new List<ServerRegion> { AllRegions() };

            try
            {
                var datacenters = await Http.GetJson<List<DatacenterEntry>>(DatacentersUrl);

                regions.AddRange(datacenters
                    .Select(x => x.Location?.Country)
                    .Where(x => !String.IsNullOrEmpty(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(code => new ServerRegion { Code = code!, Name = RegionName(code!) })
                    .OrderBy(x => x.Name, StringComparer.Ordinal));
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to fetch datacenter regions");
                App.Logger.Error(ex);
            }

            _regions = regions;

            return _regions;
        }

        public static ServerRegion AllRegions() => new() { Code = String.Empty, Name = Strings.Menu_Overlay_Servers_AllRegions };

        private static string RegionName(string code)
        {
            try
            {
                return new RegionInfo(code).EnglishName;
            }
            catch (ArgumentException)
            {
                return code.ToUpperInvariant();
            }
        }

        public static async Task<List<GameServer>> FetchAsync(long placeId, string currentJobId, Order order, bool excludeFull, ServerRegion? region)
        {
            var servers = new List<GameServer>();

            if (placeId == 0)
                return servers;

            List<GameServerResponse> live = await FetchLiveAsync(placeId);

            var stats = new Dictionary<string, GameServerResponse>(StringComparer.OrdinalIgnoreCase);

            foreach (GameServerResponse server in live)
            {
                if (!String.IsNullOrEmpty(server.Id))
                    stats[server.Id] = server;
            }

            if (region is null || region.IsAll)
            {
                servers.AddRange(stats.Values.Select(x => new GameServer
                {
                    JobId = x.Id,
                    Playing = x.Playing,
                    MaxPlayers = x.MaxPlayers,
                    Fps = x.Fps,
                    Ping = x.Ping,
                    PlayerTokens = x.PlayerTokens,
                    IsCurrent = x.Id.Equals(currentJobId, StringComparison.OrdinalIgnoreCase)
                }));
            }
            else
            {
                foreach (RoValraRegionServer server in await FetchRegionIndexAsync(placeId, region.Code))
                {
                    if (String.IsNullOrEmpty(server.ServerId))
                        continue;

                    stats.TryGetValue(server.ServerId, out GameServerResponse? stat);

                    servers.Add(new GameServer
                    {
                        JobId = server.ServerId,
                        Playing = stat?.Playing,
                        MaxPlayers = stat?.MaxPlayers,
                        Fps = stat?.Fps,
                        Ping = stat?.Ping,
                        PlayerTokens = stat?.PlayerTokens ?? [],
                        City = server.City,
                        Region = server.Region,
                        StartedAt = server.FirstSeenUtc,
                        PlaceVersion = server.PlaceVersion,
                        IsCurrent = server.ServerId.Equals(currentJobId, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }

            if (excludeFull)
                servers.RemoveAll(x => x.IsFull && !x.IsCurrent);

            servers = order == Order.Ascending
                ? [.. servers.OrderBy(x => x.HasStats ? 0 : 1).ThenBy(x => x.Playing ?? 0)]
                : [.. servers.OrderBy(x => x.HasStats ? 0 : 1).ThenByDescending(x => x.Playing ?? 0)];

            return servers;
        }

        public static async Task PopulateIconsAsync(IReadOnlyCollection<GameServer> servers)
        {
            if (servers.Count == 0)
                return;

            try
            {
                List<string> tokens = [
                    .. servers
                    .SelectMany(x => x.PlayerTokens.Take(MaxFaces))
                    .Distinct(StringComparer.Ordinal)
                ];

                Dictionary<string, string> real = tokens.Count > 0
                    ? await PlayerThumbnails.FetchByTokenAsync(tokens)
                    : [];

                IReadOnlyList<string> pool = servers.Any(x => Faces(x, real).Count == 0)
                    ? await PlayerThumbnails.FetchPoolAsync()
                    : [];

                foreach (GameServer server in servers)
                {
                    server.PlayerIcons.Clear();

                    List<string> faces = Faces(server, real);

                    server.PlayerIcons.AddRange(faces.Count > 0 ? faces : Filler(server, pool));
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to resolve server thumbnails");
                App.Logger.Error(ex);
            }
        }

        private static List<string> Faces(GameServer server, Dictionary<string, string> resolved) =>
        [
            .. server.PlayerTokens
                .Take(MaxFaces)
                .Select(token => resolved.TryGetValue(token, out string? url) ? url : null)
                .Where(x => !String.IsNullOrEmpty(x))
                .Select(x => x!)
        ];

        private static IEnumerable<string> Filler(GameServer server, IReadOnlyList<string> pool)
        {
            if (pool.Count == 0)
                return [];

            int count = Math.Clamp(server.Playing ?? MaxFaces, 1, MaxFaces);
            int offset = (StringComparer.Ordinal.GetHashCode(server.JobId) & Int32.MaxValue) % pool.Count;

            return Enumerable.Range(0, count).Select(i => pool[(offset + i) % pool.Count]);
        }

        public static async Task PopulateDetailsAsync(long placeId, IReadOnlyList<GameServer> servers)
        {
            var wanted = servers
                .Take(DetailsReach)
                .Where(x => (x.StartedAt is null || x.PlaceVersion is null) && !String.IsNullOrEmpty(x.JobId))
                .ToList();

            if (placeId == 0 || wanted.Count == 0)
                return;

            var byId = new Dictionary<string, GameServer>(StringComparer.OrdinalIgnoreCase);

            foreach (GameServer server in wanted)
                byId.TryAdd(server.JobId, server);

            async Task FetchBatch(GameServer[] batch)
            {
                try
                {
                    var response = await Http.GetJson<RoValraRegionResponse>(new Uri(
                        $"https://apis.rovalra.com/v1/servers/details?place_id={placeId}&server_ids={String.Join(',', batch.Select(x => x.JobId))}"));

                    foreach (RoValraRegionServer detail in response?.Servers ?? [])
                    {
                        if (String.IsNullOrEmpty(detail.ServerId))
                            continue;

                        if (!byId.TryGetValue(detail.ServerId, out GameServer? server))
                            continue;

                        server.StartedAt ??= detail.FirstSeenUtc;

                        server.PlaceVersion ??= detail.PlaceVersion;

                        if (String.IsNullOrEmpty(server.City))
                        {
                            server.City = detail.City;
                            server.Region = detail.Region;
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to fetch details for {batch.Length} servers");
                    App.Logger.Error(ex);
                }
            }

            await Task.WhenAll(wanted.Chunk(DetailsBatchSize).Select(FetchBatch));
        }

        public static async Task<HashSet<string>> StillRunningAsync(long placeId, IEnumerable<string> jobIds)
        {
            var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var ids = jobIds
                .Where(x => !String.IsNullOrEmpty(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (placeId == 0 || ids.Count == 0)
                return running;

            foreach (string[] batch in ids.Chunk(DetailsBatchSize))
            {
                try
                {
                    var response = await Http.GetJson<RoValraRegionResponse>(new Uri(
                        $"https://apis.rovalra.com/v1/servers/details?place_id={placeId}&server_ids={String.Join(',', batch)}"));

                    foreach (RoValraRegionServer server in response?.Servers ?? [])
                    {
                        if (!String.IsNullOrEmpty(server.ServerId))
                            running.Add(server.ServerId);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to check whether {batch.Length} servers are still running");
                    App.Logger.Error(ex);
                }
            }

            return running;
        }

        public static async Task<(GameServer? Server, bool AlreadyClosest)> FindClosestAsync(long placeId, string currentJobId)
        {
            if (placeId == 0)
                return (null, false);

            try
            {
                var response = await Http.AuthGetJson<ApiPageResponse<GameServerResponse>>(
                    UrlBuilder.BuildApiUrl("games",
                        $"v2/games/{placeId}/servers/Public?cursor=&sortOrder=Desc&excludeFullGames=true&orderBy=BestLatency"));

                GameServerResponse? best = response?.Data?
                    .FirstOrDefault(x => !String.IsNullOrEmpty(x.Id) && x.Playing < x.MaxPlayers);

                if (best is null)
                    return (null, false);

                if (best.Id.Equals(currentJobId, StringComparison.OrdinalIgnoreCase))
                    return (null, true);

                return (new GameServer
                {
                    JobId = best.Id,
                    Playing = best.Playing,
                    MaxPlayers = best.MaxPlayers,
                    Fps = best.Fps,
                    Ping = best.Ping
                }, false);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to find the closest server for {placeId}");
                App.Logger.Error(ex);

                throw;
            }
        }

        public static GameServer? PickHopTarget(IEnumerable<GameServer> servers)
        {
            var open = servers.Where(x => !x.IsCurrent && !x.IsFull).ToList();

            var candidates = open.Where(x => x.HasStats && x.Playing > 0).ToList();

            if (candidates.Count == 0)
                candidates = open;

            return candidates.Count == 0 ? null : candidates[RandomNumberGenerator.GetInt32(candidates.Count)];
        }

        private static async Task<List<GameServerResponse>> FetchLiveAsync(long placeId)
        {
            var servers = new List<GameServerResponse>();
            string? cursor = null;

            try
            {
                for (int page = 0; page < MaxStatsPages; page++)
                {
                    var response = await Http.GetJson<ApiPageResponse<GameServerResponse>>(
                        UrlBuilder.BuildApiUrl("games", $"v1/games/{placeId}/servers/Public?limit={PageSize}&sortOrder=Desc&cursor={cursor}"));

                    if (response is null)
                        break;

                    servers.AddRange(response.Data);

                    cursor = response.NextPageCursor;

                    if (String.IsNullOrEmpty(cursor))
                        break;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to fetch the server list");
                App.Logger.Error(ex);
            }

            return servers;
        }

        private static async Task<List<RoValraRegionServer>> FetchRegionIndexAsync(long placeId, string region)
        {
            var index = new List<RoValraRegionServer>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int? cursor = null;

            try
            {
                for (int page = 0; page < MaxRegionPages; page++)
                {
                    string query = $"https://apis.rovalra.com/v1/servers/region?place_id={placeId}&region={region}";

                    if (cursor is not null)
                        query += $"&cursor={cursor}";

                    var response = await Http.GetJson<RoValraRegionResponse>(new Uri(query));

                    if (response?.Servers is null)
                        break;

                    foreach (RoValraRegionServer server in response.Servers)
                    {
                        if (!String.IsNullOrEmpty(server.ServerId) && seen.Add(server.ServerId))
                            index.Add(server);
                    }

                    cursor = response.NextCursor;

                    if (cursor is null)
                        break;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to fetch the {region} server index for {placeId}");
                App.Logger.Error(ex);
            }

            return index;
        }

        public static void Join(long placeId, string jobId)
        {
            App.Logger.Info($"Joining {placeId}/{jobId}");

            Process.Start(new RobloxPlayerData().ExecutablePath,
                $"roblox://experiences/start?placeId={placeId}&gameInstanceId={jobId}");
        }
    }
}