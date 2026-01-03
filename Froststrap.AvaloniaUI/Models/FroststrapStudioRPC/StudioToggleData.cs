namespace Froststrap.Models.FroststrapStudioRPC
{
    public class StudioToggleData
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("workspace")]
        public string Workspace { get; set; } = "";
    }
}