using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows;
using System.Drawing;
using System.Drawing.Imaging;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public partial class ShortcutsViewModel : NotifyPropertyChangedViewModel
    {
        public ShortcutTask DesktopIconTask { get; } = new("Desktop", Paths.Desktop, $"{App.ProjectName}.lnk");
        public ShortcutTask StartMenuIconTask { get; } = new("StartMenu", Paths.WindowsStartMenu, $"{App.ProjectName}.lnk");
        public ShortcutTask PlayerIconTask { get; } = new("RobloxPlayer", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRoblox}.lnk", "-player");
        public ShortcutTask StudioIconTask { get; } = new("RobloxStudio", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRobloxStudio}.lnk", "-studio");
        public ShortcutTask SettingsIconTask { get; } = new("Settings", Paths.Desktop, $"{Strings.Menu_Title}.lnk", "-settings");
        public ExtractIconsTask ExtractIconsTask { get; } = new();

        public ObservableCollection<OmniSearchContent> SearchResults { get; } = new();

        private GameShortcut _selectedShortcut = new();
        public GameShortcut SelectedShortcut
        {
            get => _selectedShortcut;
            set
            {
                if (_selectedShortcut == value) return;
                _selectedShortcut = value;
                OnPropertyChanged(nameof(SelectedShortcut));
                GameShortcutStatus = "";

                if (!string.IsNullOrEmpty(value.GameId))
                    SearchQuery = value.GameId;
                else
                {
                    Application.Current.Dispatcher.Invoke(() => SearchResults.Clear());
                    SelectedSearchResult = null;
                }
            }
        }

        private OmniSearchContent? _selectedSearchResult;
        public OmniSearchContent? SelectedSearchResult
        {
            get => _selectedSearchResult;
            set
            {
                if (_selectedSearchResult == value) return;
                _selectedSearchResult = value;
                OnPropertyChanged(nameof(SelectedSearchResult));

                if (value != null)
                {
                    SelectedShortcut.GameId = value.RootPlaceId.ToString();
                    SelectedShortcut.GameName = value.Name ?? "Unknown Game";
                    SearchQuery = value.RootPlaceId.ToString();
                    
                    _searchDebounceCts?.Cancel();
                    _ = DownloadIconAsync(value.RootPlaceId, value.UniverseId);

                    OnPropertyChanged(nameof(SelectedShortcut));
                }
            }
        }

        private string _searchQuery = "";
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery == value) return;
                _searchQuery = value;
                OnPropertyChanged(nameof(SearchQuery));
                OnSearchQueryChanged(value);
            }
        }

        private string _gameShortcutStatus = "";
        public string GameShortcutStatus
        {
            get => _gameShortcutStatus;
            set
            {
                _gameShortcutStatus = value;
                OnPropertyChanged(nameof(GameShortcutStatus));
            }
        }

        public RelayCommand CreateShortcutCommand { get; }
        private CancellationTokenSource? _searchDebounceCts;

        public ShortcutsViewModel()
        {
            CreateShortcutCommand = new RelayCommand(CreateShortcut);
        }

        private async void OnSearchQueryChanged(string value)
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new CancellationTokenSource();

            if (string.IsNullOrWhiteSpace(value))
            {
                Application.Current.Dispatcher.Invoke(() => SearchResults.Clear());
                return;
            }

            try
            {
                await DebouncedSearchAsync(_searchDebounceCts.Token);
            }
            catch (OperationCanceledException) { }
        }

        private async Task DebouncedSearchAsync(CancellationToken token)
        {
            await Task.Delay(600, token);
            if (token.IsCancellationRequested) return;

            if (ulong.TryParse(SearchQuery, out ulong gameId))
            {
                return;
            }

            await SearchGamesAsync(token);
        }

        private async Task SearchGamesAsync(CancellationToken token)
        {
            try
            {
                var results = await GameSearching.GetGameSearchResultsAsync(SearchQuery).ConfigureAwait(false);
                if (token.IsCancellationRequested || results == null) return;

                var topResults = results.Take(8).ToList();
                
                var thumbRequests = topResults.Select(r => new ThumbnailRequest
                {
                    Type = ThumbnailType.GameIcon,
                    TargetId = (ulong)r.UniverseId,
                    Size = "128x128"
                }).ToList();

                var urls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, token);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    SearchResults.Clear();
                    for (int i = 0; i < topResults.Count; i++)
                    {
                        if (urls != null && i < urls.Length)
                            topResults[i].ThumbnailUrl = urls[i] ?? "";
                        
                        SearchResults.Add(topResults[i]);
                    }
                });
            }
            catch (Exception ex) { GameShortcutStatus = $"Search failed: {ex.Message}"; }
        }

        private async Task DownloadIconAsync(long placeId, long universeId)
        {
            try
            {
                GameShortcutStatus = "Downloading icon...";
                
                var request = new ThumbnailRequest
                {
                    Size = "128x128",
                    TargetId = (ulong)(universeId > 0 ? universeId : placeId),
                    Type = universeId > 0 ? "GameIcon" : "PlaceIcon",
                    Format = "Png"
                };

                string? url = await Thumbnails.GetThumbnailUrlAsync(request, CancellationToken.None).ConfigureAwait(false);
                if (string.IsNullOrEmpty(url)) return;

                using var http = new HttpClient();
                var imageBytes = await http.GetByteArrayAsync(url).ConfigureAwait(false);

                string hash = ComputeHash(imageBytes);
                string shortcutsIconDir = Path.Combine(Paths.Cache, "Icons");
                Directory.CreateDirectory(shortcutsIconDir);

                string pngPath = Path.Combine(shortcutsIconDir, $"{hash}.png");
                string icoPath = Path.Combine(shortcutsIconDir, $"{hash}.ico");

                if (!File.Exists(pngPath)) await File.WriteAllBytesAsync(pngPath, imageBytes);

                if (!File.Exists(icoPath))
                {
                    using var stream = new MemoryStream(imageBytes);
                    using var bitmap = new Bitmap(stream);
                    using var icoFile = File.Create(icoPath);
                    SaveBitmapAsIcon(bitmap, icoFile);
                }

                SelectedShortcut.IconPath = pngPath;
                OnPropertyChanged(nameof(SelectedShortcut));
                GameShortcutStatus = "Ready to create shortcut";
            }
            catch (Exception ex) { GameShortcutStatus = $"Icon error: {ex.Message}"; }
        }

        private void CreateShortcut()
        {
            if (string.IsNullOrWhiteSpace(SelectedShortcut.GameId)) return;

            try
            {
                string url = $"roblox://placeId={SelectedShortcut.GameId}/";
                string safeName = SanitizeFileName(SelectedShortcut.GameName);
                string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{safeName}.url");

                using var writer = new StreamWriter(shortcutPath);
                writer.WriteLine("[InternetShortcut]");
                writer.WriteLine($"URL={url}");
                writer.WriteLine("IDList=");

                string icoPath = Path.ChangeExtension(SelectedShortcut.IconPath, ".ico");
                if (File.Exists(icoPath))
                {
                    writer.WriteLine($"IconFile={icoPath}");
                    writer.WriteLine("IconIndex=0");
                }

                GameShortcutStatus = "Shortcut created on Desktop!";
            }
            catch (Exception ex) { GameShortcutStatus = $"Failed: {ex.Message}"; }
        }

        private static void SaveBitmapAsIcon(Bitmap bitmap, Stream output)
        {
            using var resized = new Bitmap(bitmap, new System.Drawing.Size(64, 64));
            using var iconBitmap = new Bitmap(64, 64, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(iconBitmap))
                graphics.DrawImage(resized, 0, 0, 64, 64);

            using var stream = new MemoryStream();
            iconBitmap.Save(stream, ImageFormat.Png);
            var pngBytes = stream.ToArray();

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
            if (string.IsNullOrWhiteSpace(name))
                return name;

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

    public class GameShortcut
    {
        public string GameName { get; set; } = "";
        public string GameId { get; set; } = "";
        public string IconPath { get; set; } = "";
    }
}