namespace Froststrap.Models
{
    public class FullSettingsExport
    {
        [JsonPropertyName("settings")]
        public Settings? Settings { get; set; }

        [JsonPropertyName("state")]
        public State? State { get; set; }

        [JsonPropertyName("playerState")]
        public DistributionState? PlayerState { get; set; }

        [JsonPropertyName("studioState")]
        public DistributionState? StudioState { get; set; }

        [JsonPropertyName("appStorage")]
        public Dictionary<string, object>? AppStorage { get; set; }

        [JsonPropertyName("fastFlags")]
        public Dictionary<string, object>? FastFlags { get; set; }

        [JsonPropertyName("globalBasicSettingsXml")]
        public string? GlobalBasicSettingsXml { get; set; }

        [JsonPropertyName("soberSettings")]
        public Dictionary<string, object>? SoberSettings { get; set; }

        [JsonPropertyName("exportDate")]
        public DateTime ExportDate { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }
}