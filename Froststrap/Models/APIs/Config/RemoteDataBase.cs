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

        [JsonPropertyName("bannedVersionHashes")]
        public List<string> BannedVersionHashes { get; set; } = [];

        [JsonPropertyName("packageMaps")]
        public PackageMaps PackageMaps { get; set; } = new();

        [JsonPropertyName("allowedFastFlags")]
        public string AllowedFastFlags { get; set; } = null!;

        [JsonPropertyName("dummyCookie")]
        public string Dummy { get; set; } = string.Empty;

        [JsonPropertyName("mappings")]
        public Dictionary<string, string[]> Mappings { get; set; } = [];

        [JsonPropertyName("communityMods")]
        public List<CommunityMod> CommunityMods { get; set; } = [];

    }
}