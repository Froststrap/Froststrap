namespace Froststrap.Models.APIs.RoValra
{
    public class RoValraServerLocation
    {
        [JsonPropertyName("city")]
        public string City { get; set; } = null!;

        [JsonPropertyName("country_name")]
        public string Country { get; set; } = null!;

        [JsonPropertyName("region")]
        public string Region { get; set; } = null!;
    }
}