using Avalonia.Media.Imaging;

namespace Froststrap.Models.APIs.Roblox
{
    public class OmniSearchContent
    {
        [JsonPropertyName("universeId")]
        public ulong UniverseId { get; set; }

        [JsonPropertyName("rootPlaceId")]
        public long RootPlaceId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("playerCount")]
        public int? PlayerCount { get; set; }

        private string? _thumbnailUrl;
        public string? ThumbnailUrl
        {
            get => _thumbnailUrl;
            set => _thumbnailUrl = value;
        }

        private Bitmap? _thumbnailBitmap;
        public Bitmap? ThumbnailBitmap
        {
            get => _thumbnailBitmap;
            set => _thumbnailBitmap = value;
        }
    }
}