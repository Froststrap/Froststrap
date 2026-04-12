using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.Dialogs;
using Froststrap.UI.Elements.Editor;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public partial class AppearanceViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand PreviewBootstrapperCommand => new RelayCommand(PreviewBootstrapper);
        public IAsyncRelayCommand<Control> BrowseCustomIconLocationCommand => new AsyncRelayCommand<Control>(BrowseCustomIconLocation);

        public ICommand AddCustomThemeCommand => new RelayCommand<Control>(async c => await AddCustomTheme(c));
        public ICommand EditCustomThemeCommand => new RelayCommand<Control>(async c => await EditCustomTheme(c));
        public ICommand ExportCustomThemeCommand => new RelayCommand<Control>(async c => await ExportCustomTheme(c));
        public IAsyncRelayCommand DeleteCustomThemeCommand => new AsyncRelayCommand(() => Task.Run(DeleteCustomTheme));
        public IAsyncRelayCommand RenameCustomThemeCommand => new AsyncRelayCommand(() => Task.Run(RenameCustomTheme));

        private async void PreviewBootstrapper()
        {
            App.FrostRPC?.SetDialog("Preview Launcher");

            IBootstrapperDialog dialog = await App.Settings.Prop.BootstrapperStyle.GetNew();

            if (App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.ByfronDialog)
                dialog.Message = Strings.Bootstrapper_StylePreview_ImageCancel;
            else
                dialog.Message = Strings.Bootstrapper_StylePreview_TextCancel;

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
                CustomIconLocation = files[0].Path.LocalPath;

                OnPropertyChanged(nameof(CustomIconLocation));
            }
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

        public AppearanceViewModel()
        {
            foreach (var entry in BootstrapperIconEx.Selections)
                Icons.Add(new BootstrapperIconEntry { IconType = entry });

            PopulateCustomThemes();
            InitializeGradientStops();
        }

        public IEnumerable<BootstrapperStyle> Dialogs { get; } = BootstrapperStyleEx.Selections;
        public BootstrapperStyle Dialog
        {
            get => App.Settings.Prop.BootstrapperStyle;
            set
            {
                if (App.Settings.Prop.BootstrapperStyle != value)
                {
                    App.Settings.Prop.BootstrapperStyle = value;
                    OnPropertyChanged(nameof(Dialog));
                    OnPropertyChanged(nameof(CustomThemesExpanded));
                }
            }
        }

        public bool CustomThemesExpanded => App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.CustomDialog;

        private void DeleteCustomThemeStructure(string name)
        {
            string dir = Path.Combine(Paths.CustomThemes, name);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        private void RenameCustomThemeStructure(string oldName, string newName)
        {
            string oldDir = Path.Combine(Paths.CustomThemes, oldName);
            string newDir = Path.Combine(Paths.CustomThemes, newName);
            Directory.Move(oldDir, newDir);
        }

        private async Task AddCustomTheme(Control? control)
        {
            var topLevel = TopLevel.GetTopLevel(control) ??
                           (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (topLevel is not Window parentWindow)
            {
                App.Logger.WriteLine("AppearanceVM", "AddCustomTheme: No parent window found.");
                return;
            }

            App.FrostRPC?.SetDialog("Add Custom Launcher");

            var dialog = new AddCustomThemeDialog();
            await dialog.ShowDialog(parentWindow);

            App.FrostRPC?.ClearDialog();

            if (dialog.Created)
            {
                CustomThemes.Add(dialog.ThemeName);

                SelectedCustomTheme = dialog.ThemeName;
                SelectedCustomThemeIndex = CustomThemes.IndexOf(dialog.ThemeName);

                OnPropertyChanged(nameof(SelectedCustomThemeIndex));

                if (dialog.OpenEditor)
                    await EditCustomTheme(control);
            }
        }

        private async Task EditCustomTheme(Control? control)
        {
            if (SelectedCustomTheme is null) return;

            var topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is not Window parentWindow) return;

            App.FrostRPC?.SetDialog("Editing Custom Theme");
            await new BootstrapperEditorWindow(SelectedCustomTheme).ShowDialog(parentWindow);
            App.FrostRPC?.ClearDialog();
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
                FileTypeChoices = new[] { new FilePickerFileType("Zip Archive") { Patterns = new[] { "*.zip" } } }
            });

            if (file is null) return;

            string themeDir = Path.Combine(Paths.CustomThemes, SelectedCustomTheme);

            await Task.Run(async () =>
            {
                using var outputStream = await file.OpenWriteAsync();
                using var zipStream = new ZipOutputStream(outputStream);
                zipStream.SetLevel(5);

                foreach (var filePath in Directory.EnumerateFiles(themeDir, "*.*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(themeDir, filePath);
                    var entry = new ZipEntry(relativePath) { DateTime = DateTime.Now };

                    zipStream.PutNextEntry(entry);
                    using var fileStream = File.OpenRead(filePath);
                    fileStream.CopyTo(zipStream);
                    zipStream.CloseEntry();
                }

                zipStream.Finish();
                zipStream.Close();
            });

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", $"/select,\"{file.Path.LocalPath}\"");
            }
        }

        private async void DeleteCustomTheme()
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
                await Frontend.ShowMessageBox(string.Format(Strings.Menu_Appearance_CustomThemes_DeleteFailed, SelectedCustomTheme, ex.Message), MessageBoxImage.Error);
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
                App.Logger.WriteException("Rename", ex);
                await Frontend.ShowMessageBox("Rename failed", MessageBoxImage.Error);
                return;
            }

            string newName = SelectedCustomThemeName;
            int idx = CustomThemes.IndexOf(SelectedCustomTheme);

            CustomThemes[idx] = newName;
            SelectedCustomTheme = newName;
            SelectedCustomThemeIndex = idx;

            OnPropertyChanged(nameof(SelectedCustomThemeIndex));
            OnPropertyChanged(nameof(SelectedCustomTheme));
            OnPropertyChanged(nameof(SelectedCustomThemeName));
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

            if (!string.IsNullOrEmpty(selected))
            {
                int idx = CustomThemes.IndexOf(selected);
                if (idx != -1)
                {
                    SelectedCustomThemeIndex = idx;
                    SelectedCustomTheme = selected;
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

                    OnPropertyChanged(nameof(SelectedCustomTheme));
                    OnPropertyChanged(nameof(SelectedCustomThemeName));
                    OnPropertyChanged(nameof(IsCustomThemeSelected));
                }
            }
        }

        public string SelectedCustomThemeName { get; set; } = "";
        public int SelectedCustomThemeIndex { get; set; }
        public ObservableCollection<string> CustomThemes { get; set; } = new();
        public bool IsCustomThemeSelected => SelectedCustomTheme is not null;

        #region Custom App Themes
        public IEnumerable<Theme> Themes { get; } = Enum.GetValues(typeof(Theme)).Cast<Theme>();

        public Theme Theme
        {
            get => App.Settings.Prop.Theme;
            set
            {
                App.Settings.Prop.Theme = value;
                OnPropertyChanged(nameof(Theme));
                OnPropertyChanged(nameof(CustomThemeExpanded));
                ApplyThemeUpdate();
            }
        }

        public bool CustomThemeExpanded => App.Settings.Prop.Theme == Theme.Custom;

        public IEnumerable<BackgroundMode> BackgroundTypes { get; } = Enum.GetValues(typeof(BackgroundMode)).Cast<BackgroundMode>();
        public IEnumerable<BackgroundStretch> BackgroundStretches { get; } = Enum.GetValues(typeof(BackgroundStretch)).Cast<BackgroundStretch>();

        public BackgroundMode BackgroundType
        {
            get => App.Settings.Prop.BackgroundType;
            set
            {
                App.Settings.Prop.BackgroundType = value;
                OnPropertyChanged(nameof(BackgroundType));
                OnPropertyChanged(nameof(IsGradientMode));
                OnPropertyChanged(nameof(IsImageMode));
                ApplyThemeUpdate();
            }
        }

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

        public bool IsGradientMode => BackgroundType == BackgroundMode.Gradient;
        public bool IsImageMode => BackgroundType == BackgroundMode.Image;

        public double GradientAngle
        {
            get => App.Settings.Prop.GradientAngle;
            set
            {
                App.Settings.Prop.GradientAngle = value;
                OnPropertyChanged(nameof(GradientAngle));
                ApplyThemeUpdate();
            }
        }

        public ObservableCollection<GradientStops> GradientStops { get; } = new();

        private ICommand? _addGradientStopCommand;
        public ICommand AddGradientStopCommand => _addGradientStopCommand ??= new RelayCommand(async () => await AddGradientStop());

        private ICommand? _resetGradientCommand;
        public ICommand ResetGradientCommand => _resetGradientCommand ??= new RelayCommand(() => ResetGradient());

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
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll },
                AllowMultiple = false
            });

            var file = files.FirstOrDefault();
            if (file != null)
            {
                BackgroundImagePath = file.Path.LocalPath;
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
            var newStop = new GradientStops { Offset = 0.5, Color = "#000000" };
            newStop.PropertyChanged += OnGradientStopPropertyChanged;
            GradientStops.Add(newStop);
            ApplyThemeUpdate();
        }

        private void RemoveGradientStop(GradientStops stop)
        {
            if (stop == null) return;
            stop.PropertyChanged -= OnGradientStopPropertyChanged;
            GradientStops.Remove(stop);
            ApplyThemeUpdate();
        }

        private void ResetGradient()
        {
            var defaultStops = new List<GradientStops>
            {
                new GradientStops { Offset = 0.0, Color = "#4D5560" },
                new GradientStops { Offset = 0.5, Color = "#383F47" },
                new GradientStops { Offset = 1.0, Color = "#252A30" }
            };

            foreach (var stop in GradientStops) stop.PropertyChanged -= OnGradientStopPropertyChanged;
            GradientStops.Clear();

            foreach (var stop in defaultStops)
            {
                stop.PropertyChanged += OnGradientStopPropertyChanged;
                GradientStops.Add(stop);
            }

            GradientAngle = 0;
            OnPropertyChanged(nameof(GradientAngle));

            ApplyThemeUpdate();
        }

        private async Task ExportGradient(TopLevel topLevel)
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Gradient",
                FileTypeChoices = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } },
                SuggestedFileName = "Froststrap Gradient Background.json"
            });

            if (file == null) return;

            var data = new
            {
                GradientStops = GradientStops.Select(s => new { s.Offset, s.Color }).ToList(),
                GradientAngle = GradientAngle
            };

            using var stream = await file.OpenWriteAsync();
            await JsonSerializer.SerializeAsync(stream, data, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task ImportGradient(TopLevel topLevel)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Gradient",
                FileTypeFilter = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } },
                AllowMultiple = false
            });

            var file = files.FirstOrDefault();
            if (file == null) return;

            try
            {
                using var stream = await file.OpenReadAsync();
                using var document = await JsonDocument.ParseAsync(stream);
                var root = document.RootElement;

                foreach (var s in GradientStops) s.PropertyChanged -= OnGradientStopPropertyChanged;
                GradientStops.Clear();

                if (root.TryGetProperty("GradientStops", out var stopsElement))
                {
                    foreach (var stop in stopsElement.EnumerateArray())
                    {
                        var newStop = new GradientStops
                        {
                            Offset = stop.GetProperty("Offset").GetDouble(),
                            Color = stop.GetProperty("Color").GetString() ?? "#FFFFFF"
                        };
                        newStop.PropertyChanged += OnGradientStopPropertyChanged;
                        GradientStops.Add(newStop);
                    }
                }

                if (root.TryGetProperty("GradientAngle", out var angleElement))
                {
                    GradientAngle = angleElement.GetDouble();
                }

                ApplyThemeUpdate();
            }
            catch (Exception) { }
        }

        private void ApplyThemeUpdate()
        {
            App.Settings.Prop.CustomGradientStops = GradientStops.Select(x => new GradientStops
            {
                Offset = x.Offset,
                Color = x.Color
            }).ToList();

            App.Settings.Prop.GradientAngle = GradientAngle;

            AvaloniaWindow.ApplyTheme();
        }

        private void InitializeGradientStops()
        {
            foreach (var s in GradientStops) s.PropertyChanged -= OnGradientStopPropertyChanged;
            GradientStops.Clear();

            if (App.Settings.Prop.CustomGradientStops != null && App.Settings.Prop.CustomGradientStops.Any())
            {
                foreach (var stop in App.Settings.Prop.CustomGradientStops)
                {
                    var newStop = new GradientStops
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
        }
        #endregion
    }
}