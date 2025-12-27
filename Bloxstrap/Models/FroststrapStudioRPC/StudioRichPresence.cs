namespace Bloxstrap.Models.FroststrapStudioRPC
{
    public class StudioRichPresence
    {
        [JsonPropertyName("details")]
        public string Details { get; set; } = "";

        [JsonPropertyName("state")]
        public string State { get; set; } = "";

        [JsonPropertyName("timeStart")]
        public ulong TimestampStart { get; set; } = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}