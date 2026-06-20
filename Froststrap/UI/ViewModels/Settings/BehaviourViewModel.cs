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

        public static bool LaunchAtStartup
        {
            get => App.AppStorage.GetBoolPreset("System.LaunchAtStartup");
            set => App.AppStorage.SetBoolPreset("System.LaunchAtStartup", value);
        }

        public static bool MinimizeToTray
        {
            get => App.AppStorage.GetBoolPreset("System.MinimizeToTray");
            set => App.AppStorage.SetBoolPreset("System.MinimizeToTray", value);
        }

        public static bool SystemTrayModalShown
        {
            get => App.AppStorage.GetBoolPreset("System.SystemTrayModalShown");
            set => App.AppStorage.SetBoolPreset("System.SystemTrayModalShown", value);
        }


        public static IEnumerable<Enums.AppStoragePresets.Theme> AppThemeOptions => Enum.GetValues<Enums.AppStoragePresets.Theme>();

        public static Enums.AppStoragePresets.Theme SelectedTheme
        {
            get
            {
                string? json = App.AppStorage.GetPreset("UI.Theme");
                if (string.IsNullOrEmpty(json))
                    return Enums.AppStoragePresets.Theme.Dark;

                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    string? themeValue = dict?.Values.FirstOrDefault();
                    return themeValue == "light" ? Enums.AppStoragePresets.Theme.Light : Enums.AppStoragePresets.Theme.Dark;
                }
                catch
                {
                    return Enums.AppStoragePresets.Theme.Dark;
                }
            }
            set
            {
                string userId = App.AppStorage.GetValue("UserId") ?? "0";
                string themeValue = AppStorageManager.ThemeValues[value];
                string themeObject = $"{{\"{userId}\":\"{themeValue}\"}}";
                App.AppStorage.SetPreset("UI.Theme", themeObject);
            }
        }

        public static bool IsAppStorageVisible => App.AppStorage.Loaded;

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
                            string region = $"{dc.Location.City}, {dc.Location.Country}"
                                .TrimStart(',')
                                .Trim();
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
                    AvailableRegions = ["Auto"];
                }

                await SyncSelectedRegionAfterLoad();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("BehaviourViewModel::LoadAvailableRegions", ex);
                AvailableRegions = ["Auto"];
                await SyncSelectedRegionAfterLoad();
            }
            finally
            {
                IsLoadingRegions = false;
            }
        }

        private async Task SyncSelectedRegionAfterLoad()
        {
            await Task.Delay(50);

            string current = SelectedRegion;

            var match = AvailableRegions.FirstOrDefault(r => string.Equals(r?.Trim(), current?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                if (match != current)
                {
                    SelectedRegion = match;
                }
                else
                {
                    var original = SelectedRegion;
                    SelectedRegion = null!;
                    await Task.Delay(10);
                    SelectedRegion = original;
                }
            }
            else
            {
                SelectedRegion = "Auto";
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