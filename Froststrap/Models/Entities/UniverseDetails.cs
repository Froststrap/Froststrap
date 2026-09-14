// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Entities
{
    /// <summary>
    /// Explicit loading. Load from cache before and after a fetch.
    /// </summary>
    internal class UniverseDetails
    {
        private static List<UniverseDetails> Cache { get; set; } = [];

        public GameDetailResponse Data { get; set; } = null!;

        /// <summary>
        /// Returns data for a 128x128 icon
        /// </summary>
        public ThumbnailResponse Thumbnail { get; set; } = null!;

        public static UniverseDetails? LoadFromCache(long id)
        {
            return Cache.FirstOrDefault(x => x.Data?.Id == id);
        }

        public static Task FetchSingle(long id) => FetchBulk(id.ToString(CultureInfo.InvariantCulture));

        public static async Task FetchBulk(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
                return;

            var idList = ids.Split(',')
                .Where(id => long.TryParse(id, CultureInfo.InvariantCulture, out _))
                .Select(long.Parse)
                .Distinct()
                .ToList();

            if (idList.Count == 0)
                return;

            const int chunkSize = 50;
            var chunks = idList
                .Select((id, index) => new { id, index })
                .GroupBy(x => x.index / chunkSize)
                .Select(g => string.Join(",", g.Select(x => x.id)))
                .ToList();

            var gameDetailResults = new List<GameDetailResponse>();
            var thumbnailResults = new List<ThumbnailResponse>();

            foreach (var chunk in chunks)
            {
                Uri gameDetailsUrl = UrlBuilder.BuildApiUrl("games", $"v1/games?universeIds={chunk}");
                Uri thumbnailsUrl = UrlBuilder.BuildApiUrl("thumbnails", $"v1/games/icons?universeIds={chunk}&returnPolicy=PlaceHolder&size=128x128&format=Png&isCircular=false");

                ApiArrayResponse<GameDetailResponse> gameDetailResponse;
                if (App.Cookies.Loaded)
                    gameDetailResponse = await Http.AuthGetJson<ApiArrayResponse<GameDetailResponse>>(gameDetailsUrl);
                else
                    gameDetailResponse = await Http.GetJson<ApiArrayResponse<GameDetailResponse>>(gameDetailsUrl);

                if (gameDetailResponse.Data.Any())
                    gameDetailResults.AddRange(gameDetailResponse.Data);

                var universeThumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(thumbnailsUrl);
                if (universeThumbnailResponse.Data.Any())
                    thumbnailResults.AddRange(universeThumbnailResponse.Data);
            }

            var newCacheEntries = new List<UniverseDetails>();
            foreach (var game in gameDetailResults)
            {
                var existing = Cache.FirstOrDefault(x => x.Data?.Id == game.Id);
                if (existing != null)
                    continue;

                var thumb = thumbnailResults.FirstOrDefault(t => t.TargetId == game.Id)
                            ?? new ThumbnailResponse { TargetId = game.Id, ImageUrl = "" };

                newCacheEntries.Add(new UniverseDetails
                {
                    Data = game,
                    Thumbnail = thumb
                });
            }

            if (newCacheEntries.Count > 0)
                Cache.AddRange(newCacheEntries);
        }
    }
}