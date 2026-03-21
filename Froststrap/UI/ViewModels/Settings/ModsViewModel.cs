using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;

namespace Froststrap.UI.ViewModels.Settings
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

        public ObservableCollection<ModConfig> Modifications { get; set; } = new();

        public ICommand MoveUpCommand => new RelayCommand<ModConfig>(MoveUp);
        public ICommand MoveDownCommand => new RelayCommand<ModConfig>(MoveDown);
        public ICommand DeleteModCommand => new RelayCommand<ModConfig>(DeleteMod);
        public ICommand OpenModFolderCommand => new RelayCommand<ModConfig>(OpenFolder);

        public ModsViewModel() 
            : this(new DefaultModsDialogService())
        {
        }

        public ModsViewModel(IModsDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            SyncDiskWithState();
            LoadModifications();
        }

        public ICommand OpenModGeneratorCommand => new AsyncRelayCommand(async () =>
        {
            App.Logger.WriteLine("ModsViewModel", "OpenModGeneratorCommand executed");
            await _dialogService.OpenModGeneratorAsync();
            App.Logger.WriteLine("ModsViewModel", "OpenModGeneratorCommand completed");
        });

        public ICommand OpenCommunityModsCommand => new AsyncRelayCommand(async () =>
        {
            App.Logger.WriteLine("ModsViewModel", "OpenCommunityModsCommand executed");
            await _dialogService.OpenCommunityModsAsync();
            App.Logger.WriteLine("ModsViewModel", "OpenCommunityModsCommand completed");
        });

        public ICommand OpenPresetModsCommand => new AsyncRelayCommand(async () =>
        {
            App.Logger.WriteLine("ModsViewModel", "OpenPresetModsCommand executed");
            await _dialogService.OpenPresetModsAsync();
            App.Logger.WriteLine("ModsViewModel", "OpenPresetModsCommand completed");
        });

        private void OpenFolder(ModConfig? mod)
        {
            if (mod == null) return;

            string folderPath = Path.Combine(Paths.Modifications, mod.FolderName);

            if (Directory.Exists(folderPath))
            {
                Process.Start("explorer.exe", folderPath);
            }
            else
            {
                _ = Frontend.ShowMessageBox($"The folder for '{mod.FolderName}' no longer exists.", MessageBoxImage.Error, MessageBoxButton.OK);
            }
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

            Modifications.Add(new ModConfig
            {
                FolderName = folderName,
                Target = "Both"
            });
        }

        private void SyncDiskWithState()
        {
            if (!Directory.Exists(Paths.Modifications))
            {
                Directory.CreateDirectory(Paths.Modifications);
                return;
            }

            var physicalFolders = Directory.GetDirectories(Paths.Modifications)
                .Select(Path.GetFileName)
                .Where(x => !string.IsNullOrEmpty(x))
                .ToHashSet();

            App.State.Prop.Mods.RemoveAll(x => !physicalFolders.Contains(x.FolderName));

            foreach (var folder in physicalFolders)
            {
                if (!App.State.Prop.Mods.Any(x => x.FolderName == folder))
                {
                    App.State.Prop.Mods.Add(new ModConfig
                    {
                        FolderName = folder!,
                        Target = "Both",
                        Priority = App.State.Prop.Mods.Count
                    });
                }
            }

            App.State.Save();
        }

        private void LoadModifications()
        {
            var sortedMods = App.State.Prop.Mods.OrderBy(x => x.Priority).ToList();
            Modifications = new ObservableCollection<ModConfig>(sortedMods);
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
            {
                CopyDirectory(subDir.FullName, Path.Combine(destDir, subDir.Name), overwrite);
            }
        }

        public bool RenameMod(string oldName, string newName)
        {
            string safeName = string.Join("_", newName.Split(Path.GetInvalidFileNameChars())).Trim();

            if (string.IsNullOrWhiteSpace(safeName) || safeName == oldName)
                return false;

            string oldPath = Path.Combine(Paths.Modifications, oldName);
            string newPath = Path.Combine(Paths.Modifications, safeName);

            try
            {
                if (!Directory.Exists(oldPath)) return false;

                if (Directory.Exists(newPath))
                {
                    _ = Frontend.ShowMessageBox($"A mod named '{safeName}' already exists.", MessageBoxImage.Error, MessageBoxButton.OK);
                    return false;
                }

                Directory.Move(oldPath, newPath);

                var mod = Modifications.FirstOrDefault(x => x.FolderName == oldName);
                if (mod != null)
                {
                    mod.FolderName = safeName;
                    UpdatePriorities();
                }
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ModsViewModel::RenameMod", $"Rename failed: {ex.Message}");
                return false;
            }
        }

        private void UpdatePriorities()
        {
            for (int i = 0; i < Modifications.Count; i++)
            {
                Modifications[i].Priority = i;
            }

            App.State.Prop.Mods = Modifications.ToList();
        }

        private void MoveUp(ModConfig? mod)
        {
            if (mod == null) return;
            int index = Modifications.IndexOf(mod);
            if (index > 0)
            {
                Modifications.Move(index, index - 1);
                UpdatePriorities();
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
            }
        }

        private async void DeleteMod(ModConfig? mod)
        {
            if (mod == null) return;

            var result = await Frontend.ShowMessageBox($"Delete '{mod.FolderName}' permanently?", MessageBoxImage.Warning, MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                string path = Path.Combine(Paths.Modifications, mod.FolderName);
                if (Directory.Exists(path)) Directory.Delete(path, true);

                Modifications.Remove(mod);
                UpdatePriorities();
            }
            catch (Exception ex) { App.Logger.WriteLine("ModsViewModel::Delete", ex.Message); }
        }

        public async Task ProcessDroppedFiles(string[] paths)
        {
            foreach (var path in paths)
            {
                string modName = Path.GetFileNameWithoutExtension(path) ?? "UnknownMod";
                string targetDir = Path.Combine(Paths.Modifications, modName);

                try
                {
                    if (Directory.Exists(path))
                    {
                        CopyDirectory(path, targetDir, true);
                    }
                    else if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Directory.Exists(targetDir))
                            Directory.CreateDirectory(targetDir);

                        new FastZip().ExtractZip(path, targetDir, null);
                    }
                    else
                    {
                        continue;
                    }

                    if (Modifications.Any(x => x.FolderName.Equals(modName, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var newMod = new ModConfig
                    {
                        FolderName = modName,
                        Target = "Both",
                        Priority = Modifications.Count > 0 ? Modifications.Max(x => x.Priority) + 1 : 0
                    };

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Modifications.Add(newMod);
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("ModsViewModel::ProcessDroppedFiles", $"Error: {ex.Message}");
                }
            }

            UpdatePriorities();
            App.State.Save();
        }
    }

    public class DefaultModsDialogService : IModsDialogService
    {
        public Task OpenCommunityModsAsync()
        {
            return Task.CompletedTask;
        }

        public Task OpenPresetModsAsync()
        {
            return Task.CompletedTask;
        }

        public Task OpenModGeneratorAsync()
        {
            return Task.CompletedTask;
        }
    }
}