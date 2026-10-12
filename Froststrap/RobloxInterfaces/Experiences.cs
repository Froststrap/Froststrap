// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Enums;
using Froststrap.Models.Entities;

namespace Froststrap.RobloxInterfaces
{
    internal class Experiences
    {
        private const int BatchSize = 50;
        private const int FavoritesLimit = 50;
        private const int ContinueLimit = 24;
        private const int SuggestionIconWidth = 88;

        private static readonly string SearchSession = Guid.NewGuid().ToString();

        internal sealed record GameSearchPage(List<GameTile> Tiles, string? NextCursor);

        private static async Task<(OmniSearchResponse? Response, string? NextCursor)> SearchRawAsync(
            string query, string? cursor, string pageType)
        {
            string path = $"search-api/omni-search?searchQuery={Uri.EscapeDataString(query)}"
                        + $"&pageType={pageType}&sessionId={SearchSession}";

            if (!String.IsNullOrEmpty(cursor))
                path += $"&pageToken={Uri.EscapeDataString(cursor)}";

            using var doc = await Http.GetJson<JsonDocument>(UrlBuilder.BuildApisUrl(path));

            if (doc is null)
                return (null, null);

            var response = doc.RootElement.Deserialize<OmniSearchResponse>();

            string? next = doc.RootElement.TryGetProperty("nextPageToken", out var token)
                && token.ValueKind == JsonValueKind.String
                ? token.GetString()
                : null;

            return (response, next);
        }

        public static async Task<List<GameTile>> SearchAsync(string query) =>
            (await SearchPageAsync(query)).Tiles;

        public static async Task<GameSearchPage> SearchPageAsync(string query, string? cursor = null, int limit = 0)
        {
            if (String.IsNullOrWhiteSpace(query))
                return new GameSearchPage([], null);

            var (response, next) = await SearchRawAsync(query, cursor, "all");

            var tiles = (response?.SearchResults ?? [])
                .Where(x => x.ContentGroupType == "Game")
                .SelectMany(x => x.Contents ?? [])
                .Where(x => !x.IsSponsored && x.UniverseId > 0 && x.RootPlaceId > 0)
                .GroupBy(x => x.UniverseId)
                .Select(x => x.First())
                .Select(x => new GameTile
                {
                    UniverseId = x.UniverseId,
                    PlaceId    = x.RootPlaceId,
                    Name       = x.Name,
                    Playing    = x.PlayerCount
                })
                .ToList();

            if (limit > 0 && tiles.Count > limit)
                tiles = [.. tiles.Take(limit)];

            await PopulateIconsAsync(tiles);
            await PopulateDetailsAsync(tiles);

            return new GameSearchPage(tiles, next);
        }

        public static async Task<List<OmniSearchContent>> SuggestAsync(string query, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(query) || token.IsCancellationRequested)
                return [];

            var (response, _) = await SearchRawAsync(query, null, "Game");

            if (response is null || token.IsCancellationRequested)
                return [];

            var results = (response.SearchResults ?? [])
                .SelectMany(x => x.Contents ?? [])
                .Where(x => !x.IsSponsored && x.UniverseId > 0 && x.RootPlaceId > 0)
                .GroupBy(x => x.UniverseId)
                .Select(x => x.First())
                .Take(5)
                .ToList();

            if (results.Count == 0 || token.IsCancellationRequested)
                return [];

            var requests = results
                .Select(x => new ThumbnailRequest
                {
                    Type     = ThumbnailType.GameIcon,
                    TargetId = (ulong)x.UniverseId,
                    Size     = ThumbnailSize.Large
                })
                .ToList();

            var urls = await Thumbnails.GetThumbnailUrlsAsync(requests, token);

            if (token.IsCancellationRequested)
                return [];

            await Task.WhenAll(results.Select(async (result, index) =>
            {
                string? url = urls.ElementAtOrDefault(index);

                if (String.IsNullOrEmpty(url))
                    return;

                try
                {
                    result.ThumbnailUrl    = url;
                    result.ThumbnailBitmap = await ImageLoader.LoadAsync(url, SuggestionIconWidth);
                }
                catch (Exception ex)
                {
                    App.Logger.Warn("Failed to load a search suggestion icon");
                    App.Logger.Error(ex);
                }
            }));

            return results;
        }

        public static async Task<List<GameTile>> ContinueAsync()
        {
            const string LOG_IDENT = "Experiences::ContinueAsync";

            string? cookie = App.Cookies.GetAuthCookie();

            if (String.IsNullOrEmpty(cookie))
                return [];

            try
            {
                Uri url = UrlBuilder.BuildApisUrl($"search-landing-page-api/v1?sessionId={SearchSession}");

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");

                var response = await Http.SendJson<SearchLandingResponse>(request);

                SearchLandingSort? recentSort = response?.Sorts?.FirstOrDefault(x => x.SortId == "RecentlyVisited");

                if (recentSort?.Games is null || recentSort.Games.Count == 0)
                    return [];

                var tiles = recentSort.Games
                    .Where(x => x.UniverseId > 0 && x.RootPlaceId > 0)
                    .Take(ContinueLimit)
                    .Select(x => new GameTile
                    {
                        UniverseId = x.UniverseId,
                        PlaceId    = x.RootPlaceId,
                        Name       = x.Name ?? Strings.Menu_QuickPlay_UnknownGame,
                        Playing    = x.PlayerCount
                    })
                    .ToList();

                await PopulateIconsAsync(tiles);
                await PopulateDetailsAsync(tiles);

                return tiles;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"{LOG_IDENT}: Failed to load the Continue list");
                App.Logger.Error(ex);

                return [];
            }
        }

        public static async Task<List<GameTile>> FavoritesAsync(long userId)
        {
            var response = await Http.GetJson<ApiPageResponse<FavoriteGameData>>(UrlBuilder.BuildApiUrl("games",
                $"v2/users/{userId}/favorite/games?limit={FavoritesLimit}&sortOrder=Desc"));

            var tiles = response.Data
                .Where(x => x.RootPlace is not null && x.RootPlace.Id != 0)
                .Select(x => new GameTile
                {
                    UniverseId = x.Id,
                    PlaceId    = x.RootPlace!.Id,
                    Name       = x.Name ?? Strings.Menu_QuickPlay_UnknownGame
                })
                .ToList();

            await Task.WhenAll(PopulatePlayingAsync(tiles), PopulateIconsAsync(tiles));

            return tiles;
        }

        private static async Task PopulatePlayingAsync(List<GameTile> tiles)
        {
            try
            {
                foreach (GameTile[] chunk in tiles.Chunk(BatchSize))
                {
                    Uri url = UrlBuilder.BuildApiUrl("games",
                        $"v1/games?universeIds={String.Join(',', chunk.Select(x => x.UniverseId))}");

                    var response = App.Cookies.Loaded
                        ? await Http.AuthGetJson<ApiArrayResponse<GameDetailResponse>>(url)
                        : await Http.GetJson<ApiArrayResponse<GameDetailResponse>>(url);

                    if (response?.Data is null)
                        continue;

                    foreach (GameDetailResponse detail in response.Data)
                    {
                        GameTile? tile = chunk.FirstOrDefault(x => x.UniverseId == detail.Id);
                        tile?.Playing = detail.Playing;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to fetch player counts");
                App.Logger.Error(ex);
            }
        }

        private static async Task PopulateIconsAsync(List<GameTile> tiles)
        {
            try
            {
                foreach (GameTile[] chunk in tiles.Chunk(BatchSize))
                {
                    var response = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(UrlBuilder.BuildApiUrl("thumbnails",
                        $"v1/games/icons?universeIds={String.Join(',', chunk.Select(x => x.UniverseId))}&returnPolicy=PlaceHolder&size=256x256&format=Png&isCircular=false"));

                    if (response?.Data is null)
                        continue;

                    foreach (ThumbnailResponse thumbnail in response.Data)
                    {
                        GameTile? tile = chunk.FirstOrDefault(x => x.UniverseId == thumbnail.TargetId);
                        tile?.IconUrl = thumbnail.ImageUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to fetch experience icons");
                App.Logger.Error(ex);
            }
        }

        private static async Task PopulateDetailsAsync(List<GameTile> tiles)
        {
            if (tiles.Count == 0)
                return;

            var ids = tiles
                .Select(t => t.UniverseId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
                return;

            try
            {
                await UniverseDetails.FetchBulk(String.Join(',', ids));
            }
            catch (Exception ex)
            {
                App.Logger.Warn("Failed to enrich game tiles");
                App.Logger.Error(ex);
                return;
            }

            foreach (GameTile tile in tiles)
            {
                UniverseDetails? details = UniverseDetails.LoadFromCache(tile.UniverseId);

                if (details?.Data is null)
                    continue;

                tile.Creator        = details.Data.Creator?.Name ?? String.Empty;
                tile.IsVerified     = details.Data.Creator?.HasVerifiedBadge ?? false;
                tile.FavoritedCount = details.Data.FavoritedCount;
            }

            await PopulateVotesAsync(tiles);
        }

        private static async Task PopulateVotesAsync(List<GameTile> tiles)
        {
            var ids = tiles
                .Select(t => t.UniverseId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
                return;

            var votes = new Dictionary<long, (long Up, long Down)>();

            try
            {
                foreach (long[] chunk in ids.Chunk(BatchSize))
                {
                    var response = await Http.GetJson<ApiArrayResponse<GameVotesResponse>>(
                        UrlBuilder.BuildApiUrl("games", $"v1/games/votes?universeIds={String.Join(',', chunk)}"));

                    if (response?.Data is null)
                        continue;

                    foreach (GameVotesResponse vote in response.Data)
                        votes[vote.Id] = (vote.UpVotes, vote.DownVotes);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Warn("Failed to fetch game votes");
                App.Logger.Error(ex);
                return;
            }

            foreach (GameTile tile in tiles)
            {
                if (!votes.TryGetValue(tile.UniverseId, out var vote))
                    continue;

                long total = vote.Up + vote.Down;

                if (total > 0)
                    tile.LikeRatio = (double)vote.Up / total * 100;
            }
        }

        public static void Join(GameTile tile)
        {
            App.Logger.Info($"Joining {tile.Name} ({tile.PlaceId})");

            GameServers.Join(tile.PlaceId);
        }
    }
}