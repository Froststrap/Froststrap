namespace Froststrap.Utility
{
    internal static class Http
    {
        /// <summary>
        /// Gets and deserializes a JSON API response to the specified object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <exception cref="HttpRequestException"></exception>
        /// <exception cref="JsonException"></exception>
        public static async Task<T> GetJson<T>(string url)
        {
            var request = await App.HttpClient.GetAsync(url);

            request.EnsureSuccessStatusCode();

            string json = await request.Content.ReadAsStringAsync();
            
            return JsonSerializer.Deserialize<T>(json)!;
        }

        public static async Task<T> SendJson<T>(HttpRequestMessage requestMessage)
        {
            var request = await App.HttpClient.SendAsync(requestMessage);

            request.EnsureSuccessStatusCode();

            string json = await request.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<T>(json)!;
        }
    }
}
