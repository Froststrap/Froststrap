// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Concurrent;

namespace Froststrap.Models.Entities
{
    internal class UserDetails
    {
        private static ConcurrentDictionary<long, UserDetails> Cache { get; } = new();

        private static readonly SemaphoreSlim FetchLock = new(1, 1);
        private static readonly TimeSpan FetchInterval = TimeSpan.FromMilliseconds(300);

        private const int MaxAttempts = 3;

        private static DateTime _lastFetch = DateTime.MinValue;

        public GetUserResponse Data { get; set; } = null!;

        public ThumbnailResponse? Thumbnail { get; set; }

        public static async Task<UserDetails> Fetch(long id)
        {
            if (Cache.TryGetValue(id, out UserDetails? cached))
                return cached;

            Dictionary<long, UserDetails> fetched = await FetchBatch([id]);

            if (!fetched.TryGetValue(id, out UserDetails? details))
                throw new InvalidHTTPResponseException("Roblox API for User Details returned invalid data");

            return details;
        }

        public static async Task<Dictionary<long, UserDetails>> FetchBatch(List<long> userIds)
        {
            await FetchLock.WaitAsync();

            try
            {
                var users = new Dictionary<long, UserDetails>();
                var missing = new List<long>();

                foreach (long userId in userIds.Distinct())
                {
                    if (Cache.TryGetValue(userId, out UserDetails? cached))
                        users[userId] = cached;
                    else
                        missing.Add(userId);
                }

                if (missing.Count == 0)
                    return users;

                TimeSpan wait = FetchInterval - (DateTime.UtcNow - _lastFetch);

                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait);

                _lastFetch = DateTime.UtcNow;

                List<GetUserResponse>? fetched = await FetchUsersAsync(missing);

                if (fetched is null)
                    return users;

                Dictionary<long, ThumbnailResponse> thumbnails = await FetchThumbnailsAsync([.. fetched.Select(x => x.Id)]);

                foreach (GetUserResponse user in fetched)
                {
                    var details = new UserDetails
                    {
                        Data = user,
                        Thumbnail = thumbnails.GetValueOrDefault(user.Id)
                    };

                    users[user.Id] = details;
                    Cache.TryAdd(user.Id, details);
                }

                return users;
            }
            finally
            {
                FetchLock.Release();
            }
        }

        private static async Task<List<GetUserResponse>?> FetchUsersAsync(List<long> userIds)
        {
            Uri url = UrlBuilder.BuildApiUrl("users", "v1/users");
            string body = JsonSerializer.Serialize(new UserDetailsBatchRequest { UserIds = userIds });

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    using HttpResponseMessage response = await SendUsersAsync(url, body);

                    if (response.IsSuccessStatusCode)
                    {
                        var parsed = JsonSerializer.Deserialize<ApiArrayResponse<GetUserResponse>>(await response.Content.ReadAsStringAsync());

                        if (parsed?.Data is not null)
                        {
                            List<GetUserResponse> users = [.. parsed.Data];

                            if (users.Count > 0)
                                return users;
                        }

                        return null;
                    }

                    if (response.StatusCode != HttpStatusCode.TooManyRequests)
                    {
                        App.Logger.Warn($"UserDetails: request for {userIds.Count} users failed with {(int)response.StatusCode}");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Warn($"UserDetails: failed to fetch {userIds.Count} user details");
                    App.Logger.Error(ex);
                    return null;
                }

                if (attempt < MaxAttempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(750 * attempt));
            }

            App.Logger.Warn($"UserDetails: gave up fetching {userIds.Count} user details after {MaxAttempts} attempts");

            return null;
        }

        private static async Task<HttpResponseMessage> SendUsersAsync(Uri url, string body)
        {
            if (await App.Cookies.EnsureLoadedAsync() && App.Cookies.IsAuthenticated)
                return await App.Cookies.AuthPost(url, new StringContent(body, Encoding.UTF8, "application/json"));

            return await App.HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }

        private static async Task<Dictionary<long, ThumbnailResponse>> FetchThumbnailsAsync(List<long> userIds)
        {
            var thumbnails = new Dictionary<long, ThumbnailResponse>();

            if (userIds.Count == 0)
                return thumbnails;

            Uri url = UrlBuilder.BuildApiUrl("thumbnails",
                $"v1/users/avatar-headshot?userIds={String.Join(',', userIds)}&size=180x180&format=Png&isCircular=false");

            try
            {
                ApiArrayResponse<ThumbnailResponse>? response;

                if (await App.Cookies.EnsureLoadedAsync() && App.Cookies.IsAuthenticated)
                    response = await Http.AuthGetJson<ApiArrayResponse<ThumbnailResponse>>(url);
                else
                    response = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(url);

                foreach (ThumbnailResponse thumbnail in response?.Data ?? [])
                {
                    if (thumbnail.State == "Completed" && !String.IsNullOrEmpty(thumbnail.ImageUrl))
                        thumbnails[thumbnail.TargetId] = thumbnail;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"UserDetails: failed to fetch thumbnails for {userIds.Count} users");
                App.Logger.Error(ex);
            }

            return thumbnails;
        }
    }
}
