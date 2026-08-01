namespace Froststrap.Models
{
    public class DatacentersCache
    {
        [JsonPropertyName("regions")]
        public Dictionary<string, List<int>> Regions { get; set; } = [];

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }
    }
}
