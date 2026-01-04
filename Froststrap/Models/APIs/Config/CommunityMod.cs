using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Enums;

namespace Froststrap.Models.APIs.Config
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
        public string? HexCode { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("thumbnail")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("modtype")]
        public ModType ModType { get; set; } = ModType.Mod;

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
        public bool IsMisc => ModType == ModType.Misc;

        [JsonIgnore]
        public bool IsMod => ModType == ModType.Mod;

        [JsonIgnore]
        public bool IsCustomTheme => ModType == ModType.CustomTheme;

        [JsonIgnore]
        public string ModTypeDisplay => ModType switch
        {
            ModType.Misc => "Misc Mod",
            ModType.Mod => "Color Mod",
            ModType.CustomTheme => "Custom Theme",
            _ => "Unknown"
        };
    }
}