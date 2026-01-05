using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Visuals;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.AppData;
using Froststrap.Integrations;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public class ModsViewModel : NotifyPropertyChangedViewModel
    {
        private void OpenModsFolder() => Process.Start("explorer.exe", Paths.Modifications);

        private static readonly Dictionary<string, byte[]> FontHeaders = new()
        {
            { "ttf", new byte[] { 0x00, 0x01, 0x00, 0x00 } },
            { "otf", new byte[] { 0x4F, 0x54, 0x54, 0x4F } },
            { "ttc", new byte[] { 0x74, 0x74, 0x63, 0x66 } }
        };

        public ModsViewModel()
        {
            LoadCustomCursorSets();

            LoadCursorPathsForSelectedSet();

            NotifyCursorVisibilities();

            _ = LoadFontFilesAsync();
        }

        private async void ManageCustomFont()
        {
            if (!string.IsNullOrEmpty(TextFontTask.NewState))
            {
                TextFontTask.NewState = string.Empty;
            }
            else
            {
                var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (mainWindow == null)
                    return;

                var storageProvider = mainWindow.StorageProvider;

                var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Font File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Font Files")
                        {
                            Patterns = new[] { "*.ttf", "*.otf", "*.ttc" }
                        }
                    }
                });

                if (files != null && files.Count > 0)
                {
                    var file = files[0];
                    string type = Path.GetExtension(file.Path.LocalPath).TrimStart('.').ToLowerInvariant();
                    byte[] fileHeader = File.ReadAllBytes(file.Path.LocalPath).Take(4).ToArray();

                    if (!FontHeaders.TryGetValue(type, out var expectedHeader) || !expectedHeader.SequenceEqual(fileHeader))
                    {
                        Frontend.ShowMessageBox("Custom Font Invalid", MessageBoxImage.Error);
                        return;
                    }

                    TextFontTask.NewState = file.Path.LocalPath;
                }
                else
                {
                    return;
                }
            }

            OnPropertyChanged(nameof(ChooseCustomFontVisibility));
            OnPropertyChanged(nameof(DeleteCustomFontVisibility));
        }

        public ICommand OpenModsFolderCommand => new RelayCommand(OpenModsFolder);

        public ICommand AddCustomCursorModCommand => new RelayCommand(AddCustomCursorMod);

        public ICommand RemoveCustomCursorModCommand => new RelayCommand(RemoveCustomCursorMod);

        public ICommand AddCustomShiftlockModCommand => new RelayCommand(AddCustomShiftlockMod);

        public ICommand RemoveCustomShiftlockModCommand => new RelayCommand(RemoveCustomShiftlockMod);
        public ICommand AddCustomDeathSoundCommand => new RelayCommand(AddCustomDeathSound);
        public ICommand RemoveCustomDeathSoundCommand => new RelayCommand(RemoveCustomDeathSound);

        public bool ChooseCustomFontVisibility => String.IsNullOrEmpty(TextFontTask.NewState);

        public bool DeleteCustomFontVisibility => !String.IsNullOrEmpty(TextFontTask.NewState);

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
            {
                Process.Start("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {path}");
            }
            else
            {
                Frontend.ShowMessageBox(Strings.Common_RobloxNotInstalled, MessageBoxImage.Error);
            }
        }

        private bool GetVisibility(string directory, string[] filenames, bool checkExist)
        {
            bool anyExist = filenames.Any(name => File.Exists(Path.Combine(directory, name)));
            return checkExist ? anyExist : !anyExist;
        }

        public bool ChooseCustomCursorVisibility =>
            GetVisibility(Path.Combine(Paths.Modifications, "Content", "textures", "Cursors", "KeyboardMouse"),
                          new[] { "ArrowCursor.png", "ArrowFarCursor.png", "MouseLockedCursor.png" }, checkExist: false);

        public bool DeleteCustomCursorVisibility =>
            GetVisibility(Path.Combine(Paths.Modifications, "Content", "textures", "Cursors", "KeyboardMouse"),
                          new[] { "ArrowCursor.png", "ArrowFarCursor.png", "MouseLockedCursor.png" }, checkExist: true);

        public bool ChooseCustomShiftlockVisibility =>
            GetVisibility(Path.Combine(Paths.Modifications, "Content", "textures"),
                          new[] { "MouseLockedCursor.png" }, checkExist: false);

        public bool DeleteCustomShiftlockVisibility =>
            GetVisibility(Path.Combine(Paths.Modifications, "Content", "textures"),
                          new[] { "MouseLockedCursor.png" }, checkExist: true);

        public bool ChooseCustomDeathSoundVisibility =>
            GetVisibility(Path.Combine(Paths.Modifications, "Content", "sounds"),
                          new[] { "oof.ogg" }, checkExist: false);

        public bool DeleteCustomDeathSoundVisibility =>
            GetVisibility(Path.Combine(Paths.Modifications, "Content", "sounds"),
                          new[] { "oof.ogg" }, checkExist: true);

        private async void AddCustomFile(string[] targetFiles, string targetDir, string dialogTitle, string filter, string failureText, Action postAction = null!)
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null)
                return;

            var storageProvider = mainWindow.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = dialogTitle,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(filter)
                    {
                        Patterns = new[] { filter.Contains("PNG") ? "*.png" : "*.ogg" }
                    }
                }
            });

            if (files == null || files.Count == 0)
                return;

            var file = files[0];
            string sourcePath = file.Path.LocalPath;
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

        private void RemoveCustomFile(string[] targetFiles, string targetDir, string notFoundMessage, Action postAction = null!)
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

        public void AddCustomCursorMod()
        {
            AddCustomFile(
                new[] { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" },
                Path.Combine(Paths.Modifications, "Content", "textures", "Cursors", "KeyboardMouse"),
                "Select a PNG Cursor Image",
                "PNG Images (*.png)|*.png",
                "cursors",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
                    OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
                });
        }

        public void RemoveCustomCursorMod()
        {
            RemoveCustomFile(
                new[] { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" },
                Path.Combine(Paths.Modifications, "Content", "textures", "Cursors", "KeyboardMouse"),
                "No custom cursors found to remove.",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
                    OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
                });
        }

        public void AddCustomShiftlockMod()
        {
            AddCustomFile(
                new[] { "MouseLockedCursor.png" },
                Path.Combine(Paths.Modifications, "Content", "textures"),
                "Select a PNG Shiftlock Image",
                "PNG Images (*.png)|*.png",
                "Shiftlock",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
                    OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
                });
        }

        public void RemoveCustomShiftlockMod()
        {
            RemoveCustomFile(
                new[] { "MouseLockedCursor.png" },
                Path.Combine(Paths.Modifications, "Content", "textures"),
                "No custom Shiftlock found to remove.",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
                    OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
                });
        }

        public void AddCustomDeathSound()
        {
            AddCustomFile(
                new[] { "oof.ogg" },
                Path.Combine(Paths.Modifications, "Content", "sounds"),
                "Select a Custom Death Sound",
                "OGG Audio (*.ogg)|*.ogg",
                "death sound",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomDeathSoundVisibility));
                    OnPropertyChanged(nameof(DeleteCustomDeathSoundVisibility));
                });
        }

        public void RemoveCustomDeathSound()
        {
            RemoveCustomFile(
                new[] { "oof.ogg" },
                Path.Combine(Paths.Modifications, "Content", "sounds"),
                "No custom death sound found to remove.",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomDeathSoundVisibility));
                    OnPropertyChanged(nameof(DeleteCustomDeathSoundVisibility));
                });
        }

        #region Mod Generator

        private Color _solidColor = Colors.White;
        private string _solidColorHex = "#FFFFFF";
        public string SolidColorHex
        {
            get => _solidColorHex;
            set
            {
                _solidColorHex = value;
                OnPropertyChanged(nameof(SolidColorHex));
                OnPropertyChanged(nameof(CanGenerateMod));

                if (IsValidHexColor(value))
                {
                    UpdateSolidColorFromHex(value);
                    UpdateGlyphColors();
                    StatusText = "Ready to generate mod.";
                }
                else
                {
                    StatusText = "Enter a valid hex color (e.g., #FF0000)";
                }
            }
        }

        public bool CanGenerateMod => IsValidHexColor(SolidColorHex) && IsNotGeneratingMod;

        private ObservableCollection<GlyphItem> _glyphItems = new();
        public ObservableCollection<GlyphItem> GlyphItems
        {
            get => _glyphItems;
            set
            {
                _glyphItems = value;
                OnPropertyChanged(nameof(GlyphItems));
            }
        }

        private ObservableCollection<string> _fontDisplayNames = new();
        public ObservableCollection<string> FontDisplayNames
        {
            get => _fontDisplayNames;
            set
            {
                _fontDisplayNames = value;
                OnPropertyChanged(nameof(FontDisplayNames));
            }
        }

        private string? _selectedFontDisplayName;
        public string? SelectedFontDisplayName
        {
            get => _selectedFontDisplayName;
            set
            {
                _selectedFontDisplayName = value;
                OnPropertyChanged(nameof(SelectedFontDisplayName));
                OnSelectedFontChanged();
            }
        }

        private bool _colorCursors = false;
        public bool ColorCursors
        {
            get => _colorCursors;
            set
            {
                _colorCursors = value;
                OnPropertyChanged(nameof(ColorCursors));
            }
        }

        private bool _colorShiftlock = false;
        public bool ColorShiftlock
        {
            get => _colorShiftlock;
            set
            {
                _colorShiftlock = value;
                OnPropertyChanged(nameof(ColorShiftlock));
            }
        }

        private bool _colorEmoteWheel = false;
        public bool ColorEmoteWheel
        {
            get => _colorEmoteWheel;
            set
            {
                _colorEmoteWheel = value;
                OnPropertyChanged(nameof(ColorEmoteWheel));
            }
        }

        private bool _includeModifications = true;
        public bool IncludeModifications
        {
            get => _includeModifications;
            set
            {
                _includeModifications = value;
                OnPropertyChanged(nameof(IncludeModifications));
            }
        }

        private string _statusText = "Ready to generate mod.";
        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private bool _isNotGeneratingMod = true;
        public bool IsNotGeneratingMod
        {
            get => _isNotGeneratingMod;
            set
            {
                _isNotGeneratingMod = value;
                OnPropertyChanged(nameof(IsNotGeneratingMod));
                OnPropertyChanged(nameof(CanGenerateMod));
            }
        }

        public ICommand OpenColorPickerCommand => new RelayCommand(OpenColorPicker);
        public ICommand GenerateModCommand => new AsyncRelayCommand(GenerateModAsync, () => CanGenerateMod);

        private async Task LoadFontFilesAsync()
        {
            string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");
            string fontDir = Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");

            if (!Directory.Exists(fontDir))
            {
                Directory.CreateDirectory(fontDir);
            }

            var fontFiles = Directory.GetFiles(fontDir)
                .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (fontFiles.Length == 0)
            {
                await DownloadFontFilesAsync(fontDir);
                fontFiles = Directory.GetFiles(fontDir)
                    .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var displayNames = fontFiles
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Select(f => f.Replace("BuilderIcons-", ""))
                    .Distinct()
                    .OrderBy(f => f)
                    .ToList();

                FontDisplayNames = new ObservableCollection<string>(displayNames);

                if (displayNames.Count > 0)
                {
                    SelectedFontDisplayName = displayNames[0];
                }
            });
        }

        private async Task DownloadFontFilesAsync(string fontDir)
        {
            try
            {
                string[] fontUrls = {
                    "https://raw.githubusercontent.com/RealMeddsam/config/main/BuilderIcons-Regular.ttf",
                    "https://raw.githubusercontent.com/RealMeddsam/config/main/BuilderIcons-Filled.ttf"
                };

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                foreach (var url in fontUrls)
                {
                    try
                    {
                        string fileName = Path.GetFileName(url);
                        string filePath = Path.Combine(fontDir, fileName);

                        var response = await httpClient.GetAsync(url);
                        response.EnsureSuccessStatusCode();

                        var fontData = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(filePath, fontData);
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.WriteException("ModsViewmodel::DownloadFontFilesAsync", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ModsViewmodel::DownloadFontFilesAsync", ex);
            }
        }

        private async void OnSelectedFontChanged()
        {
            if (string.IsNullOrEmpty(SelectedFontDisplayName))
            {
                GlyphItems = new ObservableCollection<GlyphItem>();
                return;
            }

            string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");
            string fontDir = Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");

            if (!Directory.Exists(fontDir))
            {
                StatusText = "Font directory not found. Please try again.";
                return;
            }

            var fontFiles = Directory.GetFiles(fontDir)
                .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (fontFiles.Length == 0)
            {
                StatusText = "No font files found. Downloading...";
                await DownloadFontFilesAsync(fontDir);
                fontFiles = Directory.GetFiles(fontDir)
                    .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            string selectedFont = FindFontFile(SelectedFontDisplayName, fontFiles);

            if (string.IsNullOrEmpty(selectedFont) || !File.Exists(selectedFont))
            {
                StatusText = $"Font file not found: {SelectedFontDisplayName}";
                GlyphItems = new ObservableCollection<GlyphItem>();
                return;
            }

            if (!IsValidHexColor(SolidColorHex))
            {
                StatusText = "Please enter a valid hex color to see preview";
                GlyphItems = new ObservableCollection<GlyphItem>();
                return;
            }

            StatusText = "Loading font preview...";
            await LoadGlyphPreviewsAsync(selectedFont);

            if (GlyphItems.Any())
            {
                StatusText = $"Loaded {GlyphItems.Count} glyphs from {SelectedFontDisplayName}";
            }
            else
            {
                StatusText = "No glyphs found in font";
            }
        }

        private string FindFontFile(string displayName, string[] fontFiles)
        {
            string? otfFile = fontFiles.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f) == $"BuilderIcons-{displayName}" &&
                f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));

            if (otfFile != null)
                return otfFile;

            string? ttfFile = fontFiles.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f) == $"BuilderIcons-{displayName}" &&
                f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase));

            return ttfFile ?? fontFiles.FirstOrDefault() ?? string.Empty;
        }

        private async Task LoadGlyphPreviewsAsync(string fontPath)
        {
            // TODO
        }

        private void UpdateGlyphColors()
        {
            if (!IsValidHexColor(SolidColorHex))
                return;

            var colorBrush = new SolidColorBrush(Avalonia.Media.Color.FromArgb(
                _solidColor.A, _solidColor.R, _solidColor.G, _solidColor.B));

            foreach (var item in GlyphItems)
            {
                item.ColorBrush = colorBrush;
            }
        }

        private bool IsValidHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return false;

            string cleanHex = hex.Trim();

            if (!cleanHex.StartsWith("#"))
                return false;

            if (cleanHex.Length != 7 && cleanHex.Length != 4)
                return false;

            string hexDigits = cleanHex.Substring(1);
            return hexDigits.All(c =>
                (c >= '0' && c <= '9') ||
                (c >= 'A' && c <= 'F') ||
                (c >= 'a' && c <= 'f'));
        }

        private void UpdateSolidColorFromHex(string hex)
        {
            try
            {
                _solidColor = Avalonia.Media.Color.Parse(hex);
            }
            catch
            {
                _solidColor = Avalonia.Media.Colors.White;
            }
        }

        private async void OpenColorPicker()
        {
            // TODO: add a color picker library that fits our future ui
            Frontend.ShowMessageBox("Use the hex color input to specify colors.", MessageBoxImage.Information);
        }

        private async Task GenerateModAsync()
        {
            const string LOG_IDENT = "ModsViewmodel::ModGenerator";

            if (!IsValidHexColor(SolidColorHex))
            {
                StatusText = "Please enter a valid hex color before generating mod.";
                return;
            }

            IsNotGeneratingMod = false;
            StatusText = "Starting mod generation...";

            try
            {
                await Task.Run(async () =>
                {
                    void SetStatus(string text) => StatusText = text;
                    void Log(string text) => App.Logger?.WriteLine(LOG_IDENT, text);

                    SetStatus("Downloading required packages...");
                    var (luaPackagesZip, extraTexturesZip, contentTexturesZip, versionHash, version) =
                        await ModGenerator.DownloadForModGenerator();

                    Log($"DownloadForModGenerator returned. Version: {version} ({versionHash})");

                    string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");

                    string luaPackagesDir = Path.Combine(froststrapTemp, "ExtraContent", "LuaPackages");
                    string extraTexturesDir = Path.Combine(froststrapTemp, "ExtraContent", "textures");
                    string contentTexturesDir = Path.Combine(froststrapTemp, "content", "textures");

                    void SafeExtract(string zipPath, string targetDir)
                    {
                        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                            return;

                        if (Directory.Exists(targetDir))
                        {
                            try { Directory.Delete(targetDir, true); }
                            catch (Exception ex)
                            {
                                App.Logger?.WriteException(LOG_IDENT, ex);
                                throw;
                            }
                        }

                        Directory.CreateDirectory(targetDir);
                        new FastZip().ExtractZip(zipPath, targetDir, null);
                    }

                    SetStatus("Extracting ZIPs...");
                    Log("Extracting downloaded ZIPs...");

                    Parallel.Invoke(
                        () => SafeExtract(luaPackagesZip, luaPackagesDir),
                        () => SafeExtract(extraTexturesZip, extraTexturesDir),
                        () => SafeExtract(contentTexturesZip, contentTexturesDir)
                    );

                    Log("Extraction complete.");

                    SetStatus("Loading mappings...");
                    Dictionary<string, string[]> mappings = await ModGenerator.LoadMappingsAsync();

                    if (mappings == null || mappings.Count == 0)
                    {
                        throw new Exception("Failed to load mappings. No mappings available.");
                    }

                    Log($"Loaded mappings with {mappings.Count} top-level entries.");

                    SetStatus("Recoloring images...");
                    Log($"Using solid color for recolor: {_solidColor}");

                    Log("Starting RecolorAllPngs...");
                    ModGenerator.RecolorAllPngs(froststrapTemp, _solidColor, mappings,
                        ColorCursors, ColorShiftlock, ColorEmoteWheel);
                    Log("RecolorAllPngs finished.");

                    try
                    {
                        SetStatus("Recoloring fonts...");
                        Log("Starting font recoloring...");

                        await ModGenerator.RecolorFontsAsync(froststrapTemp, _solidColor);
                        Log("Font recoloring finished.");
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.WriteException(LOG_IDENT, ex);
                        SetStatus("Font recoloring failed but continuing");
                    }

                    SetStatus("Cleaning up unnecessary files...");
                    var preservePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var entry in mappings.Values)
                    {
                        string fullPath = Path.Combine(froststrapTemp, Path.Combine(entry));
                        preservePaths.Add(fullPath);
                    }

                    string builderIconsFontDir = Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");
                    if (Directory.Exists(builderIconsFontDir))
                    {
                        preservePaths.Add(builderIconsFontDir);
                        var fontFiles = Directory.GetFiles(builderIconsFontDir, "*.*");
                        foreach (var fontFile in fontFiles)
                        {
                            preservePaths.Add(fontFile);
                        }
                    }

                    if (ColorCursors)
                    {
                        preservePaths.Add(Path.Combine(froststrapTemp, @"content\textures\Cursors\KeyboardMouse\IBeamCursor.png"));
                        preservePaths.Add(Path.Combine(froststrapTemp, @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png"));
                        preservePaths.Add(Path.Combine(froststrapTemp, @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png"));
                    }

                    if (ColorShiftlock)
                    {
                        preservePaths.Add(Path.Combine(froststrapTemp, @"content\textures\MouseLockedCursor.png"));
                    }

                    if (ColorEmoteWheel)
                    {
                        string emotesDir = Path.Combine(froststrapTemp, @"content\textures\ui\Emotes\Large");
                        preservePaths.Add(Path.Combine(emotesDir, "SelectedGradient.png"));
                        preservePaths.Add(Path.Combine(emotesDir, "SelectedGradient@2x.png"));
                        preservePaths.Add(Path.Combine(emotesDir, "SelectedGradient@3x.png"));
                        preservePaths.Add(Path.Combine(emotesDir, "SelectedLine.png"));
                        preservePaths.Add(Path.Combine(emotesDir, "SelectedLine@2x.png"));
                        preservePaths.Add(Path.Combine(emotesDir, "SelectedLine@3x.png"));
                    }

                    void DeleteExcept(string dir)
                    {
                        foreach (var file in Directory.GetFiles(dir))
                        {
                            if (!preservePaths.Contains(file))
                            {
                                try { File.Delete(file); } catch { }
                            }
                        }

                        foreach (var subDir in Directory.GetDirectories(dir))
                        {
                            if (!preservePaths.Contains(subDir))
                            {
                                try
                                {
                                    DeleteExcept(subDir);
                                    if (Directory.Exists(subDir) && !Directory.EnumerateFileSystemEntries(subDir).Any())
                                        Directory.Delete(subDir);
                                }
                                catch { }
                            }
                        }
                    }

                    var otfFiles = Directory.GetFiles(froststrapTemp, "*.otf", SearchOption.TopDirectoryOnly);

                    foreach (var otfFile in otfFiles)
                    {
                        File.Delete(otfFile);
                    }

                    if (Directory.Exists(luaPackagesDir)) DeleteExcept(luaPackagesDir);
                    if (Directory.Exists(extraTexturesDir)) DeleteExcept(extraTexturesDir);
                    if (Directory.Exists(contentTexturesDir)) DeleteExcept(contentTexturesDir);

                    string infoPath = Path.Combine(froststrapTemp, "info.json");
                    var infoData = new
                    {
                        FroststrapVersion = App.Version,
                        CreatedUsing = "Froststrap",
                        RobloxVersion = version,
                        RobloxVersionHash = versionHash,
                        OptionsUsed = new
                        {
                            ColorCursors = ColorCursors,
                            ColorShiftlock = ColorShiftlock,
                            ColorEmoteWheel = ColorEmoteWheel,
                        },
                        ColorsUsed = new
                        {
                            SolidColor = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}"
                        }
                    };

                    string infoJson = JsonSerializer.Serialize(infoData, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(infoPath, infoJson);

                    if (IncludeModifications)
                    {
                        if (!Directory.Exists(Paths.Modifications))
                            Directory.CreateDirectory(Paths.Modifications);

                        int copiedFiles = 0;

                        var itemsToCopy = new List<string>
                        {
                            Path.Combine(froststrapTemp, "ExtraContent"),
                            Path.Combine(froststrapTemp, "content"),
                            Path.Combine(froststrapTemp, "info.json")
                        };

                        string fontsPath = Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");

                        foreach (var item in itemsToCopy)
                        {
                            if (!File.Exists(item) && !Directory.Exists(item))
                                continue;

                            string relativePath = Path.GetRelativePath(froststrapTemp, item);
                            string targetPath = Path.Combine(Paths.Modifications, relativePath);

                            if (File.Exists(item))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                                File.Copy(item, targetPath, overwrite: true);
                                copiedFiles++;
                            }
                            else if (Directory.Exists(item))
                            {
                                foreach (var file in Directory.GetFiles(item, "*", SearchOption.AllDirectories))
                                {
                                    if (file.StartsWith(fontsPath, StringComparison.OrdinalIgnoreCase) &&
                                        file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }

                                    string fileRelativePath = Path.GetRelativePath(froststrapTemp, file);
                                    string fileTargetPath = Path.Combine(Paths.Modifications, fileRelativePath);

                                    Directory.CreateDirectory(Path.GetDirectoryName(fileTargetPath)!);
                                    File.Copy(file, fileTargetPath, overwrite: true);
                                    copiedFiles++;
                                }
                            }
                        }

                        SetStatus($"Mod generation completed! Copied {copiedFiles} files to Modifications folder.");
                        Log($"Copied {copiedFiles} files to {Paths.Modifications}");
                    }
                    else
                    {
                        var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                            ? desktop.MainWindow
                            : null;

                        if (mainWindow == null)
                        {
                            StatusText = "Save cancelled by user.";
                            Log("User cancelled save dialog.");
                            return;
                        }

                        // Use StorageProvider API
                        var storageProvider = mainWindow.StorageProvider;

                        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                        {
                            Title = "Save Froststrap Mod",
                            SuggestedFileName = "FroststrapMod.zip",
                            DefaultExtension = "zip",
                            FileTypeChoices = new[]
                            {
                                new FilePickerFileType("ZIP Archive")
                                {
                                    Patterns = new[] { "*.zip" }
                                }
                            }
                        });

                        if (file != null && !string.IsNullOrEmpty(file.Path.LocalPath))
                        {
                            using (var zip = new ZipArchive(new FileStream(file.Path.LocalPath, FileMode.Create), ZipArchiveMode.Create))
                            {
                                // ... rest of zip creation code ...
                                SetStatus($"Mod generated successfully! Saved to: {file.Path.LocalPath}");
                                Log($"Mod zip created at {file.Path.LocalPath}");
                            }
                        }
                        else
                        {
                            StatusText = "Save cancelled by user.";
                            Log("User cancelled save dialog.");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException(LOG_IDENT, ex);
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsNotGeneratingMod = true;
            }
        }

        #endregion

        #region Custom Cursor Set
        public ObservableCollection<CustomCursorSet> CustomCursorSets { get; } = new();

        private int _selectedCustomCursorSetIndex;
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

                    SelectedCustomCursorSetIndex = value;
                    NotifyCursorVisibilities();
                    LoadCursorPathsForSelectedSet();
                }
            }
        }

        public CustomCursorSet? SelectedCustomCursorSet =>
            SelectedCustomCursorSetIndex >= 0 && SelectedCustomCursorSetIndex < CustomCursorSets.Count
                ? CustomCursorSets[SelectedCustomCursorSetIndex]
                : null;

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
        public ICommand GetCurrentCursorSetCommand => new RelayCommand(GetCurrentCursorSet);
        public ICommand ExportCursorSetCommand => new RelayCommand(ExportCursorSet);
        public ICommand ImportCursorSetCommand => new RelayCommand(ImportCursorSet);
        public ICommand AddArrowCursorCommand => new RelayCommand(() => AddCursorImage("ArrowCursor.png", "Select Arrow Cursor PNG"));
        public ICommand AddArrowFarCursorCommand => new RelayCommand(() => AddCursorImage("ArrowFarCursor.png", "Select Arrow Far Cursor PNG"));
        public ICommand AddIBeamCursorCommand => new RelayCommand(() => AddCursorImage("IBeamCursor.png", "Select IBeam Cursor PNG"));
        public ICommand AddShiftlockCursorCommand => new RelayCommand(AddShiftlockCursor);
        public ICommand DeleteArrowCursorCommand => new RelayCommand(() => DeleteCursorImage("ArrowCursor.png"));
        public ICommand DeleteArrowFarCursorCommand => new RelayCommand(() => DeleteCursorImage("ArrowFarCursor.png"));
        public ICommand DeleteIBeamCursorCommand => new RelayCommand(() => DeleteCursorImage("IBeamCursor.png"));
        public ICommand DeleteShiftlockCursorCommand => new RelayCommand(() => DeleteCursorImage("MouseLockedCursor.png"));

        private void LoadCustomCursorSets()
        {
            CustomCursorSets.Clear();

            if (!Directory.Exists(Paths.CustomCursors))
                Directory.CreateDirectory(Paths.CustomCursors);

            foreach (var dir in Directory.GetDirectories(Paths.CustomCursors))
            {
                var name = Path.GetFileName(dir);

                CustomCursorSets.Add(new CustomCursorSet
                {
                    Name = name,
                    FolderPath = dir
                });
            }

            if (CustomCursorSets.Any())
                SelectedCustomCursorSetIndex = 0;

            OnPropertyChanged(nameof(IsCustomCursorSetSelected));
        }

        private void AddCustomCursorSet()
        {
            string basePath = Paths.CustomCursors;
            int index = 1;
            string newFolderPath;

            do
            {
                string folderName = $"Custom Cursor Set {index}";
                newFolderPath = Path.Combine(basePath, folderName);
                index++;
            }
            while (Directory.Exists(newFolderPath));

            try
            {
                Directory.CreateDirectory(newFolderPath);

                var newSet = new CustomCursorSet
                {
                    Name = Path.GetFileName(newFolderPath),
                    FolderPath = newFolderPath
                };

                CustomCursorSets.Add(newSet);
                SelectedCustomCursorSetIndex = CustomCursorSets.Count - 1;
                OnPropertyChanged(nameof(IsCustomCursorSetSelected));
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::AddCustomCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to create cursor set:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        private void DeleteCustomCursorSet()
        {
            if (SelectedCustomCursorSet is null)
                return;

            try
            {
                if (Directory.Exists(SelectedCustomCursorSet.FolderPath))
                    Directory.Delete(SelectedCustomCursorSet.FolderPath, true);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::DeleteCustomCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to delete cursor set:\n{ex.Message}", MessageBoxImage.Error);
                return;
            }

            CustomCursorSets.Remove(SelectedCustomCursorSet);

            if (CustomCursorSets.Any())
            {
                SelectedCustomCursorSetIndex = CustomCursorSets.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomCursorSet));
            }

            OnPropertyChanged(nameof(IsCustomCursorSetSelected));
        }

        private void RenameCustomCursorSetStructure(string oldName, string newName)
        {
            string oldDir = Path.Combine(Paths.CustomCursors, oldName);
            string newDir = Path.Combine(Paths.CustomCursors, newName);

            if (Directory.Exists(newDir))
                throw new IOException("A folder with the new name already exists.");

            Directory.Move(oldDir, newDir);
        }

        private void RenameCustomCursorSet()
        {
            const string LOG_IDENT = "ModsViewModel::RenameCustomCursorSet";

            if (SelectedCustomCursorSet is null || SelectedCustomCursorSet.Name == SelectedCustomCursorSetName)
                return;

            if (string.IsNullOrWhiteSpace(SelectedCustomCursorSetName))
            {
                Frontend.ShowMessageBox("Name cannot be empty.", MessageBoxImage.Error);
                return;
            }

            var validationResult = PathValidator.IsFileNameValid(SelectedCustomCursorSetName);

            if (validationResult != PathValidator.ValidationResult.Ok)
            {
                string msg = validationResult switch
                {
                    PathValidator.ValidationResult.IllegalCharacter => "Name contains illegal characters.",
                    PathValidator.ValidationResult.ReservedFileName => "Name is reserved.",
                    _ => "Unknown validation error."
                };

                App.Logger.WriteLine(LOG_IDENT, $"Validation result: {validationResult}");
                Frontend.ShowMessageBox(msg, MessageBoxImage.Error);
                return;
            }

            try
            {
                RenameCustomCursorSetStructure(SelectedCustomCursorSet.Name, SelectedCustomCursorSetName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                Frontend.ShowMessageBox($"Failed to rename:\n{ex.Message}", MessageBoxImage.Error);
                return;
            }

            int idx = CustomCursorSets.IndexOf(SelectedCustomCursorSet);
            CustomCursorSets[idx] = new CustomCursorSet
            {
                Name = SelectedCustomCursorSetName,
                FolderPath = Path.Combine(Paths.CustomCursors, SelectedCustomCursorSetName)
            };

            SelectedCustomCursorSetIndex = idx;
            OnPropertyChanged(nameof(SelectedCustomCursorSetIndex));
        }

        private void ApplyCursorSet()
        {
            if (SelectedCustomCursorSet is null)
            {
                Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            string sourceDir = SelectedCustomCursorSet.FolderPath;
            string targetDir = Path.Combine(Paths.Modifications, "content", "textures");
            string targetKeyboardMouse = Path.Combine(targetDir, "Cursors", "KeyboardMouse");

            try
            {
                if (!Directory.Exists(sourceDir))
                {
                    Frontend.ShowMessageBox("Selected cursor set folder does not exist.", MessageBoxImage.Error);
                    return;
                }

                Directory.CreateDirectory(targetDir);
                Directory.CreateDirectory(targetKeyboardMouse);

                var filesToDelete = new[]
                {
                    Path.Combine(targetDir, "MouseLockedCursor.png"),
                    Path.Combine(targetKeyboardMouse, "ArrowCursor.png"),
                    Path.Combine(targetKeyboardMouse, "ArrowFarCursor.png"),
                    Path.Combine(targetKeyboardMouse, "IBeamCursor.png")
                };

                foreach (var file in filesToDelete)
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }

                foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(sourceDir, file);
                    string destPath = Path.Combine(targetDir, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    File.Copy(file, destPath, overwrite: true);
                }

                Frontend.ShowMessageBox($"Cursor set '{SelectedCustomCursorSet.Name}' applied successfully!", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::ApplyCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to apply cursor set:\n{ex.Message}", MessageBoxImage.Error);
            }

            LoadCursorPathsForSelectedSet();

            OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
            OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
            OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
            OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
        }

        private void GetCurrentCursorSet()
        {
            if (SelectedCustomCursorSet is null)
            {
                Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            string sourceMouseLocked = Path.Combine(Paths.Modifications, "content", "textures", "MouseLockedCursor.png");
            string sourceKeyboardMouse = Path.Combine(Paths.Modifications, "content", "textures", "Cursors", "KeyboardMouse");

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

                Frontend.ShowMessageBox("Current cursor set copied into selected folder.", MessageBoxImage.Information);
                NotifyCursorVisibilities();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::GetCurrentCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to get current cursor set:\n{ex.Message}", MessageBoxImage.Error);
            }

            LoadCursorPathsForSelectedSet();
            NotifyCursorVisibilities();
        }

        private async void ExportCursorSet()
        {
            if (SelectedCustomCursorSet is null)
                return;

            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null)
                return;

            var storageProvider = mainWindow.StorageProvider;

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Cursor Set",
                SuggestedFileName = $"{SelectedCustomCursorSet.Name}.zip",
                DefaultExtension = "zip",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("ZIP Archive")
                    {
                        Patterns = new[] { "*.zip" }
                    }
                }
            });

            if (file == null || string.IsNullOrEmpty(file.Path.LocalPath))
                return;

            string cursorDir = SelectedCustomCursorSet.FolderPath;

            try
            {
                using var memStream = new MemoryStream();
                using var zipStream = new ZipOutputStream(memStream);

                foreach (var filePath in Directory.EnumerateFiles(cursorDir, "*.*", SearchOption.AllDirectories))
                {
                    string relativePath = filePath[(cursorDir.Length + 1)..].Replace('\\', '/');

                    var entry = new ZipEntry(relativePath)
                    {
                        DateTime = DateTime.Now,
                        Size = new FileInfo(filePath).Length
                    };

                    zipStream.PutNextEntry(entry);

                    using var fileStream = File.OpenRead(filePath);
                    fileStream.CopyTo(zipStream);

                    zipStream.CloseEntry();
                }

                zipStream.Finish();
                memStream.Position = 0;

                using var outputStream = File.OpenWrite(file.Path.LocalPath);
                memStream.CopyTo(outputStream);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::ExportCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to export cursor set:\n{ex.Message}", MessageBoxImage.Error);
                return;
            }

            Process.Start("explorer.exe", $"/select,\"{file.Path.LocalPath}\"");
        }

        private async void ImportCursorSet()
        {
            if (SelectedCustomCursorSet is null)
            {
                Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null)
                return;

            var storageProvider = mainWindow.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Cursor Set",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("ZIP Archive")
                    {
                        Patterns = new[] { "*.zip" }
                    }
                }
            });

            if (files == null || files.Count == 0)
                return;

            var file = files[0];

            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);

                ExtractZipToDirectory(file.Path.LocalPath, tempPath);

                string mouseLockedDest = Path.Combine(SelectedCustomCursorSet.FolderPath, "MouseLockedCursor.png");
                string destKeyboardMouseFolder = Path.Combine(SelectedCustomCursorSet.FolderPath, "Cursors", "KeyboardMouse");

                if (File.Exists(mouseLockedDest))
                    File.Delete(mouseLockedDest);

                foreach (var fileName in new[] { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" })
                {
                    string filePath = Path.Combine(destKeyboardMouseFolder, fileName);
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }

                string? mouseLockedSource = Directory.GetFiles(tempPath, "MouseLockedCursor.png", SearchOption.AllDirectories).FirstOrDefault();

                if (mouseLockedSource != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(mouseLockedDest)!);
                    File.Copy(mouseLockedSource, mouseLockedDest, overwrite: true);
                }

                Directory.CreateDirectory(destKeyboardMouseFolder);

                foreach (var fileName in new[] { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" })
                {
                    string? sourceFile = Directory.GetFiles(tempPath, fileName, SearchOption.AllDirectories).FirstOrDefault();
                    if (sourceFile != null)
                    {
                        string destFile = Path.Combine(destKeyboardMouseFolder, fileName);
                        File.Copy(sourceFile, destFile, overwrite: true);
                    }
                }

                Directory.Delete(tempPath, recursive: true);

                Frontend.ShowMessageBox("Cursor set imported successfully.", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::ImportCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to import cursor set:\n{ex.Message}", MessageBoxImage.Error);
            }

            LoadCursorPathsForSelectedSet();
        }

        private void ExtractZipToDirectory(string zipFilePath, string extractPath)
        {
            using var zipInputStream = new ZipInputStream(File.OpenRead(zipFilePath));

            ZipEntry? entry;
            while ((entry = zipInputStream.GetNextEntry()) != null)
            {
                if (entry.IsDirectory)
                    continue;

                string filePath = Path.Combine(extractPath, entry.Name);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                using var outputStream = File.Create(filePath);
                zipInputStream.CopyTo(outputStream);
            }
        }

        private string? GetCursorTargetPath(string fileName)
        {
            if (SelectedCustomCursorSet is null)
                return null;

            string dir = fileName == "MouseLockedCursor.png"
                ? SelectedCustomCursorSet.FolderPath
                : Path.Combine(SelectedCustomCursorSet.FolderPath, "Cursors", "KeyboardMouse");

            Directory.CreateDirectory(dir);
            return Path.Combine(dir, fileName);
        }

        private void DeleteCursorImage(string fileName)
        {
            string? destPath = GetCursorTargetPath(fileName);
            if (destPath is null || !File.Exists(destPath))
                return;

            try
            {
                File.Delete(destPath);

                UpdateCursorPathProperty(fileName, "");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"ModsViewModel::Delete{fileName}", ex);
                Frontend.ShowMessageBox($"Failed to delete {fileName}:\n{ex.Message}", MessageBoxImage.Error);
            }

            LoadCursorPathsForSelectedSet();
            NotifyCursorVisibilities();

            OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
            OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
            OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
            OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
        }

        private void AddShiftlockCursor()
        {
            AddCursorImage("MouseLockedCursor.png", "Select Shiftlock PNG");
            OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
            OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
            OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
            OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
        }

        private async void AddCursorImage(string fileName, string dialogTitle)
        {
            if (SelectedCustomCursorSet is null)
            {
                Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null)
                return;

            var storageProvider = mainWindow.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = dialogTitle,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("PNG Files")
                    {
                        Patterns = new[] { "*.png" }
                    }
                }
            });

            if (files == null || files.Count == 0)
                return;

            var file = files[0];
            string? destPath = GetCursorTargetPath(fileName);
            if (destPath is null)
                return;

            try
            {
                if (File.Exists(destPath))
                    File.Delete(destPath);

                File.Copy(file.Path.LocalPath, destPath);
                UpdateCursorPathAndPreview(fileName, file.Path.LocalPath);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"ModsViewModel::Add{fileName}", ex);
                Frontend.ShowMessageBox($"Failed to add {fileName}:\n{ex.Message}", MessageBoxImage.Error);
            }

            LoadCursorPathsForSelectedSet();
            NotifyCursorVisibilities();

            OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
            OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
            OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
            OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
        }

        private void UpdateCursorPathProperty(string fileName, string path)
        {
            switch (fileName)
            {
                case "MouseLockedCursor.png":
                    ShiftlockCursorSelectedPath = path;
                    break;
                case "ArrowCursor.png":
                    ArrowCursorSelectedPath = path;
                    break;
                case "ArrowFarCursor.png":
                    ArrowFarCursorSelectedPath = path;
                    break;
                case "IBeamCursor.png":
                    IBeamCursorSelectedPath = path;
                    break;
            }
        }

        private void UpdateCursorPathAndPreview(string fileName, string fullPath)
        {
            if (!File.Exists(fullPath))
                fullPath = "";

            Bitmap? image = LoadImageSafely(fullPath);

            switch (fileName)
            {
                case "MouseLockedCursor.png":
                    ShiftlockCursorSelectedPath = fullPath;
                    ShiftlockCursorPreview = image;
                    App.Settings.Prop.ShiftlockCursorSelectedPath = fullPath;
                    break;

                case "ArrowCursor.png":
                    ArrowCursorSelectedPath = fullPath;
                    ArrowCursorPreview = image;
                    App.Settings.Prop.ArrowCursorSelectedPath = fullPath;
                    break;

                case "ArrowFarCursor.png":
                    ArrowFarCursorSelectedPath = fullPath;
                    ArrowFarCursorPreview = image;
                    App.Settings.Prop.ArrowFarCursorSelectedPath = fullPath;
                    break;

                case "IBeamCursor.png":
                    IBeamCursorSelectedPath = fullPath;
                    IBeamCursorPreview = image;
                    App.Settings.Prop.IBeamCursorSelectedPath = fullPath;
                    break;
            }

            App.Settings.Save();
        }

        private void LoadCursorPathsForSelectedSet()
        {
            if (SelectedCustomCursorSet == null)
            {
                UpdateCursorPathAndPreview("MouseLockedCursor.png", "");
                UpdateCursorPathAndPreview("ArrowCursor.png", "");
                UpdateCursorPathAndPreview("ArrowFarCursor.png", "");
                UpdateCursorPathAndPreview("IBeamCursor.png", "");
                return;
            }

            string baseDir = SelectedCustomCursorSet.FolderPath;
            string kbMouseDir = Path.Combine(baseDir, "Cursors", "KeyboardMouse");

            UpdateCursorPathAndPreview("MouseLockedCursor.png", Path.Combine(baseDir, "MouseLockedCursor.png"));
            UpdateCursorPathAndPreview("ArrowCursor.png", Path.Combine(kbMouseDir, "ArrowCursor.png"));
            UpdateCursorPathAndPreview("ArrowFarCursor.png", Path.Combine(kbMouseDir, "ArrowFarCursor.png"));
            UpdateCursorPathAndPreview("IBeamCursor.png", Path.Combine(kbMouseDir, "IBeamCursor.png"));
        }

        private string _shiftlockCursorSelectedPath = "";
        public string ShiftlockCursorSelectedPath
        {
            get => _shiftlockCursorSelectedPath;
            set
            {
                if (_shiftlockCursorSelectedPath != value)
                {
                    _shiftlockCursorSelectedPath = value;
                    OnPropertyChanged(nameof(ShiftlockCursorSelectedPath));
                }
            }
        }

        private string _arrowCursorSelectedPath = "";
        public string ArrowCursorSelectedPath
        {
            get => _arrowCursorSelectedPath;
            set
            {
                if (_arrowCursorSelectedPath != value)
                {
                    _arrowCursorSelectedPath = value;
                    OnPropertyChanged(nameof(ArrowCursorSelectedPath));
                }
            }
        }

        private string _arrowFarCursorSelectedPath = "";
        public string ArrowFarCursorSelectedPath
        {
            get => _arrowFarCursorSelectedPath;
            set
            {
                if (_arrowFarCursorSelectedPath != value)
                {
                    _arrowFarCursorSelectedPath = value;
                    OnPropertyChanged(nameof(ArrowFarCursorSelectedPath));
                }
            }
        }

        private string _iBeamCursorSelectedPath = "";
        public string IBeamCursorSelectedPath
        {
            get => _iBeamCursorSelectedPath;
            set
            {
                if (_iBeamCursorSelectedPath != value)
                {
                    _iBeamCursorSelectedPath = value;
                    OnPropertyChanged(nameof(IBeamCursorSelectedPath));
                }
            }
        }

        private Bitmap? _shiftlockCursorPreview;
        public Bitmap? ShiftlockCursorPreview
        {
            get => _shiftlockCursorPreview;
            set { _shiftlockCursorPreview = value; OnPropertyChanged(nameof(ShiftlockCursorPreview)); }
        }

        private Bitmap? _arrowCursorPreview;
        public Bitmap? ArrowCursorPreview
        {
            get => _arrowCursorPreview;
            set { _arrowCursorPreview = value; OnPropertyChanged(nameof(ArrowCursorPreview)); }
        }

        private Bitmap? _arrowFarCursorPreview;
        public Bitmap? ArrowFarCursorPreview
        {
            get => _arrowFarCursorPreview;
            set { _arrowFarCursorPreview = value; OnPropertyChanged(nameof(ArrowFarCursorPreview)); }
        }

        private Bitmap? _iBeamCursorPreview;
        public Bitmap? IBeamCursorPreview
        {
            get => _iBeamCursorPreview;
            set { _iBeamCursorPreview = value; OnPropertyChanged(nameof(IBeamCursorPreview)); }
        }

        private static Bitmap? LoadImageSafely(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }

        public bool AddShiftlockCursorVisibility => GetCursorAddVisibility("MouseLockedCursor.png");
        public bool DeleteShiftlockCursorVisibility => GetCursorDeleteVisibility("MouseLockedCursor.png");
        public bool AddArrowCursorVisibility => GetCursorAddVisibility("ArrowCursor.png");
        public bool DeleteArrowCursorVisibility => GetCursorDeleteVisibility("ArrowCursor.png");
        public bool AddArrowFarCursorVisibility => GetCursorAddVisibility("ArrowFarCursor.png");
        public bool DeleteArrowFarCursorVisibility => GetCursorDeleteVisibility("ArrowFarCursor.png");
        public bool AddIBeamCursorVisibility => GetCursorAddVisibility("IBeamCursor.png");
        public bool DeleteIBeamCursorVisibility => GetCursorDeleteVisibility("IBeamCursor.png");

        private bool GetCursorAddVisibility(string fileName)
        {
            string? path = GetCursorTargetPath(fileName);
            return path is null || !File.Exists(path);
        }

        private bool GetCursorDeleteVisibility(string fileName)
        {
            string? path = GetCursorTargetPath(fileName);
            return path is not null && File.Exists(path);
        }

        private void NotifyCursorVisibilities()
        {
            OnPropertyChanged(nameof(AddShiftlockCursorVisibility));
            OnPropertyChanged(nameof(DeleteShiftlockCursorVisibility));
            OnPropertyChanged(nameof(AddArrowCursorVisibility));
            OnPropertyChanged(nameof(DeleteArrowCursorVisibility));
            OnPropertyChanged(nameof(AddArrowFarCursorVisibility));
            OnPropertyChanged(nameof(DeleteArrowFarCursorVisibility));
            OnPropertyChanged(nameof(AddIBeamCursorVisibility));
            OnPropertyChanged(nameof(DeleteIBeamCursorVisibility));
        }
        #endregion
    }
}
