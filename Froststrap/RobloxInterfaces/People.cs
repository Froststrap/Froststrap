// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Froststrap.RobloxInterfaces
{
    internal static class People
    {
        private const int BatchSize = 50;
        private const int MaxSearchResults = 30;

        internal enum FriendRequestResult
        {
            Success,
            AlreadyFriend,
            AlreadyRequested,
            Self,
            Blocked,
            Unknown
        }

        public static async Task<Dictionary<long, UserDetails>> FetchDetailsAsync(
            IEnumerable<long> ids,
            CancellationToken token = default)
        {
            var result = new Dictionary<long, UserDetails>();

            var distinct = ids.Where(x => x > 0).Distinct().ToList();

            if (distinct.Count == 0)
                return result;

            foreach (long[] batch in distinct.Chunk(BatchSize))
            {
                if (token.IsCancellationRequested)
                    break;

                try
                {
                    foreach (var (id, user) in await UserDetails.FetchBatch([.. batch]))
                        result[id] = user;
                }
                catch (Exception ex)
                {
                    App.Logger.Warn($"People: failed to fetch {batch.Length} user details");
                    App.Logger.Error(ex);
                }
            }

            return result;
        }

        public static async Task<Dictionary<long, UserPresence>> FetchPresenceAsync(
            IEnumerable<long> ids,
            CancellationToken token = default)
        {
            var result = new Dictionary<long, UserPresence>();

            var distinct = ids.Where(x => x > 0).Distinct().ToList();

            if (distinct.Count == 0)
                return result;

            foreach (long[] batch in distinct.Chunk(BatchSize))
            {
                if (token.IsCancellationRequested)
                    break;

                try
                {
                    var response = await AccountRequests.PostJsonAsync<UserPresenceResponse>(
                        UrlBuilder.BuildApiUrl("presence", "v1/presence/users"),
                        new { userIds = batch });

                    foreach (UserPresence presence in response?.UserPresences ?? [])
                        result[presence.UserId] = presence;
                }
                catch (Exception ex)
                {
                    App.Logger.Warn("People: failed to fetch a batch of presence");
                    App.Logger.Error(ex);
                }
            }

            return result;
        }

        public static async Task<Dictionary<long, string>> FetchHeadshotsAsync(
            IEnumerable<long> ids,
            CancellationToken token = default)
        {
            var result = new Dictionary<long, string>();

            var distinct = ids.Where(x => x > 0).Distinct().ToList();

            if (distinct.Count == 0)
                return result;

            foreach (long[] batch in distinct.Chunk(BatchSize))
            {
                if (token.IsCancellationRequested)
                    break;

                try
                {
                    var response = await Http.GetJson<ApiArrayResponse<AvatarHeadshotResponse>>(
                        new Uri($"https://thumbnails.roblox.com/v1/users/avatar-headshot"
                              + $"?userIds={String.Join(',', batch)}"
                              + $"&size=150x150&format=Png&isCircular=false"));

                    foreach (AvatarHeadshotResponse headshot in response?.Data ?? [])
                    {
                        if (headshot.State == "Completed" && !String.IsNullOrEmpty(headshot.ImageUrl))
                            result[headshot.TargetId] = headshot.ImageUrl!;
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Warn("People: failed to fetch a batch of headshots");
                    App.Logger.Error(ex);
                }
            }

            return result;
        }

        public static async Task<Dictionary<long, FriendStatus>> FetchFriendStatusesAsync(
            long me,
            IEnumerable<long> ids,
            CancellationToken token = default)
        {
            var result = new Dictionary<long, FriendStatus>();

            if (me == 0)
                return result;

            var distinct = ids.Where(x => x > 0 && x != me).Distinct().ToList();

            if (distinct.Count == 0)
                return result;

            foreach (long[] batch in distinct.Chunk(BatchSize))
            {
                if (token.IsCancellationRequested)
                    break;

                try
                {
                    var response = await Http.AuthGetJson<FriendStatusResponse>(
                        UrlBuilder.BuildApiUrl("friends",
                            $"v1/users/{me}/friends/statuses?userIds={String.Join(',', batch)}"));

                    foreach (FriendStatusEntry entry in response?.Data ?? [])
                        result[entry.Id] = FriendStatusExtensions.Parse(entry.Status);
                }
                catch (Exception ex)
                {
                    App.Logger.Warn("People: failed to fetch a batch of friend statuses");
                    App.Logger.Error(ex);
                }
            }

            return result;
        }

        public static async Task<HashSet<long>> FetchFriendIdsAsync(
            long me,
            CancellationToken token = default)
        {
            var result = new HashSet<long>();

            if (me == 0 || token.IsCancellationRequested)
                return result;

            var response = await Http.GetJson<ApiArrayResponse<FriendResponse>>(
                UrlBuilder.BuildApiUrl("friends", $"v1/users/{me}/friends"));

            foreach (FriendResponse friend in response?.Data ?? [])
            {
                if (friend.Id > 0)
                    result.Add(friend.Id);
            }

            return result;
        }

        public static async Task<List<long>> FetchFriendIdsLimitedAsync(
            long me,
            int limit,
            CancellationToken token = default)
        {
            if (me == 0 || token.IsCancellationRequested)
                return [];

            var response = await Http.GetJson<ApiArrayResponse<FriendResponse>>(
                UrlBuilder.BuildApiUrl("friends", $"v1/users/{me}/friends"));

            return [.. (response?.Data ?? [])
                .Select(x => x.Id)
                .Where(x => x > 0)
                .Take(limit)];
        }

        public static async Task<List<OmniUserSearchContent>> SearchUsersAsync(
            string query,
            CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(query) || token.IsCancellationRequested)
                return [];

            string path = $"search-api/omni-search?verticalType=user"
                        + $"&searchQuery={Uri.EscapeDataString(query.Trim())}"
                        + $"&sessionId={Guid.NewGuid()}";

            var response = await Http.GetJson<OmniUserSearchResponse>(UrlBuilder.BuildApisUrl(path));

            if (response is null || token.IsCancellationRequested)
                return [];

            return [.. (response.SearchResults ?? [])
                .Where(g => g.ContentGroupType == "User")
                .SelectMany(g => g.Contents ?? [])
                .Where(c => c.ContentId > 0 && c.ContentType == "User")
                .GroupBy(c => c.ContentId)
                .Select(g => g.First())
                .Take(MaxSearchResults)];
        }

        public static async Task<FriendRequestResult> SendFriendRequestAsync(long userId)
        {
            if (userId <= 0)
                return FriendRequestResult.Unknown;

            try
            {
                var payload = new Dictionary<string, object>
                {
                    ["friendshipOriginSourceType"] = "Unknown"
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                using var response = await App.Cookies.AuthPost(
                    UrlBuilder.BuildApiUrl("friends", $"v1/users/{userId}/request-friendship"),
                    content);

                if (response.IsSuccessStatusCode)
                    return FriendRequestResult.Success;

                string body = await response.Content.ReadAsStringAsync();

                return ParseFriendRequestFailure(body);
            }
            catch (Exception ex)
            {
                App.Logger.Error("People: failed to send friend request");
                App.Logger.Error(ex);

                return FriendRequestResult.Unknown;
            }
        }

        public static async Task<bool> UnfriendAsync(long userId)
        {
            if (userId <= 0)
                return false;

            try
            {
                using var response = await App.Cookies.AuthPost(
                    UrlBuilder.BuildApiUrl("friends", $"v1/users/{userId}/unfriend"),
                    new StringContent(String.Empty, Encoding.UTF8, "application/json"));

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"People: failed to unfriend {userId}");
                App.Logger.Error(ex);

                return false;
            }
        }

        public static async Task<bool> AcceptFriendRequestAsync(long userId)
        {
            if (userId <= 0)
                return false;

            try
            {
                using var response = await App.Cookies.AuthPost(
                    UrlBuilder.BuildApiUrl("friends", $"v1/users/{userId}/accept-friend-request"),
                    new StringContent(String.Empty, Encoding.UTF8, "application/json"));

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"People: failed to accept friend request from {userId}");
                App.Logger.Error(ex);

                return false;
            }
        }

        private static FriendRequestResult ParseFriendRequestFailure(string body)
        {
            if (String.IsNullOrWhiteSpace(body))
                return FriendRequestResult.Unknown;

            try
            {
                using var doc = JsonDocument.Parse(body);

                if (!doc.RootElement.TryGetProperty("errors", out JsonElement errors)
                    || errors.ValueKind != JsonValueKind.Array
                    || errors.GetArrayLength() == 0)
                    return FriendRequestResult.Unknown;

                JsonElement first = errors[0];

                string message = first.TryGetProperty("message", out JsonElement msg)
                    ? msg.GetString() ?? String.Empty
                    : String.Empty;

                string facing = first.TryGetProperty("userFacingMessage", out JsonElement ufm)
                    ? ufm.GetString() ?? String.Empty
                    : String.Empty;

                string combined = $"{message} {facing}";

                if (combined.Contains("already sent", StringComparison.OrdinalIgnoreCase)
                    || combined.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                    || combined.Contains("already a friend", StringComparison.OrdinalIgnoreCase))
                    return FriendRequestResult.AlreadyFriend;

                if (combined.Contains("yourself", StringComparison.OrdinalIgnoreCase))
                    return FriendRequestResult.Self;

                if (combined.Contains("blocked", StringComparison.OrdinalIgnoreCase))
                    return FriendRequestResult.Blocked;
            }
            catch (JsonException)
            {
            }

            return FriendRequestResult.Unknown;
        }
    }
}