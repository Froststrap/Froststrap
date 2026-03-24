using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using ICSharpCode.SharpZipLib.Zip;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Froststrap.UI.ViewModels.Settings.Mods
{
    public partial class ModGeneratorViewModel : ObservableObject
    {
        private Color _solidColor = Colors.White;

        public ModGeneratorViewModel()
        {
            GenerateModCommand = new AsyncRelayCommand(GenerateModAsync, CanGenerateMod);

            _ = LoadFontFilesAsync();
        }

        public event EventHandler? OpenModsEvent;
        public event EventHandler? OpenCommunityModsEvent;
        public event EventHandler? OpenPresetModsEvent;

        #region Commands

        public IAsyncRelayCommand GenerateModCommand { get; }

        [RelayCommand] private void OpenMods() => OpenModsEvent?.Invoke(this, EventArgs.Empty);
        [RelayCommand] private void OpenPresetMods() => OpenPresetModsEvent?.Invoke(this, EventArgs.Empty);
        [RelayCommand] private void OpenCommunityMods() => OpenCommunityModsEvent?.Invoke(this, EventArgs.Empty);

        #endregion

        #region Observable Properties

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateModCommand))]
        private string _solidColorHex = "#FFFFFF";

        [ObservableProperty]
        private double _progress = 0;

        [ObservableProperty]
        private bool _isProgressVisible = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateModCommand))]
        private bool _isNotGeneratingMod = true;

        [ObservableProperty]
        private string _statusText = "";

        [ObservableProperty]
        private bool _colorCursors = false;

        [ObservableProperty]
        private bool _colorShiftlock = false;

        [ObservableProperty]
        private bool _colorEmoteWheel = false;

        [ObservableProperty]
        private bool _includeModifications = true;

        [ObservableProperty]
        private SolidColorBrush _previewBrush = new(Colors.White);

        [ObservableProperty]
        private ObservableCollection<string> _fontDisplayNames = new();

        [ObservableProperty]
        private ObservableCollection<GlyphItem> _glyphItems = new();

        private string? _selectedFontDisplayName;
        public string? SelectedFontDisplayName
        {
            get => _selectedFontDisplayName;
            set
            {
                if (SetProperty(ref _selectedFontDisplayName, value))
                {
                    OnSelectedFontChanged();
                }
            }
        }

        #endregion

        public Color SelectedMediaColor
        {
            get => Color.FromRgb(_solidColor.R, _solidColor.G, _solidColor.B);
            set
            {
                _solidColor = Color.FromArgb(value.A, value.R, value.G, value.B);
                SolidColorHex = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}";

                OnPropertyChanged(nameof(SolidColorHex));
                OnPropertyChanged(nameof(SelectedMediaColor));

                UpdateGlyphColors();
                StatusText = "Ready to generate mod.";
            }
        }

        partial void OnSolidColorHexChanged(string value)
        {
            if (IsValidHexColor(value))
            {
                UpdateSolidColorFromHex(value);
                UpdateGlyphColors();
                OnPropertyChanged(nameof(SelectedMediaColor));
                StatusText = "Ready to generate mod.";
            }
            else
            {
                StatusText = "Enter a valid hex color (e.g., #FF0000)";
            }
        }

        private bool CanGenerateMod() => IsValidHexColor(SolidColorHex) && IsNotGeneratingMod;

        private string TempRoot => Path.Combine(Path.GetTempPath(), "Froststrap");
        private string FontDir => Path.Combine(Paths.Cache, "FontPreview");

        private async Task LoadFontFilesAsync()
        {
            try
            {
                if (!Directory.Exists(FontDir))
                    Directory.CreateDirectory(FontDir);

                var fontFiles = Directory.GetFiles(FontDir)
                    .Where(f => f.EndsWith(".ttf") || f.EndsWith(".otf"))
                    .ToArray();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    FontDisplayNames.Clear();
                    foreach (var file in fontFiles)
                        FontDisplayNames.Add(Path.GetFileNameWithoutExtension(file).Replace("BuilderIcons-", ""));

                    if (FontDisplayNames.Count > 0)
                        SelectedFontDisplayName = FontDisplayNames[0];
                });

                StatusText = "Ready to generate mod.";
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ModGenerator::LoadFontFiles", ex);
                StatusText = "Failed to load preview fonts.";
            }
        }


        private async void OnSelectedFontChanged()
        {
            if (string.IsNullOrEmpty(SelectedFontDisplayName) || !IsValidHexColor(SolidColorHex))
            {
                GlyphItems = new();
                return;
            }

            var fontFiles = Directory.GetFiles(FontDir)
                                     .Where(f => f.EndsWith(".ttf") || f.EndsWith(".otf"))
                                     .ToArray();

            string selectedFontPath = FindFontFile(SelectedFontDisplayName, fontFiles);

            if (!string.IsNullOrEmpty(selectedFontPath) && File.Exists(selectedFontPath))
                await LoadGlyphPreviewsAsync(selectedFontPath);
        }

        private string FindFontFile(string displayName, string[] fontFiles)
        {
            return fontFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals($"BuilderIcons-{displayName}", StringComparison.OrdinalIgnoreCase))
                   ?? fontFiles.FirstOrDefault()
                   ?? string.Empty;
        }

        private bool IsFileReady(string filename)
        {
            try
            {
                using var fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None);
                return fs.Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private async Task LoadGlyphPreviewsAsync(string fontPath)
        {
            if (!File.Exists(fontPath) || !IsFileReady(fontPath)) return;

            var glyphItems = new ObservableCollection<GlyphItem>();
            UpdateGlyphColors();

            try
            {
                string variantName = Path.GetFileNameWithoutExtension(fontPath);

                // Damn you Avalonia for not allowing me to render non Avalonia resource font files
                var fontFamily = variantName.EndsWith("Filled")
                    ? (Avalonia.Media.FontFamily)Avalonia.Application.Current!.Resources["BuilderIconsFilled"]!
                    : (Avalonia.Media.FontFamily)Avalonia.Application.Current!.Resources["BuilderIconsRegular"]!;

                var typeface = new Typeface(fontFamily);

                var characterCodes = Enumerable.Range(0xF101, 495).ToList();

                foreach (var characterCode in characterCodes)
                {
                    string text = char.ConvertFromUtf32(characterCode);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            var ft = new FormattedText(
                                text,
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                40,
                                PreviewBrush);

                            var geometry = ft.BuildGeometry(new Avalonia.Point(0, 0));
                            if (geometry == null || geometry.Bounds.Width < 1) return;

                            var bounds = geometry.Bounds;
                            var translate = new TranslateTransform(
                                (50 - bounds.Width) / 2 - bounds.X,
                                (50 - bounds.Height) / 2 - bounds.Y
                            );
                            geometry.Transform = translate;

                            glyphItems.Add(new GlyphItem
                            {
                                Data = geometry,
                                ColorBrush = PreviewBrush
                            });
                        }
                        catch (Exception ex)
                        {
                            App.Logger?.WriteException("ModGenerator::LoadGlyphPreview", ex);
                        }
                    });
                }

                GlyphItems = glyphItems;
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ModGenerator::LoadGlyphPreviews", ex);
                StatusText = "Failed to load font glyphs.";
            }
        }

        private async Task GenerateModAsync()
        {
            const string LOG_IDENT = "ModGenerator";
            if (!IsValidHexColor(SolidColorHex)) return;

            IsNotGeneratingMod = false;
            IsProgressVisible = true;
            Progress = 0;
            StatusText = "Starting mod generation...";

            try
            {
                await Task.Run(async () =>
                {
                    StatusText = "Downloading required assets...";
                    Progress = 5;
                    var (luaZip, extraZip, contentZip, vHash, vName) = await ModGenerator.DownloadForModGenerator();

                    StatusText = "Extracting files...";
                    Progress = 25;
                    string luaDir = Path.Combine(TempRoot, "ExtraContent", "LuaPackages");
                    string extraDir = Path.Combine(TempRoot, "ExtraContent", "textures");
                    string contentDir = Path.Combine(TempRoot, "content", "textures");
                    string folderName = SolidColorHex;

                    Parallel.Invoke(
                        () => SafeExtract(luaZip, luaDir),
                        () => SafeExtract(extraZip, extraDir),
                        () => SafeExtract(contentZip, contentDir)
                    );

                    StatusText = "Recoloring assets...";
                    Progress = 50;
                    var mappings = await ModGenerator.LoadMappingsAsync();

                    ModGenerator.RecolorAllPngs(TempRoot, _solidColor, mappings, ColorCursors, ColorShiftlock, ColorEmoteWheel);
                    Progress = 70;
                    await ModGenerator.RecolorFontsAsync(TempRoot, _solidColor, folderName);

                    StatusText = "Cleaning up...";
                    Progress = 80;

                    var preservePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in mappings.Values)
                        preservePaths.Add(Path.GetFullPath(Path.Combine(TempRoot, Path.Combine(entry))));

                    string builderIconsFontDir = Path.Combine(TempRoot, "ExtraContent", "LuaPackages", "Packages", "_Index", "BuilderIcons", "BuilderIcons", "Font");
                    if (Directory.Exists(builderIconsFontDir))
                    {
                        preservePaths.Add(Path.GetFullPath(builderIconsFontDir));
                        foreach (var fontFile in Directory.GetFiles(builderIconsFontDir, "*.*"))
                            preservePaths.Add(Path.GetFullPath(fontFile));
                    }

                    if (ColorCursors)
                    {
                        preservePaths.Add(Path.GetFullPath(Path.Combine(TempRoot, "content", "textures", "Cursors", "KeyboardMouse", "IBeamCursor.png")));
                        preservePaths.Add(Path.GetFullPath(Path.Combine(TempRoot, "content", "textures", "Cursors", "KeyboardMouse", "ArrowCursor.png")));
                        preservePaths.Add(Path.GetFullPath(Path.Combine(TempRoot, "content", "textures", "Cursors", "KeyboardMouse", "ArrowFarCursor.png")));
                    }

                    if (ColorShiftlock)
                        preservePaths.Add(Path.GetFullPath(Path.Combine(TempRoot, "content", "textures", "MouseLockedCursor.png")));

                    if (ColorEmoteWheel)
                    {
                        string emotesDir = Path.Combine(TempRoot, "content", "textures", "ui", "Emotes", "Large");
                        string[] emoteFiles = { "SelectedGradient.png", "SelectedGradient@2x.png", "SelectedGradient@3x.png", "SelectedLine.png", "SelectedLine@2x.png", "SelectedLine@3x.png" };
                        foreach (var e in emoteFiles)
                            preservePaths.Add(Path.GetFullPath(Path.Combine(emotesDir, e)));
                    }

                    void DeleteExcept(string dir)
                    {
                        foreach (var file in Directory.GetFiles(dir))
                            if (!preservePaths.Contains(Path.GetFullPath(file)))
                                try { File.Delete(file); } catch { }

                        foreach (var subDir in Directory.GetDirectories(dir))
                        {
                            DeleteExcept(subDir);
                            try
                            {
                                if (!Directory.EnumerateFileSystemEntries(subDir).Any() && !preservePaths.Contains(Path.GetFullPath(subDir)))
                                    Directory.Delete(subDir);
                            }
                            catch { }
                        }
                    }

                    if (Directory.Exists(luaDir)) DeleteExcept(luaDir);
                    if (Directory.Exists(extraDir)) DeleteExcept(extraDir);
                    if (Directory.Exists(contentDir)) DeleteExcept(contentDir);

                    string infoPath = Path.Combine(TempRoot, "info.json");
                    var infoData = new
                    {
                        FroststrapVersion = App.Version,
                        RobloxVersion = vName,
                        RobloxVersionHash = vHash,
                        ColorsUsed = new { SolidColor = SolidColorHex }
                    };
                    await File.WriteAllTextAsync(infoPath, JsonSerializer.Serialize(infoData, new JsonSerializerOptions { WriteIndented = true }));

                    StatusText = "Packaging...";
                    Progress = 90;

                    if (IncludeModifications)
                    {
                        string targetFolder = Path.Combine(Paths.Modifications, folderName);
                        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

                        int copiedFiles = 0;
                        var itemsToCopy = new List<string>
                {
                    Path.Combine(TempRoot, "ExtraContent"),
                    Path.Combine(TempRoot, "content"),
                    infoPath
                };

                        foreach (var item in itemsToCopy)
                        {
                            if (File.Exists(item))
                            {
                                File.Copy(item, Path.Combine(targetFolder, Path.GetFileName(item)), true);
                                copiedFiles++;
                            }
                            else if (Directory.Exists(item))
                            {
                                foreach (var file in Directory.GetFiles(item, "*", SearchOption.AllDirectories))
                                {
                                    string target = Path.Combine(targetFolder, Path.GetRelativePath(TempRoot, file));
                                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                                    File.Copy(file, target, true);
                                    copiedFiles++;
                                }
                            }
                        }

                        Progress = 100;
                        StatusText = $"Successfully applied modifications ({copiedFiles} files).";
                    }
                    else
                    {
                        StatusText = "Zipping results...";

                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var visualRoot = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                                ? desktop.MainWindow
                                : null;

                            if (visualRoot == null) return;

                            var file = await visualRoot.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                            {
                                Title = "Save Froststrap Mod",
                                SuggestedFileName = $"FroststrapMod_{SolidColorHex}.zip",
                                DefaultExtension = ".zip",
                                FileTypeChoices = new[] { new FilePickerFileType("Zip Archive") { Patterns = new[] { "*.zip" } } }
                            });

                            if (file != null)
                            {
                                string localPath = file.Path.LocalPath;
                                ModGenerator.ZipResult(TempRoot, localPath);
                                Progress = 100;
                                StatusText = $"Mod saved to {Path.GetFileName(localPath)}";
                            }
                            else
                            {
                                StatusText = "Mod generation cancelled.";
                            }
                        });
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
                IsProgressVisible = false;
                Progress = 0;
            }
        }

        private void SafeExtract(string zipPath, string targetDir)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath)) return;
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            Directory.CreateDirectory(targetDir);
            new FastZip().ExtractZip(zipPath, targetDir, null);
        }

        private bool IsValidHexColor(string hex) =>
            !string.IsNullOrWhiteSpace(hex) && Regex.IsMatch(hex, "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");

        private void UpdateSolidColorFromHex(string hex)
        {
            try { _solidColor = Color.Parse(hex); }
            catch { _solidColor = Colors.White; }
        }

        private void UpdateGlyphColors()
        {
            PreviewBrush.Color = Color.FromRgb(_solidColor.R, _solidColor.G, _solidColor.B);
        }
    }
}