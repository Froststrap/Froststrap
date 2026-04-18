using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.AppData;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    internal static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SHObjectProperties(IntPtr hwnd, uint shopObjectType, string pszObjectName, string pszPropertyPage);

        private const uint SHOP_FILEPATH = 0x00000002;

        public static void ShowFileProperties(string filePath, string tabName)
        {
            SHObjectProperties(IntPtr.Zero, SHOP_FILEPATH, filePath, tabName);
        }
    }

    public class ModsPresetsViewModel : NotifyPropertyChangedViewModel
    {
        public event EventHandler? OpenModsEvent;
        public event EventHandler? OpenCommunityModsEvent;
        public event EventHandler? OpenModGeneratorEvent;
        private void OpenMods() => OpenModsEvent?.Invoke(this, EventArgs.Empty);
        private void OpenCommunityMods() => OpenCommunityModsEvent?.Invoke(this, EventArgs.Empty);
        private void OpenModGenerator() => OpenModGeneratorEvent?.Invoke(this, EventArgs.Empty);
        public ICommand OpenModsCommand { get; }
        public ICommand OpenCommunityModsCommand { get; }
        public ICommand OpenModGeneratorCommand { get; }

        private static readonly Dictionary<string, byte[]> FontHeaders = new()
        {
            { "ttf", new byte[] { 0x00, 0x01, 0x00, 0x00 } },
            { "otf", new byte[] { 0x4F, 0x54, 0x54, 0x4F } },
            { "ttc", new byte[] { 0x74, 0x74, 0x63, 0x66 } }
        };

        public ModsPresetsViewModel()
        {
            OpenModsCommand = new RelayCommand(OpenMods);
            OpenCommunityModsCommand = new RelayCommand(OpenCommunityMods);
            OpenModGeneratorCommand = new RelayCommand(OpenModGenerator);

            AddCustomCursorModCommand = new AsyncRelayCommand(AddCustomCursorMod);
            AddCustomShiftlockModCommand = new AsyncRelayCommand(AddCustomShiftlockMod);
            AddCustomDeathSoundCommand = new AsyncRelayCommand(AddCustomDeathSound);
            ChooseCustomFontCommand = new AsyncRelayCommand(ChooseCustomFont);
            RemoveCustomFontCommand = new RelayCommand(RemoveCustomFont);
            RemoveCustomCursorModCommand = new RelayCommand(RemoveCustomCursorMod);
            RemoveCustomShiftlockModCommand = new RelayCommand(RemoveCustomShiftlockMod);
            RemoveCustomDeathSoundCommand = new RelayCommand(RemoveCustomDeathSound);

            LoadCustomCursorSets();

            LoadCursorPathsForSelectedSet();

            NotifyCursorStates();
        }

        public IAsyncRelayCommand AddCustomCursorModCommand { get; }
        public IAsyncRelayCommand AddCustomShiftlockModCommand { get; }
        public IAsyncRelayCommand AddCustomDeathSoundCommand { get; }
        public IAsyncRelayCommand ChooseCustomFontCommand { get; }
        public IRelayCommand RemoveCustomFontCommand { get; }

        public IRelayCommand RemoveCustomCursorModCommand { get; }
        public IRelayCommand RemoveCustomShiftlockModCommand { get; }
        public IRelayCommand RemoveCustomDeathSoundCommand { get; }

        public bool IsCustomFontSet => !string.IsNullOrEmpty(TextFontTask?.NewState);
            
        private async Task ChooseCustomFont()
        {
            var topLevel = GetDialogTopLevel();
            if (topLevel is null)
            {
                App.Logger.WriteLine("ModsViewModel", "Could not find a main window for custom font selection.");
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Custom Font",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Font Files")
                    {
                        Patterns = new[] { "*.ttf", "*.otf", "*.ttc" }
                    }
                },
                AllowMultiple = false
            });

            if (files == null || files.Count == 0) return;

            string? filePath = files[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                string extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
                byte[] fileHeader = await File.ReadAllBytesAsync(filePath);
                byte[] headerSnippet = fileHeader.Take(4).ToArray();

                if (!FontHeaders.TryGetValue(extension, out var expectedHeader) ||
                    !expectedHeader.SequenceEqual(headerSnippet))
                {
                    await Frontend.ShowMessageBox("Custom Font Invalid", MessageBoxImage.Error);
                    return;
                }

                TextFontTask.NewState = filePath;
                OnPropertyChanged(nameof(IsCustomFontSet));
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox($"Error loading font: {ex.Message}", MessageBoxImage.Error);
            }
        }

        private void RemoveCustomFont()
        {
            TextFontTask.NewState = string.Empty;
            OnPropertyChanged(nameof(IsCustomFontSet));
        }

        public ICommand OpenCompatSettingsCommand => new RelayCommand(OpenCompatSettings);
        public ICommand OpenModsFolderCommand => new RelayCommand(OpenModsFolder);
        public ModPresetTask OldAvatarBackgroundTask { get; } = new("OldAvatarBackground", @"ExtraContent\places\Mobile.rbxl", "OldAvatarBackground.rbxl");

        public ModPresetTask OldCharacterSoundsTask { get; } = new("OldCharacterSounds", new()
        {
            { @"content\sounds\action_footsteps_plastic.mp3", "Sounds.OldWalk.mp3"  },
            { @"content\sounds\action_jump.mp3",              "Sounds.OldJump.mp3"  },
            { @"content\sounds\action_get_up.mp3",            "Sounds.OldGetUp.mp3" },
            { @"content\sounds\action_falling.mp3",           "Sounds.Empty.mp3"    },
            { @"content\sounds\action_jump_land.mp3",         "Sounds.Empty.mp3"    },
            { @"content\sounds\action_swim.mp3",              "Sounds.Empty.mp3"    },
            { @"content\sounds\impact_water.mp3",             "Sounds.Empty.mp3"    }
        });

        public EmojiModPresetTask EmojiFontTask { get; } = new();

        public EnumModPresetTask<Enums.CursorType> CursorTypeTask { get; } = new("CursorType", new()
        {
            {
                Enums.CursorType.From2006, new()
                {
                    { @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png",    "Cursor.From2006.ArrowCursor.png"    },
                    { @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png", "Cursor.From2006.ArrowFarCursor.png" }
                }
            },
            {
                Enums.CursorType.From2013, new()
                {
                    { @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png",    "Cursor.From2013.ArrowCursor.png"    },
                    { @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png", "Cursor.From2013.ArrowFarCursor.png" }
                }
            },
            {
                Enums.CursorType.BlackAndWhiteDot, new()
                {
                    { @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png",    "Cursor.BlackAndWhiteDot.ArrowCursor.png"    },
                    { @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png", "Cursor.BlackAndWhiteDot.ArrowFarCursor.png" },
                    { @"content\textures\Cursors\KeyboardMouse\IBeamCursor.png", "Cursor.BlackAndWhiteDot.IBeamCursor.png" }
                }
            },
            {
                Enums.CursorType.PurpleCross, new()
                {
                    { @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png",    "Cursor.PurpleCross.ArrowCursor.png"    },
                    { @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png", "Cursor.PurpleCross.ArrowFarCursor.png" },
                    { @"content\textures\Cursors\KeyboardMouse\IBeamCursor.png", "Cursor.PurpleCross.IBeamCursor.png" }
                }
            }
        });

        public FontModPresetTask TextFontTask { get; } = new();

        private void OpenCompatSettings()
        {
            string path = new RobloxPlayerData().ExecutablePath;

            if (File.Exists(path))
            {
                NativeMethods.ShowFileProperties(path, "Compatibility");
            }
            else
            {
                _ = Frontend.ShowMessageBox(Strings.Common_RobloxNotInstalled, MessageBoxImage.Error);
            }
        }

        private void OpenModsFolder() => Process.Start("explorer.exe", Paths.PresetModifications);

        private static string CursorPath => Path.Combine(Paths.PresetModifications, "Content", "textures", "Cursors", "KeyboardMouse");
        private static string ShiftlockPath => Path.Combine(Paths.PresetModifications, "Content", "textures");
        private static string SoundPath => Path.Combine(Paths.PresetModifications, "Content", "sounds");

        private static readonly string[] CursorFiles = { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" };
        private static readonly string[] ShiftlockFiles = { "MouseLockedCursor.png" };
        private static readonly string[] SoundFiles = { "oof.ogg" };

        public bool HasCustomCursors => CursorFiles.Any(f => File.Exists(Path.Combine(CursorPath, f)));
        public bool HasCustomShiftlock => ShiftlockFiles.Any(f => File.Exists(Path.Combine(ShiftlockPath, f)));
        public bool HasCustomDeathSound => SoundFiles.Any(f => File.Exists(Path.Combine(SoundPath, f)));

        private void RefreshStates()
        {
            OnPropertyChanged(nameof(HasCustomCursors));
            OnPropertyChanged(nameof(HasCustomShiftlock));
            OnPropertyChanged(nameof(HasCustomDeathSound));
        }

        public async Task AddCustomCursorMod() =>
            await AddCustomFileAsync(CursorFiles, CursorPath, "Select Cursor",
                new[] { FilePickerFileTypes.ImagePng }, "cursors", RefreshStates);

        public async Task AddCustomShiftlockMod() =>
            await AddCustomFileAsync(ShiftlockFiles, ShiftlockPath, "Select Shiftlock",
                new[] { FilePickerFileTypes.ImagePng }, "shiftlock", RefreshStates);

        public async Task AddCustomDeathSound() =>
            await AddCustomFileAsync(SoundFiles, SoundPath, "Select Death Sound",
                new[] { new FilePickerFileType("Audio") { Patterns = new[] { "*.ogg" } } }, "death sound", RefreshStates);

        public void RemoveCustomCursorMod() =>
            RemoveCustomFile(CursorFiles, CursorPath, "No custom cursors found.", RefreshStates);

        public void RemoveCustomShiftlockMod() =>
            RemoveCustomFile(ShiftlockFiles, ShiftlockPath, "No shiftlock found.", RefreshStates);

        public void RemoveCustomDeathSound() =>
            RemoveCustomFile(SoundFiles, SoundPath, "No death sound found.", RefreshStates);

        private static TopLevel? GetDialogTopLevel()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return null;

            return desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
        }

        private async Task AddCustomFileAsync(string[] targetFiles, string targetDir, string dialogTitle, FilePickerFileType[] filters, string failureText, Action? postAction)
        {
            var topLevel = GetDialogTopLevel();

            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = dialogTitle,
                FileTypeFilter = filters,
                AllowMultiple = false
            });

            if (files == null || files.Count == 0) return;

            string? sourcePath = files[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(sourcePath)) return;

            try
            {
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                foreach (var name in targetFiles)
                {
                    string destPath = Path.Combine(targetDir, name);
                    File.Copy(sourcePath, destPath, overwrite: true);
                }

                postAction?.Invoke();
            }
            catch (Exception ex)
            {
                _ = Frontend.ShowMessageBox($"Failed to add {failureText}:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        private void RemoveCustomFile(string[] targetFiles, string targetDir, string notFoundMessage, Action postAction)
        {
            bool anyDeleted = false;

            foreach (var name in targetFiles)
            {
                string filePath = Path.Combine(targetDir, name);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        anyDeleted = true;
                    }
                    catch (Exception ex)
                    {
                        _ = Frontend.ShowMessageBox($"Failed to remove {name}:\n{ex.Message}", MessageBoxImage.Error);
                    }
                }
            }

            if (!anyDeleted)
            {
                _ = Frontend.ShowMessageBox(notFoundMessage, MessageBoxImage.Information);
            }

            postAction?.Invoke();
        }

        #region Custom Cursor Set
        public ObservableCollection<CustomCursorSet> CustomCursorSets { get; } = new();

        private int _selectedCustomCursorSetIndex = -1;
        public int SelectedCustomCursorSetIndex
        {
            get => _selectedCustomCursorSetIndex;
            set
            {
                if (_selectedCustomCursorSetIndex != value)
                {
                    _selectedCustomCursorSetIndex = value;
                    OnPropertyChanged(nameof(SelectedCustomCursorSetIndex));
                    OnPropertyChanged(nameof(SelectedCustomCursorSet));
                    OnPropertyChanged(nameof(IsCustomCursorSetSelected));
                    SelectedCustomCursorSetName = SelectedCustomCursorSet?.Name ?? "";

                    LoadCursorPathsForSelectedSet();
                    NotifyCursorStates();
                }
            }
        }

        public CustomCursorSet? SelectedCustomCursorSet =>
            (_selectedCustomCursorSetIndex >= 0 && _selectedCustomCursorSetIndex < CustomCursorSets.Count)
            ? CustomCursorSets[_selectedCustomCursorSetIndex] : null;

        public bool IsCustomCursorSetSelected => SelectedCustomCursorSet is not null;

        private string _selectedCustomCursorSetName = string.Empty;
        public string SelectedCustomCursorSetName
        {
            get => _selectedCustomCursorSetName;
            set
            {
                if (_selectedCustomCursorSetName != value)
                {
                    _selectedCustomCursorSetName = value;
                    OnPropertyChanged(nameof(SelectedCustomCursorSetName));
                }
            }
        }

        public IAsyncRelayCommand<Control> ImportCursorSetCommand => new AsyncRelayCommand<Control>(async c => await ImportCursorSet(c));
        public IAsyncRelayCommand<Control> ExportCursorSetCommand => new AsyncRelayCommand<Control>(async c => await ExportCursorSet(c));
        public IAsyncRelayCommand<Control> AddMouseLockedCursorCommand => new AsyncRelayCommand<Control>(async c => await AddCursorImage("MouseLockedCursor.png", c));
        public IAsyncRelayCommand<Control> AddArrowCursorCommand => new AsyncRelayCommand<Control>(async c => await AddCursorImage("ArrowCursor.png", c));
        public IAsyncRelayCommand<Control> AddArrowFarCursorCommand => new AsyncRelayCommand<Control>(async c => await AddCursorImage("ArrowFarCursor.png", c));
        public IAsyncRelayCommand<Control> AddIBeamCursorCommand => new AsyncRelayCommand<Control>(async c => await AddCursorImage("IBeamCursor.png", c));
        public IRelayCommand GetCurrentCursorSetCommand => new RelayCommand(GetCurrentCursorSet);
        public IRelayCommand AddCustomCursorSetCommand => new RelayCommand(AddCustomCursorSet);
        public IRelayCommand DeleteCustomCursorSetCommand => new RelayCommand(DeleteCustomCursorSet);
        public IRelayCommand RenameCustomCursorSetCommand => new RelayCommand(RenameCustomCursorSet);
        public IRelayCommand ApplyCursorSetCommand => new RelayCommand(ApplyCursorSet);
        public IRelayCommand DeleteCursorCommand => new RelayCommand<string>(DeleteCursorImage);

        private void LoadCustomCursorSets()
        {
            CustomCursorSets.Clear();
            Directory.CreateDirectory(Paths.CustomCursors);

            foreach (var dir in Directory.GetDirectories(Paths.CustomCursors))
            {
                CustomCursorSets.Add(new CustomCursorSet { Name = Path.GetFileName(dir), FolderPath = dir });
            }

            if (CustomCursorSets.Any()) SelectedCustomCursorSetIndex = 0;
        }

        private void AddCustomCursorSet()
        {
            string basePath = Paths.CustomCursors;
            int index = 1;
            string newFolderPath;
            do { newFolderPath = Path.Combine(basePath, $"Custom Cursor Set {index++}"); }
            while (Directory.Exists(newFolderPath));

            try
            {
                Directory.CreateDirectory(newFolderPath);
                CustomCursorSets.Add(new CustomCursorSet { Name = Path.GetFileName(newFolderPath), FolderPath = newFolderPath });
                SelectedCustomCursorSetIndex = CustomCursorSets.Count - 1;
            }
            catch (Exception ex) { App.Logger.WriteException("ModsViewModel::AddCustomCursorSet", ex); }
        }

        private void DeleteCustomCursorSet()
        {
            if (SelectedCustomCursorSet is null) return;
            try
            {
                if (Directory.Exists(SelectedCustomCursorSet.FolderPath)) Directory.Delete(SelectedCustomCursorSet.FolderPath, true);
                CustomCursorSets.Remove(SelectedCustomCursorSet);
                SelectedCustomCursorSetIndex = CustomCursorSets.Count > 0 ? 0 : -1;
            }
            catch (Exception ex) { App.Logger.WriteException("ModsViewModel::DeleteCustomCursorSet", ex); }
        }

        private void RenameCustomCursorSet()
        {
            if (SelectedCustomCursorSet is null || string.IsNullOrWhiteSpace(SelectedCustomCursorSetName) || SelectedCustomCursorSet.Name == SelectedCustomCursorSetName) return;
            if (PathValidator.IsFileNameValid(SelectedCustomCursorSetName) != PathValidator.ValidationResult.Ok) return;

            try
            {
                string newPath = Path.Combine(Paths.CustomCursors, SelectedCustomCursorSetName);
                Directory.Move(SelectedCustomCursorSet.FolderPath, newPath);
                int idx = _selectedCustomCursorSetIndex;
                CustomCursorSets[idx] = new CustomCursorSet { Name = SelectedCustomCursorSetName, FolderPath = newPath };
                SelectedCustomCursorSetIndex = idx;
            }
            catch (Exception ex) { App.Logger.WriteException("ModsViewModel::Rename", ex); }
        }

        private void ApplyCursorSet()
        {
            if (SelectedCustomCursorSet is null)
            {
                _ = Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            string sourceDir = SelectedCustomCursorSet.FolderPath;
            string targetDir = Path.Combine(Paths.PresetModifications, "content", "textures");
            string targetKB = Path.Combine(targetDir, "Cursors", "KeyboardMouse");

            try
            {
                Directory.CreateDirectory(targetKB);
                string[] targets = { Path.Combine(targetDir, "MouseLockedCursor.png"), Path.Combine(targetKB, "ArrowCursor.png"), Path.Combine(targetKB, "ArrowFarCursor.png"), Path.Combine(targetKB, "IBeamCursor.png") };
                foreach (var t in targets) if (File.Exists(t)) File.Delete(t);

                foreach (string file in Directory.GetFiles(SelectedCustomCursorSet.FolderPath, "*.png", SearchOption.AllDirectories))
                {
                    string dest = Path.Combine(targetDir, Path.GetRelativePath(SelectedCustomCursorSet.FolderPath, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest, true);
                }

                _ = Frontend.ShowMessageBox($"Cursor set '{SelectedCustomCursorSet.Name}' applied successfully!", MessageBoxImage.Information);
            }
            catch (Exception ex) { App.Logger.WriteException("ModsViewModel::ApplyCursorSet", ex); }
        }

        private async Task ExportCursorSet(Control? control)
        {
            if (SelectedCustomCursorSet is null) return;

            var topLevel = GetDialogTopLevel() ?? TopLevel.GetTopLevel(control);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Cursor Set",
                SuggestedFileName = $"{SelectedCustomCursorSet.Name}.zip",
                FileTypeChoices = new[] { new FilePickerFileType("Zip Archive") { Patterns = new[] { "*.zip" } } }
            });

            if (file is null) return;

            try
            {
                string destinationPath = file.TryGetLocalPath() ?? string.Empty;
                if (string.IsNullOrEmpty(destinationPath)) return;

                if (File.Exists(destinationPath)) File.Delete(destinationPath);

                ZipFile.CreateFromDirectory(SelectedCustomCursorSet.FolderPath, destinationPath);

                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{destinationPath}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::ExportCursorSet", ex);
            }
        }

        private async Task ImportCursorSet(Control? control)
        {
            if (SelectedCustomCursorSet is null) return;

            var topLevel = GetDialogTopLevel() ?? TopLevel.GetTopLevel(control);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Cursor Set",
                FileTypeFilter = new[] { new FilePickerFileType("Zip Archive") { Patterns = new[] { "*.zip" } } },
                AllowMultiple = false
            });

            if (files == null || files.Count == 0) return;

            try
            {
                string? zipPath = files[0].TryGetLocalPath();
                if (string.IsNullOrEmpty(zipPath)) return;

                string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                ZipFile.ExtractToDirectory(zipPath, tempPath);

                foreach (string file in Directory.GetFiles(tempPath, "*.png", SearchOption.AllDirectories))
                {
                    string? dest = GetCursorTargetPath(Path.GetFileName(file));
                    if (dest != null)
                    {
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Copy(file, dest);
                    }
                }

                Directory.Delete(tempPath, true);

                LoadCursorPathsForSelectedSet();
                NotifyCursorStates();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::ImportCursorSet", ex);
            }
        }

        private async Task AddCursorImage(string fileName, Control? control)
        {
            if (SelectedCustomCursorSet is null) return;

            var topLevel = GetDialogTopLevel() ?? TopLevel.GetTopLevel(control);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Cursor Image",
                FileTypeFilter = new[] { FilePickerFileTypes.ImagePng },
                AllowMultiple = false
            });

            if (files == null || files.Count == 0) return;

            try
            {
                string? sourcePath = files[0].TryGetLocalPath();
                string? dest = GetCursorTargetPath(fileName);

                if (sourcePath != null && dest != null)
                {
                    File.Copy(sourcePath, dest, true);
                    UpdateCursorPathAndPreview(fileName, dest);
                    NotifyCursorStates();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::AddCursor", ex);
            }
        }

        private void GetCurrentCursorSet()
        {
            if (SelectedCustomCursorSet is null)
            {
                _ = Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            string sourceMouseLocked = Path.Combine(Paths.PresetModifications, "content", "textures", "MouseLockedCursor.png");
            string sourceKeyboardMouse = Path.Combine(Paths.PresetModifications, "content", "textures", "Cursors", "KeyboardMouse");

            string targetBase = SelectedCustomCursorSet.FolderPath;
            string targetMouseLocked = Path.Combine(targetBase, "MouseLockedCursor.png");
            string targetKeyboardMouse = Path.Combine(targetBase, "Cursors", "KeyboardMouse");

            try
            {
                Directory.CreateDirectory(targetBase);
                Directory.CreateDirectory(targetKeyboardMouse);

                var filesToDelete = new[]
                {
                    targetMouseLocked,
                    Path.Combine(targetKeyboardMouse, "ArrowCursor.png"),
                    Path.Combine(targetKeyboardMouse, "ArrowFarCursor.png"),
                    Path.Combine(targetKeyboardMouse, "IBeamCursor.png")
                };

                foreach (var file in filesToDelete)
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }

                if (File.Exists(sourceMouseLocked))
                    File.Copy(sourceMouseLocked, targetMouseLocked, overwrite: true);

                if (Directory.Exists(sourceKeyboardMouse))
                {
                    foreach (var fileName in new[] { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" })
                    {
                        string source = Path.Combine(sourceKeyboardMouse, fileName);
                        string dest = Path.Combine(targetKeyboardMouse, fileName);

                        if (File.Exists(source))
                            File.Copy(source, dest, overwrite: true);
                    }
                }

                _ = Frontend.ShowMessageBox("Current cursor set copied into selected folder.", MessageBoxImage.Information);
                NotifyCursorStates();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::GetCurrentCursorSet", ex);
                _ = Frontend.ShowMessageBox($"Failed to get current cursor set:\n{ex.Message}", MessageBoxImage.Error);
            }

            LoadCursorPathsForSelectedSet();
            NotifyCursorStates();
        }

        private TopLevel? GetTopLevel()
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            return TopLevel.GetTopLevel(lifetime?.MainWindow);
        }

        private void DeleteCursorImage(string? fileName)
        {
            if (fileName is null) return;
            string? path = GetCursorTargetPath(fileName);
            if (path != null && File.Exists(path)) { File.Delete(path); UpdateCursorPathAndPreview(fileName, ""); NotifyCursorStates(); }
        }

        private string? GetCursorTargetPath(string fileName)
        {
            if (SelectedCustomCursorSet is null) return null;
            string folder = fileName == "MouseLockedCursor.png" ? SelectedCustomCursorSet.FolderPath : Path.Combine(SelectedCustomCursorSet.FolderPath, "Cursors", "KeyboardMouse");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, fileName);
        }

        private void NotifyCursorStates()
        {
            OnPropertyChanged(nameof(HasShiftlockCursor));
            OnPropertyChanged(nameof(HasArrowCursor));
            OnPropertyChanged(nameof(HasArrowFarCursor));
            OnPropertyChanged(nameof(HasIBeamCursor));
        }

        public bool HasShiftlockCursor => File.Exists(GetCursorTargetPath("MouseLockedCursor.png"));
        public bool HasArrowCursor => File.Exists(GetCursorTargetPath("ArrowCursor.png"));
        public bool HasArrowFarCursor => File.Exists(GetCursorTargetPath("ArrowFarCursor.png"));
        public bool HasIBeamCursor => File.Exists(GetCursorTargetPath("IBeamCursor.png"));

        private void UpdateCursorPathAndPreview(string fileName, string path)
        {
            var image = LoadImageSafely(path);
            if (fileName == "MouseLockedCursor.png") { ShiftlockCursorSelectedPath = path; ShiftlockCursorPreview = image; OnPropertyChanged(nameof(ShiftlockCursorPreview)); }
            else if (fileName == "ArrowCursor.png") { ArrowCursorSelectedPath = path; ArrowCursorPreview = image; OnPropertyChanged(nameof(ArrowCursorPreview)); }
            else if (fileName == "ArrowFarCursor.png") { ArrowFarCursorSelectedPath = path; ArrowFarCursorPreview = image; OnPropertyChanged(nameof(ArrowFarCursorPreview)); }
            else if (fileName == "IBeamCursor.png") { IBeamCursorSelectedPath = path; IBeamCursorPreview = image; OnPropertyChanged(nameof(IBeamCursorPreview)); }
        }

        private void LoadCursorPathsForSelectedSet()
        {
            string baseDir = SelectedCustomCursorSet?.FolderPath ?? "";
            string kbDir = string.IsNullOrEmpty(baseDir) ? "" : Path.Combine(baseDir, "Cursors", "KeyboardMouse");
            UpdateCursorPathAndPreview("MouseLockedCursor.png", string.IsNullOrEmpty(baseDir) ? "" : Path.Combine(baseDir, "MouseLockedCursor.png"));
            UpdateCursorPathAndPreview("ArrowCursor.png", string.IsNullOrEmpty(kbDir) ? "" : Path.Combine(kbDir, "ArrowCursor.png"));
            UpdateCursorPathAndPreview("ArrowFarCursor.png", string.IsNullOrEmpty(kbDir) ? "" : Path.Combine(kbDir, "ArrowFarCursor.png"));
            UpdateCursorPathAndPreview("IBeamCursor.png", string.IsNullOrEmpty(kbDir) ? "" : Path.Combine(kbDir, "IBeamCursor.png"));
        }

        private static Bitmap? LoadImageSafely(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return new Bitmap(stream);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string ShiftlockCursorSelectedPath { get; set; } = "";
        public string ArrowCursorSelectedPath { get; set; } = "";
        public string ArrowFarCursorSelectedPath { get; set; } = "";
        public string IBeamCursorSelectedPath { get; set; } = "";

        public IImage? ShiftlockCursorPreview { get; set; }
        public IImage? ArrowCursorPreview { get; set; }
        public IImage? ArrowFarCursorPreview { get; set; }
        public IImage? IBeamCursorPreview { get; set; }
        #endregion
    }
}