using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Dialogs;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;

namespace Froststrap.UI.ViewModels.Settings.Mods
{
    public partial class CommunityModsViewModel : ObservableObject
    {

        private readonly string _cacheFolder = Path.Combine(Paths.Cache, "Community Mods");

        private List<CommunityMod> _allMods = new();
        private CancellationTokenSource? _searchCts;
        private const int CacheDurationDays = 7;

        public event EventHandler? OpenModsEvent;
        public event EventHandler? OpenModGeneratorEvent;
        public event EventHandler? OpenPresetModsEvent;

        [ObservableProperty] private ObservableCollection<CommunityMod> _mods = new();
        [ObservableProperty] private bool _isLoading = true;
        [ObservableProperty] private bool _hasError;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private string _searchQuery = string.Empty;
        [ObservableProperty] private ModType? _activeFilter;

        public CommunityModsViewModel()
        {
            Directory.CreateDirectory(_cacheFolder);
            App.RemoteData.Subscribe(async (s, e) => await RefreshModsAsync());
        }

        partial void OnSearchQueryChanged(string value) => _ = SearchModsAsync();

        [RelayCommand] private void OpenMods() => OpenModsEvent?.Invoke(this, EventArgs.Empty);
        [RelayCommand] private void OpenPresetMods() => OpenPresetModsEvent?.Invoke(this, EventArgs.Empty);
        [RelayCommand] private void OpenModGenerator() => OpenModGeneratorEvent?.Invoke(this, EventArgs.Empty);

        [RelayCommand]
        private void SetFilter(object? parameter)
        {
            if (parameter == null)
            {
                ActiveFilter = null;
                ApplyFilters();
                return;
            }

            if (parameter is ModType newFilter)
            {
                if (ActiveFilter == newFilter)
                    ActiveFilter = null;
                else
                    ActiveFilter = newFilter;
            }

            ApplyFilters();
        }

        [RelayCommand]
        public async Task RefreshModsAsync()
        {
            try
            {
                IsLoading = true;
                HasError = false;

                if (App.RemoteData.LoadedState == GenericTriState.Unknown)
                    await App.RemoteData.WaitUntilDataFetched();

                _allMods = App.RemoteData.Prop.CommunityMods ?? new();
                ApplyFilters();

                _ = Task.Run(() => Task.WhenAll(_allMods.Select(LoadModThumbnailAsync)));
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load mods: {ex.Message}";
                App.Logger.WriteLine("CommunityModsViewModel::RefreshModsAsync", ex.ToString());
            }
            finally { IsLoading = false; }
        }

        private void ApplyFilters()
        {
            var query = SearchQuery.ToLower().Trim();

            var filtered = _allMods.Where(mod =>
                (ActiveFilter == null || mod.ModType == ActiveFilter) &&
                (string.IsNullOrEmpty(query) ||
                 mod.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 mod.Author?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            ).ToList();

            Dispatcher.UIThread.Invoke(() =>
            {
                Mods.Clear();
                foreach (var mod in filtered)
                {
                    mod.DownloadCommand = DownloadModCommand;
                    Mods.Add(mod);
                }
            });
        }

        [RelayCommand]
        private async Task SearchModsAsync()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            try
            {
                await Task.Delay(300, _searchCts.Token);
                ApplyFilters();
            }
            catch (OperationCanceledException) { }
        }

        [RelayCommand]
        private async Task DownloadModAsync(CommunityMod mod)
        {
            if (mod == null || mod.IsDownloading) return;

            string tempFile = Path.Combine(Path.GetTempPath(), "Froststrap", $"{Guid.NewGuid()}.zip");
            try
            {
                mod.IsDownloading = true;
                Directory.CreateDirectory(Path.GetDirectoryName(tempFile)!);

                var progress = new Progress<double>(p => mod.DownloadProgress = p);
                await DownloadFileAsync(mod.DownloadUrl, tempFile, progress);

                if (mod.IsCustomTheme)
                {
                    string themePath = Path.Combine(Paths.CustomThemes, mod.Name);
                    await ExtractZipAsync(tempFile, themePath);

                    App.Settings.Prop.SelectedCustomTheme = mod.Name;
                    App.Settings.Prop.BootstrapperStyle = BootstrapperStyle.CustomDialog;
                    App.Settings.Save();

                    _ = Frontend.ShowMessageBox($"Theme '{mod.Name}' installed and applied!", MessageBoxImage.Information);
                }
                else
                {
                    string installPath = Path.Combine(Paths.Modifications, mod.Name);
                    if (Directory.Exists(installPath))
                    {
                        var result = await Frontend.ShowMessageBox($"Overwrite existing mod '{mod.Name}'?", MessageBoxImage.Question, MessageBoxButton.YesNo);
                        if (result != MessageBoxResult.Yes) return;
                        Directory.Delete(installPath, true);
                    }

                    await ExtractZipAsync(tempFile, installPath);
                    if (mod.ModType == ModType.SkyBox) await ApplySkyboxFixAsync();

                    _ = Frontend.ShowMessageBox($"Mod '{mod.Name}' installed successfully!", MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _ = Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
                App.Logger.WriteLine("CommunityModsViewModel::DownloadModAsync", ex.ToString());
            }
            finally
            {
                mod.IsDownloading = false;
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [RelayCommand]
        private async Task OpenModInfoDialog(Control? control)
        {
            if (control?.DataContext is not CommunityMod mod) return;

            var topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is not Window parentWindow) return;

            App.FrostRPC?.SetDialog($"Viewing {mod.Name}");

            try
            {
                var dialog = new CommunityModInfoDialog(mod);
                await dialog.ShowDialog(parentWindow);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("CommunityModsViewModel::OpenModInfoDialog", ex.ToString());
            }
            finally
            {
                App.FrostRPC?.ClearDialog();
            }
        }

        private async Task ExtractZipAsync(string zipPath, string dest)
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                Directory.CreateDirectory(dest);
                ZipFile.ExtractToDirectory(zipPath, dest, true);
            });
        }

        private async Task DownloadFileAsync(string url, string path, IProgress<double> progress)
        {
            using var response = await App.HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var downloadStream = await response.Content.ReadAsStreamAsync();

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;
            while ((read = await downloadStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;
                if (totalBytes != -1) progress.Report((double)totalRead / totalBytes * 100);
            }
        }

        private async Task LoadModThumbnailAsync(CommunityMod mod)
        {
            if (string.IsNullOrEmpty(mod.ThumbnailUrl)) return;
            string cachePath = Path.Combine(_cacheFolder, $"{mod.Id}.png");

            try
            {
                byte[] data;
                if (File.Exists(cachePath) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath)).TotalDays < CacheDurationDays)
                {
                    data = await File.ReadAllBytesAsync(cachePath);
                }
                else
                {
                    data = await App.HttpClient.GetByteArrayAsync(mod.ThumbnailUrl);
                    await File.WriteAllBytesAsync(cachePath, data);
                }

                using var ms = new MemoryStream(data);
                var bitmap = new Bitmap(ms);

                await Dispatcher.UIThread.InvokeAsync(() => {
                    mod.ThumbnailImage = bitmap;
                });
            }
            catch { mod.HasThumbnailError = true; }
        }

        private async Task ApplySkyboxFixAsync()
        {
            await Task.Run(() =>
            {
                string rbxStorage = Path.Combine(Paths.Roblox, "rbx-storage");
                var files = new Dictionary<string, string>
                {
                    { "a564ec8aeef3614e788d02f0090089d8", "a5" },
                    { "7328622d2d509b95dd4dd2c721d1ca8b", "73" },
                    { "a50f6563c50ca4d5dcb255ee5cfab097", "a5" },
                    { "6c94b9385e52d221f0538aadaceead2d", "6c" },
                    { "9244e00ff9fd6cee0bb40a262bb35d31", "92" },
                    { "78cb2e93aee0cdbd79b15a866bc93a54", "78" }
                };

                try
                {
                    foreach (var file in files)
                    {
                        string targetDir = Path.Combine(rbxStorage, file.Value);
                        string targetPath = Path.Combine(targetDir, file.Key);
                        Directory.CreateDirectory(targetDir);

                        string resourceName = $"Bloxstrap.Resources.SkyboxFix.{file.Key}";
                        using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                        if (stream == null) continue;

                        if (File.Exists(targetPath)) File.SetAttributes(targetPath, FileAttributes.Normal);
                        using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
                        stream.CopyTo(fileStream);
                        File.SetAttributes(targetPath, FileAttributes.ReadOnly);
                    }
                }
                catch (Exception ex) { App.Logger.WriteLine("CommunityModsViewModel::ApplySkyboxFix", ex.ToString()); }
            });
        }
    }
}