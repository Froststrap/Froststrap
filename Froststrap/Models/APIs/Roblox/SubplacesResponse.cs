namespace Froststrap.Models.APIs.Roblox
{
    public class SubplacesResponse
    {
        [JsonPropertyName("previousPageCursor")]
        public string? PreviousPageCursor { get; set; }

        [JsonPropertyName("nextPageCursor")]
        public string? NextPageCursor { get; set; }

        [JsonPropertyName("data")]
        public List<SubplaceData> Data { get; set; } = new();
    }
}
