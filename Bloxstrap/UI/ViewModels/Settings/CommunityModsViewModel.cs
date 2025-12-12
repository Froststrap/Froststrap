using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Bloxstrap.UI.Elements.Dialogs;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public partial class CommunityModsViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<CommunityMod> _mods = new();

        [ObservableProperty]
        private bool _isLoading = true;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        private readonly HttpClient _httpClient = new();
        private List<CommunityMod> _allMods = new();
        private readonly string _cacheFolder;
        private const int CACHE_DURATION_DAYS = 7;
        private CancellationTokenSource? _searchCancellationTokenSource;

        public CommunityModsViewModel()
        {
            _cacheFolder = Path.Combine(Paths.Cache, "CommunityMods");
            Directory.CreateDirectory(_cacheFolder);
            App.RemoteData.Subscribe(OnRemoteDataLoaded);
        }

        private async void OnRemoteDataLoaded(object? sender, EventArgs e)
        {
            await LoadModsAsync();
        }

        [RelayCommand]
        private void ShowModInfo(CommunityMod mod)
        {
            if (mod == null) return;

            var dialog = new CommunityModInfoDialog(mod)
            {
                Owner = Application.Current.MainWindow
            };

            var result = dialog.ShowDialog();
        }

        [RelayCommand]
        private async Task LoadModsAsync()
        {
            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

                var remoteMods = App.RemoteData.Prop.CommunityMods;

                if (remoteMods?.Any() != true)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _allMods.Clear();
                        Mods.Clear();
                    });
                    return;
                }

                _allMods = remoteMods;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Mods.Clear();
                    foreach (var mod in _allMods)
                    {
                        mod.DownloadCommand = DownloadModCommand;
                        mod.ShowInfoCommand = ShowModInfoCommand;
                        Mods.Add(mod);
                    }
                });

                var thumbnailTasks = _allMods.Select(mod => LoadModThumbnailAsync(mod));
                await Task.WhenAll(thumbnailTasks);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load mods: {ex.Message}";
                App.Logger.WriteLine($"CommunityModsViewModel::LoadModsAsync", $"Error: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void RefreshMods()
        {
            _ = LoadModsAsync();
        }

        [RelayCommand]
        private async Task SearchModsAsync()
        {
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _searchCancellationTokenSource.Token;

            try
            {
                await Task.Delay(300, cancellationToken);

                List<CommunityMod> modsToDisplay;

                if (string.IsNullOrWhiteSpace(SearchQuery))
                {
                    modsToDisplay = _allMods;
                }
                else
                {
                    var query = SearchQuery.ToLower();
                    modsToDisplay = _allMods.Where(mod =>
                        mod.Name.ToLower().Contains(query) ||
                        (mod.HexCode?.ToLower()?.Contains(query) ?? false) ||
                        (mod.Author?.ToLower()?.Contains(query) ?? false) ||
                        mod.ModTypeDisplay.ToLower().Contains(query)
                    ).ToList();
                }

                cancellationToken.ThrowIfCancellationRequested();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Mods.Clear();
                    foreach (var mod in modsToDisplay)
                    {
                        mod.DownloadCommand = DownloadModCommand;
                        Mods.Add(mod);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"CommunityModsViewModel::SearchModsAsync", $"Search error: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DownloadModAsync(CommunityMod mod)
        {
            if (mod == null || mod.IsDownloading) return;

            string? tempFile = null;
            try
            {
                mod.IsDownloading = true;
                mod.DownloadProgress = 0;

                var progress = new Progress<double>(percent =>
                {
                    mod.DownloadProgress = percent;
                });

                var tempDir = Path.Combine(Path.GetTempPath(), "Froststrap", "Downloads");
                Directory.CreateDirectory(tempDir);
                tempFile = Path.Combine(tempDir, $"{Guid.NewGuid()}.zip");

                await DownloadFileAsync(mod.DownloadUrl, tempFile, progress);

                mod.DownloadProgress = 100;

                switch (mod.ModType)
                {
                    case ModType.Misc:
                    case ModType.Mod:
                        await ExtractModToModificationsAsync(tempFile, mod.Name);
                        Frontend.ShowMessageBox(
                            $"Mod '{mod.Name}' installed successfully!",
                            MessageBoxImage.Information,
                            MessageBoxButton.OK
                        );
                        App.Logger.WriteLine($"CommunityModsViewModel::DownloadModAsync", $"Installed mod: {mod.Name}");
                        break;

                    case ModType.CustomTheme:
                        await ExtractCustomThemeAsync(tempFile, mod.Name);
                        Frontend.ShowMessageBox(
                            $"Custom theme '{mod.Name}' installed successfully!",
                            MessageBoxImage.Information,
                            MessageBoxButton.OK
                        );
                        App.Logger.WriteLine($"CommunityModsViewModel::DownloadModAsync", $"Installed custom theme: {mod.Name}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    ex.Message,
                    MessageBoxImage.Error,
                    MessageBoxButton.OK
                );

                App.Logger.WriteLine($"CommunityModsViewModel::DownloadModAsync", $"Failed to install {mod.Name}: {ex}");
            }
            finally
            {
                mod.IsDownloading = false;
                mod.DownloadProgress = 0;

                await CleanupTempFileAsync(tempFile);
            }
        }

        private async Task ExtractCustomThemeAsync(string zipPath, string themeName)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("Theme file not found", zipPath);

            try
            {
                var themesDir = Paths.CustomThemes;
                Directory.CreateDirectory(themesDir);

                var themeDir = Path.Combine(themesDir, themeName);
                if (Directory.Exists(themeDir))
                {
                    Directory.Delete(themeDir, true);
                }

                Directory.CreateDirectory(themeDir);

                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(zipPath);

                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;

                        var destinationPath = Path.Combine(themeDir, entry.FullName);
                        var destinationDir = Path.GetDirectoryName(destinationPath);

                        if (!string.IsNullOrEmpty(destinationDir))
                            Directory.CreateDirectory(destinationDir);

                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }
                });

                App.Logger.WriteLine($"CommunityModsViewModel::ExtractCustomThemeAsync", $"Extracted custom theme '{themeName}' to {themeDir}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to extract custom theme '{themeName}': {ex.Message}", ex);
            }
        }

        private async Task ExtractModToModificationsAsync(string zipPath, string modName)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("Mod file not found", zipPath);

            try
            {
                Directory.CreateDirectory(Paths.Modifications);
                await CleanModificationsDirectoryAsync();

                await ExtractZipAsync(zipPath);

                App.Logger.WriteLine($"CommunityModsViewModel::ExtractModToModificationsAsync", $"Extracted {modName} to {Paths.Modifications}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to extract mod '{modName}': {ex.Message}", ex);
            }
        }

        private async Task ExtractZipAsync(string zipPath)
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipPath);

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var destinationPath = Path.Combine(Paths.Modifications, entry.FullName);
                    var destinationDir = Path.GetDirectoryName(destinationPath);

                    if (!string.IsNullOrEmpty(destinationDir))
                        Directory.CreateDirectory(destinationDir);

                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            });
        }

        private async Task DownloadFileAsync(string url, string filePath, IProgress<double> progress)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes > 0 && progress != null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var totalRead = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;

            do
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    isMoreToRead = false;
                }
                else
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (canReportProgress)
                    {
                        var percentage = (double)totalRead / totalBytes * 100;
                        progress?.Report(percentage);
                    }
                }
            } while (isMoreToRead);
        }

        private async Task CleanModificationsDirectoryAsync()
        {
            if (!Directory.Exists(Paths.Modifications))
                return;

            var modificationDir = new DirectoryInfo(Paths.Modifications);
            var keepFolders = new[] { "ClientSettings" };

            await Task.Run(() =>
            {
                foreach (var dir in modificationDir.GetDirectories())
                {
                    if (!keepFolders.Contains(dir.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        try { dir.Delete(true); }
                        catch { /* Ignore */ }
                    }
                }

                foreach (var file in modificationDir.GetFiles())
                {
                    try { file.Delete(); }
                    catch { /* Ignore */ }
                }
            });
        }

        private async Task CleanupTempFileAsync(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            try
            {
                await Task.Run(() =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            File.Delete(filePath);
                            return;
                        }
                        catch when (i < 2)
                        {
                            Thread.Sleep(100 * (i + 1));
                        }
                    }
                });
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        private async Task LoadModThumbnailAsync(CommunityMod mod)
        {
            if (string.IsNullOrEmpty(mod.ThumbnailUrl))
                return;

            try
            {
                await SetModThumbnailLoadingStateAsync(mod, true, false);

                var cachedImage = await GetCachedThumbnailAsync(mod);
                if (cachedImage != null)
                {
                    await SetModThumbnailAsync(mod, cachedImage);
                    return;
                }

                var imageBytes = await _httpClient.GetByteArrayAsync(mod.ThumbnailUrl);
                var bitmapImage = await CreateBitmapImageAsync(imageBytes);

                await SetModThumbnailAsync(mod, bitmapImage);
                await CacheThumbnailAsync(mod, imageBytes);
            }
            catch (Exception ex)
            {
                await SetModThumbnailLoadingStateAsync(mod, false, true);
                App.Logger.WriteLine($"CommunityModsViewModel::LoadModThumbnailAsync", $"Failed to load thumbnail for {mod.Name}: {ex.Message}");
            }
        }

        private async Task<BitmapImage?> GetCachedThumbnailAsync(CommunityMod mod)
        {
            try
            {
                var cacheFile = Path.Combine(_cacheFolder, $"{mod.Id}.png");
                var CommunityModCacheInfoFile = Path.Combine(_cacheFolder, "Cache.json");

                if (!File.Exists(cacheFile) || !File.Exists(CommunityModCacheInfoFile))
                    return null;

                var CommunityModCacheInfoJson = await File.ReadAllTextAsync(CommunityModCacheInfoFile);
                var CommunityModCacheInfo = JsonSerializer.Deserialize<Dictionary<string, CommunityModCacheInfo>>(CommunityModCacheInfoJson);

                if (CommunityModCacheInfo?.TryGetValue(mod.Id, out var info) != true || info is null)
                    return null;

                if (DateTime.UtcNow - info.LastUpdated > TimeSpan.FromDays(CACHE_DURATION_DAYS))
                    return null;

                if (info.Url != mod.ThumbnailUrl)
                    return null;

                var imageBytes = await File.ReadAllBytesAsync(cacheFile);
                return await CreateBitmapImageAsync(imageBytes);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"CommunityModsViewModel::GetCachedThumbnailAsync", $"Cache error for {mod.Id}: {ex.Message}");
                return null;
            }
        }

        private async Task CacheThumbnailAsync(CommunityMod mod, byte[] imageBytes)
        {
            try
            {
                var cacheFile = Path.Combine(_cacheFolder, $"{mod.Id}.png");
                var CommunityModCacheInfoFile = Path.Combine(_cacheFolder, "cache.json");

                await File.WriteAllBytesAsync(cacheFile, imageBytes);

                var CommunityModCacheInfo = await LoadCommunityModCacheInfoAsync(CommunityModCacheInfoFile);
                CommunityModCacheInfo[mod.Id] = new CommunityModCacheInfo
                {
                    Url = mod.ThumbnailUrl!,
                    LastUpdated = DateTime.UtcNow
                };

                await SaveCommunityModCacheInfoAsync(CommunityModCacheInfoFile, CommunityModCacheInfo);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"CommunityModsViewModel::CacheThumbnailAsync", $"Failed to cache thumbnail: {ex.Message}");
            }
        }

        private async Task<Dictionary<string, CommunityModCacheInfo>> LoadCommunityModCacheInfoAsync(string CommunityModCacheInfoFile)
        {
            if (!File.Exists(CommunityModCacheInfoFile))
                return new Dictionary<string, CommunityModCacheInfo>();

            try
            {
                var json = await File.ReadAllTextAsync(CommunityModCacheInfoFile);
                return JsonSerializer.Deserialize<Dictionary<string, CommunityModCacheInfo>>(json) ?? new();
            }
            catch
            {
                return new Dictionary<string, CommunityModCacheInfo>();
            }
        }

        private async Task SaveCommunityModCacheInfoAsync(string CommunityModCacheInfoFile, Dictionary<string, CommunityModCacheInfo> CommunityModCacheInfo)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(CommunityModCacheInfo, options);
            await File.WriteAllTextAsync(CommunityModCacheInfoFile, json);
        }

        private async Task SetModThumbnailLoadingStateAsync(CommunityMod mod, bool isLoading, bool hasError)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                mod.IsLoadingThumbnail = isLoading;
                mod.HasThumbnailError = hasError;
            });
        }

        private async Task SetModThumbnailAsync(CommunityMod mod, BitmapImage? image)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                mod.ThumbnailImage = image;
                mod.IsLoadingThumbnail = false;
                mod.HasThumbnailError = image == null;
            });
        }

        private async Task<BitmapImage> CreateBitmapImageAsync(byte[] imageBytes)
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var bitmapImage = new BitmapImage();
                using var stream = new MemoryStream(imageBytes);

                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            });
        }
    }
}