using Froststrap.RobloxInterfaces;

namespace Froststrap.Utility
{
    internal static class Thumbnails
    {
        /// <remarks>
        /// Returned array may contain null values
        /// </remarks>
        public static async Task<string?[]> GetThumbnailUrlsAsync(List<ThumbnailRequest> requests, CancellationToken token)
        {
            const string LOG_IDENT = "Thumbnails::GetThumbnailUrlsAsync";
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
                    item.Data.RequestId = item.OriginalIndex.ToString();

                var currentPayloadData = remainingRequests.Select(x => x.Data).ToList();
                var payload = new StringContent(JsonSerializer.Serialize(currentPayloadData));

                var json = await App.HttpClient.PostFromJsonWithRetriesAsync<ThumbnailBatchResponse>(
                    $"https://thumbnails.{Deployment.RobloxDomain}/v1/batch",
                    payload,
                    3,
                    token
                );

                if (json == null)
                    throw new InvalidHTTPResponseException("Deserialised ThumbnailBatchResponse is null");

                var completedIndices = new List<int>();

                foreach (var item in json.Data)
                {
                    int originalIndex = int.Parse(item.RequestId!);

                    if (item.State == "Completed")
                    {
                        urls[originalIndex] = item.ImageUrl;
                        completedIndices.Add(originalIndex);
                    }
                    else if (item.State == "Error")
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"{item.TargetId} got error code {item.ErrorCode} ({item.ErrorMessage})");
                        completedIndices.Add(originalIndex);
                    }
                    else if (item.State != "Pending")
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"{item.TargetId} got unexpected state \"{item.State}\"");
                        completedIndices.Add(originalIndex);
                    }
                }

                remainingRequests.RemoveAll(x => completedIndices.Contains(x.OriginalIndex));

                if (remainingRequests.Count > 0)
                {
                    if (i == RETRIES)
                        App.Logger.WriteLine(LOG_IDENT, $"Ran out of retries with {remainingRequests.Count} items still pending.");
                    else
                        await Task.Delay(RETRY_TIME_INCREMENT * i, token);
                }
            }

            return urls;
        }

        public static async Task<string?> GetThumbnailUrlAsync(ThumbnailRequest request, CancellationToken token)
        {
            var results = await GetThumbnailUrlsAsync(new List<ThumbnailRequest> { request }, token);
            return results.FirstOrDefault();
        }
    }
}