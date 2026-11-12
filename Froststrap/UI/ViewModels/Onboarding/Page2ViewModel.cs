// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Base;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Onboarding
{
    internal class Page2ViewModel : NotifyPropertyChangedViewModel
    {
        private static readonly string[] JsonPatterns = ["*.json"];
        private static readonly JsonSerializerOptions SerializationOptions = new() { WriteIndented = true };

        public Page2ViewModel()
        {
            InitializeGradientStops();
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

            var dialog = new UI.Elements.Dialogs.ColorPickerDialog(stop.Color);
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
                new() { Offset = 0.0, Color = "#4D5560" },
                new() { Offset = 0.5, Color = "#383F47" },
                new() { Offset = 1.0, Color = "#252A30" }
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
    }
}
