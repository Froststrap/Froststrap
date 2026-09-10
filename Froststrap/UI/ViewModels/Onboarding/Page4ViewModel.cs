// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.UI.ViewModels.Onboarding
{
    internal class Page4ViewModel : NotifyPropertyChangedViewModel
    {
        private List<string> _availableRegions = [];
        private bool _isLoadingRegions;

        public Page4ViewModel()
        {
            EnableBetterMatchmaking = App.Settings.Prop.EnableBetterMatchmaking;
            _ = LoadAvailableRegionsAsync();
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

        public static string SelectedRegion
        {
            get => App.Settings.Prop.SelectedRegion;
            set => App.Settings.Prop.SelectedRegion = value;
        }

        public List<string> AvailableRegions
        {
            get => _availableRegions;
            set => SetProperty(ref _availableRegions, value);
        }

        public bool IsLoadingRegions
        {
            get => _isLoadingRegions;
            set => SetProperty(ref _isLoadingRegions, value);
        }

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

        private static List<string> BuildAvailableRegionsWithCurrent(IEnumerable<string> baseRegions)
        {
            var list = new List<string>
            {
                "Auto"
            };

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

            if (!AvailableRegions.Any(r => string.Equals(r?.Trim(), current?.Trim(), StringComparison.OrdinalIgnoreCase)))
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

        private static string GetCachePath() => Path.Combine(Paths.Cache, "DataCentersCache.json");

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
    }
}