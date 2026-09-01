using Avalonia.Media.Imaging;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models.APIs.Config
{
    internal partial class CommunityMod : NotifyPropertyChangedViewModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("download")]
        public string DownloadUrl { get; set; } = null!;

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

        private Bitmap? _thumbnail;
        [JsonIgnore]
        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set => SetProperty(ref _thumbnail, value);
        }

        private string GetCacheFilePath()
        {
            string cacheDir = Path.Combine(Paths.Cache, "CommunityMods");
            Directory.CreateDirectory(cacheDir);
            return Path.Combine(cacheDir, $"{Id}.png");
        }

        public async Task LoadThumbnailAsync()
        {
            if (Thumbnail != null || string.IsNullOrEmpty(ThumbnailUrl))
                return;

            string cachePath = GetCacheFilePath();

            if (File.Exists(cachePath))
            {
                try
                {
                    var bitmap = await Task.Run(() =>
                    {
                        using var fs = File.OpenRead(cachePath);
                        return Bitmap.DecodeToWidth(fs, 600);
                    });
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => Thumbnail = bitmap);
                    return;
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to load cached thumbnail: {ex.Message}");
                    try { File.Delete(cachePath); } catch { }
                }
            }

            try
            {
                using var response = await App.HttpClient.GetAsync(new Uri(ThumbnailUrl));
                if (!response.IsSuccessStatusCode) return;

                await using var stream = await response.Content.ReadAsStreamAsync();
                var bitmap = await Task.Run(() => Bitmap.DecodeToWidth(stream, 600));

                await Task.Run(() =>
                {
                    using var fs = File.Create(cachePath);
                    bitmap.Save(fs);
                });

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => Thumbnail = bitmap);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception: ", ex.Message);
            }
        }
    }
}
