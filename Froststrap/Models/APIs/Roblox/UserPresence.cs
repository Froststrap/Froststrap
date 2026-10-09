using Froststrap.Enums;

namespace Froststrap.Models.APIs.Roblox
{
    internal class UserPresence
    {
        [JsonPropertyName("userPresenceType")]
        public UserPresenceType UserPresenceType { get; set; }

        [JsonPropertyName("lastLocation")]
        public string LastLocation { get; set; } = String.Empty;

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

        public bool IsInGame => UserPresenceType == UserPresenceType.InGame && UniverseId is > 0;

        public string StatusColor => GetStatusColor(UserPresenceType);

        public string ToolTipText => UserPresenceType switch
        {
            UserPresenceType.Online => "Online",
            UserPresenceType.InGame => $"Playing: {LastLocation}",
            UserPresenceType.InStudio => "In Studio",
            _ => "Offline"
        };

        private static string GetStatusColor(UserPresenceType type) => type switch
        {
            UserPresenceType.Online => "#00A2FF",
            UserPresenceType.InGame => "#02B75A",
            UserPresenceType.InStudio => "#F68802",
            _ => "#808080"
        };
    }
}