namespace Froststrap.Models.APIs.RoValra
{
    public class RoValraTimeResponse
    {
        [JsonPropertyName("servers")]
        public List<RoValrasServer>? Servers { get; set; } = null!;

        [JsonPropertyName("status")]
        public string Status = null!;
    }
}