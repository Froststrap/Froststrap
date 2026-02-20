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
        private void OpenModsFolder() => Process.Start("explorer.exe", Paths.Modifications);

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

            NotifyCursorVisibilities();

            _ = LoadFontFilesAsync();
        }

        private void ManageCustomFont()
        {
            if (!string.IsNullOrEmpty(TextFontTask.NewState))
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

        public Visibility ChooseCustomFontVisibility => !String.IsNullOrEmpty(TextFontTask.NewState) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility DeleteCustomFontVisibility => !String.IsNullOrEmpty(TextFontTask.NewState) ? Visibility.Visible : Visibility.Collapsed;

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

        private Visibility GetVisibility(string directory, string[] filenames, bool checkExist)
        {
            bool anyExist = filenames.Any(name => File.Exists(Path.Combine(directory, name)));
            return (checkExist ? anyExist : !anyExist) ? Visibility.Visible : Visibility.Collapsed;
        }

        public Visibility ChooseCustomCursorVisibility =>
    GetVisibility(Path.Combine(Paths.Modifications, "Content", "textures", "Cursors", "KeyboardMouse"),
                  new[] { "ArrowCursor.png", "ArrowFarCursor.png", "MouseLockedCursor.png" }, checkExist: false);

        public Visibility DeleteCustomCursorVisibility =>
            GetVisibility(Path.Combine(Paths.Modifications, "Content", "textures", "Cursors", "KeyboardMouse"),
                          new[] { "ArrowCursor.png", "ArrowFarCursor.png", "MouseLockedCursor.png" }, checkExist: true);

        public Visibility ChooseCustomShiftlockVisibility =>
            GetVisibility(Path.Combine(Paths.Modifications, "Content", "textures"),
                          new[] { "MouseLockedCursor.png" }, checkExist: false);

        public Visibility DeleteCustomShiftlockVisibility =>
            GetVisibility(Path.Combine(Paths.Modifications, "Content", "textures"),
                          new[] { "MouseLockedCursor.png" }, checkExist: true);

        public Visibility ChooseCustomDeathSoundVisibility =>
            GetVisibility(Path.Combine(Paths.Modifications, "Content", "sounds"),
                          new[] { "oof.ogg" }, checkExist: false);

        public Visibility DeleteCustomDeathSoundVisibility =>
            GetVisibility(Path.Combine(Paths.Modifications, "Content", "sounds"),
                          new[] { "oof.ogg" }, checkExist: true);

        private void AddCustomFile(string[] targetFiles, string targetDir, string dialogTitle, string filter, string failureText, Action postAction = null!)
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

        private System.Drawing.Color _solidColor = System.Drawing.Color.White;
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
                    OnSelectedFontChanged();
                }
                else
                {
                    StatusText = "Enter a valid hex color (e.g., #FF0000)";
                }
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

        public bool CanGenerateMod => IsValidHexColor(SolidColorHex) && IsNotGeneratingMod;

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private bool _colorCursors = false;
        public bool ColorCursors { get => _colorCursors; set { _colorCursors = value; OnPropertyChanged(nameof(ColorCursors)); } }

        private bool _colorShiftlock = false;
        public bool ColorShiftlock { get => _colorShiftlock; set { _colorShiftlock = value; OnPropertyChanged(nameof(ColorShiftlock)); } }

        private bool _colorEmoteWheel = false;
        public bool ColorEmoteWheel { get => _colorEmoteWheel; set { _colorEmoteWheel = value; OnPropertyChanged(nameof(ColorEmoteWheel)); } }

        private bool _includeModifications = true;
        public bool IncludeModifications { get => _includeModifications; set { _includeModifications = value; OnPropertyChanged(nameof(IncludeModifications)); } }

        private ObservableCollection<string> _fontDisplayNames = new();
        public ObservableCollection<string> FontDisplayNames { get => _fontDisplayNames; set { _fontDisplayNames = value; OnPropertyChanged(nameof(FontDisplayNames)); } }

        private string? _selectedFontDisplayName;
        public string? SelectedFontDisplayName { get => _selectedFontDisplayName; set { _selectedFontDisplayName = value; OnPropertyChanged(nameof(SelectedFontDisplayName)); OnSelectedFontChanged(); } }

        private ObservableCollection<GlyphItem> _glyphItems = new();
        public ObservableCollection<GlyphItem> GlyphItems { get => _glyphItems; set { _glyphItems = value; OnPropertyChanged(nameof(GlyphItems)); } }

        public ICommand OpenColorPickerCommand => new RelayCommand(OpenColorPicker);
        public ICommand GenerateModCommand => new AsyncRelayCommand(GenerateModAsync, () => CanGenerateMod);

        private string TempRoot => Path.Combine(Path.GetTempPath(), "Froststrap");
        private string FontDir => Path.Combine(TempRoot, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");

        private async Task LoadFontFilesAsync()
        {
            if (!Directory.Exists(FontDir)) Directory.CreateDirectory(FontDir);

            var fontFiles = Directory.GetFiles(FontDir).Where(f => f.EndsWith(".ttf") || f.EndsWith(".otf")).ToArray();

            if (fontFiles.Length == 0)
            {
                await DownloadFontFilesAsync(FontDir);
                fontFiles = Directory.GetFiles(FontDir).Where(f => f.EndsWith(".ttf") || f.EndsWith(".otf")).ToArray();
            }

            var displayNames = fontFiles
                .Select(f => Path.GetFileNameWithoutExtension(f).Replace("BuilderIcons-", ""))
                .Distinct()
                .OrderBy(f => f)
                .ToList();

            await Application.Current.Dispatcher.InvokeAsync(() => {
                FontDisplayNames = new ObservableCollection<string>(displayNames);
                if (displayNames.Count > 0 && SelectedFontDisplayName == null)
                    SelectedFontDisplayName = displayNames[0];
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

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                foreach (var url in fontUrls)
                {
                    var response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                        await File.WriteAllBytesAsync(Path.Combine(fontDir, Path.GetFileName(url)), await response.Content.ReadAsByteArrayAsync());
                }
            }
            catch (Exception ex) { App.Logger?.WriteException("ModGenerator::DownloadFonts", ex); }
        }

        private async void OnSelectedFontChanged()
        {
            if (string.IsNullOrEmpty(SelectedFontDisplayName) || !IsValidHexColor(SolidColorHex))
            {
                GlyphItems = new();
                return;
            }

            var fontFiles = Directory.GetFiles(FontDir).Where(f => f.EndsWith(".ttf") || f.EndsWith(".otf")).ToArray();
            string selectedFont = FindFontFile(SelectedFontDisplayName, fontFiles);

            if (!string.IsNullOrEmpty(selectedFont) && File.Exists(selectedFont))
            {
                await LoadGlyphPreviewsAsync(selectedFont);
            }
        }

        private bool IsFileReady(string filename)
        {
            try
            {
                using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return fs.Length > 0;
                }
            }
            catch (IOException)
            {
                return false;
            }
        }

        private async Task LoadGlyphPreviewsAsync(string fontPath)
        {
            if (!File.Exists(fontPath))
                return;

            int attempts = 0;
            while (!IsFileReady(fontPath) && attempts < 10)
            {
                await Task.Delay(100);
                attempts++;
            }

            var glyphItems = new ObservableCollection<GlyphItem>();

            var colorBrush = new SolidColorBrush(Color.FromArgb(
                _solidColor.A, _solidColor.R, _solidColor.G, _solidColor.B));
            colorBrush.Freeze();

            try
            {
                GlyphTypeface typeface;
                try
                {
                    typeface = new GlyphTypeface(new Uri(fontPath, UriKind.Absolute));
                }
                catch (Exception ex)
                {
                    App.Logger?.WriteException("ModsViewModel::LoadGlyphPreviewsAsync_TypefaceInit", ex);
                    StatusText = "Error loading font preview. The file may be corrupted or busy.";
                    return;
                }

                var characterCodes = typeface.CharacterToGlyphMap.Keys
                    .OrderByDescending(c => c)
                    .ToList();

                foreach (var characterCode in characterCodes)
                {
                    if (!typeface.CharacterToGlyphMap.TryGetValue(characterCode, out ushort glyphIndex))
                        continue;

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            Geometry geometry = typeface.GetGlyphOutline(glyphIndex, 40, 40);

                            var bounds = geometry.Bounds;
                            var translate = new TranslateTransform(
                                (50 - bounds.Width) / 2 - bounds.X,
                                (50 - bounds.Height) / 2 - bounds.Y
                            );
                            geometry.Transform = translate;

                            geometry.Freeze();

                            glyphItems.Add(new GlyphItem
                            {
                                Data = geometry,
                                ColorBrush = colorBrush
                            });
                        }
                        catch
                        {
                            // Skip glyphs that fail to render
                        }
                    });
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    GlyphItems = glyphItems;
                });
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ModsViewModel::LoadGlyphPreviewsAsync", ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    GlyphItems = new ObservableCollection<GlyphItem>();
                });
            }
        }

        private async Task GenerateModAsync()
        {
            const string LOG_IDENT = "ModGenerator";
            if (!IsValidHexColor(SolidColorHex)) return;

            IsNotGeneratingMod = false;
            StatusText = "Starting mod generation...";

            try
            {
                await Task.Run(async () =>
                {
                    StatusText = "Downloading required assets...";
                    var (luaZip, extraZip, contentZip, vHash, vName) = await ModGenerator.DownloadForModGenerator();

                    StatusText = "Extracting files...";
                    string luaDir = Path.Combine(TempRoot, "ExtraContent", "LuaPackages");
                    string extraDir = Path.Combine(TempRoot, "ExtraContent", "textures");
                    string contentDir = Path.Combine(TempRoot, "content", "textures");

                    Parallel.Invoke(
                        () => SafeExtract(luaZip, luaDir),
                        () => SafeExtract(extraZip, extraDir),
                        () => SafeExtract(contentZip, contentDir)
                    );

                    StatusText = "Recoloring assets (this may take a moment)...";
                    var mappings = await ModGenerator.LoadMappingsAsync();

                    // Perform recoloring
                    ModGenerator.RecolorAllPngs(TempRoot, _solidColor, mappings, ColorCursors, ColorShiftlock, ColorEmoteWheel);
                    await ModGenerator.RecolorFontsAsync(TempRoot, _solidColor);

                    StatusText = "Cleaning up unnecessary files...";

                    // 1. Build the whitelist of paths to keep
                    var preservePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Keep mapped files
                    foreach (var entry in mappings.Values)
                    {
                        preservePaths.Add(Path.GetFullPath(Path.Combine(TempRoot, Path.Combine(entry))));
                    }

                    // Keep Fonts (BuilderIcons)
                    string builderIconsFontDir = Path.Combine(TempRoot, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");
                    if (Directory.Exists(builderIconsFontDir))
                    {
                        preservePaths.Add(Path.GetFullPath(builderIconsFontDir));
                        foreach (var fontFile in Directory.GetFiles(builderIconsFontDir, "*.*"))
                            preservePaths.Add(Path.GetFullPath(fontFile));
                    }

                    // Keep Optional UI elements
                    if (ColorCursors)
                    {
                        preservePaths.Add(Path.GetFullPath(Path.Combine(TempRoot, @"content\textures\Cursors\KeyboardMouse\IBeamCursor.png")));
                        preservePaths.Add(Path.GetFullPath(Path.Combine(TempRoot, @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png")));
                        preservePaths.Add(Path.GetFullPath(Path.Combine(TempRoot, @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png")));
                    }
                    if (ColorShiftlock)
                        preservePaths.Add(Path.GetFullPath(Path.Combine(TempRoot, @"content\textures\MouseLockedCursor.png")));
                    if (ColorEmoteWheel)
                    {
                        string emotesDir = Path.Combine(TempRoot, @"content\textures\ui\Emotes\Large");
                        string[] emoteFiles = { "SelectedGradient.png", "SelectedGradient@2x.png", "SelectedGradient@3x.png", "SelectedLine.png", "SelectedLine@2x.png", "SelectedLine@3x.png" };
                        foreach (var e in emoteFiles) preservePaths.Add(Path.GetFullPath(Path.Combine(emotesDir, e)));
                    }

                    // 2. Define the deletion logic
                    void DeleteExcept(string dir)
                    {
                        foreach (var file in Directory.GetFiles(dir))
                        {
                            if (!preservePaths.Contains(Path.GetFullPath(file)))
                            {
                                try { File.Delete(file); } catch { }
                            }
                        }

                        foreach (var subDir in Directory.GetDirectories(dir))
                        {
                            // If the directory itself isn't in preservePaths, we check its contents
                            // unless you add parent folders to preservePaths, we just recurse
                            DeleteExcept(subDir);

                            // If directory is now empty and not whitelisted, delete it
                            try
                            {
                                if (!Directory.EnumerateFileSystemEntries(subDir).Any() && !preservePaths.Contains(Path.GetFullPath(subDir)))
                                    Directory.Delete(subDir);
                            }
                            catch { }
                        }
                    }

                    // Execute cleanup
                    if (Directory.Exists(luaDir)) DeleteExcept(luaDir);
                    if (Directory.Exists(extraDir)) DeleteExcept(extraDir);
                    if (Directory.Exists(contentDir)) DeleteExcept(contentDir);

                    // 3. Create info.json
                    string infoPath = Path.Combine(TempRoot, "info.json");
                    var infoData = new
                    {
                        FroststrapVersion = App.Version,
                        RobloxVersion = vName,
                        RobloxVersionHash = vHash,
                        ColorsUsed = new { SolidColor = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}" }
                    };
                    await File.WriteAllTextAsync(infoPath, JsonSerializer.Serialize(infoData, new JsonSerializerOptions { WriteIndented = true }));

                    StatusText = "Packaging...";

                    if (IncludeModifications)
                    {
                        if (!Directory.Exists(Paths.Modifications)) Directory.CreateDirectory(Paths.Modifications);
                        int copiedFiles = 0;
                        var itemsToCopy = new List<string> { Path.Combine(TempRoot, "ExtraContent"), Path.Combine(TempRoot, "content"), infoPath };

                        foreach (var item in itemsToCopy)
                        {
                            if (File.Exists(item))
                            {
                                string target = Path.Combine(Paths.Modifications, Path.GetFileName(item));
                                File.Copy(item, target, true);
                                copiedFiles++;
                            }
                            else if (Directory.Exists(item))
                            {
                                foreach (var file in Directory.GetFiles(item, "*", SearchOption.AllDirectories))
                                {
                                    string rel = Path.GetRelativePath(TempRoot, file);
                                    string target = Path.Combine(Paths.Modifications, rel);
                                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                                    File.Copy(file, target, true);
                                    copiedFiles++;
                                }
                            }
                        }
                        StatusText = $"Successfully applied modifications ({copiedFiles} files).";
                    }
                    else
                    {
                        var saveDialog = new SaveFileDialog { FileName = "FroststrapMod.zip", Filter = "ZIP Archives (*.zip)|*.zip" };
                        if (saveDialog.ShowDialog() == true)
                        {
                            using (var archive = System.IO.Compression.ZipFile.Open(saveDialog.FileName, ZipArchiveMode.Create))
                            {
                                var items = new List<string> { Path.Combine(TempRoot, "ExtraContent"), Path.Combine(TempRoot, "content"), infoPath };
                                foreach (var item in items)
                                {
                                    if (File.Exists(item)) archive.CreateEntryFromFile(item, Path.GetFileName(item));
                                    else if (Directory.Exists(item))
                                    {
                                        foreach (var file in Directory.GetFiles(item, "*", SearchOption.AllDirectories))
                                            archive.CreateEntryFromFile(file, Path.GetRelativePath(TempRoot, file));
                                    }
                                }
                            }
                            StatusText = $"Mod saved to: {saveDialog.FileName}";
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

        private void SafeExtract(string zipPath, string targetDir)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath)) return;
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            Directory.CreateDirectory(targetDir);
            new FastZip().ExtractZip(zipPath, targetDir, null);
        }

        private bool IsValidHexColor(string hex) => !string.IsNullOrWhiteSpace(hex) &&
            System.Text.RegularExpressions.Regex.IsMatch(hex, "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");

        private void UpdateSolidColorFromHex(string hex)
        {
            try { _solidColor = System.Drawing.ColorTranslator.FromHtml(hex); }
            catch { _solidColor = System.Drawing.Color.White; }
        }

        private void UpdateGlyphColors()
        {
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(_solidColor.R, _solidColor.G, _solidColor.B));
            brush.Freeze();
            foreach (var item in GlyphItems) item.ColorBrush = brush;
        }

        private void OpenColorPicker()
        {
            var dlg = new System.Windows.Forms.ColorDialog { AllowFullOpen = true, Color = _solidColor };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _solidColor = dlg.Color;
                SolidColorHex = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}";
            }
        }

        private string FindFontFile(string displayName, string[] fontFiles) =>
            fontFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == $"BuilderIcons-{displayName}") ??
            fontFiles.FirstOrDefault() ?? string.Empty;

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

        private void ExportCursorSet()
        {
            if (SelectedCustomCursorSet is null)
                return;

            var dialog = new SaveFileDialog
            {
                FileName = $"{SelectedCustomCursorSet.Name}.zip",
                Filter = $"{Strings.FileTypes_ZipArchive}|*.zip"
            };

            if (dialog.ShowDialog() != true)
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

                using var outputStream = File.OpenWrite(dialog.FileName);
                memStream.CopyTo(outputStream);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::ExportCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to export cursor set:\n{ex.Message}", MessageBoxImage.Error);
                return;
            }

            Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
        }

        private void ImportCursorSet()
        {
            if (SelectedCustomCursorSet is null)
            {
                Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Import Cursor Set",
                Filter = $"{Strings.FileTypes_ZipArchive}|*.zip",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);

                ExtractZipToDirectory(dialog.FileName, tempPath);

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

        private void AddCursorImage(string fileName, string dialogTitle)
        {
            if (SelectedCustomCursorSet is null)
            {
                Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = dialogTitle,
                Filter = "PNG files (*.png)|*.png",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            string? destPath = GetCursorTargetPath(fileName);
            if (destPath is null)
                return;

            try
            {
                if (File.Exists(destPath))
                    File.Delete(destPath);

                File.Copy(dialog.FileName, destPath);
                UpdateCursorPathAndPreview(fileName, dialog.FileName);
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

            ImageSource? image = LoadImageSafely(fullPath);

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

        private ImageSource? _shiftlockCursorPreview;
        public ImageSource? ShiftlockCursorPreview
        {
            get => _shiftlockCursorPreview;
            set { _shiftlockCursorPreview = value; OnPropertyChanged(nameof(ShiftlockCursorPreview)); }
        }

        private ImageSource? _arrowCursorPreview;
        public ImageSource? ArrowCursorPreview
        {
            get => _arrowCursorPreview;
            set { _arrowCursorPreview = value; OnPropertyChanged(nameof(ArrowCursorPreview)); }
        }

        private ImageSource? _arrowFarCursorPreview;
        public ImageSource? ArrowFarCursorPreview
        {
            get => _arrowFarCursorPreview;
            set { _arrowFarCursorPreview = value; OnPropertyChanged(nameof(ArrowFarCursorPreview)); }
        }

        private ImageSource? _iBeamCursorPreview;
        public ImageSource? IBeamCursorPreview
        {
            get => _iBeamCursorPreview;
            set { _iBeamCursorPreview = value; OnPropertyChanged(nameof(IBeamCursorPreview)); }
        }

        private static BitmapImage? LoadImageSafely(string path)
        {
            if (!File.Exists(path)) return null;
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

        public Visibility AddShiftlockCursorVisibility => GetCursorAddVisibility("MouseLockedCursor.png");
        public Visibility DeleteShiftlockCursorVisibility => GetCursorDeleteVisibility("MouseLockedCursor.png");
        public Visibility AddArrowCursorVisibility => GetCursorAddVisibility("ArrowCursor.png");
        public Visibility DeleteArrowCursorVisibility => GetCursorDeleteVisibility("ArrowCursor.png");
        public Visibility AddArrowFarCursorVisibility => GetCursorAddVisibility("ArrowFarCursor.png");
        public Visibility DeleteArrowFarCursorVisibility => GetCursorDeleteVisibility("ArrowFarCursor.png");
        public Visibility AddIBeamCursorVisibility => GetCursorAddVisibility("IBeamCursor.png");
        public Visibility DeleteIBeamCursorVisibility => GetCursorDeleteVisibility("IBeamCursor.png");

        private Visibility GetCursorAddVisibility(string fileName)
        {
            string? path = GetCursorTargetPath(fileName);
            return path is not null && File.Exists(path) ? Visibility.Collapsed : Visibility.Visible;
        }

        private Visibility GetCursorDeleteVisibility(string fileName)
        {
            string? path = GetCursorTargetPath(fileName);
            return path is not null && File.Exists(path) ? Visibility.Visible : Visibility.Collapsed;
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
