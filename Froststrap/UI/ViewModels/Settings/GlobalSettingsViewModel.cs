using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Enums.GBSPresets;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Xml.Linq;

namespace Froststrap.UI.ViewModels.Settings
{
    public interface IDialogServiceGlobal
    {
        Task OpenGlobalSettingsEditorAsync();
    }

    internal class DefaultGlobalDialogService : IDialogServiceGlobal
    {
        public Task OpenGlobalSettingsEditorAsync()
        {
            return Task.CompletedTask;
        }
    }

    public class GlobalSettingsViewModel(IDialogServiceGlobal dialogService) : NotifyPropertyChangedViewModel
    {
        private readonly IDialogServiceGlobal _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        public GlobalSettingsViewModel() : this(new DefaultGlobalDialogService())
        {
        }

        public ICommand OpenGlobalSettingsEditorCommand => new AsyncRelayCommand(async () =>
        {
            App.Logger.WriteLine("GlobalSettingsViewModel", "Opening Global Settings Editor...");
            await _dialogService.OpenGlobalSettingsEditorAsync();
        });

        public static ICommand OpenRobloxFolderCommand => new RelayCommand(() =>
        {
            string targetPath;

            if (OperatingSystem.IsMacOS())
                targetPath = Path.Combine(Paths.UserProfile, "Library", "Roblox");
            else
                targetPath = Paths.Roblox;

            Utilities.ShellExecute(targetPath, true);
        });
        public ICommand ExportCommand => new RelayCommand(ExportSettings);
        public ICommand ImportCommand => new RelayCommand(ImportSettings);

        private async void ExportSettings()
        {
            var visualRoot = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                             ? desktop.MainWindow
                             : null;

            if (visualRoot == null) return;

            var file = await visualRoot.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export GBS Settings",
                SuggestedFileName = "GlobalBasicSettings_13.xml",
                DefaultExtension = ".xml",
                FileTypeChoices =
                [ new FilePickerFileType("GBS Settings File") { Patterns = ["*.xml" ] } ]
            });

            if (file != null)
            {
                string localPath = file.Path.LocalPath;
                bool success = GBSEditor.ExportSettings(localPath);

                if (success)
                {
                    _ = Frontend.ShowMessageBox(string.Format(Strings.Menu_GlobalSettings_Export_Success, localPath), MessageBoxImage.Information);
                }
                else
                {
                    _ = Frontend.ShowMessageBox(Strings.Menu_GlobalSettings_Export_Fail, MessageBoxImage.Error);
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
                FileTypeFilter = [new FilePickerFileType("GBS Settings File") { Patterns = ["*.xml"] }]
            });

            if (result.Count == 0) return;
            var selectedFile = result[0];

            string localPath = selectedFile.Path.LocalPath;

            try
            {
                var doc = XDocument.Load(localPath);
                if (doc.Root?.Name != "roblox")
                {
                    _ = Frontend.ShowMessageBox(Strings.Menu_GlobalSettings_Import_NotGBS, MessageBoxImage.Warning);
                    return;
                }
            }
            catch
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_GlobalSettings_Import_NotXML, MessageBoxImage.Warning);
                return;
            }

            var confirm = await Frontend.ShowMessageBox(
                Strings.Menu_GlobalSettings_Import_Confirmation,
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo);

            if (confirm == MessageBoxResult.Yes)
            {
                bool success = App.GlobalSettings.ImportSettings(localPath);
                if (success)
                {
                    App.GlobalSettings.Load();
                    _ = Frontend.ShowMessageBox(Strings.Menu_GlobalSettings_Import_Success, MessageBoxImage.Information);
                }
                else
                {
                    _ = Frontend.ShowMessageBox(Strings.Menu_GlobalSettings_Import_Fail, MessageBoxImage.Error);
                }
            }
        }

        public static bool ReadOnly
        {
            get => GBSEditor.GetReadOnly();
            set => App.GlobalSettings.SetReadOnly(value);
        }

        public static int FramerateCap
        {
            get
            {
                if (int.TryParse(App.GlobalSettings.GetPreset("Rendering.FramerateCap"), out int framerate))
                {
                    if (framerate < 1)
                        return 60;
                    else
                        return framerate;
                }
                else
                    return 60;
            }
            set
            {
                if (value < 1)
                    value = -1;

                App.GlobalSettings.SetPreset("Rendering.FramerateCap", value);
            }
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

        public static bool Fullscreen
        {
            get => App.GlobalSettings.GetPreset("Rendering.Fullscreen")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("Rendering.Fullscreen", value);
        }

        public static bool MaxQualityEnabled
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

        public static string MasterVolume
        {
            get => App.GlobalSettings.GetPreset("Audio.MasterVolume")!;
            set => App.GlobalSettings.SetPreset("Audio.MasterVolume", value);
        }

        public static string MasterVolumeStudio
        {
            get => App.GlobalSettings.GetPreset("Audio.MasterVolumeStudio")!;
            set => App.GlobalSettings.SetPreset("Audio.MasterVolumeStudio", value);
        }

        public static string PartyVoiceVolume
        {
            get => App.GlobalSettings.GetPreset("Audio.PartyVoiceVolume")!;
            set => App.GlobalSettings.SetPreset("Audio.PartyVoiceVolume", value);
        }
        public static string MouseSensitivity
        {
            get => App.GlobalSettings.GetPreset("User.MouseSensitivity")!;
            set => App.GlobalSettings.SetPreset("User.MouseSensitivity", value);
        }

        public static bool ShiftLock
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

        public static bool CameraYInverted
        {
            get => App.GlobalSettings.GetPreset("User.CameraYInverted")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("User.CameraYInverted", value);
        }

        public static string HapticStrength
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

        public static bool ReducedMotion
        {
            get => App.GlobalSettings.GetPreset("UI.ReducedMotion")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("UI.ReducedMotion", value);
        }

        public static IReadOnlyDictionary<FontSize, string?> FontSizes => GBSEditor.FontSizes;
        public static FontSize SelectedFontSize
        {
            get => FontSizes.FirstOrDefault(x => x.Value == App.GlobalSettings.GetPreset("UI.FontSize")).Key;
            set => App.GlobalSettings.SetPreset("UI.FontSize", FontSizes[value]);
        }

        public static IReadOnlyDictionary<PlayerListLayOut, string?> PlayerListLayOuts => GBSEditor.PlayerListLayOuts;
        public static PlayerListLayOut SelectedPlayerListLayOut
        {
            get => PlayerListLayOuts.FirstOrDefault(x => x.Value == App.GlobalSettings.GetPreset("UI.PlayerListLayOut")).Key;
            set => App.GlobalSettings.SetPreset("UI.PlayerListLayOut", PlayerListLayOuts[value]);
        }

        public static bool PerformanceStatsVisible
        {
            get => App.GlobalSettings.GetPreset("Misc.PerformanceStatsVisible")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("Misc.PerformanceStatsVisible", value);
        }

        public static bool ChatTranslationEnabled
        {
            get => App.GlobalSettings.GetPreset("Misc.ChatTranslationEnabled")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("Misc.ChatTranslationEnabled", value);
        }

        public static bool ChatTranslationFTUXShown
        {
            get => App.GlobalSettings.GetPreset("Misc.ChatTranslationFTUXShown")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("Misc.ChatTranslationFTUXShown", value);
        }

        public static bool VREnabled
        {
            get => App.GlobalSettings.GetPreset("User.VREnabled")?.ToLower() == "true";
            set => App.GlobalSettings.SetPreset("User.VREnabled", value);
        }
    }
}
