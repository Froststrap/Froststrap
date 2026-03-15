using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Enums.GBSPresets;
using System.Windows.Input;
using System.Xml.Linq;

namespace Froststrap.UI.ViewModels.Settings
{
    public class RobloxSettingsViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand OpenRobloxFolderCommand => new RelayCommand(() => Process.Start("explorer.exe", Paths.Roblox));
        public ICommand ExportCommand => new RelayCommand(ExportSettings);
        public ICommand ImportCommand => new RelayCommand(ImportSettings);

        private async void ExportSettings()
        {
            if (!File.Exists(App.GlobalSettings.FileLocation))
            {
                _ = Frontend.ShowMessageBox("No GBS settings file found to export.", MessageBoxImage.Warning);
                return;
            }

            var visualRoot = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                             ? desktop.MainWindow
                             : null;

            if (visualRoot == null) return;

            var file = await visualRoot.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export GBS Settings",
                SuggestedFileName = "FroststrapRobloxSettings.xml",
                DefaultExtension = ".xml",
                FileTypeChoices = new[]
                { new FilePickerFileType("GBS Settings File") { Patterns = new[] { "*.xml" } }}
            });

            if (file != null)
            {
                string localPath = file.Path.LocalPath;
                bool success = App.GlobalSettings.ExportSettings(localPath);

                if (success)
                {
                    _ = Frontend.ShowMessageBox($"Settings exported successfully to {localPath}", MessageBoxImage.Information);
                }
                else
                {
                    _ = Frontend.ShowMessageBox("Failed to export settings. Make sure Roblox is not running and try again.", MessageBoxImage.Error);
                }
            }
        }

        private async void ImportSettings()
        {
            var visualRoot = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                             ? desktop.MainWindow
                             : null;

            if (visualRoot == null) return;

            var result = await visualRoot.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import GBS Settings",
                AllowMultiple = false,
                FileTypeFilter = new[]
                { new FilePickerFileType("GBS Settings File") { Patterns = new[] { "*.xml" } } }
            });

            var selectedFile = result.FirstOrDefault();
            if (selectedFile == null) return;

            string localPath = selectedFile.Path.LocalPath;

            try
            {
                var doc = XDocument.Load(localPath);
                if (doc.Root?.Name != "roblox")
                {
                    _ = Frontend.ShowMessageBox("The selected file does not appear to be a valid GBS settings file.", MessageBoxImage.Warning);
                    return;
                }
            }
            catch
            {
                _ = Frontend.ShowMessageBox("The selected file is not a valid XML file.", MessageBoxImage.Warning);
                return;
            }

            var confirm = await Frontend.ShowMessageBox(
                "This will replace all your current Roblox settings with the imported ones. Are you sure you want to continue?",
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo);

            if (confirm == MessageBoxResult.Yes)
            {
                bool success = App.GlobalSettings.ImportSettings(localPath);

                if (success)
                {
                    App.GlobalSettings.Load();
                    _ = Frontend.ShowMessageBox("Settings imported successfully!", MessageBoxImage.Information);
                }
                else
                {
                    _ = Frontend.ShowMessageBox("Failed to import settings. Make sure Roblox is not running and try again.", MessageBoxImage.Error);
                }
            }
        }

        public bool ReadOnly
        {
            get => App.GlobalSettings.GetReadOnly();
            set => App.GlobalSettings.SetReadOnly(value);
        }

        public string FramerateCap
        {
            get => App.GlobalSettings.GetPreset("Rendering.FramerateCap")!;
            set => App.GlobalSettings.SetPreset("Rendering.FramerateCap", value);
        }

        public string GraphicsQuality
        {
            get => App.GlobalSettings.GetPreset("Rendering.SavedQualityLevel")!;
            set
            {
                App.GlobalSettings.SetPreset("Rendering.SavedQualityLevel", value);
                OnPropertyChanged(nameof(GraphicsQuality));
            }
        }

        public bool Fullscreen
        {
            get => App.GlobalSettings.GetPreset("Rendering.Fullscreen")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("Rendering.Fullscreen", value);
        }

        public bool MaxQualityEnabled
        {
            get => App.GlobalSettings.GetPreset("Rendering.MaxQualityEnabled")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("Rendering.MaxQualityEnabled", value);
        }

        public bool VignetteEnabled
        {
            get
            {
                bool setting1 = App.GlobalSettings.GetPreset("Rendering.VignetteEnabled")?.ToLower() == "true";
                bool setting2 = App.GlobalSettings.GetPreset("Rendering.VignetteEnableOption")?.ToLower() == "true";

                return setting1 && setting2;
            }
            set
            {
                string val = value.ToString().ToLower();

                App.GlobalSettings.SetPreset("Rendering.VignetteEnabled", val);
                App.GlobalSettings.SetPreset("Rendering.VignetteEnableOption", val);

                OnPropertyChanged(nameof(VignetteEnabled));
            }
        }

        public string MasterVolume
        {
            get => App.GlobalSettings.GetPreset("Audio.MasterVolume")!;
            set => App.GlobalSettings.SetPreset("Audio.MasterVolume", value);
        }

        public string MasterVolumeStudio
        {
            get => App.GlobalSettings.GetPreset("Audio.MasterVolumeStudio")!;
            set => App.GlobalSettings.SetPreset("Audio.MasterVolumeStudio", value);
        }

        public string PartyVoiceVolume
        {
            get => App.GlobalSettings.GetPreset("Audio.PartyVoiceVolume")!;
            set => App.GlobalSettings.SetPreset("Audio.PartyVoiceVolume", value);
        }
        public string MouseSensitivity
        {
            get => App.GlobalSettings.GetPreset("User.MouseSensitivity")!;
            set => App.GlobalSettings.SetPreset("User.MouseSensitivity", value);
        }

        public bool ShiftLock
        {
            get => App.GlobalSettings.GetPreset("User.ShiftLock") == "1";
            set => App.GlobalSettings.SetPreset("User.ShiftLock", value ? "1" : "0");
        }

        public string MouseSensitivityFirstPersonX
        {
            get => App.GlobalSettings.GetVectorValue("User.MouseSensitivityFirstPerson", "X");
            set
            {
                App.GlobalSettings.SetVectorValue("User.MouseSensitivityFirstPerson", "X", value);
                OnPropertyChanged(nameof(MouseSensitivityFirstPersonX));
            }
        }

        public string MouseSensitivityFirstPersonY
        {
            get => App.GlobalSettings.GetVectorValue("User.MouseSensitivityFirstPerson", "Y");
            set
            {
                App.GlobalSettings.SetVectorValue("User.MouseSensitivityFirstPerson", "Y", value);
                OnPropertyChanged(nameof(MouseSensitivityFirstPersonY));
            }
        }
        public string MouseSensitivityThirdPersonX
        {
            get => App.GlobalSettings.GetVectorValue("User.MouseSensitivityThirdPerson", "X");
            set
            {
                App.GlobalSettings.SetVectorValue("User.MouseSensitivityThirdPerson", "X", value);
                OnPropertyChanged(nameof(MouseSensitivityThirdPersonX));
            }
        }

        public string MouseSensitivityThirdPersonY
        {
            get => App.GlobalSettings.GetVectorValue("User.MouseSensitivityThirdPerson", "Y");
            set
            {
                App.GlobalSettings.SetVectorValue("User.MouseSensitivityThirdPerson", "Y", value);
                OnPropertyChanged(nameof(MouseSensitivityThirdPersonY));
            }
        }

        public bool CameraYInverted
        {
            get => App.GlobalSettings.GetPreset("User.CameraYInverted")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("User.CameraYInverted", value);
        }

        public string HapticStrength
        {
            get => App.GlobalSettings.GetPreset("User.HapticStrength")!;
            set => App.GlobalSettings.SetPreset("User.HapticStrength", value);
        }

        public string UITransparency
        {
            get => App.GlobalSettings.GetPreset("UI.Transparency")!;
            set
            {
                App.GlobalSettings.SetPreset("UI.Transparency", value.Length >= 3 ? value[..3] : value);
                OnPropertyChanged(nameof(UITransparency));
            }
        }

        public bool ReducedMotion
        {
            get => App.GlobalSettings.GetPreset("UI.ReducedMotion")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("UI.ReducedMotion", value);
        }

        public IReadOnlyDictionary<FontSize, string?> FontSizes => GBSEditor.FontSizes;
        public FontSize SelectedFontSize
        {
            get => FontSizes.FirstOrDefault(x => x.Value == App.GlobalSettings.GetPreset("UI.FontSize")).Key;
            set => App.GlobalSettings.SetPreset("UI.FontSize", FontSizes[value]);
        }

        public bool PerformanceStatsVisible
        {
            get => App.GlobalSettings.GetPreset("Misc.PerformanceStatsVisible")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("Misc.PerformanceStatsVisible", value);
        }

        public bool ChatTranslationEnabled
        {
            get => App.GlobalSettings.GetPreset("Misc.ChatTranslationEnabled")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("Misc.ChatTranslationEnabled", value);
        }

        public bool ChatTranslationFTUXShown
        {
            get => App.GlobalSettings.GetPreset("Misc.ChatTranslationFTUXShown")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("Misc.ChatTranslationFTUXShown", value);
        }

        public bool VREnabled
        {
            get => App.GlobalSettings.GetPreset("User.VREnabled")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("User.VREnabled", value);
        }
    }
}