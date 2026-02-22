using System.Windows;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Xml.Linq;
using System.Diagnostics;
using System.IO;
using Bloxstrap.Models.APIs.Config;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class RobloxSettingsViewModel : NotifyPropertyChangedViewModel
    {
        private readonly RemoteDataManager _remoteDataManager;
        public ObservableCollection<SettingsSection> Sections { get; } = new();

        public ICommand OpenRobloxFolderCommand => new RelayCommand(() => Process.Start("explorer.exe", Paths.Roblox));
        public ICommand ExportCommand => new RelayCommand(ExportSettings);
        public ICommand ImportCommand => new RelayCommand(ImportSettings);

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
            foreach (var section in Sections)
            {
                foreach (var control in section.Controls)
                    control.PropertyChanged -= OnControlPropertyChanged;
            }

            Sections.Clear();
            foreach (var sectionConfig in _remoteDataManager.Prop.SettingsPage.Sections)
            {
                Sections.Add(sectionConfig);
                foreach (var control in sectionConfig.Controls)
                    control.PropertyChanged += OnControlPropertyChanged;
            }

            OnPropertyChanged(nameof(Sections));
        }

        private void OnControlPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsControl.Value) && sender is SettingsControl control && control.GBSConfig != null)
            {
                IEnumerable<string> paths = control.GBSConfig.XmlPaths?.Count > 0
                    ? control.GBSConfig.XmlPaths
                    : new[] { control.GBSConfig.XmlPath };

                foreach (var path in paths)
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    App.GlobalSettings.SetValue(path, control.GBSConfig.DataType, control.Value);
                }
            }
        }

        private void LoadCurrentValuesFromGBS()
        {
            App.GlobalSettings.Load();
            foreach (var control in Sections.SelectMany(s => s.Controls).Where(c => c.GBSConfig != null))
            {
                var path = control.GBSConfig.XmlPath ?? control.GBSConfig.XmlPaths.FirstOrDefault();
                if (string.IsNullOrEmpty(path)) continue;

                var val = App.GlobalSettings.GetValue(path, control.GBSConfig.DataType);
                control.Value = !string.IsNullOrEmpty(val) ? val : GetDefault(control);
            }
        }

        private string GetDefault(SettingsControl c) => c.Type switch
        {
            ControlType.ToggleSwitch => "false",
            ControlType.Vector2 => "0,0",
            ControlType.ComboBox => c.Options.FirstOrDefault()?.Value ?? "",
            _ => c.MinValue.ToString()
        };

        public bool ReadOnly
        {
            get => App.GlobalSettings.GetReadOnly();
            set => App.GlobalSettings.SetReadOnly(value);
        }

        private void ExportSettings()
        {
            if (!File.Exists(App.GlobalSettings.FileLocation))
            {
                Frontend.ShowMessageBox("No GBS settings file found to export.", MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "GBS Settings File (*.xml)|*.xml|All files (*.*)|*.*",
                DefaultExt = ".xml",
                FileName = $"FroststrapRobloxSettings.xml",
                Title = "Export GBS Settings"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                bool success = App.GlobalSettings.ExportSettings(saveFileDialog.FileName);

                if (success)
                {
                    Frontend.ShowMessageBox($"Settings exported successfully to {saveFileDialog.FileName}",
                        MessageBoxImage.Information);
                }
                else
                {
                    Frontend.ShowMessageBox("Failed to export settings. Make sure Roblox is not running and try again.",
                        MessageBoxImage.Error);
                }
            }
        }

        private void ImportSettings()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "GBS Settings File (*.xml)|*.xml|All files (*.*)|*.*",
                DefaultExt = ".xml",
                Title = "Import GBS Settings"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var doc = XDocument.Load(openFileDialog.FileName);
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
                    bool success = App.GlobalSettings.ImportSettings(openFileDialog.FileName);

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