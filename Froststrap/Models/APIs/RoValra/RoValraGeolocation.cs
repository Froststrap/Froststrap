namespace Froststrap.Models.APIs.RoValra
{
    public class RoValraGeolocation
    {
        [JsonPropertyName("location")]
        public RoValraServerLocation? Location { get; set; } = null!;
    }
}