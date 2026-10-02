// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class UserPresence
    {
        public const int OnlineType = 1;
        public const int InGameType = 2;
        public const int InStudioType = 3;

        [JsonPropertyName("userPresenceType")]
        public int UserPresenceType { get; set; }

        [JsonPropertyName("lastLocation")]
        public string LastLocation { get; set; } = string.Empty;

        [JsonPropertyName("placeId")]
        public long? PlaceId { get; set; }

        [JsonPropertyName("rootPlaceId")]
        public long? RootPlaceId { get; set; }

        [JsonPropertyName("gameId")]
        public string? GameId { get; set; }

        [JsonPropertyName("universeId")]
        public long? UniverseId { get; set; }

        [JsonPropertyName("userId")]
        public long UserId { get; set; }

        public string StatusColor => GetStatusColor(UserPresenceType);

        public string ToolTipText => UserPresenceType switch
        {
            OnlineType => "Online",
            InGameType => $"Playing: {LastLocation}",
            InStudioType => "In Studio",
            _ => "Offline"
        };

        private static string GetStatusColor(int type) => type switch
        {
            OnlineType => "#00A2FF",
            InGameType => "#02B75A",
            InStudioType => "#F68802",
            _ => "#808080"
        };
    }
}