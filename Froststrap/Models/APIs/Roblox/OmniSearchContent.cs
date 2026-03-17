using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Froststrap.Models.APIs.Roblox
{
    public partial class OmniSearchContent : ObservableObject
    {
        [JsonPropertyName("universeId")]
        public ulong UniverseId { get; set; }

        [JsonPropertyName("rootPlaceId")]
        public long RootPlaceId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("playerCount")]
        public int? PlayerCount { get; set; }

        [ObservableProperty] private string? _thumbnailUrl;
        [ObservableProperty] private Bitmap? _thumbnailBitmap;
    }
}