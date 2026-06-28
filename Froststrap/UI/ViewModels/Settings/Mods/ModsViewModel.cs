using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections.ObjectModel;
using System.Windows.Input;
using FontFamily = Avalonia.Media.FontFamily;

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

                    CheckFontPreviewAvailability(value);

                    if (IsPreviewOpen)
                    {
                        IsPreviewOpen = false;
                    }
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

        private ObservableCollection<GlyphItem> _previewGlyphItems = [];
        public ObservableCollection<GlyphItem> PreviewGlyphItems
        {
            get => _previewGlyphItems;
            set => SetProperty(ref _previewGlyphItems, value);
        }

        private ObservableCollection<string> _fontVariants = [];
        public ObservableCollection<string> FontVariants
        {
            get => _fontVariants;
            set => SetProperty(ref _fontVariants, value);
        }

        private bool _isLoadingPreview = false;
        public bool IsLoadingPreview
        {
            get => _isLoadingPreview;
            set => SetProperty(ref _isLoadingPreview, value);
        }

        private string _previewStatus = string.Empty;
        public string PreviewStatus
        {
            get => _previewStatus;
            set => SetProperty(ref _previewStatus, value);
        }

        private bool _isPreviewOpen = false;
        public bool IsPreviewOpen
        {
            get => _isPreviewOpen;
            set => SetProperty(ref _isPreviewOpen, value);
        }

        private bool _hasFontPreview = false;
        public bool HasFontPreview
        {
            get => _hasFontPreview;
            set => SetProperty(ref _hasFontPreview, value);
        }

        private bool _isFilledFont = false;
        public bool IsFilledFont
        {
            get => _isFilledFont;
            set => SetProperty(ref _isFilledFont, value);
        }

        private string _selectedFontVariant = "Regular";
        public string SelectedFontVariant
        {
            get => _selectedFontVariant;
            set
            {
                if (SetProperty(ref _selectedFontVariant, value))
                {
                    _currentFontVariant = value;
                    IsFilledFont = value.Equals("Filled", StringComparison.OrdinalIgnoreCase);

                    if (IsPreviewOpen && SelectedMod != null && _currentBrush != null)
                    {
                        _ = LoadGlyphsWithColorAsync(_currentBrush, _currentFontVariant);
                    }
                }
            }
        }

        private string _currentFontVariant = "Regular";
        private IBrush? _currentBrush;

        private Geometry? _regularPreviewData;
        public Geometry? RegularPreviewData
        {
            get => _regularPreviewData;
            set => SetProperty(ref _regularPreviewData, value);
        }

        private Geometry? _filledPreviewData;
        public Geometry? FilledPreviewData
        {
            get => _filledPreviewData;
            set => SetProperty(ref _filledPreviewData, value);
        }

        public ICommand TogglePreviewCommand => new RelayCommand(TogglePreview);

        private void TogglePreview()
        {
            IsPreviewOpen = !IsPreviewOpen;
            if (IsPreviewOpen && SelectedMod != null)
            {
                _ = LoadModFontPreviewAsync(SelectedMod);
            }
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

            FontVariants = ["Regular", "Filled"];
            SelectedFontVariant = "Regular";

            LoadModifications();
            _ = LoadTogglePreviewGlyphsAsync();
        }

        public ICommand OpenModGeneratorCommand => new AsyncRelayCommand(async () => await _dialogService.OpenModGeneratorAsync());
        public ICommand OpenCommunityModsCommand => new AsyncRelayCommand(async () => await _dialogService.OpenCommunityModsAsync());
        public ICommand OpenPresetModsCommand => new AsyncRelayCommand(async () => await _dialogService.OpenPresetModsAsync());

        private void OpenFolder(ModConfig? mod)
        {
            if (mod == null) return;
            string folderPath = Path.Combine(Paths.Modifications, mod.FolderName);
            if (Directory.Exists(folderPath))
                Utilities.ShellExecute(folderPath);
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
            CheckFontPreviewAvailability(SelectedMod);
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

        private static bool IsPathInside(string parentPath, string childPath)
        {
            var parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var child = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private async Task ImportModFromSource(string sourcePath, bool isZip, string? baseNameOverride = null)
        {
            string? modRoot = ValidateModStructure(sourcePath);
            if (modRoot == null)
            {
                await Frontend.ShowMessageBox(Strings.Menu_Mods_InvalidModFolders, MessageBoxImage.Error, MessageBoxButton.OK);
                return;
            }

            string modsFolder = Paths.Modifications;

            if (IsPathInside(modsFolder, modRoot))
            {
                string relative = Path.GetRelativePath(modsFolder, modRoot);
                string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string folderName = segments[0];

                if (segments.Length != 1)
                {
                    await Frontend.ShowMessageBox("Cannot import a subfolder as a mod. Please drag the mod folder directly.", MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(folderName))
                {
                    await Frontend.ShowMessageBox("Invalid mod folder name.", MessageBoxImage.Error);
                    return;
                }

                if (Modifications.Any(m => m.FolderName.Equals(folderName, StringComparison.OrdinalIgnoreCase)))
                {
                    await Frontend.ShowMessageBox($"Mod '{folderName}' is already imported.", MessageBoxImage.Information);
                    return;
                }

                var newFolderMod = new ModConfig
                {
                    FolderName = folderName,
                    Target = ModTarget.Both,
                    Enabled = true,
                    Priority = Modifications.Count > 0 ? Modifications.Max(x => x.Priority) + 1 : 0
                };

                Modifications.Add(newFolderMod);
                UpdatePriorities();
                OnPropertyChanged(nameof(HasMods));
                CheckFontPreviewAvailability(newFolderMod);
                await Frontend.ShowMessageBox($"Mod '{folderName}' imported successfully.", MessageBoxImage.Information);
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

            CheckFontPreviewAvailability(newMod);

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

        private async Task LoadTogglePreviewGlyphsAsync()
        {
            try
            {
                var regularFont = new FontFamily("avares://Froststrap/Resources/Fonts#BuilderIcons-Regular");
                var regularTypeface = new Typeface(regularFont);
                var regularText = char.ConvertFromUtf32(0xF101);
                var regularFt = new FormattedText(regularText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, regularTypeface, 20, Brushes.White);
                RegularPreviewData = regularFt.BuildGeometry(new Point(0, 0));

                var filledFont = new FontFamily("avares://Froststrap/Resources/Fonts#BuilderIcons-Filled");
                var filledTypeface = new Typeface(filledFont);
                var filledText = char.ConvertFromUtf32(0xF101);
                var filledFt = new FormattedText(filledText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, filledTypeface, 20, Brushes.White);
                FilledPreviewData = filledFt.BuildGeometry(new Point(0, 0));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ModsViewModel::LoadTogglePreviewGlyphs", ex.Message);
            }
        }

        private async Task LoadModFontPreviewAsync(ModConfig mod)
        {
            if (mod == null || string.IsNullOrWhiteSpace(mod.FolderName))
            {
                PreviewGlyphItems = [];
                return;
            }

            IsLoadingPreview = true;
            PreviewStatus = Strings.Menu_Mods_Preview_Loading;
            PreviewGlyphItems = [];

            try
            {
                IBrush? brush = await GetColorFromInfoJsonAsync(mod);

                if (brush == null)
                {
                    PreviewStatus = Strings.Menu_Mods_Preview_NoColor;
                    IsLoadingPreview = false;
                    HasFontPreview = false;
                    return;
                }

                _currentBrush = brush;
                _currentFontVariant = IsFilledFont ? "Filled" : "Regular";

                string modPath = Path.Combine(Paths.Modifications, mod.FolderName);
                string builderIconsPath = Path.Combine(modPath, "ExtraContent", "LuaPackages", "Packages", "_Index", "BuilderIcons", "BuilderIcons");

                if (Directory.Exists(builderIconsPath))
                {
                    string jsonPath = Path.Combine(builderIconsPath, "BuilderIcons.json");
                    if (File.Exists(jsonPath))
                    {
                        try
                        {
                            var json = File.ReadAllText(jsonPath);
                            var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("faces", out var facesElement))
                            {
                                foreach (var face in facesElement.EnumerateArray())
                                {
                                    if (face.TryGetProperty("name", out var nameElement))
                                    {
                                        string name = nameElement.GetString() ?? "";
                                        if (name.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
                                            name.Contains("Filled", StringComparison.OrdinalIgnoreCase))
                                        {
                                            IsFilledFont = true;
                                            _currentFontVariant = "Filled";
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* Use default Regular */ }
                    }
                }

                PreviewStatus = string.Format(Strings.Menu_Mods_Preview_LoadingFont, _currentFontVariant);
                await LoadGlyphsWithColorAsync(brush, _currentFontVariant);
                PreviewStatus = Strings.Menu_Mods_Preview_Loaded;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ModsViewModel::LoadModFontPreview", ex.Message);
                PreviewStatus = string.Format(Strings.Menu_Mods_Preview_Failed, ex.Message);
                PreviewGlyphItems = [];
            }
            finally
            {
                IsLoadingPreview = false;
            }
        }

        private static async Task<IBrush?> GetColorFromInfoJsonAsync(ModConfig mod)
        {
            string infoPath = Path.Combine(Paths.Modifications, mod.FolderName, "info.json");
            if (!File.Exists(infoPath))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(infoPath);
                var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("ColorsUsed", out var colorsElement))
                    return null;

                if (colorsElement.TryGetProperty("SolidColor", out var solidElement))
                {
                    string hex = solidElement.GetString() ?? "";
                    if (!string.IsNullOrEmpty(hex) && Color.TryParse(hex, out var color))
                    {
                        return new SolidColorBrush(color);
                    }
                }

                if (colorsElement.TryGetProperty("GradientStops", out var stopsElement))
                {
                    var stops = new Avalonia.Media.GradientStops();
                    foreach (var stop in stopsElement.EnumerateArray())
                    {
                        double offset = stop.GetProperty("Offset").GetDouble();
                        string colorHex = stop.GetProperty("Color").GetString() ?? "#FFFFFF";
                        if (Color.TryParse(colorHex, out var color))
                        {
                            stops.Add(new GradientStop(color, offset));
                        }
                    }

                    if (stops.Count >= 2)
                    {
                        double angle = 90;
                        if (colorsElement.TryGetProperty("Angle", out var angleElement))
                        {
                            angle = angleElement.GetDouble();
                        }

                        var gradientBrush = new LinearGradientBrush { GradientStops = stops };

                        double angleRad = angle * Math.PI / 180.0;
                        double dx = Math.Sin(angleRad);
                        double dy = -Math.Cos(angleRad);
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len > 0)
                        {
                            dx /= len;
                            dy /= len;
                        }
                        gradientBrush.StartPoint = new RelativePoint(0.5 - dx / 2, 0.5 - dy / 2, RelativeUnit.Relative);
                        gradientBrush.EndPoint = new RelativePoint(0.5 + dx / 2, 0.5 + dy / 2, RelativeUnit.Relative);

                        return gradientBrush;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ModsViewModel::GetColorFromInfoJson", ex.Message);
            }

            return null;
        }

        private async Task LoadGlyphsWithColorAsync(IBrush brush, string fontVariant)
        {
            var glyphItems = new ObservableCollection<GlyphItem>();

            try
            {
                string resourceKey = fontVariant.Equals("Filled", StringComparison.OrdinalIgnoreCase)
                    ? "BuilderIconsFilled"
                    : "BuilderIconsRegular";

                FontFamily? fontFamily = null;
                if (Application.Current?.Resources.TryGetResource(resourceKey, null, out object? resource) == true)
                    fontFamily = resource as FontFamily;

                if (fontFamily == null)
                {
                    fontFamily = fontVariant.Equals("Filled", StringComparison.OrdinalIgnoreCase)
                        ? new FontFamily("avares://Froststrap/Resources/Fonts#BuilderIcons-Filled")
                        : new FontFamily("avares://Froststrap/Resources/Fonts#BuilderIcons-Regular");
                }

                var typeface = new Typeface(fontFamily);

                var characterCodes = Enumerable.Range(0xF101, 100).ToList();

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
                                brush);

                            var geometry = ft.BuildGeometry(new Point(0, 0));
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
                                Brush = brush
                            });
                        }
                        catch (Exception ex)
                        {
                            App.Logger?.WriteLine("ModsViewModel::LoadGlyphsWithColor", $"Glyph Error: {ex.Message}");
                        }
                    });
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PreviewGlyphItems = glyphItems;
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ModsViewModel::LoadGlyphsWithColor", ex.Message);
                throw;
            }
        }

        private void CheckFontPreviewAvailability(ModConfig? mod)
        {
            HasFontPreview = false;

            if (mod == null || string.IsNullOrWhiteSpace(mod.FolderName))
                return;

            try
            {
                string modPath = Path.Combine(Paths.Modifications, mod.FolderName);

                string infoPath = Path.Combine(modPath, "info.json");
                if (!File.Exists(infoPath))
                    return;

                var json = File.ReadAllText(infoPath);
                var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("ColorsUsed", out var colorsElement))
                    return;

                bool hasColor = colorsElement.TryGetProperty("SolidColor", out _) ||
                               colorsElement.TryGetProperty("GradientStops", out _);

                HasFontPreview = hasColor;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ModsViewModel::CheckFontPreviewAvailability", ex.Message);
                HasFontPreview = false;
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