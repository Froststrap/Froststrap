// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Models.RobloxApi;
using Froststrap.RobloxInterfaces;

namespace Froststrap.Models.Entities
{
    internal class UserDetails
    {
        private static List<UserDetails> Cache { get; set; } = [];

        public GetUserResponse Data { get; set; } = null!;

        public ThumbnailResponse Thumbnail { get; set; } = null!;

        public static async Task<UserDetails> Fetch(long id)
        {
            Uri userUrl = UrlBuilder.BuildApiUrl("users", "v1/users/" + id);
            Uri thumbnailsUrl = UrlBuilder.BuildApiUrl("thumbnails", $"v1/users/avatar-headshot?userIds={id}&size=180x180&format=Png&isCircular=false");

            var cached = Cache.FirstOrDefault(x => x.Data?.Id == id);
            if (cached != null)
                return cached;

            var userResponse = await Http.GetJson<GetUserResponse>(userUrl);
            _ = userResponse ?? throw new InvalidHTTPResponseException("Roblox API for User Details returned invalid data");

            var thumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(thumbnailsUrl);
            if (thumbnailResponse is null || !thumbnailResponse.Data.Any())
                throw new InvalidHTTPResponseException("Roblox API for Thumbnails returned invalid data");

            var details = new UserDetails
            {
                Data = userResponse,
                Thumbnail = thumbnailResponse.Data.First()
            };

            Cache.Add(details);
            return details;
        }

        public static async Task<Dictionary<long, UserDetails>> FetchBatch(List<long> userIds)
        {
            var users = new Dictionary<long, UserDetails>();
            var missing = new List<long>();

            foreach (long userId in userIds.Distinct())
            {
                var cached = Cache.FirstOrDefault(x => x.Data?.Id == userId);

                if (cached is not null)
                    users[userId] = cached;
                else
                    missing.Add(userId);
            }

            if (!missing.Any())
                return users;

            var payload = new UserDetailsBatchRequest { UserIds = missing };

            var usersResponse = await Http.SendJson<ApiArrayResponse<GetUserResponse>>(new HttpRequestMessage
            {
                RequestUri = new Uri("https://users.roblox.com/v1/users"),
                Method = HttpMethod.Post,
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            });

            if (usersResponse is null)
                throw new InvalidHTTPResponseException("Roblox API for User Details returned invalid data");

            var thumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(
                new Uri($"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={String.Join(',', missing)}&size=180x180&format=Png&isCircular=false"));

            if (thumbnailResponse is null)
                throw new InvalidHTTPResponseException("Roblox API for Thumbnails returned invalid data");

            foreach (var user in usersResponse.Data)
            {
                var thumbnail = thumbnailResponse.Data.FirstOrDefault(x => x.TargetId == user.Id);

                if (thumbnail is null)
                    continue;

                var details = new UserDetails { Data = user, Thumbnail = thumbnail };

                users[user.Id] = details;
                Cache.Add(details);
            }

            return users;
        }

    }
}