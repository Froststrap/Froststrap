using FluentAvalonia.UI.Controls;

namespace Froststrap.Models.APIs.Config
{
    internal class RemoteDataBase
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

        [JsonPropertyName("mappings")]
        public Dictionary<string, string[]> Mappings { get; set; } = [];

        [JsonPropertyName("communityMods")]
        public List<CommunityMod> CommunityMods { get; set; } = [];

    }
}