using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media.Imaging;

namespace Bloxstrap.Models.APIs.Config
{
    public partial class CommunityMod : ObservableObject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("download")]
        public string DownloadUrl { get; set; } = null!;

        [JsonPropertyName("hexcode")]
        public string HexCode { get; set; } = null!;

        [JsonPropertyName("author")]
        public string Author { get; set; } = null!;

        [JsonPropertyName("description")]
        public string Description { get; set; } = null!;

        [JsonPropertyName("thumbnail")]
        public string ThumbnailUrl { get; set; } = null!;

        [JsonPropertyName("modtype")]
        public ModType ModType { get; set; } = ModType.ColorMod;

        [JsonIgnore]
        private BitmapImage? _thumbnailImage;
        [JsonIgnore]
        public BitmapImage? ThumbnailImage
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

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private double _downloadProgress;

        [ObservableProperty]
        private IAsyncRelayCommand? _downloadCommand;

        [ObservableProperty]
        private IRelayCommand? _showInfoCommand;

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