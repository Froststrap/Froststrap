using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO.Compression;
using Bloxstrap.Integrations;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class ModGeneratorViewModel : NotifyPropertyChangedViewModel
    {
        public event EventHandler? OpenModsEvent;
        public event EventHandler? OpenCommunityModsEvent;
        public event EventHandler? OpenPresetModsEvent;
        private void OpenMods() => OpenModsEvent?.Invoke(this, EventArgs.Empty);
        private void OpenCommunityMods() => OpenCommunityModsEvent?.Invoke(this, EventArgs.Empty);
        private void OpenPresetMods() => OpenPresetModsEvent?.Invoke(this, EventArgs.Empty);
        public ICommand OpenModsCommand => new RelayCommand(OpenMods);
        public ICommand OpenCommunityModsCommand => new RelayCommand(OpenCommunityMods);
        public ICommand OpenPresetModsCommand => new RelayCommand(OpenPresetMods);

        public ModGeneratorViewModel()
        {
            _ = LoadFontFilesAsync();
        }

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
            Regex.IsMatch(hex, "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");

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
    }
}
