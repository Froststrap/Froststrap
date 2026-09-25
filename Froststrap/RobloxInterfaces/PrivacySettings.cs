using System;
using System.Collections.Generic;
using System.Text;

namespace Froststrap.RobloxInterfaces
{
    internal class PrivacySettings
    {
        private const string OnlineSetting = "whoCanSeeMyOnlineStatus";

        private const string JoinSetting = "whoCanJoinMeInExperiences";

        private static readonly Uri SettingsUrl = new("https://apis.roblox.com/user-settings-api/v1/user-settings/settings-and-options");

        private static readonly Uri UpdateUrl = new("https://apis.roblox.com/user-settings-api/v1/user-settings");

        public static readonly IReadOnlyList<string> OnlineLevels = new[] { "AllUsers", "FriendsFollowingAndFollowers", "FriendsAndFollowing", "Friends", "TrustedFriends", "NoOne" };

        private static readonly IReadOnlyList<string> JoinLevels = new[] { "All", "Followers", "Following", "Friends", "TrustedFriends", "NoOne" };

        public static async Task<(string? Online, string? Join)> FetchAsync()
        {
            var response = await Http.AuthGetJson<UserSettingsResponse>(SettingsUrl);

            return (response.OnlineStatus?.CurrentValue, response.JoinStatus?.CurrentValue);
        }

        private static int Rank(string? value)
        {
            if (value is null)
                return -1;

            int rank = OnlineLevels.ToList().IndexOf(value);

            return rank >= 0 ? rank : JoinLevels.ToList().IndexOf(value);
        }

        public static IReadOnlyList<(string Setting, string Value)> PlanOnlineVisibility(string online, string? join)
        {
            int onlineRank = OnlineLevels.ToList().IndexOf(online);

            if (onlineRank < 0)
                throw new ArgumentException($"Unknown online visibility '{online}'", nameof(online));

            var plan = new List<(string, string)>();

            int joinRank = Rank(join);

            if (joinRank >= 0 && joinRank < onlineRank)
            {
                bool joinNames = JoinLevels.Contains(join!);

                plan.Add((JoinSetting, joinNames ? JoinLevels[onlineRank] : OnlineLevels[onlineRank]));
            }

            plan.Add((OnlineSetting, online));

            return plan;
        }

        public static async Task<bool> SetOnlineVisibilityAsync(string online, string? join)
        {
            const string LOG_IDENT = "PrivacySettings::SetOnlineVisibilityAsync";

            var plan = PlanOnlineVisibility(online, join);

            await App.Cookies.EnsureBrowserTrackerAsync();

            App.Logger.Info(LOG_IDENT, $"Setting online visibility to {online} with joining at {join ?? "unreported"}: {String.Join(", then ", plan.Select(x => $"{x.Setting}={x.Value}"))}");

            foreach ((string setting, string value) in plan)
                await PostAsync(setting, value);

            return plan.Count > 1;
        }

        private static async Task PostAsync(string setting, string value)
        {
            string json = JsonSerializer.Serialize(new Dictionary<string, string> { [setting] = value });

            using var first = await App.Cookies.AuthPost(UpdateUrl, new StringContent(json, Encoding.UTF8, "application/json"));

            if (first.StatusCode == HttpStatusCode.Forbidden && first.Headers.TryGetValues("x-csrf-token", out var tokens))
            {
                using var retry = await App.Cookies.AuthPost(UpdateUrl, new StringContent(json, Encoding.UTF8, "application/json"), tokens.First());

                await EnsureAcceptedAsync(retry, setting, value);
                return;
            }

            await EnsureAcceptedAsync(first, setting, value);
        }

        private static async Task EnsureAcceptedAsync(HttpResponseMessage response, string setting, string value)
        {
            if (response.IsSuccessStatusCode)
                return;

            string body = await response.Content.ReadAsStringAsync();
            string? reason = null;

            try
            {
                using var document = JsonDocument.Parse(body);

                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("errors", out JsonElement errors)
                    && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
                    && errors[0].ValueKind == JsonValueKind.Object
                    && errors[0].TryGetProperty("message", out JsonElement message)
                    && message.ValueKind == JsonValueKind.String)
                {
                    reason = message.GetString();
                }
            }
            catch (JsonException) { }

            if (String.IsNullOrWhiteSpace(reason))
                reason = String.IsNullOrWhiteSpace(body) ? null : body.Length > 200 ? body[..200] : body;

            throw new SettingRejectedException(setting, value, (int)response.StatusCode, reason);
        }
    }
}
