namespace Froststrap.Models.APIs.RoValra
{
    public class DatacenterLocation
    {
        [JsonPropertyName("city")]
        public string City { get; set; } = "";

        [JsonPropertyName("country")]
        public string Country { get; set; } = "";
    }
}
