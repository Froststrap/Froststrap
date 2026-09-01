namespace Froststrap.Utility
{
    internal static class Thumbnails
    {
        /// <remarks>
        /// Returned array may contain null values
        /// </remarks>
        public static async Task<string?[]> GetThumbnailUrlsAsync(List<ThumbnailRequest> requests, CancellationToken token)
        {
            const int RETRIES = 5;
            const int RETRY_TIME_INCREMENT = 500; // ms

            string?[] urls = new string?[requests.Count];

            var remainingRequests = requests
                .Select((req, index) => new { OriginalIndex = index, Data = req })
                .ToList();

            for (int i = 1; i <= RETRIES; i++)
            {
                if (remainingRequests.Count == 0)
                    break;

                foreach (var item in remainingRequests)
                    item.Data.RequestId = item.OriginalIndex.ToString(CultureInfo.InvariantCulture);

                var currentPayloadData = remainingRequests.Select(x => x.Data).ToList();
                using var payload = new StringContent(JsonSerializer.Serialize(currentPayloadData));

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

                remainingRequests.RemoveAll(x => completedIndices.Contains(x.OriginalIndex));

                if (remainingRequests.Count > 0)
                {
                    if (i == RETRIES)
                        App.Logger.Warn($"Ran out of retries with {remainingRequests.Count} items still pending.");
                    else
                        await Task.Delay(RETRY_TIME_INCREMENT * i, token);
                }
            }

            return urls;
        }

        public static async Task<string?> GetThumbnailUrlAsync(ThumbnailRequest request, CancellationToken token)
        {
            var results = await GetThumbnailUrlsAsync([request], token);
            return results.FirstOrDefault();
        }
    }
}
