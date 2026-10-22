using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Froststrap.RobloxInterfaces
{
    internal static class PrivateServers
    {
        private const int PageSize = 25;

        private static Uri ServerUrl(long id) => UrlBuilder.BuildApiUrl("games", $"v1/vip-servers/{id}");
        private static Uri PermissionsUrl(long id) => UrlBuilder.BuildApiUrl("games", $"v1/vip-servers/{id}/permissions");

        public static Task<ApiPageResponse<PrivateServerEntry>> ListPageAsync(long placeId, string? cursor)
        {
            string path = $"v1/games/{placeId}/private-servers?limit={PageSize}&sortOrder=Desc";

            if (!String.IsNullOrEmpty(cursor))
                path += $"&cursor={Uri.EscapeDataString(cursor)}";

            return Http.AuthGetJson<ApiPageResponse<PrivateServerEntry>>(UrlBuilder.BuildApiUrl("games", path));
        }

        public static Task<PrivateServerDetails> DetailsAsync(long id) =>
            Http.AuthGetJson<PrivateServerDetails>(ServerUrl(id));

        public static Task RenameAsync(PrivateServerDetails current, string name) =>
            UpdateAsync(current, "name", name, name, current.Active, false);

        public static Task SetActiveAsync(PrivateServerDetails current, bool active) =>
            UpdateAsync(current, "active", active.ToString(), current.Name, active, false);

        public static Task RegenerateLinkAsync(PrivateServerDetails current) =>
            UpdateAsync(current, "newJoinCode", "true", current.Name, current.Active, true);

        public static Task SetFriendsAllowedAsync(PrivateServerDetails current, bool allowed) =>
            UpdatePermissionsAsync(current, "friendsAllowed", allowed.ToString(), allowed, null, null);

        public static Task AddUserAsync(PrivateServerDetails current, PrivateServerUser user) =>
            UpdatePermissionsAsync(current, "usersToAdd",
                user.Id.ToString(CultureInfo.InvariantCulture),
                null, user.Id, null);

        public static Task RemoveUserAsync(PrivateServerDetails current, PrivateServerUser user) =>
            UpdatePermissionsAsync(current, "usersToRemove",
                user.Id.ToString(CultureInfo.InvariantCulture),
                null, null, user.Id);

        public static async Task<PrivateServerUser?> FindUserAsync(string username)
        {
            var body = new UsernamesRequest
            {
                Usernames = [username],
                ExcludeBannedUsers = true
            };

            var request = new HttpRequestMessage(HttpMethod.Post, UrlBuilder.BuildApiUrl("users", "v1/usernames/users"))
            {
                Content = new StringContent(JsonHelper.Serialize(body), Encoding.UTF8, "application/json")
            };

            var response = await Http.SendJson<ApiArrayResponse<PrivateServerUser>>(request);

            return response.Data?.FirstOrDefault();
        }

        public static void Join(long placeId, string accessCode)
        {
            App.Logger.Info($"Joining a private server of {placeId}");

            GameServers.Launch($"placeId={placeId}&accessCode={Uri.EscapeDataString(accessCode)}");
        }

        private static Task UpdateAsync(PrivateServerDetails current, string setting, string value,
            string? name, bool active, bool newJoinCode) =>
            PatchAsync(ServerUrl(current.Id), setting, value, JsonHelper.Serialize(new PrivateServerUpdateRequest
            {
                Name = name,
                Active = active,
                NewJoinCode = newJoinCode
            }));

        private static Task UpdatePermissionsAsync(PrivateServerDetails current, string setting, string value,
            bool? friendsAllowed, long? addUser, long? removeUser)
        {
            PrivateServerPermissions p = current.Permissions ?? new PrivateServerPermissions();

            return PatchAsync(PermissionsUrl(current.Id), setting, value, JsonHelper.Serialize(new PrivateServerPermissionsUpdateRequest
            {
                ClanAllowed = p.ClanAllowed,
                EnemyClanId = p.EnemyClanId,
                FriendsAllowed = friendsAllowed ?? p.FriendsAllowed,
                UsersToAdd = addUser is long a ? [a] : [],
                UsersToRemove = removeUser is long r ? [r] : []
            }));
        }

        private static Task PatchAsync(Uri url, string setting, string value, string jsonBody) =>
            AccountRequests.SendAsync(HttpMethod.Patch, url, jsonBody, setting, value);
    }

    internal sealed class UsernamesRequest
    {
        [JsonPropertyName("usernames")]
        public string[] Usernames { get; set; } = [];

        [JsonPropertyName("excludeBannedUsers")]
        public bool ExcludeBannedUsers { get; set; }
    }

    internal sealed class PrivateServerUpdateRequest
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("newJoinCode")]
        public bool NewJoinCode { get; set; }
    }

    internal sealed class PrivateServerPermissionsUpdateRequest
    {
        [JsonPropertyName("clanAllowed")]
        public bool ClanAllowed { get; set; }

        [JsonPropertyName("enemyClanId")]
        public long? EnemyClanId { get; set; }

        [JsonPropertyName("friendsAllowed")]
        public bool FriendsAllowed { get; set; }

        [JsonPropertyName("usersToAdd")]
        public long[] UsersToAdd { get; set; } = [];

        [JsonPropertyName("usersToRemove")]
        public long[] UsersToRemove { get; set; } = [];
    }
}