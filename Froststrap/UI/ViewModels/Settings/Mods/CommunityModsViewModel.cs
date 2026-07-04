using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Dialogs;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;

namespace Froststrap.UI.ViewModels.Settings.Mods
{
    public partial class CommunityModsViewModel : NotifyPropertyChangedViewModel
    {
        private readonly string _cacheFolder = Path.Combine(Paths.Cache, "Community Mods");
        private List<CommunityMod> _allMods = [];
        private CancellationTokenSource? _searchCts;
        private const int CacheDurationDays = 7;

        public event EventHandler? OpenModsEvent;
        public event EventHandler? OpenModGeneratorEvent;
        public event EventHandler? OpenPresetModsEvent;

        private ObservableCollection<CommunityMod> _mods = [];
        public ObservableCollection<CommunityMod> Mods
        {
            get => _mods;
            set => SetProperty(ref _mods, value);
        }

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    _ = SearchModsAsync();
                }
            }
        }

        private ModType? _activeFilter;
        public ModType? ActiveFilter
        {
            get => _activeFilter;
            set => SetProperty(ref _activeFilter, value);
        }

        public CommunityModsViewModel()
        {
            Directory.CreateDirectory(_cacheFolder);
            App.RemoteData.Subscribe(async (_, _) => await RefreshModsAsync());
        }

        [RelayCommand] private void OpenMods() => OpenModsEvent?.Invoke(this, EventArgs.Empty);
        [RelayCommand] private void OpenPresetMods() => OpenPresetModsEvent?.Invoke(this, EventArgs.Empty);
        [RelayCommand] private void OpenModGenerator() => OpenModGeneratorEvent?.Invoke(this, EventArgs.Empty);

        [RelayCommand]
        private void SetFilter(object? parameter)
        {
            if (parameter is null)
            {
                ActiveFilter = null;
            }
            else if (parameter is ModType newFilter)
            {
                ActiveFilter = ActiveFilter == newFilter ? null : newFilter;
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

                _allMods = App.RemoteData.Prop.CommunityMods ?? [];
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
        private static async Task DownloadModAsync(CommunityMod mod)
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

                    _ = Frontend.ShowMessageBox(string.Format(Strings.Menu_CommunityMods_ThemeInstalled, mod.Name), MessageBoxImage.Information);
                }
                else
                {
                    string installPath = Path.Combine(Paths.Modifications, mod.Name);
                    if (Directory.Exists(installPath))
                    {
                        var result = await Frontend.ShowMessageBox(string.Format(Strings.Menu_CommunityMods_Overwrite, mod.Name), MessageBoxImage.Question, MessageBoxButton.YesNo);
                        if (result != MessageBoxResult.Yes) return;
                        Directory.Delete(installPath, true);
                    }

                    await ExtractZipAsync(tempFile, installPath);

                    var existingMod = App.State.Prop.Mods.FirstOrDefault(m =>
                        string.Equals(m.FolderName, mod.Name, StringComparison.OrdinalIgnoreCase));

                    if (existingMod != null)
                    {
                        existingMod.Enabled = true;
                        App.Logger.WriteLine("CommunityModsViewModel::DownloadModAsync", $"Enabled existing mod '{mod.Name}'.");
                    }
                    else
                    {
                        int maxPriority = App.State.Prop.Mods.Count > 0 ? App.State.Prop.Mods.Max(m => m.Priority) : 0;
                        var newMod = new ModConfig
                        {
                            FolderName = mod.Name,
                            Enabled = true,
                            Priority = maxPriority + 1,
                            Target = ModTarget.Both
                        };
                        App.State.Prop.Mods.Add(newMod);
                        App.Logger.WriteLine("CommunityModsViewModel::DownloadModAsync", $"Added mod '{mod.Name}' to state.");
                    }

                    App.State.SaveSetting("Mods");

                    _ = Frontend.ShowMessageBox(string.Format(Strings.Menu_CommunityMods_ModInstalled, mod.Name), MessageBoxImage.Information);
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
        private static async Task OpenModInfoDialog(Control? control)
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

        private static async Task ExtractZipAsync(string zipPath, string dest)
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(dest))
                    Directory.Delete(dest, true);
                Directory.CreateDirectory(dest);

                // For some reason on linux, it places the mod inside a subfolder, so we have to do this
                if (OperatingSystem.IsLinux())
                {
                    string tempExtract = Path.Combine(Path.GetTempPath(), "Froststrap", Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempExtract);
                    try
                    {
                        ZipFile.ExtractToDirectory(zipPath, tempExtract, true);

                        var entries = Directory.GetFileSystemEntries(tempExtract);
                        if (entries.Length == 1 && Directory.Exists(entries[0]))
                        {
                            string rootDir = entries[0];
                            foreach (var file in Directory.GetFiles(rootDir, "*", SearchOption.AllDirectories))
                            {
                                string relative = Path.GetRelativePath(rootDir, file);
                                string target = Path.Combine(dest, relative);
                                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                                File.Move(file, target);
                            }
                            Directory.Delete(rootDir, true);
                        }
                        else
                        {
                            foreach (var entry in entries)
                            {
                                string name = Path.GetFileName(entry);
                                string target = Path.Combine(dest, name);
                                if (Directory.Exists(entry))
                                    Directory.Move(entry, target);
                                else
                                    File.Move(entry, target);
                            }
                        }
                    }
                    finally
                    {
                        if (Directory.Exists(tempExtract))
                            Directory.Delete(tempExtract, true);
                    }
                }
                else
                {
                    ZipFile.ExtractToDirectory(zipPath, dest, true);
                }
            });
        }

        private static async Task DownloadFileAsync(string url, string path, IProgress<double> progress)
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
    }
}