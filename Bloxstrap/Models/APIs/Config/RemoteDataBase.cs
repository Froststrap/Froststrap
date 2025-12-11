using Wpf.Ui.Controls;

namespace Bloxstrap.Models.APIs.Config
{
    public class RemoteDataBase
    {
        [JsonPropertyName("alertEnabled")]
        public bool AlertEnabled { get; set; } = false!;

        [JsonPropertyName("alertContent")]
        public string AlertContent { get; set; } = null!;

        [JsonPropertyName("alertSeverity")]
        public InfoBarSeverity AlertSeverity { get; set; } = InfoBarSeverity.Warning;

        [JsonPropertyName("packageMaps")]
        public PackageMaps PackageMaps { get; set; } = new();

        [JsonPropertyName("allowedFastFlags")]
        public string AllowedFastFlags { get; set; } = null!;

        [JsonPropertyName("settingsPage")]
        public SettingsPageConfig SettingsPage { get; set; } = new();

        [JsonPropertyName("projectDownloadLink")]
        public string ProjectDownloadLink { get; set; } = "https://github.com/RealMeddsam/Froststrap/releases";

        [JsonPropertyName("mappings")]
        public Dictionary<string, string[]> Mappings { get; set; } = new Dictionary<string, string[]>();
    }
}