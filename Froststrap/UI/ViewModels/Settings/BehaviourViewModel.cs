using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels.Settings
{
    public class BehaviourViewModel : NotifyPropertyChangedViewModel
    {
        public BehaviourViewModel()
        {
            App.Cookies.StateChanged += (object? _, CookieState state) => CookieLoadingFailed = state != CookieState.Success && state != CookieState.Unknown;
        }

        public ObservableCollection<ProcessPriorityOption> ProcessPriorityOptions { get; } = new ObservableCollection<ProcessPriorityOption>(Enum.GetValues(typeof(ProcessPriorityOption)).Cast<ProcessPriorityOption>());

        public ProcessPriorityOption SelectedPriority
        {
            get => App.Settings.Prop.SelectedProcessPriority;
            set => App.Settings.Prop.SelectedProcessPriority = value;
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

        // Ill move to global settings in the future, too lazy to do it now
        public bool IsAppStorageVisible => App.StorageSettings.Loaded && (ShowLaunchAtStartup || ShowMinimizeToTray || ShowSystemTrayModal || ShowTheme);
        public bool ShowLaunchAtStartup => !string.IsNullOrEmpty(App.StorageSettings.Prop.LaunchAtStartup);
        public bool ShowMinimizeToTray => !string.IsNullOrEmpty(App.StorageSettings.Prop.MinimizeToTray);
        public bool ShowSystemTrayModal => !string.IsNullOrEmpty(App.StorageSettings.Prop.SystemTrayModalShown);
        public bool ShowTheme => !string.IsNullOrEmpty(App.StorageSettings.Prop.DeviceLevelTheme);

        public bool LaunchAtStartup
        {
            get => App.StorageSettings.Prop.LaunchAtStartup?.ToLower() == "true";
            set => App.StorageSettings.Prop.LaunchAtStartup = value.ToString().ToLower();
        }

        public bool SystemTrayModalShown
        {
            get => App.StorageSettings.Prop.SystemTrayModalShown?.ToLower() == "true";
            set => App.StorageSettings.Prop.SystemTrayModalShown = value.ToString().ToLower();
        }

        public bool MinimizeToTray
        {
            get => App.StorageSettings.Prop.MinimizeToTray?.ToLower() == "true";
            set => App.StorageSettings.Prop.MinimizeToTray = value.ToString().ToLower();
        }

        public IEnumerable<AppStorageSettingTheme> AppThemeOptions => Enum.GetValues(typeof(AppStorageSettingTheme)).Cast<AppStorageSettingTheme>();

        public AppStorageSettingTheme SelectedTheme
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

        public bool Error773Fix
        {
            get => App.Settings.Prop.Error773Fix;
            set => App.Settings.Prop.Error773Fix = value;
        }

        public bool BackgroundUpdates
        {
            get => App.Settings.Prop.BackgroundUpdatesEnabled;
            set => App.Settings.Prop.BackgroundUpdatesEnabled = value;
        }

        public bool CloseCrashHandler
        {
            get => App.Settings.Prop.AutoCloseCrashHandler;
            set => App.Settings.Prop.AutoCloseCrashHandler = value;
        }

        public bool ConfirmLaunches
        {
            get => App.Settings.Prop.ConfirmLaunches;
            set => App.Settings.Prop.ConfirmLaunches = value;
        }

        public bool CookieLoadingFinished => true;

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

        public bool EnableBetterMatchmaking
        {
            get => App.Settings.Prop.EnableBetterMatchmaking;
            set => App.Settings.Prop.EnableBetterMatchmaking = value;
        }

        public bool EnableBetterMatchmakingRandomization
        {
            get => App.Settings.Prop.EnableBetterMatchmakingRandomization;
            set => App.Settings.Prop.EnableBetterMatchmakingRandomization = value;
        }

        public CleanerOptions SelectedCleanUpMode
        {
            get => App.Settings.Prop.CleanerOptions;
            set => App.Settings.Prop.CleanerOptions = value;
        }

        public IEnumerable<CleanerOptions> CleanerOptions { get; } = CleanerOptionsEx.Selections;

        public CleanerOptions CleanerOption
        {
            get => App.Settings.Prop.CleanerOptions;
            set
            {
                App.Settings.Prop.CleanerOptions = value;
            }
        }

        private List<string> CleanerItems = App.Settings.Prop.CleanerDirectories;

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