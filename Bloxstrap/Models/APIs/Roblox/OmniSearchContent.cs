namespace Bloxstrap.Models.APIs.Roblox
{
    public class OmniSearchContent
    {
        [JsonPropertyName("universeId")]
        public long UniverseId { get; set; }

        [JsonPropertyName("rootPlaceId")]
        public long RootPlaceId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("playerCount")]
        public int? PlayerCount { get; set; }

        public string? ThumbnailUrl { get; set; }
    }
}
