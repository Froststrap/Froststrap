using System.Collections.ObjectModel;
using System.Windows;
using System.Drawing;
using Avalonia.Media.Imaging;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;

namespace Froststrap.UI.ViewModels.Settings
{
    public partial class ShortcutsViewModel : ObservableObject
    {
        public ShortcutTask DesktopIconTask { get; } = new("Desktop", Paths.Desktop, $"{App.ProjectName}.lnk");
        public ShortcutTask StartMenuIconTask { get; } = new("StartMenu", Paths.WindowsStartMenu, $"{App.ProjectName}.lnk");
        public ShortcutTask PlayerIconTask { get; } = new("RobloxPlayer", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRoblox}.lnk", "-player");
        public ShortcutTask StudioIconTask { get; } = new("RobloxStudio", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRobloxStudio}.lnk", "-studio");
        public ShortcutTask SettingsIconTask { get; } = new("Settings", Paths.Desktop, $"{Strings.Menu_Title}.lnk", "-settings");
        public ShortcutTask AccountManagerIconTask { get; } = new("AccountManager", Paths.Desktop, "Account Manager.lnk", "-accountmanager");
        public ExtractIconsTask ExtractIconsTask { get; } = new();

        [ObservableProperty] private string _searchQuery = "";
        [ObservableProperty] private bool _isGameSearchLoading;
        [ObservableProperty] private string _placeId = "";
        [ObservableProperty] private string _jobId = "";
        [ObservableProperty] private string _accessCode = "";

        [ObservableProperty] private string _previewName = "No Game Selected";
        [ObservableProperty] private string _previewId = "ID: 0";
        [ObservableProperty] private Bitmap? _previewIcon;
        [ObservableProperty] private string _shortcutStatus = "Ready";
        [ObservableProperty] private bool _isSearchFlyoutOpen;

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

                if (PreviewIcon != null)
                {
                    try
                    {
                        ShortcutStatus = "Saving icon...";
                        using var ms = new MemoryStream();
                        PreviewIcon.Save(ms);
                        byte[] imageBytes = ms.ToArray();

                        string hash = ComputeHash(imageBytes);
                        string icoPath = Path.Combine(shortcutsIconDir, $"{hash}.ico");

                        if (!File.Exists(icoPath))
                        {
                            ShortcutStatus = "Converting icon...";
                            using var icoFile = File.Create(icoPath);
                            SaveBitmapAsIcon(PreviewIcon, icoFile);
                        }
                        finalIconPath = icoPath;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("ShortcutsViewModel", $"Icon processing failed: {ex.Message}");
                    }
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
                    await SearchGamesAsync();
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task<Bitmap?> LoadBitmapFromUrl(string? url, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(url)) return null;

            try
            {
                var response = await App.HttpClient.GetByteArrayAsync(url, token);
                using var ms = new MemoryStream(response);
                return new Bitmap(ms);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ShortcutsViewModel", $"Failed to load preview bitmap: {ex.Message}");
                return null;
            }
        }

        partial void OnSelectedSearchResultChanged(OmniSearchContent? value)
        {
            if (value is null) return;

            _isProcessingSelection = true;

            PlaceId = value.RootPlaceId.ToString();
            SearchQuery = PlaceId;

            PreviewName = value.Name!;
            PreviewId = $"ID: {value.RootPlaceId}";

            PreviewIcon = value.ThumbnailBitmap;

            ShortcutStatus = "Ready to create";

            _isProcessingSelection = false;
            IsSearchFlyoutOpen = false;
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
                    PreviewIcon = await LoadBitmapFromUrl(details.Thumbnail.ImageUrl, token);
                }
                else
                {
                    PreviewName = $"Game {id}";
                    PreviewId = $"ID: {id}";
                    PreviewIcon = null;
                }
            }
            catch (Exception)
            {
                PreviewName = $"Game {id}";
                ShortcutStatus = "Ready with manual ID.";
            }
        }

        [RelayCommand]
        private async Task SearchGamesAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery) || SearchQuery.Length < 3)
            {
                IsSearchFlyoutOpen = false;
                return;
            }

            IsGameSearchLoading = true;
            try
            {
                var results = await GameSearching.GetGameSearchResultsAsync(SearchQuery);

                if (results.Any())
                {
                    var thumbRequests = results.Select(x => new ThumbnailRequest
                    {
                        TargetId = (ulong)x.RootPlaceId,
                        Type = ThumbnailType.PlaceIcon,
                        Size = "128x128",
                        Format = ThumbnailFormat.Png,
                        IsCircular = false
                    }).ToList();

                    var fetchedUrls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, CancellationToken.None);

                    for (int i = 0; i < results.Count; i++)
                    {
                        if (fetchedUrls != null && i < fetchedUrls.Length && !string.IsNullOrEmpty(fetchedUrls[i]))
                        {
                            try
                            {
                                var response = await App.HttpClient.GetByteArrayAsync(fetchedUrls[i]);
                                using var ms = new MemoryStream(response);
                                results[i].ThumbnailBitmap = new Bitmap(ms);
                            }
                            catch { }
                        }
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    SearchResults.Clear();
                    foreach (var res in results) SearchResults.Add(res);
                    IsSearchFlyoutOpen = SearchResults.Count > 0 && !string.IsNullOrWhiteSpace(SearchQuery);
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ShortcutsViewModel", $"Search error: {ex.Message}");
            }
            finally
            {
                IsGameSearchLoading = false;
            }
        }

        private static void SaveBitmapAsIcon(Bitmap bitmap, Stream output)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms);
            ms.Position = 0;

            using var resized = Bitmap.DecodeToWidth(ms, 64);

            using var pngStream = new MemoryStream();
            resized.Save(pngStream);
            byte[] pngBytes = pngStream.ToArray();

            using var writer = new BinaryWriter(output);
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)1);

            writer.Write((byte)64);
            writer.Write((byte)64);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);

            writer.Write(pngBytes.Length);
            writer.Write(22);
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