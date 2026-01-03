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
    }
}