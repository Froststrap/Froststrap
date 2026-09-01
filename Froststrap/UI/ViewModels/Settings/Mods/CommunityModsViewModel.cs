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
    internal partial class CommunityModsViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private List<CommunityMod> _allMods = [];
        private CancellationTokenSource? _searchCts;
        private bool _disposed;

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
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load mods: {ex.Message}";
                App.Logger.Error($"Unhandled exception: {ex}");
            }
            finally { IsLoading = false; }
        }

        private void ApplyFilters()
        {
            var query = SearchQuery.ToUpperInvariant().Trim();

            var filtered = _allMods.Where(mod =>
                (ActiveFilter == null || mod.ModType == ActiveFilter) &&
                (string.IsNullOrEmpty(query) ||
                 mod.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 mod.Author?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            ).ToList();

            foreach (var mod in filtered)
                mod.DownloadCommand = DownloadModCommand;

            Dispatcher.UIThread.Invoke(() =>
            {
                Mods.Clear();
                foreach (var mod in filtered)
                {
                    Mods.Add(mod);
                    _ = mod.LoadThumbnailAsync();
                }
            });
        }

        [RelayCommand]
        private async Task SearchModsAsync()
        {
            await _searchCts!.CancelAsync();
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

                string baseName = mod.Name;
                string finalName = baseName;

                if (mod.IsCustomTheme)
                {
                    string themePath = Path.Combine(Paths.CustomThemes, finalName);

                    if (Directory.Exists(themePath))
                    {
                        var result = await Frontend.ShowMessageBox(
                            string.Format(CultureInfo.InvariantCulture, Strings.Menu_CommunityMods_Overwrite, baseName),
                            MessageBoxImage.Question,
                            MessageBoxButton.YesNo);

                        if (result == MessageBoxResult.Yes)
                        {
                            Directory.Delete(themePath, true);
                        }
                        else
                        {
                            int counter = 1;
                            while (Directory.Exists(Path.Combine(Paths.CustomThemes, $"{baseName} {counter}")))
                                counter++;
                            finalName = $"{baseName} {counter}";
                            themePath = Path.Combine(Paths.CustomThemes, finalName);
                        }
                    }

                    await ExtractZipAsync(tempFile, themePath);

                    App.Settings.Prop.SelectedCustomTheme = finalName;
                    App.Settings.Prop.BootstrapperStyle = BootstrapperStyle.CustomDialog;
                    App.Settings.Save();

                    _ = Frontend.ShowMessageBox(string.Format(CultureInfo.InvariantCulture, Strings.Menu_CommunityMods_ThemeInstalled, finalName), MessageBoxImage.Information);
                }
                else
                {
                    string installPath = Path.Combine(Paths.Modifications, finalName);

                    if (Directory.Exists(installPath))
                    {
                        var result = await Frontend.ShowMessageBox(
                            string.Format(CultureInfo.InvariantCulture, Strings.Menu_CommunityMods_Overwrite, baseName),
                            MessageBoxImage.Question,
                            MessageBoxButton.YesNo);

                        if (result == MessageBoxResult.Yes)
                        {
                            Directory.Delete(installPath, true);
                        }
                        else
                        {
                            int counter = 1;
                            while (Directory.Exists(Path.Combine(Paths.Modifications, $"{baseName} {counter}")))
                                counter++;
                            finalName = $"{baseName} {counter}";
                            installPath = Path.Combine(Paths.Modifications, finalName);
                        }
                    }

                    await ExtractZipAsync(tempFile, installPath);

                    var existingMod = App.State.Prop.Mods.FirstOrDefault(m =>
                        string.Equals(m.FolderName, finalName, StringComparison.OrdinalIgnoreCase));

                    if (existingMod != null)
                    {
                        existingMod.Enabled = true;
                        App.Logger.Info($"Enabled existing mod '{finalName}'.");
                    }
                    else
                    {
                        int maxPriority = App.State.Prop.Mods.Count > 0 ? App.State.Prop.Mods.Max(m => m.Priority) : 0;
                        var newMod = new ModConfig
                        {
                            FolderName = finalName,
                            Enabled = true,
                            Priority = maxPriority + 1,
                            Target = ModTarget.Both
                        };
                        App.State.Prop.Mods.Add(newMod);
                        App.Logger.Info($"Added mod '{finalName}' to state.");
                    }

                    App.State.SaveSetting("Mods");
                    _ = Frontend.ShowMessageBox(string.Format(CultureInfo.InvariantCulture, Strings.Menu_CommunityMods_ModInstalled, finalName), MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _ = Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
                App.Logger.Error(ex);
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
                App.Logger.Error($"Unhandled exception: {ex.Message}");
            }
            finally
            {
                App.FrostRPC?.ClearDialog();
            }
        }

        private static readonly HashSet<string> RequiredModFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "content",
            "ExtraContent",
            "PlatformContent"
        };

        private static async Task ExtractZipAsync(string zipPath, string dest)
        {
            await Task.Run(() =>
            {
                string tempExtract = Path.Combine(Path.GetTempPath(), "Froststrap", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempExtract);

                try
                {
                    ZipFile.ExtractToDirectory(zipPath, tempExtract, true);

                    string[] entries = Directory.GetFileSystemEntries(tempExtract);

                    if (Directory.Exists(dest))
                        Directory.Delete(dest, true);
                    Directory.CreateDirectory(dest);

                    if (entries.Length == 1 && Directory.Exists(entries[0]))
                    {
                        string rootDir = entries[0];
                        string rootName = Path.GetFileName(rootDir);

                        if (RequiredModFolders.Contains(rootName))
                        {
                            string target = Path.Combine(dest, rootName);
                            Directory.Move(rootDir, target);
                        }
                        else
                        {
                            CopyDirectoryContents(rootDir, dest);
                        }
                    }
                    else
                    {
                        foreach (string entry in entries)
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
            });
        }

        private static void CopyDirectoryContents(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectoryContents(subDir, destSubDir);
            }
        }

        private static async Task DownloadFileAsync(string url, string path, IProgress<double> progress)
        {
            using var response = await App.HttpClient.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead);
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _searchCts?.Cancel();
                _searchCts?.Dispose();
                _searchCts = null;
            }

            _disposed = true;
        }
    }
}
