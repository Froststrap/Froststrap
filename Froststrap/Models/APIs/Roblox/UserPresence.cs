namespace Froststrap.Models.APIs.Roblox
{
    public class UserPresence
    {
        [JsonPropertyName("userPresenceType")]
        public int UserPresenceType { get; set; }

        [JsonPropertyName("lastLocation")]
        public string? LastLocation { get; set; }

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
    }
}
