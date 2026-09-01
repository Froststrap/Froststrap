namespace Froststrap.Models.APIs.Roblox
{
    // lmao its just one property
    internal class UniverseIdResponse
    {
        [JsonPropertyName("universeId")]
        public long UniverseId { get; set; }
    }
}
