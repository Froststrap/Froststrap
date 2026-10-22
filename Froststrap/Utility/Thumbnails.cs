// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Concurrent;

namespace Froststrap.Utility
{
    internal static class Thumbnails
    {
        private static readonly ConcurrentDictionary<string, string> UrlCache = new();

        /// <remarks>
        /// Returned array may contain null values
        /// </remarks>
        public static async Task<string?[]> GetThumbnailUrlsAsync(List<ThumbnailRequest> requests, CancellationToken token)
        {
            const int RETRIES = 5;
            const int RETRY_TIME_INCREMENT = 500; // ms

            string?[] urls = new string?[requests.Count];

            var pending = new List<int>();

            for (int index = 0; index < requests.Count; index++)
            {
                if (UrlCache.TryGetValue(CacheKey(requests[index]), out string? cachedUrl))
                    urls[index] = cachedUrl;
                else
                    pending.Add(index);
            }

            for (int i = 1; i <= RETRIES && pending.Count > 0; i++)
            {
                foreach (int index in pending)
                    requests[index].RequestId = index.ToString(CultureInfo.InvariantCulture);

                var currentPayloadData = pending.Select(x => requests[x]).ToList();
                using var payload = new StringContent(JsonHelper.Serialize(currentPayloadData));

                Uri apiUrl = UrlBuilder.BuildApiUrl("thumbnails", "v1/batch");

                var json = await App.HttpClient.PostFromJsonWithRetriesAsync<ThumbnailBatchResponse>(apiUrl, payload, 3, token)
                           ?? throw new InvalidHTTPResponseException("Deserialised ThumbnailBatchResponse is null");

                List<int> completedIndices = [];

                foreach (var item in json.Data)
                {
                    int originalIndex = int.Parse(item.RequestId!, CultureInfo.InvariantCulture);

                    if (item.State == "Completed")
                    {
                        urls[originalIndex] = item.ImageUrl;
                        completedIndices.Add(originalIndex);
                    }
                    else if (item.State == "Error")
                    {
                        App.Logger.Error($"{item.TargetId} got error code {item.ErrorCode} ({item.ErrorMessage})");
                        completedIndices.Add(originalIndex);
                    }
                    else if (item.State != "Pending")
                    {
                        App.Logger.Error($"{item.TargetId} got unexpected state \"{item.State}\"");
                        completedIndices.Add(originalIndex);
                    }
                }

                pending.RemoveAll(completedIndices.Contains);

                if (pending.Count > 0)
                {
                    if (i == RETRIES)
                        App.Logger.Warn($"Ran out of retries with {pending.Count} items still pending.");
                    else
                        await Task.Delay(RETRY_TIME_INCREMENT * i, token);
                }
            }

            for (int index = 0; index < requests.Count; index++)
            {
                if (!String.IsNullOrEmpty(urls[index]))
                    UrlCache[CacheKey(requests[index])] = urls[index]!;
            }

            return urls;
        }

        public static async Task<string?> GetThumbnailUrlAsync(ThumbnailRequest request, CancellationToken token)
        {
            var results = await GetThumbnailUrlsAsync([request], token);
            return results.FirstOrDefault();
        }

        private static string CacheKey(ThumbnailRequest request) =>
            $"{request.TargetId}:{request.Token}:{request.Type}:{request.Size}:{request.Format}:{request.IsCircular}";
    }
}
