namespace Froststrap.Models.APIs.Roblox
{
    internal class UserSettingsResponse
    {
        [JsonPropertyName("whoCanSeeMyOnlineStatus")]
        public UserSettingValue? OnlineStatus { get; set; }

        [JsonPropertyName("whoCanJoinMeInExperiences")]
        public UserSettingValue? JoinStatus { get; set; }
    }

    internal class UserSettingValue
    {
        [JsonPropertyName("currentValue")]
        public string? CurrentValue { get; set; }
    }
}
