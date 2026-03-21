namespace Froststrap.Models.APIs.Roblox
{
    public class UserPresence
    {
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
            1 => "Online",
            2 => $"Playing: {LastLocation}",
            3 => "In Studio",
            _ => "Offline"
        };

        private static string GetStatusColor(int type) => type switch
        {
            1 => "#00A2FF", // Online (Blue)
            2 => "#02B75A", // In Game (Green)
            3 => "#F68802", // Studio (Orange)
            _ => "#808080"  // Offline (Grey)
        };
    }
}
