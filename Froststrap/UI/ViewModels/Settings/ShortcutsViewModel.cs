using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace Froststrap.UI.ViewModels.Settings
{
    public partial class ShortcutsViewModel : ObservableObject
    {
        public ShortcutTask DesktopIconTask { get; } = new("Desktop", Paths.Desktop, $"{App.ProjectName}.lnk");
        public ShortcutTask StartMenuIconTask { get; } = new("StartMenu", Paths.WindowsStartMenu, $"{App.ProjectName}.lnk");
        public ShortcutTask PlayerIconTask { get; } = new("RobloxPlayer", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRoblox}.lnk", "-player");
        public ShortcutTask StudioIconTask { get; } = new("RobloxStudio", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRobloxStudio}.lnk", "-studio");
        public ShortcutTask SettingsIconTask { get; } = new("Settings", Paths.Desktop, $"{Strings.Menu_Title}.lnk", "-settings");
        public ExtractIconsTask ExtractIconsTask { get; } = new();

        [ObservableProperty] private string _searchQuery = "";
        [ObservableProperty] private bool _isGameSearchLoading;
        [ObservableProperty] private string _placeId = "";
        [ObservableProperty] private string _jobId = "";
        [ObservableProperty] private string _accessCode = "";

        [ObservableProperty] private string _previewName = "No Game Selected";
        [ObservableProperty] private string _previewId = "ID: 0";
        [ObservableProperty] private string? _previewIconUrl;
        [ObservableProperty] private string _shortcutStatus = "Ready";

        [ObservableProperty] private OmniSearchContent? _selectedSearchResult;
        public ObservableCollection<OmniSearchContent> SearchResults { get; } = new();

        private CancellationTokenSource? _searchDebounceCts;
        private bool _isProcessingSelection = false;

        [RelayCommand]
        public async Task CreateGameShortcut()
        {
            if (string.IsNullOrEmpty(PlaceId) || PreviewName == "No Game Selected")
            {
                ShortcutStatus = "Select a game first.";
                return;
            }

            try
            {
                ShortcutStatus = "Processing...";

                string argData = PlaceId;
                if (!string.IsNullOrEmpty(JobId)) argData += $";{JobId}";
                if (!string.IsNullOrEmpty(AccessCode)) argData += $";{AccessCode}";

                string safeName = SanitizeFileName(PreviewName);
                string lnkPath = Path.Combine(Paths.Desktop, $"{safeName}.lnk");

                string shortcutsIconDir = Path.Combine(Paths.Cache, "Game Shortcuts");
                Directory.CreateDirectory(shortcutsIconDir);

                string? finalIconPath = null;

                if (!string.IsNullOrEmpty(PreviewIconUrl))
                {
                    ShortcutStatus = "Downloading icon...";
                    var imageBytes = await App.HttpClient.GetByteArrayAsync(PreviewIconUrl);
                    string hash = ComputeHash(imageBytes);
                    string icoPath = Path.Combine(shortcutsIconDir, $"{hash}.ico");

                    if (!File.Exists(icoPath))
                    {
                        ShortcutStatus = "Converting icon...";
                        using var stream = new MemoryStream(imageBytes);
                        using var bitmap = new Bitmap(stream);
                        using var icoFile = File.Create(icoPath);
                        SaveBitmapAsIcon(bitmap, icoFile);
                    }
                    finalIconPath = icoPath;
                }

                ShortcutStatus = "Creating...";
                Shortcut.Create(Paths.Application, $"-gameshortcut \"{argData}\"", lnkPath, finalIconPath);
                ShortcutStatus = "Shortcut created!";
            }
            catch (Exception ex)
            {
                ShortcutStatus = "Error creating shortcut.";
                App.Logger.WriteLine("ShortcutsViewModel", $"Error: {ex.Message}");
            }
        }

        partial void OnSearchQueryChanged(string value)
        {
            if (_isProcessingSelection) return;

            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();

            _ = HandleQueryInputAsync(value, _searchDebounceCts.Token);
        }

        private async Task HandleQueryInputAsync(string value, CancellationToken token)
        {
            try
            {
                await Task.Delay(600, token);

                if (long.TryParse(value, out long id))
                {
                    PlaceId = id.ToString();
                    await FetchInfoForId(id, token);
                }
                else if (!string.IsNullOrWhiteSpace(value))
                {
                    await SearchGamesAsync(token);
                }
            }
            catch (OperationCanceledException) { }
        }

        partial void OnSelectedSearchResultChanged(OmniSearchContent? value)
        {
            if (value is null) return;

            _isProcessingSelection = true;

            PlaceId = value.RootPlaceId.ToString();
            SearchQuery = PlaceId;

            PreviewName = value.Name!;
            PreviewId = $"ID: {value.RootPlaceId}";
            PreviewIconUrl = value.ThumbnailUrl;

            ShortcutStatus = "Ready to create";

            _isProcessingSelection = false;
        }

        private async Task FetchInfoForId(long id, CancellationToken token)
        {
            try
            {
                ShortcutStatus = "Updating preview...";

                await UniverseDetails.FetchBulk(id.ToString());
                var details = UniverseDetails.LoadFromCache(id);

                if (details != null)
                {
                    PreviewName = details.Data.Name;
                    PreviewId = $"ID: {id}";
                    PreviewIconUrl = details.Thumbnail.ImageUrl;
                }
                else
                {
                    PreviewName = $"Game {id}";
                    PreviewId = $"ID: {id}";
                    PreviewIconUrl = null;
                }
            }
            catch (Exception)
            {
                PreviewName = $"Game {id}";
                ShortcutStatus = "Ready with manual ID.";
            }
        }

        private async Task SearchGamesAsync(CancellationToken token)
        {
            IsGameSearchLoading = true;
            try
            {
                var results = await GameSearching.GetGameSearchResultsAsync(SearchQuery);
                if (token.IsCancellationRequested || results == null) return;

                var thumbRequests = results.Select(r => new ThumbnailRequest
                {
                    Type = ThumbnailType.GameIcon,
                    TargetId = (ulong)r.UniverseId,
                    Size = "128x128"
                }).ToList();

                var urls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, token);

                Dispatcher.UIThread.Invoke(() =>
                {
                    for (int i = 0; i < results.Count; i++)
                    {
                        if (urls != null && i < urls.Length)
                            results[i].ThumbnailUrl = urls[i] ?? string.Empty;
                        SearchResults.Add(results[i]);
                    }
                });
            }
            catch (Exception) { }
            finally { IsGameSearchLoading = false; }
        }

        private static void SaveBitmapAsIcon(Bitmap bitmap, Stream output)
        {
            using var resized = new Bitmap(bitmap, new System.Drawing.Size(64, 64));
            using var stream = new MemoryStream();
            resized.Save(stream, ImageFormat.Png);
            byte[] pngBytes = stream.ToArray();

            using var writer = new BinaryWriter(output);
            writer.Write((short)0); writer.Write((short)1); writer.Write((short)1);
            writer.Write((byte)64); writer.Write((byte)64); writer.Write((byte)0);
            writer.Write((byte)0); writer.Write((short)1); writer.Write((short)32);
            writer.Write(pngBytes.Length); writer.Write(22);
            writer.Write(pngBytes);
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private static string ComputeHash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}