// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Extensions
{
    internal static class HttpClientEx
    {
        public static async Task<HttpResponseMessage> PostWithRetriesAsync(this HttpClient client, Uri url, HttpContent? content, int retries, CancellationToken token)
        {
            HttpResponseMessage response = null!;

            for (int i = 1; i <= retries; i++)
            {
                try
                {
                    response = await client.PostAsync(url, content, token);
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    App.Logger.Error("Unhandled exception: ", ex);

                    if (i == retries)
                        throw;
                }
            }

            return response;
        }

        public static async Task<T?> PostFromJsonWithRetriesAsync<T>(this HttpClient client, Uri url, HttpContent? content, int retries, CancellationToken token) where T : class
        {
            HttpResponseMessage response = await PostWithRetriesAsync(client, url, content, retries, token);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(token);
            return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: token);
        }
    }
}
