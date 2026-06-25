using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;

namespace Froststrap.UI.ViewModels.Settings.Mods
{
    public interface IModsDialogService
    {
        Task OpenCommunityModsAsync();
        Task OpenPresetModsAsync();
        Task OpenModGeneratorAsync();
    }

    public partial class ModsViewModel : NotifyPropertyChangedViewModel
    {
        private readonly IModsDialogService _dialogService;

        private ModConfig? _selectedMod;
        public ModConfig? SelectedMod
        {
            get => _selectedMod;
            set
            {
                if (SetProperty(ref _selectedMod, value))
                {
                    NewName = value?.FolderName ?? string.Empty;
                    OnPropertyChanged(nameof(HasMods));
                }
            }
        }

        private string _newName = string.Empty;
        public string NewName
        {
            get => _newName;
            set => SetProperty(ref _newName, value);
        }

        private bool _isDragOver;
        public bool IsDragOver
        {
            get => _isDragOver;
            set => SetProperty(ref _isDragOver, value);
        }

        public bool HasMods => Modifications.Count > 0;

        public ObservableCollection<ModConfig> Modifications { get; set; } = [];

        public static IEnumerable<ModTarget> TargetOptions => Enum.GetValues<ModTarget>();

        public ICommand MoveUpCommand => new RelayCommand<ModConfig>(MoveUp);
        public ICommand MoveDownCommand => new RelayCommand<ModConfig>(MoveDown);
        public ICommand DeleteModCommand => new RelayCommand<ModConfig>(DeleteMod);
        public ICommand OpenModFolderCommand => new RelayCommand<ModConfig>(OpenFolder);
        public ICommand ImportFolderCommand => new AsyncRelayCommand<object>(ImportFolderAsync);
        public ICommand ImportZipCommand => new AsyncRelayCommand<object>(ImportZipAsync);
        public ICommand RenameModCommand => new RelayCommand(RenameMod);
        public ICommand ExportModCommand => new AsyncRelayCommand<ModConfig>(ExportModAsync);

        public ModsViewModel()
            : this(new DefaultModsDialogService())
        {
        }

        public ModsViewModel(IModsDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            LoadModifications();
        }

        public ICommand OpenModGeneratorCommand => new AsyncRelayCommand(async () => await _dialogService.OpenModGeneratorAsync());
        public ICommand OpenCommunityModsCommand => new AsyncRelayCommand(async () => await _dialogService.OpenCommunityModsAsync());
        public ICommand OpenPresetModsCommand => new AsyncRelayCommand(async () => await _dialogService.OpenPresetModsAsync());

        private void OpenFolder(ModConfig? mod)
        {
            if (mod == null) return;
            string folderPath = Path.Combine(Paths.Modifications, mod.FolderName);
            if (Directory.Exists(folderPath))
                Utilities.ShellExecute(folderPath, select: true);
            else
                _ = Frontend.ShowMessageBox(string.Format(Strings.Menu_Mods_FolderDosentExist, mod.FolderName), MessageBoxImage.Error, MessageBoxButton.OK);
        }

        [RelayCommand]
        public void AddMod()
        {
            string modsFolder = Paths.Modifications;
            string baseName = "New Mod";
            string folderName = baseName;
            int counter = 1;

            while (Modifications.Any(x => x.FolderName.Equals(folderName, StringComparison.OrdinalIgnoreCase)) ||
                   Directory.Exists(Path.Combine(modsFolder, folderName)))
            {
                folderName = $"{baseName} {counter}";
                counter++;
            }

            if (!Directory.Exists(modsFolder))
                Directory.CreateDirectory(modsFolder);

            Directory.CreateDirectory(Path.Combine(modsFolder, folderName));

            var newMod = new ModConfig
            {
                FolderName = folderName,
                Target = ModTarget.Both,
                Enabled = true,
                Priority = Modifications.Count > 0 ? Modifications.Max(x => x.Priority) + 1 : 1
            };
            Modifications.Add(newMod);
            UpdatePriorities();
            OnPropertyChanged(nameof(HasMods));
        }

        private void LoadModifications()
        {
            var sortedMods = App.State.Prop.Mods.OrderBy(x => x.Priority).ToList();
            Modifications = new ObservableCollection<ModConfig>(sortedMods);
            SelectedMod = Modifications.FirstOrDefault();
        }

        public static void CopyDirectory(string sourceDir, string destDir, bool overwrite)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            Directory.CreateDirectory(destDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destDir, file.Name);
                if (File.Exists(targetFilePath)) Filesystem.AssertReadOnly(targetFilePath);
                file.CopyTo(targetFilePath, overwrite);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
                CopyDirectory(subDir.FullName, Path.Combine(destDir, subDir.Name), overwrite);
        }

        public void UpdatePriorities()
        {
            for (int i = 0; i < Modifications.Count; i++)
                Modifications[i].Priority = i + 1;

            App.State.Prop.Mods = [.. Modifications];
            App.State.SaveSetting("Mods");
        }

        private void MoveUp(ModConfig? mod)
        {
            if (mod == null) return;
            int index = Modifications.IndexOf(mod);
            if (index > 0)
            {
                Modifications.Move(index, index - 1);
                UpdatePriorities();
                SelectedMod = mod;
            }
        }

        private void MoveDown(ModConfig? mod)
        {
            if (mod == null) return;
            int index = Modifications.IndexOf(mod);
            if (index < Modifications.Count - 1)
            {
                Modifications.Move(index, index + 1);
                UpdatePriorities();
                SelectedMod = mod;
            }
        }

        private async void DeleteMod(ModConfig? mod)
        {
            if (mod == null || string.IsNullOrWhiteSpace(mod.FolderName)) return;

            string modPath = Path.Combine(Paths.Modifications, mod.FolderName);
            if (Path.GetFullPath(modPath) == Path.GetFullPath(Paths.Modifications))
                return;

            var result = await Frontend.ShowMessageBox(string.Format(Strings.Menu_Mods_DeleteMod, mod.FolderName), MessageBoxImage.Warning, MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (Directory.Exists(modPath)) Directory.Delete(modPath, true);
                Modifications.Remove(mod);
                UpdatePriorities();
                OnPropertyChanged(nameof(HasMods));
                if (SelectedMod == mod) SelectedMod = null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ModsViewModel::Delete", ex.Message);
                _ = Frontend.ShowMessageBox($"Failed to delete mod: {ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
            }
        }

        private void RenameMod()
        {
            if (SelectedMod == null || string.IsNullOrWhiteSpace(NewName))
                return;

            string oldName = SelectedMod.FolderName;
            string newName = NewName.Trim();

            if (oldName == newName) return;

            string safeName = string.Join("_", newName.Split(Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrWhiteSpace(safeName))
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_Mods_InvalidFolderName, MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            if (Modifications.Any(m => m.FolderName.Equals(safeName, StringComparison.OrdinalIgnoreCase) && m != SelectedMod))
            {
                _ = Frontend.ShowMessageBox(string.Format(Strings.Menu_Mods_AlreadyExist, safeName), MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            string oldPath = Path.Combine(Paths.Modifications, oldName);
            string newPath = Path.Combine(Paths.Modifications, safeName);

            try
            {
                if (Directory.Exists(oldPath))
                    Directory.Move(oldPath, newPath);

                SelectedMod.FolderName = safeName;
                UpdatePriorities();
                NewName = safeName;
                OnPropertyChanged(nameof(HasMods));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ModsViewModel::RenameMod", ex.Message);
                _ = Frontend.ShowMessageBox($"Failed to rename mod: {ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
            }
        }


        private static readonly HashSet<string> RequiredModFolders = ["content", "ExtraContent", "PlatformContent"];

        private static string? ValidateModStructure(string rootDir)
        {
            foreach (string folder in RequiredModFolders)
                if (Directory.Exists(Path.Combine(rootDir, folder)))
                    return rootDir;

            foreach (string subDir in Directory.GetDirectories(rootDir))
                foreach (string folder in RequiredModFolders)
                    if (Directory.Exists(Path.Combine(subDir, folder)))
                        return subDir;

            return null;
        }

        private async Task ImportFolderAsync(object? parameter)
        {
            if (parameter is not Avalonia.Visual control)
            {
                return;
            }

            if (TopLevel.GetTopLevel(control) is not TopLevel topLevel)
            {
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { AllowMultiple = false, Title = "Select Mod Folder" });
            if (folders.Count == 0) return;

            string folderPath = folders[0].Path.LocalPath;
            await ImportModFromSource(folderPath, isZip: false);
        }

        private async Task ImportZipAsync(object? parameter)
        {
            if (parameter is not Avalonia.Visual control)
            {
                return;
            }

            if (TopLevel.GetTopLevel(control) is not TopLevel topLevel)
            {
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    AllowMultiple = true,
                    Title = "Select Mod ZIP Archive",
                    FileTypeFilter = [new FilePickerFileType("Zip Files") { Patterns = ["*.zip"] }]
                });
            if (files.Count == 0) return;

            string zipPath = files[0].Path.LocalPath;
            string zipFileName = Path.GetFileNameWithoutExtension(zipPath);
            string tempDir = Path.Combine(Path.GetTempPath(), "Froststrap_ModImport_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                await Task.Run(() =>
                {
                    var fastZip = new FastZip();
                    fastZip.ExtractZip(zipPath, tempDir, null);
                });
                await ImportModFromSource(tempDir, isZip: true, baseNameOverride: zipFileName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ModsViewModel::ImportZip", ex.Message);
                await Frontend.ShowMessageBox($"Failed to extract ZIP: {ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        public async Task ImportFromPaths(string[] paths)
        {
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    await ImportModFromSource(path, isZip: false, baseNameOverride: null);
                }
                else if (File.Exists(path) &&
                         Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    string zipFileName = Path.GetFileNameWithoutExtension(path);
                    string tempDir = Path.Combine(Path.GetTempPath(), "Froststrap_ModImport_" + Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);

                    try
                    {
                        await Task.Run(() =>
                        {
                            var fastZip = new FastZip();
                            fastZip.ExtractZip(path, tempDir, null);
                        });
                        await ImportModFromSource(tempDir, isZip: true, baseNameOverride: zipFileName);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("ModsViewModel::ImportFromPaths", ex.Message);
                        await Frontend.ShowMessageBox($"Failed to extract ZIP: {ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
                    }
                    finally
                    {
                        try { Directory.Delete(tempDir, true); } catch { }
                    }
                }
                else
                {
                    await Frontend.ShowMessageBox(string.Format(Strings.Menu_Mods_UnsupportedFile, path), MessageBoxImage.Warning, MessageBoxButton.OK);
                }
            }
        }

        private async Task ImportModFromSource(string sourcePath, bool isZip, string? baseNameOverride = null)
        {
            string? modRoot = ValidateModStructure(sourcePath);
            if (modRoot == null)
            {
                await Frontend.ShowMessageBox(
                    Strings.Menu_Mods_InvalidModFolders,
                    MessageBoxImage.Error, MessageBoxButton.OK);
                return;
            }

            string baseName;
            if (!string.IsNullOrEmpty(baseNameOverride))
                baseName = baseNameOverride;
            else if (isZip)
                baseName = Path.GetFileNameWithoutExtension(sourcePath);
            else
                baseName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            string safeName = string.Join("_", baseName.Split(Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "ImportedMod";

            string modsFolder = Paths.Modifications;
            string newFolderName = safeName;
            int counter = 1;
            while (Directory.Exists(Path.Combine(modsFolder, newFolderName)) ||
                   Modifications.Any(x => x.FolderName.Equals(newFolderName, StringComparison.OrdinalIgnoreCase)))
            {
                newFolderName = $"{safeName} {counter}";
                counter++;
            }

            string destFolder = Path.Combine(modsFolder, newFolderName);
            Directory.CreateDirectory(destFolder);
            CopyDirectory(modRoot, destFolder, overwrite: false);

            var newMod = new ModConfig
            {
                FolderName = newFolderName,
                Target = ModTarget.Both,
                Enabled = true,
                Priority = Modifications.Count > 0 ? Modifications.Max(x => x.Priority) + 1 : 0
            };
            Modifications.Add(newMod);
            UpdatePriorities();
            OnPropertyChanged(nameof(HasMods));

            await Frontend.ShowMessageBox(string.Format(Strings.Menu_Mods_Imported, newFolderName), MessageBoxImage.Information, MessageBoxButton.OK);
        }

        private async Task ExportModAsync(ModConfig? mod)
        {
            if (mod == null || string.IsNullOrWhiteSpace(mod.FolderName))
            {
                await Frontend.ShowMessageBox("Please select a mod to export.", MessageBoxImage.Warning);
                return;
            }

            string modPath = Path.Combine(Paths.Modifications, mod.FolderName);
            if (!Directory.Exists(modPath))
            {
                await Frontend.ShowMessageBox($"Mod folder '{mod.FolderName}' does not exist.", MessageBoxImage.Error);
                return;
            }

            Window? mainWindow = null;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                mainWindow = desktop.MainWindow;

            if (mainWindow == null && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop2)
                mainWindow = desktop2.Windows.FirstOrDefault(w => w.IsActive) as Window;

            if (mainWindow == null)
                return;

            var file = await mainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Mod",
                SuggestedFileName = $"{mod.FolderName}.zip",
                DefaultExtension = ".zip",
                FileTypeChoices =
                [
                    new FilePickerFileType("Zip Archive") { Patterns = ["*.zip"] }
                ]
            });

            if (file == null) return;

            try
            {
                await Task.Run(() =>
                {
                    if (File.Exists(file.Path.LocalPath))
                        File.Delete(file.Path.LocalPath);

                    System.IO.Compression.ZipFile.CreateFromDirectory(modPath, file.Path.LocalPath);
                });

                await Frontend.ShowMessageBox($"Mod '{mod.FolderName}' exported successfully!", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ModsViewModel::ExportMod", ex.Message);
                await Frontend.ShowMessageBox($"Failed to export mod: {ex.Message}", MessageBoxImage.Error);
            }
        }
    }

    public class DefaultModsDialogService : IModsDialogService
    {
        public Task OpenCommunityModsAsync() => Task.CompletedTask;
        public Task OpenPresetModsAsync() => Task.CompletedTask;
        public Task OpenModGeneratorAsync() => Task.CompletedTask;
    }
}