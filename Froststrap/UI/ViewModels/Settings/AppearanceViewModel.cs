using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.Dialogs;
using Froststrap.UI.Elements.Editor;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public class AppearanceViewModel : NotifyPropertyChangedViewModel
    {
        private readonly UserControl _page;

        public ICommand PreviewBootstrapperCommand => new RelayCommand(PreviewBootstrapper);
        public ICommand BrowseCustomIconLocationCommand => new RelayCommand(BrowseCustomIconLocation);

        public ICommand AddCustomThemeCommand => new RelayCommand(AddCustomTheme);
        public ICommand DeleteCustomThemeCommand => new RelayCommand(DeleteCustomTheme);
        public ICommand RenameCustomThemeCommand => new RelayCommand(RenameCustomTheme);
        public ICommand EditCustomThemeCommand => new RelayCommand(EditCustomTheme);
        public ICommand ExportCustomThemeCommand => new RelayCommand(ExportCustomTheme);

        private void PreviewBootstrapper()
        {
            App.FrostRPC?.SetDialog("Preview Launcher");

            IBootstrapperDialog dialog = App.Settings.Prop.BootstrapperStyle.GetNew();

            if (App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.ByfronDialog)
                dialog.Message = Strings.Bootstrapper_StylePreview_ImageCancel;
            else
                dialog.Message = Strings.Bootstrapper_StylePreview_TextCancel;

            dialog.CancelEnabled = true;
            dialog.ShowBootstrapper();

            App.FrostRPC?.ClearDialog();
        }

        private async void BrowseCustomIconLocation()
        {
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null)
                return;

            var storageProvider = mainWindow.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Icon File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Icon Files")
                    {
                        Patterns = new[] { "*.ico" }
                    }
                }
            });

            if (files != null && files.Count > 0)
            {
                var file = files[0];
                CustomIconLocation = file.Path.LocalPath;
                OnPropertyChanged(nameof(CustomIconLocation));
            }
        }

        public AppearanceViewModel(UserControl page)
        {
            _page = page;

            foreach (var entry in BootstrapperIconEx.Selections)
                Icons.Add(new BootstrapperIconEntry { IconType = entry });

            PopulateCustomThemes();

            InitializeGradientStops();
        }

        public ObservableCollection<GradientStops> GradientStops { get; } = new ObservableCollection<GradientStops>();

        public IEnumerable<Theme> Themes { get; } = Enum.GetValues(typeof(Theme)).Cast<Theme>();

        public Theme Theme
        {
            get => App.Settings.Prop.Theme;
            set
            {
                App.Settings.Prop.Theme = value;

                if (value == Theme.Custom && GradientStops.Count == 0)
                {
                    ApplyDefaultGradientStops();
                }

                AvaloniaWindow.ApplyGlobalTheme();
                OnPropertyChanged(nameof(CustomGlobalThemeExpanded));
            }
        }

        public bool CustomGlobalThemeExpanded => App.Settings.Prop.Theme == Theme.Custom;

        public double GradientAngle
        {
            get => App.Settings.Prop.GradientAngle;
            set
            {
                App.Settings.Prop.GradientAngle = value;

                AvaloniaWindow.ApplyGlobalTheme();

                OnPropertyChanged(nameof(GradientAngle));
            }
        }

        public IEnumerable<BackgroundMode> BackgroundTypes { get; } = Enum.GetValues(typeof(BackgroundMode)).Cast<BackgroundMode>();

        public BackgroundMode BackgroundType
        {
            get => App.Settings.Prop.BackgroundType;
            set
            {
                App.Settings.Prop.BackgroundType = value;

                if (value == BackgroundMode.Gradient && GradientStops.Count == 0)
                {
                    ApplyDefaultGradientStops();
                }

                if (value == BackgroundMode.Image)
                {
                    if (!string.IsNullOrEmpty(App.Settings.Prop.BackgroundImagePath) &&  !File.Exists(App.Settings.Prop.BackgroundImagePath))
                    {
                        App.Settings.Prop.BackgroundImagePath = null;
                    }
                }

                AvaloniaWindow.ApplyGlobalTheme();
                OnPropertyChanged(nameof(IsGradientMode));
                OnPropertyChanged(nameof(IsImageMode));
            }
        }

        public bool IsGradientMode => BackgroundType == BackgroundMode.Gradient;
        public bool IsImageMode => BackgroundType == BackgroundMode.Image;

        public IEnumerable<BackgroundStretch> BackgroundStretches { get; } = Enum.GetValues(typeof(BackgroundStretch)).Cast<BackgroundStretch>();

        public BackgroundStretch BackgroundStretch
        {
            get => App.Settings.Prop.BackgroundStretch;
            set
            {
                App.Settings.Prop.BackgroundStretch = value;
                AvaloniaWindow.ApplyGlobalTheme();
            }
        }

        public double BackgroundOpacity
        {
            get => App.Settings.Prop.BackgroundOpacity * 100;
            set
            {
                double newOpacity = value / 100.0;
                App.Settings.Prop.BackgroundOpacity = newOpacity;
            }
        }

        public async void SelectBackgroundImage()
        {
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null)
                return;

            var storageProvider = mainWindow.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Background Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif" }
                    }
                }
            });

            if (files != null && files.Count > 0)
            {
                var file = files[0];
                SetBackgroundImage(file.Path.LocalPath);
            }
        }

        private void SetBackgroundImage(string sourcePath)
        {
            try
            {
                string destinationPath = Path.Combine(Paths.Base, "CustomAppThemeBackground" + Path.GetExtension(sourcePath));

                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                File.Copy(sourcePath, destinationPath);

                App.Settings.Prop.BackgroundImagePath = destinationPath;

                AvaloniaWindow.ApplyGlobalTheme();
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    $"Failed to set background image: {ex.Message}",
                    MessageBoxImage.Error,
                    MessageBoxButton.OK
                );
            }
        }

        public void ClearBackgroundImage()
        {
            try
            {
                if (!string.IsNullOrEmpty(App.Settings.Prop.BackgroundImagePath) && File.Exists(App.Settings.Prop.BackgroundImagePath))
                {
                    File.Delete(App.Settings.Prop.BackgroundImagePath);
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    $"Failed to clear background image: {ex.Message}",
                    MessageBoxImage.Error,
                    MessageBoxButton.OK
                );
            }
            finally
            {
                App.Settings.Prop.BackgroundImagePath = null;
                AvaloniaWindow.ApplyGlobalTheme();
            }
        }

        public void ResetGradientStops()
        {
            GradientStops.Clear();
            ApplyDefaultGradientStops();
            App.Settings.Prop.CustomGradientStops = GradientStops.ToList();

            AvaloniaWindow.ApplyGlobalTheme();
        }

        private void InitializeGradientStops()
        {
            if (App.Settings.Prop.CustomGradientStops != null && App.Settings.Prop.CustomGradientStops.Any())
            {
                foreach (var stop in App.Settings.Prop.CustomGradientStops)
                    GradientStops.Add(stop);
            }
            else if (App.Settings.Prop.Theme == Theme.Custom)
            {
                ApplyDefaultGradientStops();
            }
        }

        private void ApplyDefaultGradientStops()
        {
            var defaultStops = new List<GradientStops>
            {
                new GradientStops { Offset = 0.0, Color = "#4D5560" },
                new GradientStops { Offset = 0.5, Color = "#383F47" },
                new GradientStops { Offset = 1.0, Color = "#252A30" }
            };

            GradientStops.Clear();
            foreach (var stop in defaultStops)
                GradientStops.Add(stop);

            App.Settings.Prop.CustomGradientStops = defaultStops;
        }

        public static List<string> Languages => Locale.GetLanguages();

        public string SelectedLanguage
        {
            get => Locale.SupportedLocales[App.Settings.Prop.Locale];
            set => App.Settings.Prop.Locale = Locale.GetIdentifierFromName(value);
        }

        public string DownloadingStatus
        {
            get => App.Settings.Prop.DownloadingStringFormat;
            set => App.Settings.Prop.DownloadingStringFormat = value;
        }

        public IEnumerable<BootstrapperStyle> Dialogs { get; } = BootstrapperStyleEx.Selections;

        public BootstrapperStyle Dialog
        {
            get => App.Settings.Prop.BootstrapperStyle;
            set
            {
                App.Settings.Prop.BootstrapperStyle = value;
                OnPropertyChanged(nameof(CustomThemesExpanded)); // TODO: only fire when needed
            }
        }

        public bool CustomThemesExpanded => App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.CustomDialog;

        public ObservableCollection<BootstrapperIconEntry> Icons { get; set; } = new();

        public BootstrapperIcon Icon
        {
            get => App.Settings.Prop.BootstrapperIcon;
            set => App.Settings.Prop.BootstrapperIcon = value;
        }

        public IEnumerable<WindowsBackdrops> BackdropOptions => Enum.GetValues(typeof(WindowsBackdrops)).Cast<WindowsBackdrops>();

        public WindowsBackdrops SelectedBackdrop
        {
            get => App.Settings.Prop.SelectedBackdrop;
            set => App.Settings.Prop.SelectedBackdrop = value;
        }

        public string Title
        {
            get => App.Settings.Prop.BootstrapperTitle;
            set => App.Settings.Prop.BootstrapperTitle = value;
        }

        public string CustomIconLocation
        {
            get => App.Settings.Prop.BootstrapperIconCustomLocation;
            set
            {
                if (String.IsNullOrEmpty(value))
                {
                    if (App.Settings.Prop.BootstrapperIcon == BootstrapperIcon.IconCustom)
                        App.Settings.Prop.BootstrapperIcon = BootstrapperIcon.IconFroststrap;
                }
                else
                {
                    App.Settings.Prop.BootstrapperIcon = BootstrapperIcon.IconCustom;
                }

                App.Settings.Prop.BootstrapperIconCustomLocation = value;

                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(Icons));
            }
        }

        private void DeleteCustomThemeStructure(string name)
        {
            string dir = Path.Combine(Paths.CustomThemes, name);
            Directory.Delete(dir, true);
        }

        private void RenameCustomThemeStructure(string oldName, string newName)
        {
            string oldDir = Path.Combine(Paths.CustomThemes, oldName);
            string newDir = Path.Combine(Paths.CustomThemes, newName);
            Directory.Move(oldDir, newDir);
        }

        private async void AddCustomTheme()
        {
            App.FrostRPC?.SetDialog("Add Custom Launcher");

			var parentWindow = (Window)TopLevel.GetTopLevel(_page)!;

			var dialog = new AddCustomThemeDialog();
            await dialog.ShowDialog(parentWindow);

            App.FrostRPC?.ClearDialog();

            if (dialog.Created)
            {
                CustomThemes.Add(dialog.ThemeName);
                SelectedCustomThemeIndex = CustomThemes.Count - 1;

                OnPropertyChanged(nameof(SelectedCustomThemeIndex));
                OnPropertyChanged(nameof(IsCustomThemeSelected));

                if (dialog.OpenEditor)
                    EditCustomTheme();
            }
        }

        private void DeleteCustomTheme()
        {
            if (SelectedCustomTheme is null)
                return;

            try
            {
                DeleteCustomThemeStructure(SelectedCustomTheme);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AppearanceViewModel::DeleteCustomTheme", ex);
                Frontend.ShowMessageBox(string.Format(Strings.Menu_Appearance_CustomThemes_DeleteFailed, SelectedCustomTheme, ex.Message), MessageBoxImage.Error);
                return;
            }

            CustomThemes.Remove(SelectedCustomTheme);

            if (CustomThemes.Any())
            {
                SelectedCustomThemeIndex = CustomThemes.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomThemeIndex));
            }

            OnPropertyChanged(nameof(IsCustomThemeSelected));
        }

        private void RenameCustomTheme()
        {
            const string LOG_IDENT = "AppearanceViewModel::RenameCustomTheme";

            if (SelectedCustomTheme is null || SelectedCustomTheme == SelectedCustomThemeName)
                return;

            if (string.IsNullOrEmpty(SelectedCustomThemeName))
            {
                Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_NameEmpty, MessageBoxImage.Error);
                return;
            }

            var validationResult = PathValidator.IsFileNameValid(SelectedCustomThemeName);

            if (validationResult != PathValidator.ValidationResult.Ok)
            {
                switch (validationResult)
                {
                    case PathValidator.ValidationResult.IllegalCharacter:
                        Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_NameIllegalCharacters, MessageBoxImage.Error);
                        break;
                    case PathValidator.ValidationResult.ReservedFileName:
                        Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_NameReserved, MessageBoxImage.Error);
                        break;
                    default:
                        App.Logger.WriteLine(LOG_IDENT, $"Got unhandled PathValidator::ValidationResult {validationResult}");
                        Debug.Assert(false);

                        Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_Unknown, MessageBoxImage.Error);
                        break;
                }
                return;
            }

            // better to check for the file instead of the directory so broken themes can be overwritten
            string path = Path.Combine(Paths.CustomThemes, SelectedCustomThemeName, "Theme.xml");
            if (File.Exists(path))
            {
                Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_NameTaken, MessageBoxImage.Error);
                return;
            }

            try
            {
                RenameCustomThemeStructure(SelectedCustomTheme, SelectedCustomThemeName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                Frontend.ShowMessageBox(string.Format(Strings.Menu_Appearance_CustomThemes_RenameFailed, SelectedCustomTheme, ex.Message), MessageBoxImage.Error);
                return;
            }

            int idx = CustomThemes.IndexOf(SelectedCustomTheme);
            CustomThemes[idx] = SelectedCustomThemeName;

            SelectedCustomThemeIndex = idx;
            OnPropertyChanged(nameof(SelectedCustomThemeIndex));
        }

        private async void EditCustomTheme()
        {
            if (SelectedCustomTheme is null)
                return;

            App.FrostRPC?.SetDialog("Edit Custom Theme");

			var parentWindow = (Window)TopLevel.GetTopLevel(_page)!;
			await new BootstrapperEditorWindow(SelectedCustomTheme).ShowDialog(parentWindow);

            App.FrostRPC?.ClearDialog();
        }

        private async void ExportCustomTheme()
        {
            if (SelectedCustomTheme is null)
                return;

            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null)
                return;

            var storageProvider = mainWindow.StorageProvider;

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Custom Theme",
                SuggestedFileName = $"{SelectedCustomTheme}.zip",
                DefaultExtension = "zip",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Zip Archive")
                    {
                        Patterns = new[] { "*.zip" }
                    }
                }
            });

            if (file == null || string.IsNullOrEmpty(file.Path.LocalPath))
                return;

            string themeDir = Path.Combine(Paths.CustomThemes, SelectedCustomTheme);

            using var memStream = new MemoryStream();
            using var zipStream = new ZipOutputStream(memStream);

            foreach (var filePath in Directory.EnumerateFiles(themeDir, "*.*", SearchOption.AllDirectories))
            {
                string relativePath = filePath[(themeDir.Length + 1)..];

                var entry = new ZipEntry(relativePath);
                entry.DateTime = DateTime.Now;

                zipStream.PutNextEntry(entry);

                using var fileStream = File.OpenRead(filePath);
                fileStream.CopyTo(zipStream);
            }

            zipStream.CloseEntry();
            zipStream.Finish();
            memStream.Position = 0;

            using var outputStream = File.OpenWrite(file.Path.LocalPath);
            memStream.CopyTo(outputStream);

            Process.Start("explorer.exe", $"/select,\"{file.Path.LocalPath}\"");
        }

        private void PopulateCustomThemes()
        {
            string? selected = App.Settings.Prop.SelectedCustomTheme;

            Directory.CreateDirectory(Paths.CustomThemes);

            foreach (string directory in Directory.GetDirectories(Paths.CustomThemes))
            {
                if (!File.Exists(Path.Combine(directory, "Theme.xml")))
                    continue; // missing the main theme file, ignore

                string name = Path.GetFileName(directory);
                CustomThemes.Add(name);
            }

            if (selected != null)
            {
                int idx = CustomThemes.IndexOf(selected);

                if (idx != -1)
                {
                    SelectedCustomThemeIndex = idx;
                    OnPropertyChanged(nameof(SelectedCustomThemeIndex));
                }
                else
                {
                    SelectedCustomTheme = null;
                }
            }
        }

        public string? SelectedCustomTheme
        {
            get => App.Settings.Prop.SelectedCustomTheme;
            set => App.Settings.Prop.SelectedCustomTheme = value;
        }

        public string SelectedCustomThemeName { get; set; } = "";

        public int SelectedCustomThemeIndex { get; set; }

        public ObservableCollection<string> CustomThemes { get; set; } = new();
        public bool IsCustomThemeSelected => SelectedCustomTheme is not null;
    }
}