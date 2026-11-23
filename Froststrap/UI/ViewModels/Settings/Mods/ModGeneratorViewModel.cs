// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Color = Avalonia.Media.Color;

namespace Froststrap.UI.ViewModels.Settings.Mods
{
    internal sealed partial class ModGeneratorViewModel : NotifyPropertyChangedViewModel
    {
        private Color _solidColor = Colors.White;

        public bool IsGradientMode => GradientStops.Count >= 2;


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

        #region Commands
        public IAsyncRelayCommand GenerateModCommand { get; }
        public ICommand AddGradientStopCommand { get; }
        public ICommand RemoveGradientStopCommand { get; }
        public ICommand OpenColorPickerCommand { get; }
        public ICommand SelectRobloxIconCommand { get; }
        public ICommand ClearRobloxIconCommand { get; }
        public ICommand SelectSpinnerIconCommand { get; }
        public ICommand ClearSpinnerIconCommand { get; }
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

        private double _progress;
        public double Progress { get => _progress; set => SetProperty(ref _progress, value); }

        private bool _isProgressVisible;
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

        private bool _colorCursors;
        public bool ColorCursors { get => _colorCursors; set => SetProperty(ref _colorCursors, value); }

        private bool _colorShiftlock;
        public bool ColorShiftlock { get => _colorShiftlock; set => SetProperty(ref _colorShiftlock, value); }

        private bool _colorEmoteWheel;
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

        private bool _dontColorPlayIcon;
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
                StatusText = "";
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
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to load font files");
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
                        catch (Exception ex) { App.Logger.Error(ex, "Failed to load glyph preview"); }
                    });
                }
                GlyphItems = glyphItems;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to load glyph previews");
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
            if (!IsValidHexColor(SolidColorHex)) return;

            IsNotGeneratingMod = false;
            IsProgressVisible = true;
            Progress = 0;
            StatusText = Strings.Menu_ModGenerator_Starting;

            try
            {
                await Task.Run(async () =>
                {
                    string colorArg = $"{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}";
                    double angleArg = 0;
                    if (IsGradientMode)
                    {
                        var orderedStops = GradientStops.OrderBy(s => s.Offset);
                        colorArg = string.Join(",", orderedStops.Select(s => $"{s.Offset.ToString(CultureInfo.InvariantCulture)}:{s.Color.TrimStart('#')}"));
                        angleArg = GradientAngle;
                    }

                    string modFolderName = string.IsNullOrWhiteSpace(ModFolderName)
                        ? GetNextAvailableModFolderName()
                        : ModFolderName;

                    var options = new ModGenerator.Options
                    {
                        Color = colorArg,
                        Angle = angleArg,
                        Bands = GradientBands > 0 ? GradientBands : null,
                        RobloxIconPath = RobloxIconImagePath,
                        SpinnerPath = CustomSpinnerPath,
                        SkipPlayIcon = DontColorPlayIcon,
                        Cursors = ColorCursors,
                        Shiftlock = ColorShiftlock,
                        EmoteWheel = ColorEmoteWheel
                    };

                    await ModGenerator.GenerateAsync(options, progress =>
                    {
                        var (status, start, end) = progress.Stage switch
                        {
                            ModGenerator.Stage.Downloading => (Strings.Menu_ModGenerator_Downloading, 0, 35),
                            ModGenerator.Stage.Extracting => (Strings.Menu_ModGenerator_Extracting, 35, 50),
                            ModGenerator.Stage.Recoloring => (Strings.Menu_ModGenerator_RecoloringPNGS, 50, 88),
                            _ => (Strings.Menu_ModGenerator_CleaningUp, 88, 90)
                        };

                        string detail = progress switch
                        {
                            { Total: 0 } => "",
                            { Stage: ModGenerator.Stage.Downloading } => string.Format(CultureInfo.InvariantCulture, " ({0:0.0} / {1:0.0} MB)", progress.Done / 1048576.0, progress.Total / 1048576.0),
                            { Stage: ModGenerator.Stage.Extracting } => string.Format(CultureInfo.InvariantCulture, " ({0:0}%)", progress.Fraction * 100),
                            { Stage: ModGenerator.Stage.Recoloring } => string.Format(CultureInfo.InvariantCulture, " ({0}/{1})", progress.Done, progress.Total),
                            _ => ""
                        };

                        StatusText = status + detail;
                        Progress = start + (end - start) * progress.Fraction;
                    });

                    string outputRoot = ModGenerator.OutputDirectory;
                    string infoPath = Path.Combine(outputRoot, "info.json");

                    StatusText = Strings.Menu_ModGenerator_Packaging;
                    Progress = 90;

                    if (IncludeModifications)
                    {
                        string targetFolder = Path.Combine(Paths.Modifications, modFolderName);
                        if (!Directory.Exists(targetFolder))
                            Directory.CreateDirectory(targetFolder);

                        var itemsToCopy = new List<string> { Path.Combine(outputRoot, "ExtraContent"), Path.Combine(outputRoot, "content"), infoPath };
                        var filesToCopy = itemsToCopy
                            .SelectMany(item => File.Exists(item) ? [item] : Directory.Exists(item) ? Directory.GetFiles(item, "*", SearchOption.AllDirectories) : [])
                            .ToList();

                        int copiedFiles = 0;
                        foreach (var file in filesToCopy)
                        {
                            string target = Path.Combine(targetFolder, Path.GetRelativePath(outputRoot, file));
                            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                            File.Copy(file, target, true);
                            copiedFiles++;

                            StatusText = string.Format(CultureInfo.InvariantCulture, "{0} ({1}/{2})", Strings.Menu_ModGenerator_Packaging, copiedFiles, filesToCopy.Count);
                            Progress = 90 + 10.0 * copiedFiles / filesToCopy.Count;
                        }

                        var generatedMod = App.State.Prop.Mods.FirstOrDefault(m =>
                            string.Equals(m.FolderName, modFolderName, StringComparison.OrdinalIgnoreCase));

                        if (generatedMod is null)
                        {
                            generatedMod = new ModConfig
                            {
                                FolderName = modFolderName,
                                Target = ModTarget.Both
                            };
                            App.State.Prop.Mods.Add(generatedMod);
                            App.Logger.Info($"Added mod '{modFolderName}' to state.");
                        }

                        generatedMod.Enabled = true;

                        var otherMods = App.State.Prop.Mods.Where(m => m != generatedMod).OrderBy(m => m.Priority).ToList();
                        generatedMod.Priority = 1;
                        for (int i = 0; i < otherMods.Count; i++)
                            otherMods[i].Priority = i + 2;

                        App.State.SaveSetting("Mods");

                        Progress = 100;
                        StatusText = string.Format(CultureInfo.InvariantCulture, Strings.Menu_ModGenerator_AppliedModifications, copiedFiles);
                    }
                    else
                    {
                        StatusText = "Zipping results...";
                        var file = await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var visualRoot = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                            if (visualRoot == null) return null;
                            return await visualRoot.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                            {
                                Title = "Save Froststrap Mod",
                                SuggestedFileName = $"FroststrapMod_{SolidColorHex}.zip",
                                DefaultExtension = ".zip",
                                FileTypeChoices = [new FilePickerFileType("Zip Archive") { Patterns = ["*.zip"] }]
                            });
                        });

                        if (file != null)
                        {
                            await ModGenerator.ZipAsync(file.Path.LocalPath);
                            Progress = 100;
                            StatusText = string.Format(CultureInfo.InvariantCulture, Strings.Menu_ModGenerator_SavedTo, Path.GetFileName(file.Path.LocalPath));
                        }
                        else
                        {
                            StatusText = Strings.Menu_ModGenerator_Cancelled;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to generate mod");
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsNotGeneratingMod = true;
                IsProgressVisible = false;
                Progress = 0;
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
            if (GradientStops.Count > 0)
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
                App.Logger.Error(ex, "Failed to open colour picker");
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
