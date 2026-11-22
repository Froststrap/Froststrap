// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal sealed class UserSettingsResponse
    {
        [JsonPropertyName("whoCanSeeMyOnlineStatus")]
        public UserSettingValue? OnlineStatus { get; set; }

        [JsonPropertyName("whoCanJoinMeInExperiences")]
        public UserSettingValue? JoinStatus { get; set; }
    }

    internal sealed class UserSettingValue
    {
        [JsonPropertyName("currentValue")]
        public string? CurrentValue { get; set; }

        [JsonPropertyName("options")]
        public List<UserSettingOption>? Options { get; set; }

        public IReadOnlyList<string> Available => Options?
            .Select(x => x.Option?.OptionValue)
            .OfType<string>()
            .ToList() ?? new List<string>();
    }

    internal sealed class UserSettingOption
    {
        [JsonPropertyName("option")]
        public UserSettingOptionValue? Option { get; set; }
    }

    internal sealed class UserSettingOptionValue
    {
        [JsonPropertyName("optionValue")]
        public string? OptionValue { get; set; }
    }
}