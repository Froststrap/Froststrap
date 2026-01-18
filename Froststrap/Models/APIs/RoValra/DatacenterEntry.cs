namespace Froststrap.Models.APIs.RoValra
{
    public class DatacenterEntry
    {
        [JsonPropertyName("location")]
        public DatacenterLocation Location { get; set; } = new();

        [JsonPropertyName("dataCenterIds")]
        public List<int> DataCenterIds { get; set; } = new();
    }
}
