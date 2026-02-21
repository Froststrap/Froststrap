using Bloxstrap.AppData;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO.Compression;
using Bloxstrap.Integrations;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class ModsPresetsViewModel : NotifyPropertyChangedViewModel
    {
        public event EventHandler? OpenModsEvent;
        public event EventHandler? OpenCommunityModsEvent;
        public event EventHandler? OpenModGeneratorEvent;
        private void OpenMods() => OpenModsEvent?.Invoke(this, EventArgs.Empty);
        private void OpenCommunityMods() => OpenCommunityModsEvent?.Invoke(this, EventArgs.Empty);
        private void OpenModGenerator() => OpenModGeneratorEvent?.Invoke(this, EventArgs.Empty);
        public ICommand OpenModsCommand => new RelayCommand(OpenMods);
        public ICommand OpenCommunityModsCommand => new RelayCommand(OpenCommunityMods);
        public ICommand OpenModGeneratorCommand => new RelayCommand(OpenModGenerator);

        private static readonly Dictionary<string, byte[]> FontHeaders = new()
        {
            { "ttf", new byte[] { 0x00, 0x01, 0x00, 0x00 } },
            { "otf", new byte[] { 0x4F, 0x54, 0x54, 0x4F } },
            { "ttc", new byte[] { 0x74, 0x74, 0x63, 0x66 } }
        };

        public ModsPresetsViewModel()
        {
            LoadCustomCursorSets();

            LoadCursorPathsForSelectedSet();

            NotifyCursorStates();
        }

        private void ManageCustomFont()
        {
            if (IsCustomFontSet)
            {
                TextFontTask.NewState = string.Empty;
            }
            else
            {
                var dialog = new OpenFileDialog { Filter = $"{Strings.Menu_FontFiles}|*.ttf;*.otf;*.ttc" };

                if (dialog.ShowDialog() != true) return;

                string type = Path.GetExtension(dialog.FileName).TrimStart('.').ToLowerInvariant();
                byte[] fileHeader = File.ReadAllBytes(dialog.FileName).Take(4).ToArray();

                if (!FontHeaders.TryGetValue(type, out var expectedHeader) || !expectedHeader.SequenceEqual(fileHeader))
                {
                    Frontend.ShowMessageBox("Custom Font Invalid", MessageBoxImage.Error);
                    return;
                }

                TextFontTask.NewState = dialog.FileName;
            }

            OnPropertyChanged(nameof(IsCustomFontSet));
        }

        public ICommand AddCustomCursorModCommand => new RelayCommand(AddCustomCursorMod);

        public ICommand RemoveCustomCursorModCommand => new RelayCommand(RemoveCustomCursorMod);

        public ICommand AddCustomShiftlockModCommand => new RelayCommand(AddCustomShiftlockMod);

        public ICommand RemoveCustomShiftlockModCommand => new RelayCommand(RemoveCustomShiftlockMod);
        public ICommand AddCustomDeathSoundCommand => new RelayCommand(AddCustomDeathSound);
        public ICommand RemoveCustomDeathSoundCommand => new RelayCommand(RemoveCustomDeathSound);

        public bool IsCustomFontSet => !string.IsNullOrEmpty(TextFontTask.NewState);

        public ICommand ManageCustomFontCommand => new RelayCommand(ManageCustomFont);

        public ICommand OpenCompatSettingsCommand => new RelayCommand(OpenCompatSettings);
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
                PInvoke.SHObjectProperties(HWND.Null, SHOP_TYPE.SHOP_FILEPATH, path, "Compatibility");
            else
                Frontend.ShowMessageBox(Strings.Common_RobloxNotInstalled, MessageBoxImage.Error);

        }

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

        public void AddCustomCursorMod() =>
            AddCustomFile(CursorFiles, CursorPath, "Select Cursor", "PNG (*.png)|*.png", "cursors", RefreshStates);

        public void RemoveCustomCursorMod() =>
            RemoveCustomFile(CursorFiles, CursorPath, "No custom cursors found.", RefreshStates);

        public void AddCustomShiftlockMod() =>
            AddCustomFile(ShiftlockFiles, ShiftlockPath, "Select Shiftlock", "PNG (*.png)|*.png", "shiftlock", RefreshStates);

        public void RemoveCustomShiftlockMod() =>
            RemoveCustomFile(ShiftlockFiles, ShiftlockPath, "No shiftlock found.", RefreshStates);

        public void AddCustomDeathSound() =>
            AddCustomFile(SoundFiles, SoundPath, "Select Death Sound", "OGG (*.ogg)|*.ogg", "death sound", RefreshStates);

        public void RemoveCustomDeathSound() =>
            RemoveCustomFile(SoundFiles, SoundPath, "No death sound found.", RefreshStates);

        private void AddCustomFile(string[] targetFiles, string targetDir, string dialogTitle, string filter, string failureText, Action postAction)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Title = dialogTitle
            };

            if (dialog.ShowDialog() != true)
                return;

            string sourcePath = dialog.FileName;
            Directory.CreateDirectory(targetDir);

            try
            {
                foreach (var name in targetFiles)
                {
                    string destPath = Path.Combine(targetDir, name);
                    File.Copy(sourcePath, destPath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to add {failureText}:\n{ex.Message}", MessageBoxImage.Error);
                return;
            }

            postAction?.Invoke();
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
                        Frontend.ShowMessageBox($"Failed to remove {name}:\n{ex.Message}", MessageBoxImage.Error);
                    }
                }
            }

            if (!anyDeleted)
            {
                Frontend.ShowMessageBox(notFoundMessage, MessageBoxImage.Information);
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

        public ICommand AddCustomCursorSetCommand => new RelayCommand(AddCustomCursorSet);
        public ICommand DeleteCustomCursorSetCommand => new RelayCommand(DeleteCustomCursorSet);
        public ICommand RenameCustomCursorSetCommand => new RelayCommand(RenameCustomCursorSet);
        public ICommand ApplyCursorSetCommand => new RelayCommand(ApplyCursorSet);
        public ICommand ImportCursorSetCommand => new RelayCommand(ImportCursorSet);
        public ICommand ExportCursorSetCommand => new RelayCommand(ExportCursorSet);
        public ICommand AddCursorCommand => new RelayCommand<string>(AddCursorImage);
        public ICommand DeleteCursorCommand => new RelayCommand<string>(DeleteCursorImage);

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
                Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
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

                Frontend.ShowMessageBox($"Cursor set '{SelectedCustomCursorSet.Name}' applied successfully!", MessageBoxImage.Information);
            }
            catch (Exception ex) { App.Logger.WriteException("ModsViewModel::ApplyCursorSet", ex); }
        }

        private void ExportCursorSet()
        {
            if (SelectedCustomCursorSet is null) return;
            var dialog = new SaveFileDialog { FileName = $"{SelectedCustomCursorSet.Name}.zip", Filter = "Zip Archive|*.zip" };
            if (dialog.ShowDialog() != true) return;

            try
            {
                if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);
                System.IO.Compression.ZipFile.CreateFromDirectory(SelectedCustomCursorSet.FolderPath, dialog.FileName);
                Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
            }
            catch (Exception ex) { App.Logger.WriteException("ModsViewModel::ExportCursorSet", ex); }
        }

        private void ImportCursorSet()
        {
            if (SelectedCustomCursorSet is null) return;
            var dialog = new OpenFileDialog { Filter = "Zip Archive|*.zip" };
            if (dialog.ShowDialog() != true) return;

            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                System.IO.Compression.ZipFile.ExtractToDirectory(dialog.FileName, tempPath);
                foreach (string file in Directory.GetFiles(tempPath, "*.png", SearchOption.AllDirectories))
                {
                    string? dest = GetCursorTargetPath(Path.GetFileName(file));
                    if (dest != null) { if (File.Exists(dest)) File.Delete(dest); File.Copy(file, dest); }
                }
                Directory.Delete(tempPath, true);
                LoadCursorPathsForSelectedSet();
                NotifyCursorStates();
            }
            catch (Exception ex) { App.Logger.WriteException("ModsViewModel::ImportCursorSet", ex); }
        }

        private void AddCursorImage(string? fileName)
        {
            if (SelectedCustomCursorSet is null || fileName is null) return;
            var dialog = new OpenFileDialog { Filter = "PNG files (*.png)|*.png" };
            if (dialog.ShowDialog() != true) return;

            try
            {
                string? dest = GetCursorTargetPath(fileName);
                if (dest != null) { File.Copy(dialog.FileName, dest, true); UpdateCursorPathAndPreview(fileName, dest); NotifyCursorStates(); }
            }
            catch (Exception ex) { App.Logger.WriteException("ModsViewModel::AddCursor", ex); }
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

        private static BitmapImage? LoadImageSafely(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var bitmap = new BitmapImage();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                return bitmap;
            }
            catch { return null; }
        }

        public string ShiftlockCursorSelectedPath { get; set; } = "";
        public string ArrowCursorSelectedPath { get; set; } = "";
        public string ArrowFarCursorSelectedPath { get; set; } = "";
        public string IBeamCursorSelectedPath { get; set; } = "";
        public ImageSource? ShiftlockCursorPreview { get; set; }
        public ImageSource? ArrowCursorPreview { get; set; }
        public ImageSource? ArrowFarCursorPreview { get; set; }
        public ImageSource? IBeamCursorPreview { get; set; }
        #endregion
    }
}
