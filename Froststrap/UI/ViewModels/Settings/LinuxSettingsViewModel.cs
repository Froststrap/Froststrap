// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;

using Froststrap.Clients.Contracts;

namespace Froststrap.UI.ViewModels.Settings;

internal sealed class LinuxSettingsViewModel : NotifyPropertyChangedViewModel
{
    private static ISettingsSync? Settings => App.Clients.Player.SettingsSync;

    public static bool SoberEnabled => Settings?.Loaded == true;

    public LinuxSettingsViewModel()
    {
        StudioEnvEntries = [];
        foreach (var kv in App.Settings.Prop.StudioEnvironmentVariables)
            StudioEnvEntries.Add(new EnvEntry(kv.Key, kv.Value, RemoveEnvEntry));

        AddStudioEnvCommand = new RelayCommand(() =>
        {
            var newEntry = new EnvEntry("", "", RemoveEnvEntry);
            StudioEnvEntries.Add(newEntry);
            OnPropertyChanged(nameof(HasStudioEnvEntries));
        });

        OpenWineCfgCommand = new RelayCommand(OpenWineCfg);

        OnPropertyChanged(nameof(IsWineAvailable));
    }

    public static bool SoberAllowGamepadPermission
    {
        get => Settings?.GetPreset("AllowGamepadPermission") == "true";
        set => Settings?.SetPreset("AllowGamepadPermission", value);
    }

    public static bool SoberEnableGamemode
    {
        get => Settings?.GetPreset("EnableGamemode") == "true";
        set => Settings?.SetPreset("EnableGamemode", value);
    }

    public static bool SoberEnableHiDpi
    {
        get => Settings?.GetPreset("EnableHiDpi") == "true";
        set => Settings?.SetPreset("EnableHiDpi", value);
    }

    public static bool SoberTouchMode
    {
        get => Settings?.GetPreset("TouchMode") == "on";
        set => Settings?.SetPreset("TouchMode", value ? "on" : "off");
    }

    public static bool SoberUseConsoleExperience
    {
        get => Settings?.GetPreset("UseConsoleExperience") == "true";
        set => Settings?.SetPreset("UseConsoleExperience", value);
    }

    public static bool SoberUseOpengl
    {
        get => Settings?.GetPreset("UseOpengl") == "true";
        set => Settings?.SetPreset("UseOpengl", value);
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
            _ = Frontend.ShowMessageBox(string.Format(CultureInfo.InvariantCulture, Strings.Menu_LinuxSettings_FailedToStartWineCfg, ex.Message), MessageBoxImage.Error);
        }
    }

    public static bool IsWineAvailable
    {
        get
        {
            if (App.Clients.Studio.RuntimeEnvironment is null) return false;
            string baseWineDir = Clients.Launchers.WineLauncher.WineRoot;
            string wineBinary = Clients.Launchers.WineLauncher.WineExecutable;
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

    public bool HasStudioEnvEntries => StudioEnvEntries.Count > 0;
    public ICommand AddStudioEnvCommand { get; }

    internal sealed class EnvEntry : NotifyPropertyChangedViewModel
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
        OnPropertyChanged(nameof(HasStudioEnvEntries));
    }
}
