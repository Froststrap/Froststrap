namespace Froststrap.Models.APIs.Roblox
{
    internal class SubplaceData
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("universeId")]
        public long UniverseId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
    }
}
