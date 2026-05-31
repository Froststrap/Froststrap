using System.Runtime.InteropServices;
using static Froststrap.AppStorageManager;

namespace Froststrap.UI.ViewModels.Settings
{
    public class BehaviourViewModel : NotifyPropertyChangedViewModel
    {
        private List<string> _availableRegions = [];
        private bool _isLoadingRegions = false;

        public BehaviourViewModel()
        {
            App.Cookies.StateChanged += (_, state) =>
                CookieLoadingFailed = state is not (CookieState.Success or CookieState.Unknown);

            Task.Run(LoadAvailableRegionsAsync);
        }

        public static IEnumerable<ProcessPriorityOption> ProcessPriorityOptions => Enum.GetValues<ProcessPriorityOption>();
        public static ProcessPriorityOption SelectedPriority
        {
            get => App.Settings.Prop.SelectedProcessPriority;
            set => App.Settings.Prop.SelectedProcessPriority = value;
        }

        // Ill move to global settings in the future, too lazy to do it now
        public static bool IsAppStorageVisible => App.StorageSettings.Loaded && (ShowLaunchAtStartup || ShowMinimizeToTray || ShowSystemTrayModal || ShowTheme);
        public static bool ShowLaunchAtStartup => !string.IsNullOrEmpty(App.StorageSettings.Prop.LaunchAtStartup);
        public static bool ShowMinimizeToTray => !string.IsNullOrEmpty(App.StorageSettings.Prop.MinimizeToTray);
        public static bool ShowSystemTrayModal => !string.IsNullOrEmpty(App.StorageSettings.Prop.SystemTrayModalShown);
        public static bool ShowTheme => !string.IsNullOrEmpty(App.StorageSettings.Prop.DeviceLevelTheme);

        public static bool LaunchAtStartup
        {
            get => AppStorageManager.GetBoolValue("LaunchAtStartup");
            set => AppStorageManager.SetBoolValue("LaunchAtStartup", value);
        }

        public static bool MinimizeToTray
        {
            get => AppStorageManager.GetBoolValue("MinimizeToTray");
            set => AppStorageManager.SetBoolValue("MinimizeToTray", value);
        }

        public static bool SystemTrayModalShown
        {
            get => AppStorageManager.GetBoolValue("SystemTrayModalShown");
            set => AppStorageManager.SetBoolValue("SystemTrayModalShown", value);
        }

        public static IEnumerable<AppStorageManager.AppStorageSettingTheme> AppThemeOptions => Enum.GetValues<AppStorageManager.AppStorageSettingTheme>();

        public static AppStorageManager.AppStorageSettingTheme SelectedTheme
        {
            get => AppStorageManager.GetTheme();
            set => AppStorageManager.SetTheme(value);
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

        public bool EnableBetterMatchmaking
        {
            get => App.Settings.Prop.EnableBetterMatchmaking;
            set
            {
                App.Settings.Prop.EnableBetterMatchmaking = value;
                OnPropertyChanged(nameof(EnableBetterMatchmaking));
            }
        }

        public static bool JoinSmallerServer
        {
            get => App.Settings.Prop.JoinSmallerServer;
            set => App.Settings.Prop.JoinSmallerServer = value;
        }

        public static int MaxServerCheck
        {
            get => App.Settings.Prop.MaxServerCheck;
            set => App.Settings.Prop.MaxServerCheck = value;
        }

        public static int BestRegionAmounts
        {
            get => App.Settings.Prop.BestRegionAmounts;
            set => App.Settings.Prop.BestRegionAmounts = value;
        }

        public string SelectedRegion
        {
            get => App.Settings.Prop.SelectedRegion;
            set
            {
                App.Settings.Prop.SelectedRegion = value;
                OnPropertyChanged(nameof(SelectedRegion));
            }
        }

        public List<string> AvailableRegions
        {
            get => _availableRegions;
            set
            {
                _availableRegions = value;
                OnPropertyChanged(nameof(AvailableRegions));
            }
        }

        public bool IsLoadingRegions
        {
            get => _isLoadingRegions;
            set
            {
                _isLoadingRegions = value;
                OnPropertyChanged(nameof(IsLoadingRegions));
            }
        }

        private async Task LoadAvailableRegionsAsync()
        {
            try
            {
                IsLoadingRegions = true;

                var datacenters = await Http.GetJson<List<DatacenterEntry>>(new Uri("https://apis.rovalra.com/v1/datacenters/list"));

                if (datacenters != null && datacenters.Count > 0)
                {
                    var regions = new HashSet<string>();

                    foreach (var dc in datacenters)
                    {
                        if (dc.Location != null && !string.IsNullOrEmpty(dc.Location.City))
                        {
                            string region = $"{dc.Location.City}, {dc.Location.Country}".TrimStart(',').Trim();
                            regions.Add(region);
                        }
                        else if (dc.Location != null && !string.IsNullOrEmpty(dc.Location.Country))
                        {
                            regions.Add(dc.Location.Country);
                        }
                    }

                    var sortedRegions = regions.OrderBy(r => r).ToList();
                    sortedRegions.Insert(0, "Auto");
                    AvailableRegions = sortedRegions;
                }
                else
                {
                    AvailableRegions = new List<string> { "Auto" };
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("BehaviourViewModel::LoadAvailableRegions", ex);
                AvailableRegions = new List<string> { "Auto" };
            }
            finally
            {
                IsLoadingRegions = false;
                OnPropertyChanged(nameof(SelectedRegion));
            }
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