using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Windows.Input;
using Color = Avalonia.Media.Color;

namespace Froststrap.UI.ViewModels.Settings.Mods
{
    public partial class ModGeneratorViewModel : NotifyPropertyChangedViewModel
    {
        private Color _solidColor = Colors.White;

        public bool IsGradientMode => GradientStops.Count >= 2;

        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public ModGeneratorViewModel()
        {
            GenerateModCommand = new AsyncRelayCommand(GenerateModAsync, CanGenerateMod);

            GradientStops.Add(new Models.GradientStops { Offset = 0.0, Color = "#FFFFFF" });
            GradientStops.Add(new Models.GradientStops { Offset = 1.0, Color = "#000000" });

            GradientStops.CollectionChanged += (s, e) => OnGradientCollectionChanged();
            foreach (var stop in GradientStops)
                stop.PropertyChanged += OnGradientStopPropertyChanged;

            AddGradientStopCommand = new RelayCommand(AddGradientStop);
            RemoveGradientStopCommand = new RelayCommand<Models.GradientStops?>(RemoveGradientStop);
            OpenColorPickerCommand = new RelayCommand<Models.GradientStops?>(OpenColorPickerAsync);

            SelectRobloxIconCommand = new RelayCommand(SelectRobloxIcon);
            ClearRobloxIconCommand = new RelayCommand(() => RobloxIconImagePath = "");

            SelectSpinnerIconCommand = new RelayCommand(SelectSpinnerIcon);
            ClearSpinnerIconCommand = new RelayCommand(() => CustomSpinnerPath = "");

            _ = LoadFontFilesAsync();
        }

        public event EventHandler? OpenModsEvent;
        public event EventHandler? OpenCommunityModsEvent;
        public event EventHandler? OpenPresetModsEvent;

        #region Commands
        public IAsyncRelayCommand GenerateModCommand { get; }
        public ICommand AddGradientStopCommand { get; }
        public ICommand RemoveGradientStopCommand { get; }
        public ICommand OpenColorPickerCommand { get; }
        public ICommand SelectRobloxIconCommand { get; }
        public ICommand ClearRobloxIconCommand { get; }
        public ICommand SelectSpinnerIconCommand { get; }
        public ICommand ClearSpinnerIconCommand { get; }

        [RelayCommand] private void OpenMods() => OpenModsEvent?.Invoke(this, EventArgs.Empty);
        [RelayCommand] private void OpenPresetMods() => OpenPresetModsEvent?.Invoke(this, EventArgs.Empty);
        [RelayCommand] private void OpenCommunityMods() => OpenCommunityModsEvent?.Invoke(this, EventArgs.Empty);
        #endregion

        #region Observable Properties

        private string _solidColorHex = "#FFFFFF";
        public string SolidColorHex
        {
            get => _solidColorHex;
            set
            {
                var cleaned = string.IsNullOrWhiteSpace(value)
                    ? value
                    : string.Concat(value.Where(c => !char.IsWhiteSpace(c)));

                if (SetProperty(ref _solidColorHex, cleaned))
                    GenerateModCommand.NotifyCanExecuteChanged();
            }
        }

        private double _progress = 0;
        public double Progress { get => _progress; set => SetProperty(ref _progress, value); }

        private bool _isProgressVisible = false;
        public bool IsProgressVisible { get => _isProgressVisible; set => SetProperty(ref _isProgressVisible, value); }

        private bool _isNotGeneratingMod = true;
        public bool IsNotGeneratingMod
        {
            get => _isNotGeneratingMod;
            set
            {
                if (SetProperty(ref _isNotGeneratingMod, value))
                    GenerateModCommand.NotifyCanExecuteChanged();
            }
        }

        private string _statusText = "";
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private bool _colorCursors = false;
        public bool ColorCursors { get => _colorCursors; set => SetProperty(ref _colorCursors, value); }

        private bool _colorShiftlock = false;
        public bool ColorShiftlock { get => _colorShiftlock; set => SetProperty(ref _colorShiftlock, value); }

        private bool _colorEmoteWheel = false;
        public bool ColorEmoteWheel { get => _colorEmoteWheel; set => SetProperty(ref _colorEmoteWheel, value); }

        private bool _includeModifications = true;
        public bool IncludeModifications { get => _includeModifications; set => SetProperty(ref _includeModifications, value); }

        private SolidColorBrush _previewBrush = new(Colors.White);
        public SolidColorBrush PreviewBrush { get => _previewBrush; set => SetProperty(ref _previewBrush, value); }

        private ObservableCollection<string> _fontDisplayNames = [];
        public ObservableCollection<string> FontDisplayNames { get => _fontDisplayNames; set => SetProperty(ref _fontDisplayNames, value); }

        private ObservableCollection<GlyphItem> _glyphItems = [];
        public ObservableCollection<GlyphItem> GlyphItems { get => _glyphItems; set => SetProperty(ref _glyphItems, value); }

        private ObservableCollection<Models.GradientStops> _gradientStops = [];
        public ObservableCollection<Models.GradientStops> GradientStops { get => _gradientStops; set => SetProperty(ref _gradientStops, value); }

        private double _gradientAngle = 90;
        public double GradientAngle
        {
            get => _gradientAngle;
            set
            {
                if (SetProperty(ref _gradientAngle, value))
                {
                    _ = Dispatcher.UIThread.InvokeAsync(() => OnSelectedFontChanged());
                    GenerateModCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string? _selectedFontDisplayName;
        public string? SelectedFontDisplayName
        {
            get => _selectedFontDisplayName;
            set
            {
                if (SetProperty(ref _selectedFontDisplayName, value))
                    OnSelectedFontChanged();
            }
        }

        private string _modFolderName = "";
        public string ModFolderName
        {
            get => _modFolderName;
            set
            {
                if (SetProperty(ref _modFolderName, value))
                {
                    ValidateFolderName();
                    GenerateModCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _folderNameValidationError = "";
        public string FolderNameValidationError
        {
            get => _folderNameValidationError;
            set
            {
                if (SetProperty(ref _folderNameValidationError, value))
                    OnPropertyChanged(nameof(HasFolderNameValidationError));
            }
        }

        private string _robloxIconImagePath = "";
        public string RobloxIconImagePath
        {
            get => _robloxIconImagePath;
            set => SetProperty(ref _robloxIconImagePath, value);
        }

        private int _gradientBands = 32;
        public int GradientBands
        {
            get => _gradientBands;
            set
            {
                if (value < 8) value = 8;
                if (value > 512) value = 512;
                if (SetProperty(ref _gradientBands, value))
                {
                    GenerateModCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _dontColorPlayIcon = false;
        public bool DontColorPlayIcon
        {
            get => _dontColorPlayIcon;
            set => SetProperty(ref _dontColorPlayIcon, value);
        }

        private string _customSpinnerPath = "";
        public string CustomSpinnerPath
        {
            get => _customSpinnerPath;
            set => SetProperty(ref _customSpinnerPath, value);
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
                if (GradientStops.Count > 0)
                    GradientStops[0].Color = SolidColorHex;
                UpdateGlyphColors();
                StatusText = Strings.Menu_ModGenerator_ReadyToGenerate;
            }
        }

        private bool CanGenerateMod()
        {
            if (!IsNotGeneratingMod) return false;
            if (!GradientStops.All(s => IsValidHexColor(s.Color))) return false;

            if (IncludeModifications)
            {
                if (string.IsNullOrWhiteSpace(ModFolderName))
                    return true;
                else
                    return string.IsNullOrEmpty(FolderNameValidationError);
            }
            return true;
        }

        public static string SuggestedFolderName => GetNextAvailableModFolderName();

        public bool HasFolderNameValidationError => !string.IsNullOrEmpty(FolderNameValidationError);

        private static string TempRoot => Path.Combine(Path.GetTempPath(), "Froststrap");

        private async Task LoadFontFilesAsync()
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    FontDisplayNames.Clear();
                    FontDisplayNames.Add("Regular");
                    FontDisplayNames.Add("Filled");
                    if (FontDisplayNames.Count > 0)
                        SelectedFontDisplayName = FontDisplayNames[0];
                });
                StatusText = Strings.Menu_ModGenerator_ReadyToGenerate;
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ModGenerator::LoadFontFiles", ex);
                StatusText = Strings.Menu_ModGenerator_FailedPreview;
            }
        }

        private async void OnSelectedFontChanged()
        {
            if (string.IsNullOrEmpty(SelectedFontDisplayName) || !IsValidHexColor(SolidColorHex))
            {
                GlyphItems = [];
                return;
            }
            await LoadGlyphPreviewsAsync(SelectedFontDisplayName);
        }

        private async Task LoadGlyphPreviewsAsync(string fontVariant)
        {
            var glyphItems = new ObservableCollection<GlyphItem>();
            UpdateGlyphColors();
            try
            {
                var resourceKey = string.Equals(fontVariant, "Filled", StringComparison.OrdinalIgnoreCase)
                    ? "BuilderIconsFilled" : "BuilderIconsRegular";

                Avalonia.Media.FontFamily? fontFamily = null;
                if (Avalonia.Application.Current?.Resources.TryGetResource(resourceKey, null, out object? resource) == true)
                    fontFamily = resource as Avalonia.Media.FontFamily;

                fontFamily ??= string.Equals(fontVariant, "Filled", StringComparison.OrdinalIgnoreCase)
                    ? new Avalonia.Media.FontFamily("avares://Froststrap/Resources/Fonts#BuilderIcons-Filled")
                    : new Avalonia.Media.FontFamily("avares://Froststrap/Resources/Fonts#BuilderIcons-Regular");

                var typeface = new Typeface(fontFamily);
                var characterCodes = Enumerable.Range(0xF101, 495).ToList();

                IBrush glyphBrush = IsGradientMode ? CreateGradientBrush() : PreviewBrush;

                foreach (var characterCode in characterCodes)
                {
                    string text = char.ConvertFromUtf32(characterCode);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                typeface, 40, PreviewBrush);
                            var geometry = ft.BuildGeometry(new Avalonia.Point(0, 0));
                            if (geometry == null || geometry.Bounds.Width < 1) return;

                            var bounds = geometry.Bounds;
                            var translate = new TranslateTransform(
                                (50 - bounds.Width) / 2 - bounds.X,
                                (50 - bounds.Height) / 2 - bounds.Y);
                            geometry.Transform = translate;

                            glyphItems.Add(new GlyphItem { Data = geometry, Brush = glyphBrush });
                        }
                        catch (Exception ex) { App.Logger?.WriteException("ModGenerator::LoadGlyphPreview", ex); }
                    });
                }
                GlyphItems = glyphItems;
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ModGenerator::LoadGlyphPreviews", ex);
                StatusText = Strings.Menu_ModGenerator_FailedPreview;
            }
        }

        private void ValidateFolderName()
        {
            if (string.IsNullOrWhiteSpace(ModFolderName))
            {
                FolderNameValidationError = "";
                return;
            }

            if (ModFolderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                FolderNameValidationError = Strings.Menu_ModGeneratorName_Invalid;
                return;
            }

            string fullPath = Path.Combine(Paths.Modifications, ModFolderName);
            if (Directory.Exists(fullPath))
                FolderNameValidationError = Strings.Menu_ModGeneratorName_Exists;
            else
                FolderNameValidationError = "";
        }

        private static string GetNextAvailableModFolderName()
        {
            string baseName = "Generated Mod";
            string folder = Path.Combine(Paths.Modifications, baseName);
            if (!Directory.Exists(folder))
                return baseName;

            int counter = 1;
            while (true)
            {
                string candidate = $"{baseName} {counter}";
                folder = Path.Combine(Paths.Modifications, candidate);
                if (!Directory.Exists(folder))
                    return candidate;
                counter++;
            }
        }

        private IBrush CreateGradientBrush()
        {
            if (!IsGradientMode || GradientStops.Count < 2)
                return PreviewBrush;

            var stops = GradientStops.OrderBy(s => s.Offset).ToList();
            var avaloniaStops = new Avalonia.Media.GradientStops();
            foreach (var stop in stops)
            {
                if (IsValidHexColor(stop.Color))
                    avaloniaStops.Add(new Avalonia.Media.GradientStop(Color.Parse(stop.Color), stop.Offset));
            }
            if (avaloniaStops.Count < 2)
                return PreviewBrush;

            var brush = new LinearGradientBrush
            {
                GradientStops = avaloniaStops
            };

            double angleRad = GradientAngle * Math.PI / 180.0;
            double dx = Math.Sin(angleRad);
            double dy = -Math.Cos(angleRad);
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 0)
            {
                dx /= len;
                dy /= len;
            }
            brush.StartPoint = new RelativePoint(0.5 - dx / 2, 0.5 - dy / 2, RelativeUnit.Relative);
            brush.EndPoint = new RelativePoint(0.5 + dx / 2, 0.5 + dy / 2, RelativeUnit.Relative);

            return brush;
        }

        private async Task GenerateModAsync()
        {
            const string LOG_IDENT = "ModGenerator";
            if (!IsValidHexColor(SolidColorHex)) return;

            IsNotGeneratingMod = false;
            IsProgressVisible = true;
            Progress = 0;
            StatusText = Strings.Menu_ModGenerator_Starting;

            try
            {
                await Task.Run(async () =>
                {
                    StatusText = Strings.Menu_ModGenerator_Downnloading;
                    Progress = 5;
                    var (luaZip, extraZip, contentZip, vHash, vName) = await ModGenerator.DownloadForModGenerator();

                    StatusText = Strings.Menu_ModGenerator_Extracting;
                    Progress = 25;
                    string luaDir = Path.Combine(TempRoot, "ExtraContent", "LuaPackages");
                    string extraDir = Path.Combine(TempRoot, "ExtraContent", "textures");
                    string contentDir = Path.Combine(TempRoot, "content", "textures");

                    string tempFolderName = SolidColorHex;

                    Parallel.Invoke(
                        () => SafeExtract(luaZip, luaDir),
                        () => SafeExtract(extraZip, extraDir),
                        () => SafeExtract(contentZip, contentDir));

                    StatusText = Strings.Menu_ModGenerator_RecoloringPNGS;
                    Progress = 50;
                    string foundationImagesDir = Path.Combine(TempRoot, "ExtraContent", "LuaPackages", "Packages", "_Index", "FoundationImages", "FoundationImages", "SpriteSheets");
                    string? getImageSetDataPath = Path.Combine(TempRoot, "ExtraContent", "LuaPackages", "Packages", "_Index", "FoundationImages", "FoundationImages", "Generated", "GetImageSetData.lua");

                    var mappings = await ModGenerator.LoadMappingsAsync();

                    List<ModGenerator.GradientStop>? gradientStopsForSprites = null;
                    float gradientAngleForSprites = 0f;
                    if (IsGradientMode)
                    {
                        gradientStopsForSprites = [.. GradientStops.OrderBy(s => s.Offset)
                            .Select(s => new ModGenerator.GradientStop((float)s.Offset, Color.Parse(s.Color)))];
                        gradientAngleForSprites = (float)GradientAngle;
                    }

                    ModGenerator.RecolorAllPngs(
                        TempRoot,
                        _solidColor,
                        mappings,
                        ColorCursors,
                        ColorShiftlock,
                        ColorEmoteWheel,
                        gradientStopsForSprites,
                        gradientAngleForSprites,
                        getImageSetDataPath,
                        RobloxIconImagePath,
                        CustomSpinnerPath
                    );
                    Progress = 70;

                    StatusText = Strings.Menu_ModGenerator_RecoloringFonts;

                    string? gradientArg = null;
                    double? angleArg = null;
                    if (IsGradientMode)
                    {
                        var orderedStops = GradientStops.OrderBy(s => s.Offset);
                        gradientArg = string.Join(",", orderedStops.Select(s => $"{s.Offset}:{s.Color.TrimStart('#')}"));
                        angleArg = GradientAngle;
                    }

                    string modFolderName = string.IsNullOrWhiteSpace(ModFolderName)
                        ? GetNextAvailableModFolderName()
                        : ModFolderName;

                    string? imageMapArg = null;
                    if (!string.IsNullOrWhiteSpace(RobloxIconImagePath) && File.Exists(RobloxIconImagePath))
                        imageMapArg = $"tilt:{RobloxIconImagePath}";

                    int? bandsArg = GradientBands > 0 ? GradientBands : (int?)null;

                    string? skipGlyphsArg = null;
                    if (DontColorPlayIcon)
                    {
                        skipGlyphsArg = "play-large,play-small";
                    }

                    await ModGenerator.RecolorFontsAsync(TempRoot, _solidColor, gradientArg, angleArg, imageMapArg, bandsArg, skipGlyphsArg);

                    WriteBuilderIconsJson(TempRoot);

                    StatusText = Strings.Menu_ModGenerator_CleaningUp;
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

                    string builderIconsJsonPath = Path.Combine(TempRoot, "ExtraContent", "LuaPackages", "Packages", "_Index", "BuilderIcons", "BuilderIcons", "BuilderIcons.json");
                    preservePaths.Add(Path.GetFullPath(builderIconsJsonPath));

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
                        string[] emoteFiles = ["SelectedGradient.png", "SelectedGradient@2x.png", "SelectedGradient@3x.png", "SelectedLine.png", "SelectedLine@2x.png", "SelectedLine@3x.png"];
                        foreach (var e in emoteFiles)
                            preservePaths.Add(Path.GetFullPath(Path.Combine(emotesDir, e)));
                    }

                    preservePaths.Add(Path.GetFullPath(foundationImagesDir));

                    void DeleteExcept(string dir)
                    {
                        if (preservePaths.Contains(Path.GetFullPath(dir)))
                            return;

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
                        ColorsUsed = IsGradientMode
                            ? new { GradientStops = GradientStops.Select(s => new { s.Offset, s.Color }), Angle = GradientAngle }
                            : (object)new { SolidColor = SolidColorHex }
                    };
                    await File.WriteAllTextAsync(infoPath, JsonSerializer.Serialize(infoData, _jsonOptions));

                    StatusText = Strings.Menu_ModGenerator_Packaging;
                    Progress = 90;

                    if (IncludeModifications)
                    {
                        string targetFolder = Path.Combine(Paths.Modifications, modFolderName);
                        if (!Directory.Exists(targetFolder))
                            Directory.CreateDirectory(targetFolder);

                        int copiedFiles = 0;
                        var itemsToCopy = new List<string> { Path.Combine(TempRoot, "ExtraContent"), Path.Combine(TempRoot, "content"), infoPath };
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

                        var existingMod = App.State.Prop.Mods.FirstOrDefault(m =>
                            string.Equals(m.FolderName, modFolderName, StringComparison.OrdinalIgnoreCase));

                        if (existingMod is null)
                        {
                            int maxPriority = App.State.Prop.Mods.Count > 0 ? App.State.Prop.Mods.Max(m => m.Priority) : 0;
                            var newMod = new ModConfig
                            {
                                FolderName = modFolderName,
                                Enabled = true,
                                Priority = maxPriority + 1,
                                Target = ModTarget.Both
                            };
                            App.State.Prop.Mods.Add(newMod);
                            App.State.SaveSetting("Mods");
                            App.Logger.WriteLine(LOG_IDENT, $"Added mod '{modFolderName}' to state.");
                        }
                        else
                        {
                            existingMod.Enabled = true;
                            App.State.SaveSetting("Mods");
                        }

                        Progress = 100;
                        StatusText = string.Format(Strings.Menu_ModGenerator_AppliedModifications, copiedFiles);
                    }
                    else
                    {
                        StatusText = "Zipping results...";
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var visualRoot = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                            if (visualRoot == null) return;
                            var file = await visualRoot.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                            {
                                Title = "Save Froststrap Mod",
                                SuggestedFileName = $"FroststrapMod_{SolidColorHex}.zip",
                                DefaultExtension = ".zip",
                                FileTypeChoices = [new FilePickerFileType("Zip Archive") { Patterns = ["*.zip"] }]
                            });

                            if (file != null)
                            {
                                ModGenerator.ZipResult(TempRoot, file.Path.LocalPath);
                                Progress = 100;
                                StatusText = string.Format(Strings.Menu_ModGenerator_SavedTo, Path.GetFileName(file.Path.LocalPath));
                            }
                            else
                            {
                                StatusText = Strings.Menu_ModGenerator_Cancelled;
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

        private static void WriteBuilderIconsJson(string targetDir)
        {
            var jsonPath = Path.Combine(targetDir, "ExtraContent", "LuaPackages", "Packages", "_Index", "BuilderIcons", "BuilderIcons", "BuilderIcons.json");
            var dir = Path.GetDirectoryName(jsonPath);
            if (dir == null) return;
            Directory.CreateDirectory(dir);

            var jsonContent = @"{
  ""name"": ""Builder Icons"",
  ""loadStrategy"": ""sameFamilyOnly"",
  ""faces"": [
    {
      ""name"": ""Regular"",
      ""weight"": 400,
      ""style"": ""normal"",
      ""assetId"": ""rbxasset://LuaPackages/Packages/_Index/BuilderIcons/BuilderIcons/Font/BuilderIcons-Regular.otf""
    },
    {
      ""name"": ""Bold"",
      ""weight"": 700,
      ""style"": ""normal"",
      ""assetId"": ""rbxasset://LuaPackages/Packages/_Index/BuilderIcons/BuilderIcons/Font/BuilderIcons-Filled.otf""
    }
  ]
}";
            File.WriteAllText(jsonPath, jsonContent);
        }

        private static void SafeExtract(string zipPath, string targetDir)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath)) return;
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            Directory.CreateDirectory(targetDir);

            string targetRoot = Path.GetFullPath(targetDir) + Path.DirectorySeparatorChar;

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.FullName))
                    continue;

                string normalizedEntry = entry.FullName
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar)
                    .TrimStart(Path.DirectorySeparatorChar);

                if (string.IsNullOrWhiteSpace(normalizedEntry))
                    continue;

                string destinationPath = Path.GetFullPath(Path.Combine(targetDir, normalizedEntry));
                if (!destinationPath.StartsWith(targetRoot, StringComparison.Ordinal))
                    continue;

                bool isDirectory = string.IsNullOrEmpty(entry.Name)
                    || entry.FullName.EndsWith('/')
                    || entry.FullName.EndsWith('\\');

                if (isDirectory)
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                string? parent = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }

        private static bool IsValidHexColor(string hex) =>
            !string.IsNullOrWhiteSpace(hex) && Regex.IsMatch(hex, "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");

        private void UpdateGlyphColors() => PreviewBrush.Color = Color.FromRgb(_solidColor.R, _solidColor.G, _solidColor.B);

        private void OnGradientCollectionChanged()
        {
            OnPropertyChanged(nameof(IsGradientMode));
            UpdateSolidColorFromGradient();
            _ = Dispatcher.UIThread.InvokeAsync(() => OnSelectedFontChanged());
            GenerateModCommand.NotifyCanExecuteChanged();
        }

        private void OnGradientStopPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Models.GradientStops.Color))
                UpdateSolidColorFromGradient();
            _ = Dispatcher.UIThread.InvokeAsync(() => OnSelectedFontChanged());
            GenerateModCommand.NotifyCanExecuteChanged();
        }

        private void UpdateSolidColorFromGradient()
        {
            if (GradientStops.Any())
            {
                var firstHex = GradientStops[0].Color;
                if (IsValidHexColor(firstHex))
                {
                    _solidColor = Color.Parse(firstHex);
                    SolidColorHex = firstHex;
                    OnPropertyChanged(nameof(SelectedMediaColor));
                    UpdateGlyphColors();
                }
            }
        }

        private void AddGradientStop()
        {
            var newStop = new Models.GradientStops { Offset = 0.5, Color = "#808080" };
            newStop.PropertyChanged += OnGradientStopPropertyChanged;
            GradientStops.Add(newStop);
        }

        private void RemoveGradientStop(Models.GradientStops? stop)
        {
            if (stop == null || GradientStops.Count <= 1) return;
            stop.PropertyChanged -= OnGradientStopPropertyChanged;
            GradientStops.Remove(stop);
        }

        private async void OpenColorPickerAsync(Models.GradientStops? stop)
        {
            if (stop == null) return;

            Window? window = null;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                window = desktop.MainWindow;

            if (window == null && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop2)
                window = desktop2.Windows.FirstOrDefault(w => w.IsActive) as Window;

            if (window == null)
            {
                return;
            }

            try
            {
                var dialog = new Elements.Dialogs.ColorPickerDialog(stop.Color);
                var result = await dialog.ShowDialog<string>(window);
                if (!string.IsNullOrWhiteSpace(result))
                    stop.Color = result;
            }
            catch (Exception ex)
            {
                StatusText = $"Color picker error: {ex.Message}";
                App.Logger?.WriteException("OpenColorPickerAsync", ex);
            }
        }

        private async void SelectRobloxIcon()
        {
            Window? window = GetActiveWindow();

            if (window == null && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop2)
                window = desktop2.Windows.FirstOrDefault(w => w.IsActive) as Window;

            if (window == null)
            {
                return;
            }

            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Roblox Icon Image",
                FileTypeFilter = [FilePickerFileTypes.ImageAll],
                AllowMultiple = false
            });

            if (files.Count > 0)
                RobloxIconImagePath = files[0].Path.LocalPath;
        }

        private async void SelectSpinnerIcon()
        {
            Window? window = GetActiveWindow();
            if (window == null)
            {
                return;
            }

            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Custom Spinner Image",
                FileTypeFilter = [FilePickerFileTypes.ImageAll],
                AllowMultiple = false
            });

            if (files.Count > 0)
                CustomSpinnerPath = files[0].Path.LocalPath;
        }

        private static Window? GetActiveWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow ?? desktop.Windows.FirstOrDefault(w => w.IsActive);
            return null;
        }
    }
}