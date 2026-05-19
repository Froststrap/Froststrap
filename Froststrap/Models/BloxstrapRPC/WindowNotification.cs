namespace Froststrap.Models.BloxstrapRPC
{
    public class WindowNotification
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("duration")]
        public int? Duration { get; set; }
    }
}