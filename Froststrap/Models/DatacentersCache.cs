namespace Froststrap.Models
{
    public class DatacentersCache
    {
        [JsonPropertyName("regions")]
        public List<string> Regions { get; set; } = [];

        [JsonPropertyName("datacenterMap")]
        public Dictionary<int, string> DatacenterMap { get; set; } = [];

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }
    }
}
