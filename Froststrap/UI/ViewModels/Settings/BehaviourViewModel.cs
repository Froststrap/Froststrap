namespace Froststrap.UI.ViewModels.Settings
{
    internal class BehaviourViewModel : NotifyPropertyChangedViewModel
    {
        private List<string> _availableRegions = [];
        private bool _isLoadingRegions;

        private string _selectedSortOrder;
        private SortOrderComboBoxItem _selectedSortOrderItem;

        private static string GetCachePath() => Path.Combine(Paths.Cache, "DataCentersCache.json");

        private static async Task SaveDatacentersToCacheAsync(Dictionary<int, string> datacenterMap)
        {
            try
            {
                var regionDict = new Dictionary<string, List<int>>();
                foreach (var kvp in datacenterMap)
                {
                    if (!regionDict.TryGetValue(kvp.Value, out var list))
                    {
                        list = [];
                        regionDict[kvp.Value] = list;
                    }
                    list.Add(kvp.Key);
                }

                var sortedDict = regionDict
                    .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var cache = new DatacentersCache
                {
                    Regions = sortedDict,
                    LastUpdated = DateTime.UtcNow
                };

                Directory.CreateDirectory(Paths.Cache);
                var json = JsonSerializer.Serialize(cache);
                await File.WriteAllTextAsync(GetCachePath(), json);
            }
            catch { /* ignore */ }
        }

        private static async Task<(List<string> regions, Dictionary<int, string> datacenterMap)?> LoadDatacentersFromCacheAsync(bool allowExpired = false)
        {
            try
            {
                if (!File.Exists(GetCachePath())) return null;

                var json = await File.ReadAllTextAsync(GetCachePath());
                var cache = JsonSerializer.Deserialize<DatacentersCache>(json);

                if (cache == null) return null;

                if (!allowExpired && cache.LastUpdated < DateTime.UtcNow.AddDays(-7))
                    return null;

                var map = new Dictionary<int, string>();
                var regions = new List<string>();

                foreach (var kvp in cache.Regions)
                {
                    regions.Add(kvp.Key);
                    foreach (var id in kvp.Value)
                        map[id] = kvp.Key;
                }

                return (regions, map);
            }
            catch
            {
                return null;
            }
        }

        public BehaviourViewModel()
        {
            App.Cookies.StateChanged += (_, state) =>
                CookieLoadingFailed = state is not (CookieState.Success or CookieState.Unknown);

            _selectedSortOrder = App.Settings.Prop.SelectedServerSortOrder ?? "BestLatency";
            _selectedSortOrderItem = SortOrderOptions.FirstOrDefault(x => x.Tag == _selectedSortOrder)
                                     ?? SortOrderOptions.First();

            _ = LoadAvailableRegionsAsync();
        }

        public static IEnumerable<SoftKeyProfile> SoftKeyProfiles => Enum.GetValues<SoftKeyProfile>();

        public bool SoftKeyEnabled
        {
            get => App.Settings.Prop.SoftKeyEnabled;
            set
            {
                App.Settings.Prop.SoftKeyEnabled = value;
                OnPropertyChanged(nameof(SoftKeyEnabled));
            }
        }

        public SoftKeyProfile SoftKeyProfile
        {
            get => App.Settings.Prop.SoftKeyProfile;
            set
            {
                App.Settings.Prop.SoftKeyProfile = value;
                OnPropertyChanged(nameof(SoftKeyProfile));
            }
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

        public List<SortOrderComboBoxItem> SortOrderOptions { get; } =
        [
            new() { Content = Strings.Common_Auto, Tag = "BestLatency" },
            new() { Content = Strings.Menu_RegionSelector_LargeServers, Tag = "OccupancyDesc" },
            new() { Content = Strings.Menu_RegionSelector_SmallServers, Tag = "OccupancyAsc" }
        ];

        public string SelectedSortOrder
        {
            get => _selectedSortOrder;
            set
            {
                if (_selectedSortOrder != value)
                {
                    _selectedSortOrder = value;
                    App.Settings.Prop.SelectedServerSortOrder = value;
                    OnPropertyChanged(nameof(SelectedSortOrder));
                    OnPropertyChanged(nameof(IsRegionSelectionEnabled));
                }
            }
        }

        public SortOrderComboBoxItem SelectedSortOrderItem
        {
            get => _selectedSortOrderItem;
            set
            {
                if (_selectedSortOrderItem != value)
                {
                    _selectedSortOrderItem = value;
                    OnPropertyChanged(nameof(SelectedSortOrderItem));
                    if (value != null)
                        SelectedSortOrder = value.Tag;
                }
            }
        }

        public bool IsRegionSelectionEnabled => SelectedSortOrder != "BestLatency";

        private async Task LoadAvailableRegionsAsync()
        {
            List<string> baseRegions;

            var cacheResult = await LoadDatacentersFromCacheAsync();
            if (cacheResult != null)
            {
                baseRegions = cacheResult.Value.regions;
                AvailableRegions = BuildAvailableRegionsWithCurrent(baseRegions);
                await SyncSelectedRegionAfterLoad();
                return;
            }

            IsLoadingRegions = true;

            try
            {
                var datacenters = await Http.GetJson<List<DatacenterEntry>>(
                    new Uri("https://apis.rovalra.com/v1/datacenters/list"));

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

                    baseRegions = [.. regions.OrderBy(r => r, StringComparer.OrdinalIgnoreCase)];

                    var map = new Dictionary<int, string>();
                    foreach (var dc in datacenters)
                    {
                        string regionKey = string.IsNullOrWhiteSpace(dc.Location?.City) && string.IsNullOrWhiteSpace(dc.Location?.Country)
                            ? "Unknown"
                            : $"{dc.Location.City}, {dc.Location.Country}".Trim().Trim(',', ' ');
                        foreach (var id in dc.DataCenterIds)
                            map[id] = regionKey;
                    }
                    await SaveDatacentersToCacheAsync(map);
                }
                else
                {
                    baseRegions = [];
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception: ", ex);

                var stale = await LoadDatacentersFromCacheAsync(allowExpired: true);
                if (stale != null)
                {
                    baseRegions = stale.Value.regions;
                }
                else
                {
                    baseRegions = [];
                }
            }
            finally
            {
                IsLoadingRegions = false;
            }

            AvailableRegions = BuildAvailableRegionsWithCurrent(baseRegions);
            await SyncSelectedRegionAfterLoad();
        }

        private List<string> BuildAvailableRegionsWithCurrent(IEnumerable<string> baseRegions)
        {
            var list = new List<string>();

            foreach (var region in baseRegions)
            {
                if (!string.Equals(region, "Auto", StringComparison.OrdinalIgnoreCase))
                    list.Add(region);
            }

            string current = SelectedRegion;
            if (!string.IsNullOrEmpty(current) &&
                !string.Equals(current, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                bool exists = list.Any(r => string.Equals(r?.Trim(), current?.Trim(),
                    StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    list.Add(current);
                }
            }

            return list;
        }

        private async Task SyncSelectedRegionAfterLoad()
        {
            await Task.Delay(50);

            string current = SelectedRegion;

            if (string.Equals(current, "Auto", StringComparison.OrdinalIgnoreCase) ||
                !AvailableRegions.Any(r => string.Equals(r?.Trim(), current?.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                SelectedRegion = AvailableRegions.FirstOrDefault() ?? string.Empty;
            }
            else
            {
                var match = AvailableRegions.FirstOrDefault(r =>
                    string.Equals(r?.Trim(), current?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null && match != current)
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