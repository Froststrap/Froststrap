using Froststrap.AppData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Froststrap.RobloxInterfaces
{
    internal class Experiences
    {
        private const int BatchSize = 50;
        private const int FavoritesLimit = 50;
        private const int ContinueLimit = 24;

        private static readonly string SearchSession = Guid.NewGuid().ToString();

        public static async Task<List<GameTile>> SearchAsync(string query)
        {
            var response = await Http.GetJson<OmniSearchResponse>(UrlBuilder.BuildApiUrl("apis",
                $"search-api/omni-search?searchQuery={Uri.EscapeDataString(query)}&pageType=all&sessionId={SearchSession}"));

            var tiles = (response.SearchResults ?? [])
                .Where(x => x.ContentGroupType == "Game")
                .SelectMany(x => x.Contents ?? [])
                .Where(x => !x.IsSponsored && x.RootPlaceId != 0)
                .GroupBy(x => x.UniverseId)
                .Select(x => x.First())
                .Select(x => new GameTile
                {
                    UniverseId = x.UniverseId,
                    PlaceId = x.RootPlaceId,
                    Name = x.Name,
                    Playing = x.PlayerCount
                })
                .ToList();

            await PopulateIconsAsync(tiles);

            return tiles;
        }

        public static async Task<List<GameTile>> ContinueAsync()
        {
            const string LOG_IDENT = "Experiences::ContinueAsync";

            string? cookie = App.Cookies.GetAuthCookie();

            if (String.IsNullOrEmpty(cookie))
                return [];

            try
            {
                Uri url = UrlBuilder.BuildApiUrl("apis", $"search-landing-page-api/v1?sessionId={SearchSession}");

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
                        PlaceId = x.RootPlaceId,
                        Name = x.Name ?? Strings.Menu_QuickPlay_UnknownGame,
                        Playing = x.PlayerCount
                    })
                    .ToList();

                await PopulateIconsAsync(tiles);

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
                    PlaceId = x.RootPlace!.Id,
                    Name = x.Name ?? Strings.Menu_QuickPlay_UnknownGame
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

        public static void Join(GameTile tile)
        {
            App.Logger.Info($"Joining {tile.Name} ({tile.PlaceId})");

            GameServers.Launch($"placeId={tile.PlaceId}");
        }
    }
}