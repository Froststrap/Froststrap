using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public class LinuxSettingsViewModel : NotifyPropertyChangedViewModel
    {
        public static bool SoberEnabled => App.SoberSettings.Loaded;

        public LinuxSettingsViewModel()
        {
            StudioEnvEntries = [];
            foreach (var kv in App.Settings.Prop.StudioEnvironmentVariables)
                StudioEnvEntries.Add(new EnvEntry(kv.Key, kv.Value, RemoveEnvEntry));

            AddStudioEnvCommand = new RelayCommand(() =>
            {
                var newEntry = new EnvEntry("", "", RemoveEnvEntry);
                StudioEnvEntries.Add(newEntry);
            });

            OpenWineCfgCommand = new RelayCommand(OpenWineCfg);

            OnPropertyChanged(nameof(IsWineAvailable));
        }

        public static bool SoberAllowGamepadPermission
        {
            get => App.SoberSettings.GetPreset("AllowGamepadPermission") == "true";
            set => App.SoberSettings.SetPreset("AllowGamepadPermission", value);
        }

        public static bool SoberEnableGamemode
        {
            get => App.SoberSettings.GetPreset("EnableGamemode") == "true";
            set => App.SoberSettings.SetPreset("EnableGamemode", value);
        }

        public static bool SoberEnableHiDpi
        {
            get => App.SoberSettings.GetPreset("EnableHiDpi") == "true";
            set => App.SoberSettings.SetPreset("EnableHiDpi", value);
        }

        public static bool SoberTouchMode
        {
            get => App.SoberSettings.GetPreset("TouchMode") == "on";
            set => App.SoberSettings.SetPreset("TouchMode", value ? "on" : "off");
        }

        public static bool SoberUseConsoleExperience
        {
            get => App.SoberSettings.GetPreset("UseConsoleExperience") == "true";
            set => App.SoberSettings.SetPreset("UseConsoleExperience", value);
        }

        public static bool SoberUseLibsecret
        {
            get => App.SoberSettings.GetPreset("UseLibsecret") == "true";
            set => App.SoberSettings.SetPreset("UseLibsecret", value);
        }

        public static bool SoberUseOpengl
        {
            get => App.SoberSettings.GetPreset("UseOpengl") == "true";
            set => App.SoberSettings.SetPreset("UseOpengl", value);
        }

        public ICommand OpenWineCfgCommand { get; }

        private void OpenWineCfg()
        {
            string baseWineDir = Path.Combine(Paths.Base, "Wine");
            var wineMgr = new WineManager(baseWineDir);

            string wineBinary = Path.Combine(baseWineDir, "kombucha", "bin", "wine");
            if (!File.Exists(wineBinary))
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_LinuxSettings_WineBinaryNotFound, MessageBoxImage.Error);
                return;
            }

            string winePrefix = wineMgr.PrefixDir;
            var psi = new ProcessStartInfo
            {
                FileName = wineBinary,
                Arguments = "winecfg",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["WINEPREFIX"] = winePrefix;
            psi.EnvironmentVariables["WINEDLLOVERRIDES"] = "winemenubuilder.exe=d";

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _ = Frontend.ShowMessageBox(string.Format(Strings.Menu_LinuxSettings_FailedToStartWineCfg, ex.Message), MessageBoxImage.Error);
            }
        }

        public static bool IsWineAvailable
        {
            get
            {
                if (!OperatingSystem.IsLinux()) return false;
                string baseWineDir = Path.Combine(Paths.Base, "Wine");
                string wineBinary = Path.Combine(baseWineDir, "kombucha", "bin", "wine");
                if (!File.Exists(wineBinary)) return false;

                var wineMgr = new WineManager(baseWineDir);
                string winePrefix = wineMgr.PrefixDir;
                return Directory.Exists(winePrefix);
            }
        }

        public IEnumerable<StudioRenderer> StudioRendererOptions { get; } = Enum.GetValues<StudioRenderer>();

        public static StudioRenderer SelectedStudioRenderer
        {
            get => App.Settings.Prop.StudioRenderer;
            set => App.Settings.Prop.StudioRenderer = value;
        }

        public static bool StudioGameMode
        {
            get => App.Settings.Prop.StudioGameMode;
            set => App.Settings.Prop.StudioGameMode = value;
        }

        public static bool StudioDebug
        {
            get => App.Settings.Prop.StudioDebug;
            set => App.Settings.Prop.StudioDebug = value;
        }

        public bool VirtualDesktopEnabled
        {
            get => !string.IsNullOrEmpty(App.Settings.Prop.StudioVirtualDesktop);
            set
            {
                if (!value)
                    VirtualDesktopResolution = "";
                else if (string.IsNullOrEmpty(VirtualDesktopResolution))
                    VirtualDesktopResolution = "1920x1080";
                OnPropertyChanged(nameof(VirtualDesktopEnabled));
                OnPropertyChanged(nameof(VirtualDesktopResolution));
            }
        }

        public static string VirtualDesktopResolution
        {
            get => App.Settings.Prop.StudioVirtualDesktop ?? string.Empty;
            set => App.Settings.Prop.StudioVirtualDesktop = value;
        }

        public static string StudioLauncher
        {
            get => App.Settings.Prop.StudioLauncher ?? string.Empty;
            set => App.Settings.Prop.StudioLauncher = value;
        }

        public static bool EnableWebView2
        {
            get => App.Settings.Prop.EnableWebView2;
            set => App.Settings.Prop.EnableWebView2 = value;
        }

        public ObservableCollection<EnvEntry> StudioEnvEntries { get; set; }
        public ICommand AddStudioEnvCommand { get; }

        public class EnvEntry : NotifyPropertyChangedViewModel
        {
            private string _key;
            private string _value;
            private string _originalKey;
            private readonly Action<EnvEntry> _removeAction;

            public string Key
            {
                get => _key;
                set
                {
                    if (_key == value) return;
                    var oldKey = _key;
                    _key = value;
                    OnPropertyChanged();
                    UpdateDictionary(oldKey);
                }
            }

            public string Value
            {
                get => _value;
                set
                {
                    if (_value == value) return;
                    _value = value;
                    OnPropertyChanged();
                    UpdateDictionary(_key);
                }
            }

            public ICommand RemoveCommand { get; }

            public EnvEntry(string key, string value, Action<EnvEntry> removeAction)
            {
                _key = key;
                _originalKey = key;
                _value = value;
                _removeAction = removeAction;
                RemoveCommand = new RelayCommand(() => _removeAction(this));
            }

            private void UpdateDictionary(string currentKey)
            {
                var dict = App.Settings.Prop.StudioEnvironmentVariables;

                if (!string.IsNullOrEmpty(_originalKey) && _originalKey != currentKey)
                    dict.Remove(_originalKey);

                if (!string.IsNullOrWhiteSpace(currentKey))
                    dict[currentKey] = Value;
                else
                    dict.Remove(currentKey);

                _originalKey = currentKey;
                App.Settings.Save();
            }
        }

        private void RemoveEnvEntry(EnvEntry entry)
        {
            App.Settings.Prop.StudioEnvironmentVariables.Remove(entry.Key);
            StudioEnvEntries.Remove(entry);
        }
    }
}