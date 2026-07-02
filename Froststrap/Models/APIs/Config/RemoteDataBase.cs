using FluentAvalonia.UI.Controls;

namespace Froststrap.Models.APIs.Config
{
    public class RemoteDataBase
    {
        [JsonPropertyName("alertEnabled")]
        public bool AlertEnabled { get; set; } = false!;

        [JsonPropertyName("alertContent")]
        public string AlertContent { get; set; } = null!;

        [JsonPropertyName("alertSeverity")]
        public FAInfoBarSeverity AlertSeverity { get; set; } = FAInfoBarSeverity.Informational;

        [JsonPropertyName("packageMaps")]
        public PackageMaps PackageMaps { get; set; } = new();

        [JsonPropertyName("allowedFastFlags")]
        public string AllowedFastFlags { get; set; } = null!;

        [JsonPropertyName("mappings")]
        public Dictionary<string, string[]> Mappings { get; set; } = [];

        [JsonPropertyName("dummyCookie")]
        public string Dummy { get; set; } = string.Empty;

        [JsonPropertyName("communityMods")]
        public List<CommunityMod> CommunityMods { get; set; } = [];
    }
}