namespace Froststrap.Models.Persistable
{
    internal class RobloxState
    {
        public AppState Player { get; set; } = new();

        public AppState Studio { get; set; } = new();

        public List<string> ModManifest { get; set; } = [];
    }
}