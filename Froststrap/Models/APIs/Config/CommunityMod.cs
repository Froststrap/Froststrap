using Avalonia.Media.Imaging;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models.APIs.Config
{
    public partial class CommunityMod : NotifyPropertyChangedViewModel
    {

        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("download")]
        public string DownloadUrl { get; set; } = null!;

        [JsonPropertyName("hexcode")]
        public string? HexCode { get; set; } = null!;

        [JsonPropertyName("gradient")]
        public List<GradientStop>? GradientStops { get; set; }

        [JsonPropertyName("angle")]
        public double? GradientAngle { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; } = null!;

        [JsonPropertyName("description")]
        public string Description { get; set; } = null!;

        [JsonPropertyName("thumbnail")]
        public string ThumbnailUrl { get; set; } = null!;

        [JsonPropertyName("modtype")]
        public ModType ModType { get; set; } = ModType.ColorMod;

        [JsonIgnore]
        private Bitmap? _thumbnailImage;
        [JsonIgnore]
        public Bitmap? ThumbnailImage
        {
            get => _thumbnailImage;
            set => SetProperty(ref _thumbnailImage, value);
        }

        [JsonIgnore]
        private bool _isLoadingThumbnail;
        [JsonIgnore]
        public bool IsLoadingThumbnail
        {
            get => _isLoadingThumbnail;
            set => SetProperty(ref _isLoadingThumbnail, value);
        }

        [JsonIgnore]
        private bool _hasThumbnailError;
        [JsonIgnore]
        public bool HasThumbnailError
        {
            get => _hasThumbnailError;
            set => SetProperty(ref _hasThumbnailError, value);
        }

        private bool _isDownloading;

        [JsonIgnore]
        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        private double _downloadProgress;
        [JsonIgnore]
        public double DownloadProgress
        {
            get => _downloadProgress;
            set => SetProperty(ref _downloadProgress, value);
        }

        private object? _downloadCommand;
        [JsonIgnore]
        public object? DownloadCommand
        {
            get => _downloadCommand;
            set => SetProperty(ref _downloadCommand, value);
        }

        [JsonIgnore]
        public bool IsCustomTheme => ModType == ModType.CustomTheme;

        [JsonIgnore]
        public bool IsColorMod => ModType == ModType.ColorMod;

        [JsonIgnore]
        public string ModTypeDisplay => ModType switch
        {
            ModType.MiscMod => "Misc Mod",
            ModType.ColorMod => "Color Mod",
            ModType.SkyBox => "SkyBox",
            ModType.Cursor => "Cursor",
            ModType.AvatarEditor => "Avatar Editor",
            ModType.CustomTheme => "Custom Theme",
            _ => "Unknown"
        };
    }
}