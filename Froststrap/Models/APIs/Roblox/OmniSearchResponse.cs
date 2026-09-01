namespace Froststrap.Models.APIs.Roblox
{
    internal class OmniSearchResponse
    {
        [JsonPropertyName("searchResults")]
        public List<OmniSearchGroup>? SearchResults { get; set; }
    }
}