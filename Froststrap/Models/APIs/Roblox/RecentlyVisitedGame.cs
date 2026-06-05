namespace Froststrap.Models.APIs.Roblox
{
    public class RecentlyVisitedGame
    {
        [JsonPropertyName("universeId")]
        public long UniverseId { get; set; }

        [JsonPropertyName("rootPlaceId")]
        public long RootPlaceId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("playerCount")]
        public int PlayerCount { get; set; }
    }
}