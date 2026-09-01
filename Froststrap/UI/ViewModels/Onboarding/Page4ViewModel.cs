using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.ViewModels.Onboarding
{
    internal class Page4ViewModel : NotifyPropertyChangedViewModel
    {
        private List<string> _availableRegions = [];
        private bool _isLoadingRegions;
        private string _selectedSortOrder;
        private SortOrderComboBoxItem _selectedSortOrderItem;

        public Page4ViewModel()
        {
            _selectedSortOrder = App.Settings.Prop.SelectedServerSortOrder ?? "BestLatency";
            _selectedSortOrderItem = SortOrderOptions.FirstOrDefault(x => x.Tag == _selectedSortOrder)
                                     ?? SortOrderOptions.First();
            Task.Run(LoadAvailableRegionsAsync);
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
            try
            {
                IsLoadingRegions = true;

                var datacenters = await Http.GetJson<List<DatacenterEntry>>(
                    new Uri("https://apis.rovalra.com/v1/datacenters/list"));

                List<string> baseRegions = [];

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

                    baseRegions = regions.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
                }

                AvailableRegions = BuildAvailableRegionsWithCurrent(baseRegions);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                AvailableRegions = BuildAvailableRegionsWithCurrent([]);
            }
            finally
            {
                IsLoadingRegions = false;
            }

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
    }
}