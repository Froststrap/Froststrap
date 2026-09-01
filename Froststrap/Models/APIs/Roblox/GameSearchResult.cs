namespace Froststrap.Models.APIs.Roblox
{
    internal class GameSearchResult
    {
        [JsonPropertyName("rootPlaceId")]
        public long RootPlaceId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("playerCount")]
        public int? PlayerCount { get; set; }

        public override string ToString() => string.IsNullOrWhiteSpace(Name) ? RootPlaceId.ToString(CultureInfo.InvariantCulture) : $"{Name} ({RootPlaceId})";
    }
}
