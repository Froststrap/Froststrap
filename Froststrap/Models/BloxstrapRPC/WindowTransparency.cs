namespace Froststrap.Models.BloxstrapRPC
{
    public class WindowTransparency
    {
        [JsonPropertyName("transparency")]
        public float? Transparency { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }

        [JsonPropertyName("useAlpha")]
        public bool? UseAlpha { get; set; }
    }
}