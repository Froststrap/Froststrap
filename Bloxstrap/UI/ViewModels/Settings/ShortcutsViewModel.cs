using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows;

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

        public ObservableCollection<GameSearchResult> SearchResults { get; } = new();

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
                {
                    SearchQuery = value.GameId;
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SearchResults.Clear();
                    });
                    SelectedSearchResult = null;
                }
            }
        }

        private GameSearchResult? _selectedSearchResult;
        public GameSearchResult? SelectedSearchResult
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
                    SelectedShortcut.GameName = value.Name;
                    SearchQuery = value.RootPlaceId.ToString();
                    _searchDebounceCts?.Cancel();
                    _ = DownloadIconAsync();

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
                if (_gameShortcutStatus == value) return;

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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SearchResults.Clear();
                });
                return;
            }

            try
            {
                await DebouncedSearchAsync(_searchDebounceCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled, ignore
            }
        }

        private async Task DebouncedSearchAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(600, token);
                if (token.IsCancellationRequested) return;

                await SearchGamesAsync();

                if (SelectedSearchResult == null && ulong.TryParse(SearchQuery, out ulong gameId))
                {
                    var matchingGame = SearchResults.FirstOrDefault(r => r.RootPlaceId == (long)gameId);
                    if (matchingGame != null)
                    {
                        SelectedShortcut.GameId = gameId.ToString();
                        SelectedShortcut.GameName = matchingGame.Name;
                        _ = DownloadIconAsync();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled, ignore
            }
            catch (Exception ex)
            {
                GameShortcutStatus = $"Search error: {ex.Message}";
            }
        }

        private async Task SearchGamesAsync()
        {
            try
            {
                var results = await GameSearching.GetGameSearchResultsAsync(SearchQuery).ConfigureAwait(false);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    SearchResults.Clear();

                    foreach (var result in results.Take(5))
                        SearchResults.Add(result);
                });
            }
            catch (Exception ex)
            {
                GameShortcutStatus = $"Search failed: {ex.Message}";
            }
        }

        private async Task DownloadIconAsync()
        {
            if (!ulong.TryParse(SelectedShortcut.GameId, out ulong gameId))
            {
                GameShortcutStatus = "Invalid Game ID";
                return;
            }

            try
            {
                var request = new ThumbnailRequest
                {
                    TargetId = gameId,
                    Type = "PlaceIcon",
                    Size = "128x128",
                    Format = "Png"
                };

                string? url = await Thumbnails.GetThumbnailUrlAsync(request, CancellationToken.None).ConfigureAwait(false);
                if (string.IsNullOrEmpty(url))
                {
                    GameShortcutStatus = "No icon found";
                    return;
                }

                using var http = new HttpClient();
                var imageBytes = await http.GetByteArrayAsync(url).ConfigureAwait(false);

                string hash = ComputeHash(imageBytes);
                string shortcutsIconDir = Path.Combine(Paths.Cache, "Game Shortcuts");
                Directory.CreateDirectory(shortcutsIconDir);

                string pngPath = Path.Combine(shortcutsIconDir, $"{hash}.png");
                if (!File.Exists(pngPath))
                {
                    await File.WriteAllBytesAsync(pngPath, imageBytes);
                }

                string icoPath = Path.Combine(shortcutsIconDir, $"{hash}.ico");
                if (!File.Exists(icoPath))
                {
                    using var stream = new MemoryStream(imageBytes);
                    using var bitmap = new Bitmap(stream);
                    using var icoFile = File.Create(icoPath);
                    SaveBitmapAsIcon(bitmap, icoFile);
                }

                SelectedShortcut.IconPath = pngPath;

                OnPropertyChanged(nameof(SelectedShortcut));
                GameShortcutStatus = "Icon downloaded";
            }
            catch (Exception ex)
            {
                GameShortcutStatus = $"Error downloading icon: {ex.Message}";
            }
        }

        private void CreateShortcut()
        {
            if (string.IsNullOrWhiteSpace(SelectedShortcut.GameId))
            {
                GameShortcutStatus = "Game ID required";
                return;
            }

            try
            {
                string url = $"roblox://placeId={SelectedShortcut.GameId}/";
                string safeName = SanitizeFileName(SelectedShortcut.GameName);

                if (string.IsNullOrWhiteSpace(safeName))
                    safeName = $"Roblox Game {SelectedShortcut.GameId}";

                string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{safeName}.url");

                if (File.Exists(shortcutPath))
                    File.Delete(shortcutPath);

                using var writer = new StreamWriter(shortcutPath);
                writer.WriteLine("[InternetShortcut]");
                writer.WriteLine($"URL={url}");

                if (!string.IsNullOrEmpty(SelectedShortcut.IconPath) && File.Exists(SelectedShortcut.IconPath))
                {
                    string pngPath = SelectedShortcut.IconPath;
                    string icoPath = Path.ChangeExtension(pngPath, ".ico");

                    if (File.Exists(icoPath))
                    {
                        writer.WriteLine($"IconFile={icoPath}");
                        writer.WriteLine("IconIndex=0");
                    }
                }

                GameShortcutStatus = $"Shortcut created: {safeName}.url";
            }
            catch (Exception ex)
            {
                GameShortcutStatus = $"Error creating shortcut: {ex.Message}";
            }
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