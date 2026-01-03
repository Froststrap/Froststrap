namespace Froststrap.Models.APIs.Roblox
{
    public class FetchResult
    {
        [JsonPropertyName("data")]
        public List<ServerInstance> Servers { get; set; } = new();

        [JsonPropertyName("nextPageCursor")]
        public string NextCursor { get; set; } = string.Empty;

        [JsonIgnore]
        public int NewlyFetchedCount { get; set; }
    }
}