namespace Froststrap.Models.APIs.Config
{
    public class DatacentersCache
    {
        [JsonPropertyName("regions")]
        public List<string> Regions { get; set; } = new();

        [JsonPropertyName("datacenterMap")]
        public Dictionary<int, string> DatacenterMap { get; set; } = new();

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }
    }
}
