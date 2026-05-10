using System.Runtime.InteropServices;

namespace Froststrap.UI.ViewModels.Settings
{
    public class BehaviourViewModel : NotifyPropertyChangedViewModel
    {
        public BehaviourViewModel()
        {
            App.Cookies.StateChanged += (_, state) =>
                CookieLoadingFailed = state is not (CookieState.Success or CookieState.Unknown);
        }

        public static IEnumerable<ProcessPriorityOption> ProcessPriorityOptions => Enum.GetValues<ProcessPriorityOption>();
        public static ProcessPriorityOption SelectedPriority
        {
            get => App.Settings.Prop.SelectedProcessPriority;
            set => App.Settings.Prop.SelectedProcessPriority = value;
        }

        // Ill move to global settings in the future, too lazy to do it now
        public static bool IsAppStorageVisible => App.StorageSettings.Loaded && (ShowLaunchAtStartup || ShowMinimizeToTray || ShowSystemTrayModal || ShowTheme) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool ShowLaunchAtStartup => !string.IsNullOrEmpty(App.StorageSettings.Prop.LaunchAtStartup);
        public static bool ShowMinimizeToTray => !string.IsNullOrEmpty(App.StorageSettings.Prop.MinimizeToTray);
        public static bool ShowSystemTrayModal => !string.IsNullOrEmpty(App.StorageSettings.Prop.SystemTrayModalShown);
        public static bool ShowTheme => !string.IsNullOrEmpty(App.StorageSettings.Prop.DeviceLevelTheme);

        public static bool LaunchAtStartup
        {
            get => App.StorageSettings.Prop.LaunchAtStartup?.ToLower() == "true";
            set => App.StorageSettings.Prop.LaunchAtStartup = value.ToString().ToLower();
        }

        public static bool SystemTrayModalShown
        {
            get => App.StorageSettings.Prop.SystemTrayModalShown?.ToLower() == "true";
            set => App.StorageSettings.Prop.SystemTrayModalShown = value.ToString().ToLower();
        }

        public static bool MinimizeToTray
        {
            get => App.StorageSettings.Prop.MinimizeToTray?.ToLower() == "true";
            set => App.StorageSettings.Prop.MinimizeToTray = value.ToString().ToLower();
        }

        public static IEnumerable<AppStorageSettingTheme> AppThemeOptions => Enum.GetValues<AppStorageSettingTheme>();

        public static AppStorageSettingTheme SelectedTheme
        {
            get
            {
                var json = App.StorageSettings.Prop.DeviceLevelTheme;
                return (!string.IsNullOrEmpty(json) && json.Contains("dark")) ? AppStorageSettingTheme.Dark : AppStorageSettingTheme.Light;
            }
            set
            {
                string themeStr = (value == AppStorageSettingTheme.Dark) ? "dark" : "light";
                string userId = App.StorageSettings.Prop.UserId ?? "0";
                App.StorageSettings.Prop.DeviceLevelTheme = $"{{\"{userId}\":\"{themeStr}\"}}";
            }
        }

        // too lazy to make new folder and place it there
        public enum AppStorageSettingTheme
        {
            Light,
            Dark
        }

        public bool MultiInstances
        {
            get => App.Settings.Prop.MultiInstanceLaunching;
            set => HandleMultiInstanceChange(value);
        }

        private async void HandleMultiInstanceChange(bool value)
        {
            if (value && !App.Settings.Prop.MultiInstanceLaunching)
            {
                var result = await Frontend.ShowMessageBox(
                    "Roblox stated that multi-instance launching is considered an exploit, but it isn't bannable.\n\n" +
                    "Are you sure you want to enable multi-instance launching?",
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo
                );

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            App.Settings.Prop.MultiInstanceLaunching = value;

            if (!value)
            {
                Error773Fix = false;
                OnPropertyChanged(nameof(Error773Fix));
            }

            OnPropertyChanged(nameof(MultiInstances));
        }

        public static bool Error773Fix
        {
            get => App.Settings.Prop.Error773Fix;
            set => App.Settings.Prop.Error773Fix = value;
        }

        public static bool BackgroundUpdates
        {
            get => App.Settings.Prop.BackgroundUpdatesEnabled;
            set => App.Settings.Prop.BackgroundUpdatesEnabled = value;
        }

        public static bool CloseCrashHandler
        {
            get => App.Settings.Prop.AutoCloseCrashHandler;
            set => App.Settings.Prop.AutoCloseCrashHandler = value;
        }

        public static bool ConfirmLaunches
        {
            get => App.Settings.Prop.ConfirmLaunches;
            set => App.Settings.Prop.ConfirmLaunches = value;
        }

        public static bool CookieLoadingFinished => true;

        public bool CookieAccess
        {
            get => App.Settings.Prop.AllowCookieAccess;
            set
            {
                App.Settings.Prop.AllowCookieAccess = value;
                if (value)
                    Task.Run(App.Cookies.LoadCookies);

                OnPropertyChanged(nameof(CookieAccess));
            }
        }

        private bool _cookieLoadingFailed;
        public bool CookieLoadingFailed
        {
            get => _cookieLoadingFailed;
            set
            {
                _cookieLoadingFailed = value;
                OnPropertyChanged(nameof(CookieLoadingFailed));
            }
        }

        public static bool EnableBetterMatchmaking
        {
            get => App.Settings.Prop.EnableBetterMatchmaking;
            set => App.Settings.Prop.EnableBetterMatchmaking = value;
        }

        public static bool EnableBetterMatchmakingRandomization
        {
            get => App.Settings.Prop.EnableBetterMatchmakingRandomization;
            set => App.Settings.Prop.EnableBetterMatchmakingRandomization = value;
        }

        public static CleanerOptions SelectedCleanUpMode
        {
            get => App.Settings.Prop.CleanerOptions;
            set => App.Settings.Prop.CleanerOptions = value;
        }

        public IEnumerable<CleanerOptions> CleanerOptions { get; } = CleanerOptionsEx.Selections;

        public static CleanerOptions CleanerOption
        {
            get => App.Settings.Prop.CleanerOptions;
            set
            {
                App.Settings.Prop.CleanerOptions = value;
            }
        }

        private readonly List<string> CleanerItems = App.Settings.Prop.CleanerDirectories;

        public bool CleanerLogs
        {
            get => CleanerItems.Contains("RobloxLogs");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxLogs");
                else
                    CleanerItems.Remove("RobloxLogs");
            }
        }

        public bool CleanerCache
        {
            get => CleanerItems.Contains("RobloxCache");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxCache");
                else
                    CleanerItems.Remove("RobloxCache");
            }
        }

        public bool CleanerFroststrap
        {
            get => CleanerItems.Contains("FroststrapLogs");
            set
            {
                if (value)
                    CleanerItems.Add("FroststrapLogs");
                else
                    CleanerItems.Remove("FroststrapLogs");
            }
        }
    }
}