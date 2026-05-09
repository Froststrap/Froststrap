namespace Froststrap.Models
{
    public class PlaceInfo(long id, long universeId, string name, string? thumbnailUrl)
    {
        public long Id { get; set; } = id;
        public long UniverseId { get; set; } = universeId;
        public string Name { get; set; } = name;
        public string? ThumbnailUrl { get; set; } = thumbnailUrl;
    }
}