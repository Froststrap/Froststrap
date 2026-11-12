// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Froststrap.Backend;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.Bootstrapper;
using Froststrap.UI.Elements.Dialogs;
using System.IO.Compression;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    internal partial class AppearanceViewModel : NotifyPropertyChangedViewModel
    {
        private static readonly string[] _zipFilter = ["*.zip"];
        private static readonly string[] JsonPatterns = ["*.json"];
        private static readonly JsonSerializerOptions SerializationOptions = new() { WriteIndented = true };
        private CustomThemeCycleSelectionWrapper? _selectedThemeWrapper;
        private CustomDialog? _customThemePreview;
        private Action? _themeReloadHandler;

        public ICommand PreviewBootstrapperCommand => new RelayCommand(PreviewBootstrapper);
        public ICommand PreviewCustomThemeCommand => new RelayCommand(() => PreviewCustomTheme());
        public IAsyncRelayCommand BrowseCustomIconLocationCommand => new AsyncRelayCommand<Control>(async c => await BrowseCustomIconLocation(c));
        public IAsyncRelayCommand BrowseCustomRobloxIconLocationCommand => new AsyncRelayCommand<Control>(async c => await BrowseCustomRobloxIconLocation(c));

        public ICommand AddCustomThemeCommand => new RelayCommand<Control>(async c => await AddCustomTheme(c));
        public ICommand EditCustomThemeCommand => new RelayCommand<Control>(async c => await EditCustomTheme(c));
        public ICommand ExportCustomThemeCommand => new RelayCommand<Control>(async c => await ExportCustomTheme(c));
        public IAsyncRelayCommand DeleteCustomThemeCommand => new AsyncRelayCommand(() => Task.Run(DeleteCustomTheme));
        public IAsyncRelayCommand RenameCustomThemeCommand => new AsyncRelayCommand(() => Task.Run(RenameCustomTheme));

        private async void PreviewBootstrapper()
        {
            App.FrostRPC?.SetDialog("Preview Launcher");

            IBootstrapperDialog dialog = await App.Settings.Prop.BootstrapperStyle.GetNew();

            dialog.Message = App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.ByfronDialog
                ? Strings.Bootstrapper_StylePreview_ImageCancel
                : Strings.Bootstrapper_StylePreview_TextCancel;

            dialog.CancelEnabled = true;
            dialog.ShowBootstrapper();

            App.FrostRPC?.ClearDialog();
        }

        private async Task BrowseCustomIconLocation(Control? control)
        {
            if (control is null) return;

            var topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is not Window parentWindow) return;

            var storageProvider = parentWindow.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Icon File",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.ico", "*.png", "*.jpg", "*.jpeg", "*.bmp"] }]
            });

            if (files.Count > 0)
            {
                CustomIconLocation = files[0].Path.LocalPath;
                OnPropertyChanged(nameof(CustomIconLocation));
            }
        }

        private async Task BrowseCustomRobloxIconLocation(Control? control)
        {
            if (control is null) return;

            var topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is not Window parentWindow) return;

            var storageProvider = parentWindow.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Roblox Icon File",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.ico", "*.png", "*.jpg", "*.jpeg", "*.bmp"] }]
            });

            if (files.Count > 0)
            {
                CustomRobloxIconLocation = files[0].Path.LocalPath;
                OnPropertyChanged(nameof(CustomRobloxIconLocation));
            }
        }

        public static List<string> Languages => Locale.GetLanguages();

        public string SelectedLanguage
        {
            get
            {
                string identifier = App.Settings.Prop.Locale;
                if (identifier == "nil")
                    return Strings.Common_SystemDefault;

                return Locale.SupportedLocales.TryGetValue(identifier, out var value)
                    ? value
                    : Strings.Common_SystemDefault;
            }
            set
            {
                string? identifier = Locale.SupportedLocales.FirstOrDefault(x => x.Value == value).Key;

                if (identifier == null && value == Strings.Common_SystemDefault)
                    identifier = "nil";

                identifier ??= "nil";

                if (App.Settings.Prop.Locale != identifier)
                {
                    App.Settings.Prop.Locale = identifier;
                    OnPropertyChanged(nameof(SelectedLanguage));

                    Locale.Set(identifier);
                }
            }
        }

        public ObservableCollection<BootstrapperIconEntry> Icons { get; set; } = [];

        public BootstrapperIcon Icon
        {
            get => App.Settings.Prop.BootstrapperIcon;
            set
            {
                App.Settings.Prop.BootstrapperIcon = value;
                OnPropertyChanged(nameof(IsCustomIconSelected));
            }
        }

        public static bool IsCustomIconSelected => App.Settings.Prop.BootstrapperIcon == BootstrapperIcon.IconCustom;

        public ObservableCollection<RobloxIconEntry> RobloxIcons { get; set; } = [];

        public RobloxIcon RobloxIcon
        {
            get => App.Settings.Prop.RobloxIcon;
            set
            {
                App.Settings.Prop.RobloxIcon = value;
                OnPropertyChanged(nameof(IsCustomRobloxIconSelected));
            }
        }

        public static bool IsCustomRobloxIconSelected => App.Settings.Prop.RobloxIcon == RobloxIcon.IconCustom;

        public static bool DisableAnimations
        {
            get => App.Settings.Prop.DisableAnimations;
            set
            {
                App.Settings.Prop.DisableAnimations = value;
                App.Settings.Save();
                if (value)
                    App.ApplyAnimationSettings();
                else
                    App.RemoveAnimationSettings();
            }
        }

        public static IEnumerable<WindowsBackdrops> BackdropOptions => Enum.GetValues<WindowsBackdrops>().Where(IsBackdropSupported);

        public WindowsBackdrops SelectedBackdrop
        {
            get => App.Settings.Prop.SelectedBackdrop;
            set
            {
                var newValue = IsBackdropSupported(value) ? value : WindowsBackdrops.None;
                if (App.Settings.Prop.SelectedBackdrop != newValue)
                {
                    App.Settings.Prop.SelectedBackdrop = newValue;
                    OnPropertyChanged(nameof(SelectedBackdrop));
                    AvaloniaWindow.UpdateBackdropForAllWindows();
                }
            }
        }

        private static bool IsBackdropSupported(WindowsBackdrops backdrop)
        {
            if (OperatingSystem.IsMacOS())
                return backdrop is WindowsBackdrops.None or WindowsBackdrops.Blur;

            if (!OperatingSystem.IsWindows())
                return backdrop == WindowsBackdrops.None;

            if (backdrop == WindowsBackdrops.Blur)
                return false;

            return backdrop switch
            {
                WindowsBackdrops.Mica => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000),
                WindowsBackdrops.Acrylic => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763),
                _ => true
            };
        }

        public static string Title
        {
            get => App.Settings.Prop.BootstrapperTitle;
            set => App.Settings.Prop.BootstrapperTitle = value;
        }

        public static bool ShowInfo
        {
            get => App.Settings.Prop.BootstrapperShowInfo;
            set => App.Settings.Prop.BootstrapperShowInfo = value;
        }

        public string CustomIconLocation
        {
            get => App.Settings.Prop.BootstrapperIconCustomLocation;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    if (App.Settings.Prop.BootstrapperIcon == BootstrapperIcon.IconCustom)
                        App.Settings.Prop.BootstrapperIcon = BootstrapperIcon.IconFroststrap;
                }
                else
                {
                    App.Settings.Prop.BootstrapperIcon = BootstrapperIcon.IconCustom;
                }

                App.Settings.Prop.BootstrapperIconCustomLocation = value;
                BootstrapperIconEx.InvalidateCustomIcon();

                var customEntry = Icons.FirstOrDefault(e => e.IconType == BootstrapperIcon.IconCustom);
                customEntry?.RefreshImage();

                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(Icons));

                if (!App.Settings.Prop.EnableActivityTracking && App.Settings.Prop.EnableWindowManipulation)
                {
                    App.Settings.Prop.EnableWindowManipulation = true;
                    WindowManipulationEnabled = false;
                }
            }
        }

        public bool WindowManipulationEnabled
        {
            get => ActivityTrackingEnabled && App.Settings.Prop.EnableWindowManipulation;
            set
            {
                if (value && !ActivityTrackingEnabled)
                    return;

                if (App.Settings.Prop.EnableWindowManipulation == value)
                    return;

                App.Settings.Prop.EnableWindowManipulation = value;

                if (!value)
                {
                    App.Settings.Prop.RobloxTitle = "Roblox";
                    App.Settings.Prop.RobloxIcon = RobloxIcon.IconDefault;
                    App.Settings.Prop.RobloxIconCustomLocation = "";
                    App.Settings.Prop.AutoChangeTitle = false;
                    App.Settings.Prop.AutoChangeTitleWithPlayerCount = false;
                    App.Settings.Prop.AutoChangeIcon = false;

                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(RobloxIcon));
                    OnPropertyChanged(nameof(CustomRobloxIconLocation));
                    OnPropertyChanged(nameof(IsCustomRobloxIconSelected));
                    OnPropertyChanged(nameof(AutoChangeTitle));
                    OnPropertyChanged(nameof(AutoChangeTitlePlayerCounter));
                    OnPropertyChanged(nameof(AutoChangeIcon));
                }

                OnPropertyChanged(nameof(WindowManipulationEnabled));
            }
        }

        public bool ActivityTrackingEnabled
        {
            get => App.Settings.Prop.EnableActivityTracking;
            set
            {
                if (App.Settings.Prop.EnableActivityTracking == value)
                    return;

                App.Settings.Prop.EnableActivityTracking = value;
                OnPropertyChanged(nameof(ActivityTrackingEnabled));

                if (!value)
                    WindowManipulationEnabled = false;
            }
        }

        public string CustomRobloxIconLocation
        {
            get => App.Settings.Prop.RobloxIconCustomLocation;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    if (App.Settings.Prop.RobloxIcon == RobloxIcon.IconCustom)
                        App.Settings.Prop.RobloxIcon = RobloxIcon.IconDefault;
                }
                else
                {
                    App.Settings.Prop.RobloxIcon = RobloxIcon.IconCustom;
                }

                App.Settings.Prop.RobloxIconCustomLocation = value;
                RobloxIconEx.InvalidateCustomIcon();

                var customEntry = RobloxIcons.FirstOrDefault(e => e.IconType == RobloxIcon.IconCustom);
                customEntry?.RefreshImage();

                OnPropertyChanged(nameof(RobloxIcon));
                OnPropertyChanged(nameof(RobloxIcons));
            }
        }

        public string WindowTitle
        {
            get => App.Settings.Prop.RobloxTitle;
            set
            {
                App.Settings.Prop.RobloxTitle = value;
                OnPropertyChanged(nameof(WindowTitle));
            }
        }

        public AppearanceViewModel()
        {
            foreach (var entry in BootstrapperIconEx.Selections)
                Icons.Add(new() { IconType = entry });

            foreach (var entry in RobloxIconEx.Selections)
                RobloxIcons.Add(new() { IconType = entry });

            PopulateCustomThemes();
            InitializeGradientStops();

            if (BackgroundMusicEnabled)
                _ = LoadOutputDevicesAsync();
        }

        public bool AutoChangeTitle
        {
            get => App.Settings.Prop.AutoChangeTitle;
            set
            {
                App.Settings.Prop.AutoChangeTitle = value;

                if (!value)
                {
                    AutoChangeTitlePlayerCounter = false;
                    OnPropertyChanged(nameof(AutoChangeTitlePlayerCounter));
                }

                OnPropertyChanged(nameof(AutoChangeTitle));
            }
        }

        public bool AutoChangeTitlePlayerCounter
        {
            get => App.Settings.Prop.AutoChangeTitleWithPlayerCount;
            set
            {
                App.Settings.Prop.AutoChangeTitleWithPlayerCount = value;
                OnPropertyChanged(nameof(AutoChangeTitlePlayerCounter));
            }
        }

        public bool AutoChangeIcon
        {
            get => App.Settings.Prop.AutoChangeIcon;
            set
            {
                App.Settings.Prop.AutoChangeIcon = value;
                OnPropertyChanged(nameof(AutoChangeIcon));
            }
        }

        public IEnumerable<BootstrapperStyle> Dialogs { get; } = BootstrapperStyleEx.Selections;

        public BootstrapperStyle Dialog
        {
            get => App.Settings.Prop.BootstrapperStyle;
            set
            {
                App.Settings.Prop.BootstrapperStyle = value;
                OnPropertyChanged(nameof(CustomThemesExpanded));
            }
        }

        public static bool CustomThemesExpanded => App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.CustomDialog;

        public bool IsThemeCyclingEnabled
        {
            get => App.Settings.Prop.CycleEnabled;
            set
            {
                App.Settings.Prop.CycleEnabled = value;
                OnPropertyChanged(nameof(IsThemeCyclingEnabled));
            }
        }

        public static IEnumerable<CycleFrequency> CycleFrequencies => Enum.GetValues<CycleFrequency>();

        public CycleFrequency SelectedCycleFrequency
        {
            get => App.Settings.Prop.CycleFrequency;
            set
            {
                App.Settings.Prop.CycleFrequency = value;
                OnPropertyChanged(nameof(SelectedCycleFrequency));
                OnPropertyChanged(nameof(IsIntervalValueVisible));
            }
        }

        public int CycleIntervalValue
        {
            get => App.Settings.Prop.CycleIntervalValue;
            set
            {
                App.Settings.Prop.CycleIntervalValue = value;
                OnPropertyChanged(nameof(CycleIntervalValue));
            }
        }

        public bool IsIntervalValueVisible => SelectedCycleFrequency != CycleFrequency.EveryLaunch;

        public ObservableCollection<CustomThemeCycleSelectionWrapper> CustomThemeSelections { get; set; } = [];

        public void InitializeCustomThemeSelections()
        {
            foreach (var w in CustomThemeSelections)
                w.PropertyChanged -= OnCycleSelectionPropertyChanged;

            CustomThemeSelections.Clear();
            var savedList = App.Settings.Prop.CycleEnabledCustomThemes;

            foreach (var themeName in CustomThemes)
            {
                var selection = new CustomThemeCycleSelectionWrapper
                {
                    ThemeName = themeName,
                    IsSelected = savedList.Contains(themeName)
                };
                selection.PropertyChanged += OnCycleSelectionPropertyChanged;
                CustomThemeSelections.Add(selection);
            }
        }

        public CustomThemeCycleSelectionWrapper? SelectedThemeWrapper
        {
            get => _selectedThemeWrapper;
            set
            {
                if (_selectedThemeWrapper != value)
                {
                    _selectedThemeWrapper = value;
                    OnPropertyChanged();
                    SelectedCustomTheme = value?.ThemeName;
                }
            }
        }

        private void OnCycleSelectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is CustomThemeCycleSelectionWrapper wrapper && e.PropertyName == nameof(wrapper.IsSelected))
            {
                var savedList = App.Settings.Prop.CycleEnabledCustomThemes;
                if (wrapper.IsSelected)
                {
                    if (!savedList.Contains(wrapper.ThemeName))
                        savedList.Add(wrapper.ThemeName);
                }
                else
                {
                    savedList.Remove(wrapper.ThemeName);
                }
            }
        }

        private static string GetThemeDirectoryOrThrow(string name)
            => Paths.TryGetCustomThemeDirectory(name, out string? directory)
                ? directory
                : throw new InvalidOperationException($"'{name}' is not a valid custom theme name");

        private static void DeleteCustomThemeStructure(string name)
        {
            string dir = GetThemeDirectoryOrThrow(name);

            // the preview watcher holds a handle on the theme folder
            ThemeFileWatcher.Stop();

            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        private static void RenameCustomThemeStructure(string oldName, string newName)
        {
            string oldDir = GetThemeDirectoryOrThrow(oldName);
            string newDir = GetThemeDirectoryOrThrow(newName);

            ThemeFileWatcher.Stop();
            Directory.Move(oldDir, newDir);
        }

        private async Task AddCustomTheme(Control? control)
        {
            var topLevel = TopLevel.GetTopLevel(control) ??
                           (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (topLevel is not Window parentWindow)
            {
                App.Logger.Info("No parent window found.");
                return;
            }

            App.FrostRPC?.SetDialog("Add Custom Launcher");

            var dialog = new AddCustomThemeDialog();
            await dialog.ShowDialog(parentWindow);

            App.FrostRPC?.ClearDialog();

            if (dialog.Created)
            {
                CustomThemes.Add(dialog.ThemeName);
                var wrapper = new CustomThemeCycleSelectionWrapper { ThemeName = dialog.ThemeName, IsSelected = false };
                wrapper.PropertyChanged += OnCycleSelectionPropertyChanged;
                CustomThemeSelections.Add(wrapper);

                SelectedThemeWrapper = wrapper;
                SelectedCustomThemeIndex = CustomThemes.IndexOf(dialog.ThemeName);
                OnPropertyChanged(nameof(SelectedCustomThemeIndex));

                if (dialog.OpenEditor)
                    await EditCustomTheme(control);
            }
        }

        private async Task EditCustomTheme(Control? control)
        {
            if (SelectedCustomTheme is null) return;

            string themePath = Paths.TryGetCustomThemeDirectory(SelectedCustomTheme, out string? directory)
                ? Path.Combine(directory, "Theme.xml")
                : SelectedCustomTheme;

            if (directory is null || !File.Exists(themePath))
            {
                await Frontend.ShowMessageBox(
                    string.Format(CultureInfo.InvariantCulture, Strings.CustomTheme_Errors_FileNotFound, themePath),
                    MessageBoxImage.Error,
                    MessageBoxButton.OK);
                return;
            }

            if (!ExternalEditor.OpenTheme(SelectedCustomTheme))
            {
                await Frontend.ShowMessageBox(
                    string.Format(CultureInfo.InvariantCulture, Strings.CustomTheme_Editor_Errors_OpenFailed, directory),
                    MessageBoxImage.Error,
                    MessageBoxButton.OK);
            }
        }

        /// <param name="reload">
        /// Live-reload after a file change: errors are expected mid-edit so they're only logged, and the new
        /// window takes the old one's place without stealing focus from the user's editor.
        /// </param>
        private void PreviewCustomTheme(bool reload = false)
        {
            if (SelectedCustomTheme is null) return;

            try
            {
                string directory = GetThemeDirectoryOrThrow(SelectedCustomTheme);

                CustomDialog dialog = new();
                dialog.ApplyCustomTheme(SelectedCustomTheme);

                dialog.Closed += (_, _) =>
                {
                    if (!ReferenceEquals(_customThemePreview, dialog))
                        return;

                    // nothing left to live-reload, so release the folder
                    _customThemePreview = null;
                    ThemeFileWatcher.Stop();
                };

                CustomDialog? previous = _customThemePreview;

                if (reload && previous is not null)
                {
                    dialog.ShowActivated = false;
                    dialog.WindowStartupLocation = WindowStartupLocation.Manual;
                    dialog.Position = previous.Position;
                }

                // swap before closing so the old dialog's Closed handler doesn't stop the watcher
                _customThemePreview = dialog;

                dialog.Message = Strings.Bootstrapper_StylePreview_TextCancel;
                dialog.CancelEnabled = true;
                dialog.ShowBootstrapper();

                // close the old one only once the new one is up, so there's no gap/flicker
                previous?.CloseBootstrapper();

                WatchSelectedTheme(directory);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to preview custom theme");

                if (!reload)
                {
                    _ = Frontend.ShowMessageBox(
                        string.Format(CultureInfo.InvariantCulture, Strings.CustomTheme_Editor_Errors_PreviewFailed, ex.Message),
                        MessageBoxImage.Error,
                        MessageBoxButton.OK);
                }
            }
        }

        private void WatchSelectedTheme(string directory)
        {
            if (!Directory.Exists(directory))
                return;

            _themeReloadHandler ??= () => Avalonia.Threading.Dispatcher.UIThread.Post(ReloadCustomThemePreview);
            ThemeFileWatcher.Watch(directory, _themeReloadHandler);
        }

        private void ReloadCustomThemePreview()
        {
            if (_customThemePreview == null)
                return;

            PreviewCustomTheme(reload: true);
        }

        private async Task ExportCustomTheme(Control? control)
        {
            if (SelectedCustomTheme is null) return;

            var topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is not Window parentWindow) return;

            var file = await parentWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Theme",
                SuggestedFileName = $"{SelectedCustomTheme}.zip",
                FileTypeChoices = [new FilePickerFileType("Zip Archive") { Patterns = _zipFilter }]
            });

            if (file is null)
                return;

            if (!Paths.TryGetCustomThemeDirectory(SelectedCustomTheme, out string? themeDir))
                return;

            await Task.Run(async () =>
            {
                using var outputStream = await file.OpenWriteAsync();
                using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: false);

                foreach (var filePath in Directory.EnumerateFiles(themeDir, "*.*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(themeDir, filePath).Replace('\\', '/');

                    if (relativePath.StartsWith(".vscode/", StringComparison.OrdinalIgnoreCase)
                        || relativePath.StartsWith(".zed/", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                    entry.LastWriteTime = DateTimeOffset.Now;

                    await using var entryStream = await entry.OpenAsync();
                    using var fileStream = File.OpenRead(filePath);
                    await fileStream.CopyToAsync(entryStream);
                }
            });

            Froststrap.Utility.Threading.ShellExecute(file.Path.LocalPath, select: true);
        }

        private async void DeleteCustomTheme()
        {
            if (SelectedCustomTheme is null) return;

            var themeToDelete = SelectedCustomTheme;

            try
            {
                DeleteCustomThemeStructure(themeToDelete);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to delete custom theme");
                await Frontend.ShowMessageBox(string.Format(CultureInfo.InvariantCulture, Strings.Menu_Appearance_CustomThemes_DeleteFailed, themeToDelete, ex.Message), MessageBoxImage.Error);
                return;
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var wrapperToDelete = CustomThemeSelections.FirstOrDefault(w => w.ThemeName == themeToDelete);

                App.Settings.Prop.CycleEnabledCustomThemes.Remove(themeToDelete);

                if (wrapperToDelete != null)
                {
                    wrapperToDelete.PropertyChanged -= OnCycleSelectionPropertyChanged;
                    CustomThemeSelections.Remove(wrapperToDelete);
                }

                int idx = CustomThemes.IndexOf(themeToDelete);
                if (idx >= 0) CustomThemes.RemoveAt(idx);

                if (CustomThemes.Count > 0)
                {
                    var newSelectedWrapper = CustomThemeSelections.LastOrDefault();
                    SelectedThemeWrapper = newSelectedWrapper;
                    SelectedCustomThemeIndex = CustomThemes.Count - 1;
                }
                else
                {
                    SelectedThemeWrapper = null;
                    SelectedCustomTheme = null;
                    SelectedCustomThemeIndex = -1;
                }

                OnPropertyChanged(nameof(IsCustomThemeSelected));
                OnPropertyChanged(nameof(SelectedCustomThemeIndex));
            });
        }

        private async void RenameCustomTheme()
        {
            if (SelectedCustomTheme is null || SelectedCustomTheme == SelectedCustomThemeName)
                return;

            if (string.IsNullOrEmpty(SelectedCustomThemeName))
            {
                await Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_NameEmpty, MessageBoxImage.Error);
                return;
            }

            var validationResult = PathValidator.IsFileNameValid(SelectedCustomThemeName);
            if (validationResult != PathValidator.ValidationResult.Ok)
            {
                string errorString = validationResult switch
                {
                    PathValidator.ValidationResult.IllegalCharacter => Strings.CustomTheme_Add_Errors_NameIllegalCharacters,
                    PathValidator.ValidationResult.ReservedFileName => Strings.CustomTheme_Add_Errors_NameReserved,
                    _ => Strings.CustomTheme_Add_Errors_Unknown
                };
                await Frontend.ShowMessageBox(errorString, MessageBoxImage.Error);
                return;
            }

            string path = Path.Combine(Paths.CustomThemes, SelectedCustomThemeName, "Theme.xml");
            if (File.Exists(path))
            {
                await Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_NameTaken, MessageBoxImage.Error);
                return;
            }

            try
            {
                RenameCustomThemeStructure(SelectedCustomTheme, SelectedCustomThemeName);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to rename custom theme");
                await Frontend.ShowMessageBox("Rename failed", MessageBoxImage.Error);
                return;
            }

            string newName = SelectedCustomThemeName;
            string oldName = SelectedCustomTheme;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                int idx = CustomThemes.IndexOf(oldName);
                if (idx >= 0) CustomThemes[idx] = newName;

                var wrapper = CustomThemeSelections.FirstOrDefault(w => w.ThemeName == oldName);
                if (wrapper != null)
                {
                    wrapper.ThemeName = newName;
                    if (App.Settings.Prop.CycleEnabledCustomThemes.Remove(oldName))
                    {
                        App.Settings.Prop.CycleEnabledCustomThemes.Add(newName);
                    }
                }

                SelectedThemeWrapper = wrapper;
                SelectedCustomTheme = newName;
                SelectedCustomThemeIndex = idx;

                OnPropertyChanged(nameof(SelectedCustomThemeIndex));
                OnPropertyChanged(nameof(SelectedCustomTheme));
                OnPropertyChanged(nameof(SelectedCustomThemeName));
            });
        }

        private void PopulateCustomThemes()
        {
            string? selected = App.Settings.Prop.SelectedCustomTheme;
            CustomThemes.Clear();

            if (!Directory.Exists(Paths.CustomThemes))
                Directory.CreateDirectory(Paths.CustomThemes);

            foreach (string directory in Directory.GetDirectories(Paths.CustomThemes))
            {
                if (!File.Exists(Path.Combine(directory, "Theme.xml")))
                    continue;
                CustomThemes.Add(Path.GetFileName(directory));
            }

            InitializeCustomThemeSelections();

            if (!string.IsNullOrEmpty(selected))
            {
                var wrapper = CustomThemeSelections.FirstOrDefault(w => w.ThemeName == selected);
                if (wrapper != null)
                {
                    SelectedThemeWrapper = wrapper;
                    SelectedCustomThemeIndex = CustomThemes.IndexOf(selected);
                    SelectedCustomThemeName = selected;
                    OnPropertyChanged(nameof(SelectedCustomThemeIndex));
                    OnPropertyChanged(nameof(SelectedCustomThemeName));
                }
            }
        }

        public string? SelectedCustomTheme
        {
            get => App.Settings.Prop.SelectedCustomTheme;
            set
            {
                if (App.Settings.Prop.SelectedCustomTheme != value)
                {
                    App.Settings.Prop.SelectedCustomTheme = value;
                    SelectedCustomThemeName = value ?? string.Empty;

                    if (value != null)
                        SelectedThemeWrapper = CustomThemeSelections.FirstOrDefault(w => w.ThemeName == value);
                    else
                        SelectedThemeWrapper = null;

                    if (value != null && App.Settings.Prop.CycleEnabledCustomThemes.Contains(value))
                    {
                        App.Settings.Prop.CycleCurrentIndex = App.Settings.Prop.CycleEnabledCustomThemes.IndexOf(value);
                    }

                    OnPropertyChanged(nameof(SelectedCustomTheme));
                    OnPropertyChanged(nameof(SelectedCustomThemeName));
                    OnPropertyChanged(nameof(IsCustomThemeSelected));
                }
            }
        }

        public string SelectedCustomThemeName { get; set; } = "";
        public int SelectedCustomThemeIndex { get; set; }
        public ObservableCollection<string> CustomThemes { get; set; } = [];
        public bool IsCustomThemeSelected => SelectedCustomTheme is not null;

        #region Custom App Themes
        public IEnumerable<Theme> Themes { get; } = Enum.GetValues<Theme>();

        public Theme Theme
        {
            get => App.Settings.Prop.Theme;
            set
            {
                App.Settings.Prop.Theme = value;

                if (value == Theme.Custom && GradientStops.Count == 0)
                    AddDefaultGradientStops();

                OnPropertyChanged(nameof(Theme));
                OnPropertyChanged(nameof(CustomThemeExpanded));
                OnPropertyChanged(nameof(IsColorTheme));
                OnPropertyChanged(nameof(IsImageTheme));
                OnPropertyChanged(nameof(CanRemoveGradientStop));
                ApplyThemeUpdate();
            }
        }

        public static bool CustomThemeExpanded => App.Settings.Prop.Theme is Theme.Custom or Theme.Image;

        public IEnumerable<BackgroundStretch> BackgroundStretches { get; } = Enum.GetValues<BackgroundStretch>();

        public BackgroundStretch BackgroundStretch
        {
            get => App.Settings.Prop.BackgroundStretch;
            set
            {
                App.Settings.Prop.BackgroundStretch = value;
                OnPropertyChanged(nameof(BackgroundStretch));
                ApplyThemeUpdate();
            }
        }

        public double BackgroundOpacity
        {
            get => App.Settings.Prop.BackgroundOpacity;
            set
            {
                App.Settings.Prop.BackgroundOpacity = value;
                OnPropertyChanged(nameof(BackgroundOpacity));
                ApplyThemeUpdate();
            }
        }

        public string BackgroundImagePath
        {
            get => App.Settings.Prop.BackgroundImagePath ?? string.Empty;
            set
            {
                App.Settings.Prop.BackgroundImagePath = value;
                OnPropertyChanged(nameof(BackgroundImagePath));
                ApplyThemeUpdate();
            }
        }

        public bool BackgroundMusicEnabled
        {
            get => App.Settings.Prop.BackgroundMusicEnabled;
            set
            {
                App.Settings.Prop.BackgroundMusicEnabled = value;
                OnPropertyChanged(nameof(BackgroundMusicEnabled));

                if (value)
                {
                    BackgroundMusic.Retry();
                    if (OutputDevices.Count == 0)
                        _ = LoadOutputDevicesAsync();
                }
                else
                {
                    BackgroundMusic.Refresh();
                }
            }
        }

        public string BackgroundMusicFolder
        {
            get => App.Settings.Prop.BackgroundMusicFolder;
            set
            {
                App.Settings.Prop.BackgroundMusicFolder = value;
                OnPropertyChanged(nameof(BackgroundMusicFolder));

                // picking the same folder again is how you'd pick up songs added to it
                BackgroundMusic.Retry();
            }
        }

        public bool BackgroundMusicResume
        {
            get => App.Settings.Prop.BackgroundMusicResume;
            set
            {
                App.Settings.Prop.BackgroundMusicResume = value;
                OnPropertyChanged(nameof(BackgroundMusicResume));
            }
        }

        public ObservableCollection<OutputDeviceOption> OutputDevices { get; } = [];
        public ObservableCollection<string> ChannelPairs { get; } = [];

        private OutputDeviceOption? _outputDevice;
        public OutputDeviceOption? OutputDevice
        {
            get => _outputDevice;
            set
            {
                // the ComboBox clears its selection while the list is rebuilt
                if (value is null || value == _outputDevice)
                    return;

                _outputDevice = value;
                OnPropertyChanged(nameof(OutputDevice));
                UpdateChannelPairs(value.Channels);

                if (App.Settings.Prop.BackgroundMusicDevice != value.Id)
                {
                    App.Settings.Prop.BackgroundMusicDevice = value.Id;
                    App.Settings.Prop.BackgroundMusicChannelPair = 0;
                    OnPropertyChanged(nameof(BackgroundMusicChannelPair));
                    BackgroundMusic.Refresh();
                }
            }
        }

        public int BackgroundMusicChannelPair
        {
            get => App.Settings.Prop.BackgroundMusicChannelPair;
            set
            {
                if (value < 0 || value == App.Settings.Prop.BackgroundMusicChannelPair)
                    return;

                App.Settings.Prop.BackgroundMusicChannelPair = value;
                OnPropertyChanged(nameof(BackgroundMusicChannelPair));
                BackgroundMusic.Refresh();
            }
        }

        public bool HasChannelPairs => ChannelPairs.Count > 1;
        public bool HasOutputDevices => OutputDevices.Count > 1;

        private async Task LoadOutputDevicesAsync()
        {
            var devices = await Task.Run(() => AudioPlayer.ListDevices());
            string saved = App.Settings.Prop.BackgroundMusicDevice;

            int defaultChannels = devices.FirstOrDefault(d => d.IsDefault)?.Channels ?? 2;
            var options = new List<OutputDeviceOption> { new("", Strings.Menu_Appearance_BackgroundMusic_Device_Default, defaultChannels) };
            // backends without device listing only report the default, which is already the first row
            options.AddRange(devices.Where(d => d.Id.Length > 0).Select(d => new OutputDeviceOption(d.Id, d.Name, d.Channels)));

            // keep a device that's unplugged right now, so the setting isn't lost
            if (saved.Length > 0 && !options.Any(o => o.Id == saved))
                options.Add(new(saved, Strings.Menu_Appearance_BackgroundMusic_Device_Missing, 2));

            OutputDevices.Clear();
            foreach (var option in options)
                OutputDevices.Add(option);

            _outputDevice = null;
            OutputDevice = options.First(o => o.Id == saved);
            OnPropertyChanged(nameof(HasOutputDevices));
        }

        private void UpdateChannelPairs(int channels)
        {
            int pairs = Math.Max(1, channels / 2);
            ChannelPairs.Clear();
            for (int i = 0; i < pairs; i++)
                ChannelPairs.Add(string.Create(CultureInfo.InvariantCulture, $"{i * 2 + 1}/{i * 2 + 2}"));

            OnPropertyChanged(nameof(HasChannelPairs));
            OnPropertyChanged(nameof(BackgroundMusicChannelPair));
        }

        private ICommand? _selectMusicCommand;
        public ICommand SelectMusicCommand => _selectMusicCommand ??= new RelayCommand<TopLevel>(async tl =>
        {
            if (tl == null) return;

            var folders = await tl.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Strings.Menu_Appearance_BackgroundMusic_Title,
                AllowMultiple = false
            });

            if (folders.Count > 0)
                BackgroundMusicFolder = folders[0].Path.LocalPath;
        });

        private ICommand? _clearMusicCommand;
        public ICommand ClearMusicCommand => _clearMusicCommand ??= new RelayCommand(() =>
        {
            BackgroundMusicFolder = string.Empty;
        });

        public bool IsColorTheme => this.Theme == Enums.Theme.Custom;
        public bool IsImageTheme => this.Theme == Enums.Theme.Image;
        public double? GradientAngle
        {
            get => App.Settings.Prop.GradientAngle;
            set
            {
                if (!value.HasValue || value.Value < 0 || value.Value > 360)
                    return;

                if (App.Settings.Prop.GradientAngle == value)
                    return;

                App.Settings.Prop.GradientAngle = value.Value;
                OnPropertyChanged(nameof(GradientAngle));
                ApplyThemeUpdate();
            }
        }

        public ObservableCollection<GradientStops> GradientStops { get; } = [];
        public bool CanRemoveGradientStop => GradientStops.Count > 1;

        private ICommand? _addGradientStopCommand;
        public ICommand AddGradientStopCommand => _addGradientStopCommand ??= new RelayCommand(async () => await AddGradientStop());

        private ICommand? _resetGradientCommand;
        public ICommand ResetGradientCommand => _resetGradientCommand ??= new RelayCommand(ResetGradient);

        private ICommand? _removeGradientStopCommand;
        public ICommand RemoveGradientStopCommand => _removeGradientStopCommand ??= new RelayCommand<GradientStops>(stop =>
        {
            if (stop != null)
                RemoveGradientStop(stop);
        });

        private ICommand? _exportGradientCommand;
        public ICommand ExportGradientCommand => _exportGradientCommand ??= new RelayCommand<TopLevel>(async topLevel =>
        {
            if (topLevel != null)
                await ExportGradient(topLevel);
        });

        private ICommand? _importGradientCommand;
        public ICommand ImportGradientCommand => _importGradientCommand ??= new RelayCommand<TopLevel>(async topLevel =>
        {
            if (topLevel != null)
                await ImportGradient(topLevel);
        });

        private ICommand? _selectImageCommand;
        public ICommand SelectImageCommand => _selectImageCommand ??= new RelayCommand<TopLevel>(async tl =>
        {
            if (tl == null) return;

            var files = await tl.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Background Image",
                FileTypeFilter = [FilePickerFileTypes.ImageAll],
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                BackgroundImagePath = files[0].Path.LocalPath;
            }
        });

        private ICommand? _clearImageCommand;
        public ICommand ClearImageCommand => _clearImageCommand ??= new RelayCommand(() =>
        {
            BackgroundImagePath = string.Empty;
        });

        private ICommand? _openColorPickerCommand;
        public ICommand OpenColorPickerCommand => _openColorPickerCommand ??= new RelayCommand<Control>(async control =>
        {
            if (control?.DataContext is not GradientStops stop) return;

            var topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is not Window parentWindow) return;

            var dialog = new ColorPickerDialog(stop.Color);
            var result = await dialog.ShowDialog<string>(parentWindow);

            if (!string.IsNullOrWhiteSpace(result))
            {
                stop.Color = result;
            }
        });

        private void OnGradientStopPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ApplyThemeUpdate();
        }

        private async Task AddGradientStop()
        {
            GradientStops newStop = new() { Offset = 0.5, Color = "#FFFFFF" };
            newStop.PropertyChanged += OnGradientStopPropertyChanged;
            GradientStops.Add(newStop);
            OnPropertyChanged(nameof(CanRemoveGradientStop));
            ApplyThemeUpdate();
        }

        private void RemoveGradientStop(GradientStops stop)
        {
            if (stop == null || GradientStops.Count <= 1) return;
            stop.PropertyChanged -= OnGradientStopPropertyChanged;
            GradientStops.Remove(stop);
            OnPropertyChanged(nameof(CanRemoveGradientStop));
            ApplyThemeUpdate();
        }

        private void AddDefaultGradientStops()
        {
            List<GradientStops> defaultStops =
            [
                new() { Offset = 0.0, Color = "#8025304A" },
                new() { Offset = 0.5, Color = "#80272F48" },
                new() { Offset = 1.0, Color = "#80202C36" }
            ];

            foreach (var stop in defaultStops)
            {
                stop.PropertyChanged += OnGradientStopPropertyChanged;
                GradientStops.Add(stop);
            }

            OnPropertyChanged(nameof(CanRemoveGradientStop));
        }

        private void ResetGradient()
        {
            foreach (var stop in GradientStops) stop.PropertyChanged -= OnGradientStopPropertyChanged;
            GradientStops.Clear();

            AddDefaultGradientStops();

            GradientAngle = 0;
            OnPropertyChanged(nameof(GradientAngle));

            ApplyThemeUpdate();
        }

        private async Task ExportGradient(TopLevel topLevel)
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Gradient",
                FileTypeChoices = [new FilePickerFileType("JSON Files") { Patterns = JsonPatterns }],
                SuggestedFileName = "Froststrap Gradient Background.json"
            });

            if (file == null) return;

            var data = new GradientExport
            {
                GradientStops = [.. GradientStops.Select(s => new Models.GradientStops { Offset = s.Offset, Color = s.Color })],
                GradientAngle = GradientAngle
            };

            using var stream = await file.OpenWriteAsync();
            await JsonHelper.SerializeAsync(stream, data, SerializationOptions);
        }

        private async Task ImportGradient(TopLevel topLevel)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Gradient",
                FileTypeFilter = [new FilePickerFileType("JSON Files") { Patterns = JsonPatterns }],
                AllowMultiple = false
            });

            if (files.Count == 0) return;
            var file = files[0];

            try
            {
                using var stream = await file.OpenReadAsync();
                using var document = await JsonDocument.ParseAsync(stream);
                var root = document.RootElement;

                foreach (var s in GradientStops) s.PropertyChanged -= OnGradientStopPropertyChanged;
                GradientStops.Clear();

                if (root.TryGetProperty(nameof(GradientStops), out var stopsElement))
                {
                    foreach (var stop in stopsElement.EnumerateArray())
                    {
                        GradientStops newStop = new()
                        {
                            Offset = stop.GetProperty("Offset").GetDouble(),
                            Color = stop.GetProperty("Color").GetString() ?? "#FFFFFF"
                        };
                        newStop.PropertyChanged += OnGradientStopPropertyChanged;
                        GradientStops.Add(newStop);
                    }
                }

                if (root.TryGetProperty(nameof(GradientAngle), out var angleElement))
                {
                    GradientAngle = angleElement.GetDouble();
                }

                if (GradientStops.Count == 0)
                    AddDefaultGradientStops();

                OnPropertyChanged(nameof(CanRemoveGradientStop));
                ApplyThemeUpdate();
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to import gradient");
            }
        }

        private void ApplyThemeUpdate()
        {
            App.Settings.Prop.CustomGradientStops = [.. GradientStops.Select(x => new GradientStops
            {
                Offset = x.Offset,
                Color = x.Color
            })];

            App.Settings.Prop.GradientAngle = GradientAngle;

            AvaloniaWindow.ApplyTheme();
        }

        private void InitializeGradientStops()
        {
            foreach (var s in GradientStops) s.PropertyChanged -= OnGradientStopPropertyChanged;
            GradientStops.Clear();

            var savedStops = App.Settings.Prop.CustomGradientStops;
            if (savedStops != null && savedStops.Count > 0)
            {
                foreach (var stop in savedStops)
                {
                    GradientStops newStop = new()
                    {
                        Offset = stop.Offset,
                        Color = stop.Color
                    };

                    newStop.PropertyChanged += OnGradientStopPropertyChanged;
                    GradientStops.Add(newStop);
                }
            }
            else if (App.Settings.Prop.Theme == Theme.Custom)
            {
                ResetGradient();
            }

            OnPropertyChanged(nameof(CanRemoveGradientStop));
        }
        #endregion
    }

    internal class CustomThemeCycleSelectionWrapper : NotifyPropertyChangedViewModel
    {
        private bool _isSelected;
        private string _themeName = string.Empty;

        public string ThemeName
        {
            get => _themeName;
            set
            {
                if (_themeName != value)
                {
                    _themeName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    internal sealed record OutputDeviceOption(string Id, string Name, int Channels)
    {
        public override string ToString() => Name;
    }
}
