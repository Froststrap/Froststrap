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

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("thumbnail")]
        public string? ThumbnailUrl { get; set; }

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
    }
}
