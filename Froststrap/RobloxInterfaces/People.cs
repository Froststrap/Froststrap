// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Net.Http;
using System.Text;
using System.Text.Json;

using Froststrap.Integrations.OverlayModules;

namespace Froststrap.RobloxInterfaces
{
    internal static class People
    {
        private const int BatchSize = 50;
        private const int DetailsBatchSize = 100;
        private const int MaxSearchResults = 30;
        private const int MaxFriendPages = 60;

        private static readonly TimeSpan FriendPageDelay = TimeSpan.FromMilliseconds(120);

        private static readonly SemaphoreSlim FriendListLock = new(1, 1);
        private static readonly TimeSpan FriendListTtl = TimeSpan.FromMinutes(2);

        private static List<long>? _friendList;
        private static long _friendListOwner;
        private static DateTime _friendListAt = DateTime.MinValue;

        private static readonly Lock PresenceLock = new();
        private static readonly TimeSpan PresenceTtl = TimeSpan.FromSeconds(30);

        private static readonly Dictionary<long, (UserPresence Presence, DateTime At)> PresenceCache = [];

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

            foreach (long[] batch in distinct.Chunk(DetailsBatchSize))
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
            bool force = false,
            CancellationToken token = default)
        {
            var result = new Dictionary<long, UserPresence>();

            var distinct = ids.Where(x => x > 0).Distinct().ToList();

            if (distinct.Count == 0)
                return result;

            var missing = new List<long>();

            bool live = RealtimeMessaging.UserhubConnected;

            lock (PresenceLock)
            {
                DateTime cutoff = DateTime.UtcNow - PresenceTtl;

                foreach (long id in distinct)
                {
                    if (!force
                        && PresenceCache.TryGetValue(id, out var cached)
                        && (live || cached.At >= cutoff))
                        result[id] = cached.Presence;
                    else
                        missing.Add(id);
                }
            }

            if (missing.Count == 0)
                return result;

            foreach (long[] batch in missing.Chunk(BatchSize))
            {
                if (token.IsCancellationRequested)
                    break;

                try
                {
                    var response = await AccountRequests.PostJsonAsync<UserPresenceResponse>(
                        UrlBuilder.BuildApiUrl("presence", "v1/presence/users"),
                        new { userIds = batch });

                    foreach (UserPresence presence in response?.UserPresences ?? [])
                    {
                        result[presence.UserId] = presence;

                        lock (PresenceLock)
                            PresenceCache[presence.UserId] = (presence, DateTime.UtcNow);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Warn("People: failed to fetch a batch of presence");
                    App.Logger.Error(ex);
                }
            }

            return result;
        }

        public static void CachePresence(long userId, UserPresence presence)
        {
            if (userId <= 0)
                return;

            lock (PresenceLock)
                PresenceCache[userId] = (presence, DateTime.UtcNow);
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

        public static async Task<List<long>> FetchAllFriendIdsAsync(
            long me,
            bool force = false,
            CancellationToken token = default)
        {
            if (me == 0 || token.IsCancellationRequested)
                return [];

            await FriendListLock.WaitAsync(token);

            try
            {
                if (!force
                    && _friendListOwner == me
                    && _friendList is { Count: > 0 }
                    && DateTime.UtcNow - _friendListAt < FriendListTtl)
                    return [.. _friendList];

                var ids = new List<long>();
                var seen = new HashSet<long>();

                try
                {
                    await AppendFindFriendsAsync(me, ids, seen, token);
                }
                catch (Exception ex)
                {
                    App.Logger.Warn("People: failed to page through the friend list");
                    App.Logger.Error(ex);
                }

                if (ids.Count == 0)
                {
                    try
                    {
                        await AppendPublicFriendsAsync(me, ids, seen, token);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Warn("People: failed to fetch the friend list");
                        App.Logger.Error(ex);
                    }
                }

                try
                {
                    await AppendOnlineFriendsAsync(me, ids, seen, token);
                }
                catch (Exception ex)
                {
                    App.Logger.Warn("People: failed to fetch the online friend list");
                    App.Logger.Error(ex);
                }

                if (ids.Count > 0 && !token.IsCancellationRequested)
                {
                    _friendList = ids;
                    _friendListOwner = me;
                    _friendListAt = DateTime.UtcNow;
                }

                return ids;
            }
            finally
            {
                FriendListLock.Release();
            }
        }

        private static async Task AppendFindFriendsAsync(
            long me,
            List<long> ids,
            HashSet<long> seen,
            CancellationToken token)
        {
            var cursors = new HashSet<string>(StringComparer.Ordinal);
            string? cursor = null;

            for (int page = 0; page < MaxFriendPages; page++)
            {
                if (token.IsCancellationRequested)
                    return;

                string path = $"v1/users/{me}/friends/find?userSort=1";

                if (!String.IsNullOrEmpty(cursor))
                    path += $"&cursor={Uri.EscapeDataString(cursor)}";

                FriendFindResponse? response = await Http.AuthGetJson<FriendFindResponse>(
                    UrlBuilder.BuildApiUrl("friends", path));

                if (response?.PageItems is null)
                    return;

                foreach (FriendFindItem item in response.PageItems)
                {
                    if (item.Id > 0 && seen.Add(item.Id))
                        ids.Add(item.Id);
                }

                cursor = response.NextCursor;

                if (String.IsNullOrEmpty(cursor) || !cursors.Add(cursor))
                    return;

                if (token.IsCancellationRequested)
                    return;

                await Task.Delay(FriendPageDelay, CancellationToken.None);
            }
        }

        private static async Task AppendPublicFriendsAsync(
            long me,
            List<long> ids,
            HashSet<long> seen,
            CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            var response = await Http.GetJson<ApiArrayResponse<FriendResponse>>(
                UrlBuilder.BuildApiUrl("friends", $"v1/users/{me}/friends"));

            foreach (FriendResponse friend in response?.Data ?? [])
            {
                if (friend.Id > 0 && seen.Add(friend.Id))
                    ids.Add(friend.Id);
            }
        }

        private static async Task AppendOnlineFriendsAsync(
            long me,
            List<long> ids,
            HashSet<long> seen,
            CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            var response = await Http.AuthGetJson<ApiArrayResponse<FriendResponse>>(
                UrlBuilder.BuildApiUrl("friends", $"v1/users/{me}/friends/online"));

            foreach (FriendResponse friend in response?.Data ?? [])
            {
                if (friend.Id > 0 && seen.Add(friend.Id))
                    ids.Add(friend.Id);
            }
        }

        public static async Task<HashSet<long>> FetchFriendIdsAsync(
            long me,
            bool force = false,
            CancellationToken token = default)
        {
            return [.. await FetchAllFriendIdsAsync(me, force, token)];
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