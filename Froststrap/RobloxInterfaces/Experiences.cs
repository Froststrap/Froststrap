using Froststrap.AppData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Froststrap.RobloxInterfaces
{
    internal class Experiences
    {
        private const int BatchSize = 50;
        private const int FavoritesLimit = 50;

        private static readonly string SearchSession = Guid.NewGuid().ToString();

        public static async Task<List<GameTile>> SearchAsync(string query)
        {
            var response = await Http.GetJson<OmniSearchResponse>(UrlBuilder.BuildApiUrl("apis",
                $"search-api/omni-search?searchQuery={Uri.EscapeDataString(query)}&pageType=all&sessionId={SearchSession}"));

            var tiles = response.SearchResults
                .Where(x => x.ContentGroupType == "Game")
                .SelectMany(x => x.Contents)
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

        public static async Task<List<GameTile>> FavoritesAsync(long userId)
        {
            var response = await Http.GetJson<ApiPageResponse<FavoriteGameResponse>>(UrlBuilder.BuildApiUrl("games",
                $"v2/users/{userId}/favorite/games?limit={FavoritesLimit}&sortOrder=Desc"));

            var tiles = response.Data
                .Where(x => x.RootPlace is not null && x.RootPlace.Id != 0)
                .Select(x => new GameTile
                {
                    UniverseId = x.Id,
                    PlaceId = x.RootPlace!.Id,
                    Name = x.Name
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

                        if (tile is not null)
                            tile.Playing = detail.Playing;
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

                        if (tile is not null)
                            tile.IconUrl = thumbnail.ImageUrl;
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

            Process.Start(new RobloxPlayerData().ExecutablePath,
                $"roblox://experiences/start?placeId={tile.PlaceId}");
        }
    }
}