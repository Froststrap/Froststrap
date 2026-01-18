using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Models.APIs.Config;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Xml.Linq;

namespace Froststrap.UI.ViewModels.Settings
{
    public class RobloxSettingsViewModel : INotifyPropertyChanged
    {
        private void OpenRobloxFolder() => Process.Start("explorer.exe", Paths.Roblox);

        public ICommand OpenRobloxFolderCommand => new RelayCommand(OpenRobloxFolder);

        private readonly RemoteDataManager _remoteDataManager;

        public ObservableCollection<SettingsSection> Sections { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public RobloxSettingsViewModel(RemoteDataManager remoteDataManager)
        {
            _remoteDataManager = remoteDataManager;
            _remoteDataManager.Subscribe(OnRemoteDataLoaded);
        }

        private void OnRemoteDataLoaded(object? sender, EventArgs e)
        {
            LoadFromRemoteConfig();
            LoadCurrentValuesFromGBS();
        }

        public void LoadFromRemoteConfig()
        {
            Sections.Clear();
            var pageConfig = _remoteDataManager.Prop.SettingsPage;

            foreach (var sectionConfig in pageConfig.Sections)
            {
                Sections.Add(sectionConfig);
            }

            SubscribeToControlChanges();
            OnPropertyChanged(nameof(Sections));
        }

        private void SubscribeToControlChanges()
        {
            foreach (var section in Sections)
            {
                foreach (var control in section.Controls)
                {
                    control.PropertyChanged += OnControlPropertyChanged;
                }
            }
        }

        private void OnControlPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is SettingsControl control && control.GBSConfig != null)
            {
                IEnumerable<string> paths = control.GBSConfig.XmlPaths.Count > 0
                    ? control.GBSConfig.XmlPaths
                    : new[] { control.GBSConfig.XmlPath };

                foreach (var path in paths)
                {
                    App.GlobalSettings.SetValue(path, control.GBSConfig.DataType, control.Value);
                }
            }
        }

        private void LoadCurrentValuesFromGBS()
        {
            App.GlobalSettings.Load();

            foreach (var section in Sections)
            {
                foreach (var control in section.Controls)
                {
                    if (control.GBSConfig != null)
                    {
                        IEnumerable<string> paths = control.GBSConfig.XmlPaths.Count > 0
                            ? control.GBSConfig.XmlPaths
                            : new[] { control.GBSConfig.XmlPath };

                        if (control.Type == ControlType.ToggleSwitch && paths.Count() > 1)
                        {
                            // Logical AND of all values
                            bool allTrue = paths.All(p =>
                                App.GlobalSettings.GetValue(p, "bool")?.ToLower() == "true"
                            );

                            control.Value = allTrue.ToString().ToLower();
                        }
                        else if (!string.IsNullOrEmpty(control.GBSConfig.XmlPath))
                        {
                            var currentValue = App.GlobalSettings.GetValue(control.GBSConfig.XmlPath, control.GBSConfig.DataType);
                            control.Value = !string.IsNullOrEmpty(currentValue) ? currentValue : GetDefaultValueForControl(control);
                        }
                    }
                }
            }
        }

        private string GetDefaultValueForControl(SettingsControl control)
        {
            return control.Type switch
            {
                ControlType.Slider => control.MinValue.ToString(),
                ControlType.ToggleSwitch => "false",
                ControlType.ComboBox when control.Options.Count > 0 => control.Options[0].Value,
                ControlType.Vector2 => "0,0",
                _ => ""
            };
        }

        public bool ReadOnly
        {
            get => App.GlobalSettings.GetReadOnly();
            set => App.GlobalSettings.SetReadOnly(value);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ICommand? _exportCommand;
        private ICommand? _importCommand;

        public ICommand ExportCommand => _exportCommand ??= new RelayCommand(ExportSettings);
        public ICommand ImportCommand => _importCommand ??= new RelayCommand(ImportSettings);

        private async void ExportSettings()
        {
            if (!File.Exists(App.GlobalSettings.FileLocation))
            {
                Frontend.ShowMessageBox("No GBS settings file found to export.", MessageBoxImage.Warning);
                return;
            }

            // Get the main window
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null)
                return;

            // Use StorageProvider API
            var storageProvider = mainWindow.StorageProvider;

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export GBS Settings",
                SuggestedFileName = "FroststrapRobloxSettings.xml",
                DefaultExtension = "xml",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("GBS Settings File")
                    {
                        Patterns = new[] { "*.xml" }
                    },
                    new FilePickerFileType("All files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (file != null && !string.IsNullOrEmpty(file.Path.LocalPath))
            {
                bool success = App.GlobalSettings.ExportSettings(file.Path.LocalPath);

                if (success)
                {
                    Frontend.ShowMessageBox($"Settings exported successfully to {file.Path.LocalPath}",
                        MessageBoxImage.Information);
                }
                else
                {
                    Frontend.ShowMessageBox("Failed to export settings. Make sure Roblox is not running and try again.",
                        MessageBoxImage.Error);
                }
            }
        }

        private async void ImportSettings()
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null)
                return;

            var storageProvider = mainWindow.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import GBS Settings",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("GBS Settings File")
                    {
                        Patterns = new[] { "*.xml" }
                    }
                }
            });

            if (files != null && files.Count > 0)
            {
                var file = files[0];
                var filePath = file.Path.LocalPath;

                try
                {
                    var doc = XDocument.Load(filePath);
                    if (doc.Root?.Name != "roblox")
                    {
                        Frontend.ShowMessageBox("The selected file does not appear to be a valid GBS settings file.",
                            MessageBoxImage.Warning);
                        return;
                    }
                }
                catch
                {
                    Frontend.ShowMessageBox("The selected file is not a valid XML file.",
                        MessageBoxImage.Warning);
                    return;
                }

                var result = Frontend.ShowMessageBox(
                    "This will replace all your current Roblox settings with the imported ones. Are you sure you want to continue?",
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    bool success = App.GlobalSettings.ImportSettings(filePath);

                    if (success)
                    {
                        LoadCurrentValuesFromGBS();

                        Frontend.ShowMessageBox("Settings imported successfully!",
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        Frontend.ShowMessageBox("Failed to import settings. Make sure Roblox is not running and try again.",
                            MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}