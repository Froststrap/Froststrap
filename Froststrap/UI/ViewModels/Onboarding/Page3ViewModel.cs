namespace Froststrap.UI.ViewModels.Onboarding
{
    public class Page3ViewModel : NotifyPropertyChangedViewModel
    {
        private List<string> _availableRegions = [];
        private bool _isLoadingRegions = false;

        public Page3ViewModel()
        {
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

                var datacenters = await Http.GetJson<List<DatacenterEntry>>(
                    new Uri("https://apis.rovalra.com/v1/datacenters/list"));

                List<string> baseRegions = new List<string>();

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

                    baseRegions = regions.OrderBy(r => r).ToList();
                }

                AvailableRegions = BuildAvailableRegionsWithCurrent(baseRegions);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("BehaviourViewModel::LoadAvailableRegions", ex);
                AvailableRegions = BuildAvailableRegionsWithCurrent(new List<string>());
            }
            finally
            {
                IsLoadingRegions = false;
            }

            await SyncSelectedRegionAfterLoad();
        }

        private List<string> BuildAvailableRegionsWithCurrent(IEnumerable<string> baseRegions)
        {
            string current = SelectedRegion;
            var list = new List<string> { "Auto" };

            foreach (var region in baseRegions)
            {
                if (!string.Equals(region, "Auto", StringComparison.OrdinalIgnoreCase))
                    list.Add(region);
            }

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

            var match = AvailableRegions.FirstOrDefault(r =>
                string.Equals(r?.Trim(), current?.Trim(), StringComparison.OrdinalIgnoreCase));

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
    }
}