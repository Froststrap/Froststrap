using System.Security.Cryptography;

namespace Froststrap.RobloxInterfaces
{
    internal class PlayerThumbnails
    {
        private const string Size = "48x48";

        private const int BatchSize = 100;

        private const int PoolAttempts = 90;

        private const int HighestUserId = 200_000_000;

        private static readonly Uri BatchUrl = UrlBuilder.BuildApiUrl("thumbnails", "v1/batch");

        private static readonly Lazy<Task<IReadOnlyList<string>>> Pool = new(
            FetchPoolCoreAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);

        public static async Task<Dictionary<string, string>> FetchByTokenAsync(IReadOnlyList<string> tokens)
        {
            var icons = new Dictionary<string, string>(StringComparer.Ordinal);

            ThumbnailSize size = Size.ToThumbnailSize();

            foreach (string[] chunk in tokens.Chunk(BatchSize))
            {
                var requests = new List<ThumbnailRequest>(chunk.Length);
                var byRequestId = new Dictionary<string, string>(chunk.Length, StringComparer.Ordinal);

                for (int i = 0; i < chunk.Length; i++)
                {
                    string requestId = $"{i}:{chunk[i]}:AvatarHeadShot:{Size}:png:regular";

                    requests.Add(new ThumbnailRequest { RequestId = requestId, Token = chunk[i], Size = size });

                    byRequestId[requestId] = chunk[i];
                }

                try
                {
                    var message = new HttpRequestMessage(HttpMethod.Post, BatchUrl)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(requests), Encoding.UTF8, "application/json")
                    };

                    var response = await Http.SendJson<ApiArrayResponse<ThumbnailResponse>>(message);

                    foreach (ThumbnailResponse thumbnail in response.Data)
                    {
                        if (thumbnail.State != "Completed" || String.IsNullOrEmpty(thumbnail.ImageUrl))
                            continue;

                        if (byRequestId.TryGetValue(thumbnail.RequestId ?? String.Empty, out string? token))
                            icons[token] = thumbnail.ImageUrl;
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Warn("Failed to resolve player thumbnails");
                    App.Logger.Error(ex);
                }
            }

            return icons;
        }

        public static Task<IReadOnlyList<string>> FetchPoolAsync() => Pool.Value;

        private static async Task<IReadOnlyList<string>> FetchPoolCoreAsync()
        {
            try
            {
                var ids = new HashSet<long>();

                while (ids.Count < PoolAttempts)
                    ids.Add(RandomNumberGenerator.GetInt32(1, HighestUserId));

                var icons = new List<string>();

                foreach (long[] chunk in ids.Chunk(BatchSize))
                {
                    var response = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(
                        UrlBuilder.BuildApiUrl("thumbnails", $"v1/users/avatar-headshot?userIds={String.Join(',', chunk)}&size={Size}&format=Png&isCircular=false"));

                    icons.AddRange(response.Data
                        .Where(x => x.State == "Completed" && !String.IsNullOrEmpty(x.ImageUrl))
                        .Select(x => x.ImageUrl!));
                }

                List<string> pool = [.. icons.Distinct(StringComparer.Ordinal)];

                App.Logger.Info($"Pooled {pool.Count} faces");

                return pool;
            }
            catch (Exception ex)
            {
                App.Logger.Warn("Failed to pool filler thumbnails");
                App.Logger.Error(ex);

                return [];
            }
        }
    }
}