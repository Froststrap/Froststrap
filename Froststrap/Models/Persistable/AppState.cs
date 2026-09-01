namespace Froststrap.Models.Persistable
{
    internal class AppState
    {
        public string VersionGuid { get; set; } = string.Empty;

        public Dictionary<string, string> PackageHashes { get; set; } = [];

        public int Size { get; set; }
    }
}
