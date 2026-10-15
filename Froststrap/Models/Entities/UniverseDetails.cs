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
        private static readonly Lock CacheLock = new();
        private static readonly SemaphoreSlim FetchLock = new(1, 1);
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);

        private static List<UniverseDetails> Cache { get; set; } = [];

        public GameDetailResponse Data { get; set; } = null!;

        /// <summary>
        /// Returns data for a 128x128 icon
        /// </summary>
        public ThumbnailResponse Thumbnail { get; set; } = null!;

        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

        public static UniverseDetails? LoadFromCache(long id)
        {
            lock (CacheLock)
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

            await FetchLock.WaitAsync();

            try
            {
                DateTime cutoff = DateTime.UtcNow - CacheTtl;

                List<long> stale;

                lock (CacheLock)
                {
                    stale = [.. idList.Where(id =>
                        Cache.FirstOrDefault(x => x.Data?.Id == id) is not { } entry || entry.FetchedAt < cutoff)];
                }

                if (stale.Count == 0)
                    return;

                const int chunkSize = 50;
                var chunks = stale
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

                lock (CacheLock)
                {
                    foreach (var game in gameDetailResults)
                    {
                        ThumbnailResponse? thumb = thumbnailResults.FirstOrDefault(t => t.TargetId == game.Id);

                        var existing = Cache.FirstOrDefault(x => x.Data?.Id == game.Id);

                        if (existing is not null)
                        {
                            existing.Data = game;
                            existing.FetchedAt = DateTime.UtcNow;

                            if (thumb is not null && !String.IsNullOrEmpty(thumb.ImageUrl))
                                existing.Thumbnail = thumb;

                            continue;
                        }

                        Cache.Add(new UniverseDetails
                        {
                            Data = game,
                            Thumbnail = thumb ?? new ThumbnailResponse { TargetId = game.Id, ImageUrl = "" },
                            FetchedAt = DateTime.UtcNow
                        });
                    }
                }
            }
            finally
            {
                FetchLock.Release();
            }
        }
    }
}
