using Bloxstrap.UI.Elements.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public partial class CommunityModsViewModel : ObservableObject
    {
        public event EventHandler? OpenModsEvent;
        public event EventHandler? OpenModGeneratorEvent;
        public event EventHandler? OpenPresetModsEvent;
        private void OpenMods() => OpenModsEvent?.Invoke(this, EventArgs.Empty);
        private void OpenPresetMods() => OpenPresetModsEvent?.Invoke(this, EventArgs.Empty);
        private void OpenModGenerator() => OpenModGeneratorEvent?.Invoke(this, EventArgs.Empty);
        public ICommand OpenModsCommand => new RelayCommand(OpenMods);
        public ICommand OpenPresetModsCommand => new RelayCommand(OpenPresetMods);
        public ICommand OpenModGeneratorCommand => new RelayCommand(OpenModGenerator);

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

        [ObservableProperty]
        private bool _showAll = true;

        [ObservableProperty]
        private bool _showColorMods = false;

        [ObservableProperty]
        private bool _showCustomThemes = false;

        [ObservableProperty]
        private bool _showMiscMods = false;

        [ObservableProperty]
        private bool _showSkyBox = false;

        [ObservableProperty]
        private bool _showCursor = false;

        [ObservableProperty]
        private bool _showAvatarEditor = false;

        private readonly HttpClient _httpClient = new();
        private List<CommunityMod> _allMods = new();
        private readonly string _cacheFolder;
        private const int CACHE_DURATION_DAYS = 7;
        private CancellationTokenSource? _searchCancellationTokenSource;

        public CommunityModsViewModel()
        {
            _cacheFolder = Path.Combine(Paths.Cache, "Community Mods");
            Directory.CreateDirectory(_cacheFolder);
            App.RemoteData.Subscribe(OnRemoteDataLoaded);
        }

        private async void OnRemoteDataLoaded(object? sender, EventArgs e)
        {
            await LoadModsAsync();
        }

        [RelayCommand]
        private void FilterAll()
        {
            ResetAllFilters();
            ShowAll = true;
            ApplyFilters();
        }

        [RelayCommand]
        private void FilterColorMods()
        {
            ResetAllFilters();
            ShowColorMods = true;
            ApplyFilters();
        }

        [RelayCommand]
        private void FilterCustomThemes()
        {
            ResetAllFilters();
            ShowCustomThemes = true;
            ApplyFilters();
        }

        [RelayCommand]
        private void FilterMiscMods()
        {
            ResetAllFilters();
            ShowMiscMods = true;
            ApplyFilters();
        }

        [RelayCommand]
        private void FilterSkyBox()
        {
            ResetAllFilters();
            ShowSkyBox = true;
            ApplyFilters();
        }

        [RelayCommand]
        private void FilterCursor()
        {
            ResetAllFilters();
            ShowCursor = true;
            ApplyFilters();
        }

        [RelayCommand]
        private void FilterAvatarEditor()
        {
            ResetAllFilters();
            ShowAvatarEditor = true;
            ApplyFilters();
        }

        private void ResetAllFilters()
        {
            ShowAll = false;
            ShowColorMods = false;
            ShowCustomThemes = false;
            ShowMiscMods = false;
            ShowSkyBox = false;
            ShowCursor = false;
            ShowAvatarEditor = false;
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

                if (App.RemoteData.LoadedState == GenericTriState.Unknown)
                {
                    await App.RemoteData.WaitUntilDataFetched();
                }

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
                ApplyFilters();

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
                ApplyFilters();
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

        private void ApplyFilters()
        {
            try
            {
                IEnumerable<CommunityMod> filteredMods = _allMods;

                if (ShowColorMods)
                {
                    filteredMods = filteredMods.Where(mod => mod.ModType == ModType.ColorMod);
                }
                else if (ShowCustomThemes)
                {
                    filteredMods = filteredMods.Where(mod => mod.ModType == ModType.CustomTheme);
                }
                else if (ShowMiscMods)
                {
                    filteredMods = filteredMods.Where(mod => mod.ModType == ModType.MiscMod);
                }
                else if (ShowSkyBox)
                {
                    filteredMods = filteredMods.Where(mod => mod.ModType == ModType.SkyBox);
                }
                else if (ShowCursor)
                {
                    filteredMods = filteredMods.Where(mod => mod.ModType == ModType.Cursor);
                }
                else if (ShowAvatarEditor)
                {
                    filteredMods = filteredMods.Where(mod => mod.ModType == ModType.AvatarEditor);
                }

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    var query = SearchQuery.ToLower();
                    filteredMods = filteredMods.Where(mod =>
                        mod.Name.ToLower().Contains(query) ||
                        (mod.HexCode?.ToLower()?.Contains(query) ?? false) ||
                        (mod.Author?.ToLower()?.Contains(query) ?? false) ||
                        mod.ModTypeDisplay.ToLower().Contains(query)
                    );
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Mods.Clear();
                    foreach (var mod in filteredMods)
                    {
                        mod.DownloadCommand = DownloadModCommand;
                        mod.ShowInfoCommand = ShowModInfoCommand;
                        Mods.Add(mod);
                    }
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"CommunityModsViewModel::ApplyFilters", $"Filter error: {ex.Message}");
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

                if (mod.IsCustomTheme)
                {
                    await ExtractCustomThemeAsync(tempFile, mod.Name);

                    App.Settings.Prop.SelectedCustomTheme = mod.Name;
                    App.Settings.Save();

                    if (App.Settings.Prop.BootstrapperStyle != BootstrapperStyle.CustomDialog)
                    {
                        App.Settings.Prop.BootstrapperStyle = BootstrapperStyle.CustomDialog;
                        App.Settings.Save();
                    }

                    Frontend.ShowMessageBox(
                        $"Custom theme '{mod.Name}' installed successfully!\n" +
                        "The theme has been saved to your Custom Themes folder and has been automatically selected as your current theme.",
                        MessageBoxImage.Information,
                        MessageBoxButton.OK
                    );
                    App.Logger.WriteLine($"CommunityModsViewModel::DownloadModAsync", $"Installed and selected custom theme: {mod.Name}");
                }
                else
                {
                    bool hasExistingMods = Directory.Exists(Paths.Modifications) &&
                                          (Directory.GetFiles(Paths.Modifications).Any() ||
                                           Directory.GetDirectories(Paths.Modifications)
                                               .Any(dir => !dir.EndsWith("ClientSettings", StringComparison.OrdinalIgnoreCase)));

                    if (hasExistingMods)
                    {
                        var result = Frontend.ShowMessageBox(
                            "Existing mods found in the Modifications folder.\n\n" +
                            $"Would you like to delete existing mods before installing '{mod.Name}'?",
                            MessageBoxImage.Question,
                            MessageBoxButton.YesNo
                        );

                        if (result == MessageBoxResult.Yes)
                        {
                            await CleanModificationsDirectoryAsync();
                        }
                    }

                    await ExtractZipAsync(tempFile);

                    Frontend.ShowMessageBox(
                        $"Mod '{mod.Name}' installed successfully!",
                        MessageBoxImage.Information,
                        MessageBoxButton.OK
                    );

                    App.Logger.WriteLine($"CommunityModsViewModel::DownloadModAsync", $"Installed mod: {mod.Name}");
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