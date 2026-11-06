namespace Froststrap.Models
{
    internal sealed class VersionCacheEntry
    {
        public string Version { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}