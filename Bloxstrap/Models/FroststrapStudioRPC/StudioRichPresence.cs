namespace Bloxstrap.Models.FroststrapStudioRPC
{
    public class StudioRichPresence
    {
        [JsonPropertyName("details")]
        public string Details { get; set; } = "";

        [JsonPropertyName("state")]
        public string State { get; set; } = "";

        [JsonPropertyName("testing")]
        public bool Testing { get; set; } = false;

        [JsonPropertyName("scriptType")]
        public string ScriptType { get; set; } = "developing";

        [JsonPropertyName("placeId")]
        public long PlaceId { get; set; } = 0;

        [JsonPropertyName("isPublic")]
        public bool IsPublic { get; set; } = false;

        [JsonPropertyName("devCount")]
        public int DevCount { get; set; } = 1;
    }
}