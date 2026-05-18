namespace Froststrap.Models.BloxstrapRPC
{
    public class WindowColor
    {

        [JsonPropertyName("border")]
        public string? Border { get; set; }

        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("reset")]
        public bool? Reset { get; set; }
    }
}