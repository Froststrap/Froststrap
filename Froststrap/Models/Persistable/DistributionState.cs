namespace Froststrap.Models.Persistable
{
    internal class DistributionState
    {
        public string VersionGuid { get; set; } = string.Empty;

        public Dictionary<string, string> PackageHashes { get; set; } = [];

        public List<string> ModManifest { get; set; } = [];
    }
}