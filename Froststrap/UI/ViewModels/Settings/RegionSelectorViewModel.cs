/*
*  Froststrap
*  Copyright (c) Froststrap Team
*
*  This file is part of Froststrap and is distributed under the terms of the
*  GNU Affero General Public License, version 3 or later.
*
*  SPDX-License-Identifier: AGPL-3.0-or-later
*/

using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels.Settings
{
    internal class SortOrderComboBoxItem
    {
        public string Content { get; set; } = "";
        public string Tag { get; set; } = "";
    }

    internal class RegionSelectorViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private const int MaxServers = 20;

        private readonly HashSet<string> _displayedServerIds = [];
        private RobloxServerFetcher? _fetcher;
        private Dictionary<int, string>? _dcMap;
        private CancellationTokenSource? _searchDebounceCts;
        private bool _disposed;

        #region Fields
        private bool _hasSearched;
        private string _placeId = "";
        private string _selectedRegion = "";
        private bool _isLoading;
        private bool _isGameSearchLoading;
        private string _loadingMessage = "";
        private string _nextCursor = "";
        private string? _roblosecurity;
        private bool _hasValidCookies;
        private string _searchQuery = "";
        private OmniSearchContent? _selectedSearchResult;
        private string _selectedSortOrder = "BestLatency";
        private SortOrderComboBoxItem? _selectedSortOrderItem;
        private int _lastFetchProcessedCount;
        private string? _thumbnailUrl;
        private string? _selectedRegionInput;
        private bool _isSearchFlyoutOpen;
        #endregion

        #region Properties
        public bool HasSearched
        {
            get => _hasSearched;
            set => SetProperty(ref _hasSearched, value);
        }

        public string PlaceId
        {
            get => _placeId;
            set
            {
                if (SetProperty(ref _placeId, value))
                    SearchCommand.NotifyCanExecuteChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(ServerListMessage));
                    OnPropertyChanged(nameof(IsServerListEmptyAndNotLoading));
                    OnPropertyChanged(nameof(ShowLoadingIndicator));
                    SearchCommand.NotifyCanExecuteChanged();
                    LoadMoreCommand.NotifyCanExecuteChanged();
                    SearchGamesCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsGameSearchLoading
        {
            get => _isGameSearchLoading;
            set
            {
                if (SetProperty(ref _isGameSearchLoading, value))
                {
                    OnPropertyChanged(nameof(ShowLoadingIndicator));
                    SearchGamesCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        public string NextCursor
        {
            get => _nextCursor;
            set
            {
                if (SetProperty(ref _nextCursor, value))
                    LoadMoreCommand.NotifyCanExecuteChanged();
            }
        }

        public string? Roblosecurity
        {
            get => _roblosecurity;
            set => SetProperty(ref _roblosecurity, value);
        }

        public bool HasValidCookies
        {
            get => _hasValidCookies;
            set
            {
                if (SetProperty(ref _hasValidCookies, value))
                {
                    OnPropertyChanged(nameof(ServerListMessage));
                    SearchCommand.NotifyCanExecuteChanged();
                    SearchGamesCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    OnSearchQueryChanged(value);
                    SearchGamesCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public OmniSearchContent? SelectedSearchResult
        {
            get => _selectedSearchResult;
            set
            {
                if (SetProperty(ref _selectedSearchResult, value))
                    OnSelectedSearchResultChanged(value);
            }
        }

        public string SelectedSortOrder
        {
            get => _selectedSortOrder;
            set
            {
                if (SetProperty(ref _selectedSortOrder, value))
                {
                    OnPropertyChanged(nameof(IsRegionSelectionEnabled));
                    SearchCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public SortOrderComboBoxItem? SelectedSortOrderItem
        {
            get => _selectedSortOrderItem;
            set
            {
                if (SetProperty(ref _selectedSortOrderItem, value))
                    OnSelectedSortOrderItemChanged(value);
            }
        }

        public int LastFetchProcessedCount
        {
            get => _lastFetchProcessedCount;
            set => SetProperty(ref _lastFetchProcessedCount, value);
        }

        public string? ThumbnailUrl
        {
            get => _thumbnailUrl;
            set => SetProperty(ref _thumbnailUrl, value);
        }

        public string? SelectedRegionInput
        {
            get => _selectedRegionInput;
            set => SetProperty(ref _selectedRegionInput, value);
        }

        public bool IsSearchFlyoutOpen
        {
            get => _isSearchFlyoutOpen;
            set => SetProperty(ref _isSearchFlyoutOpen, value);
        }

        public ObservableCollection<string> Regions { get; } = [];
        public ObservableCollection<ServerEntry> Servers { get; } = [];
        public ObservableCollection<OmniSearchContent> SearchResults { get; } = [];

        public List<SortOrderComboBoxItem> SortOrderOptions { get; } =
        [
            new() { Content = Strings.Common_Auto, Tag = "BestLatency" },
            new() { Content = Strings.Menu_RegionSelector_LargeServers, Tag = "OccupancyDesc" },
            new() { Content = Strings.Menu_RegionSelector_SmallServers, Tag = "OccupancyAsc" }
        ];

        public bool IsServerListEmpty => Servers.Count == 0;
        public bool IsServerListEmptyAndNotLoading => IsServerListEmpty && !IsLoading;
        public bool ShowLoadingIndicator => IsLoading && !IsGameSearchLoading;

        public string ServerListMessage => !HasValidCookies ? Strings.Menu_RegionSelector_LoginRequired :
            IsLoading ? "" :
            !HasSearched ? Strings.Menu_RegionSelector_EnterPlaceId :
            IsServerListEmpty ? (LastFetchProcessedCount == 0 ? Strings.Menu_RegionSelector_NoPublicServers : Strings.Menu_RegionSelector_NoServersForRegion) : "";

        public IAsyncRelayCommand SearchCommand { get; }
        public IAsyncRelayCommand LoadMoreCommand { get; }
        public IAsyncRelayCommand SearchGamesCommand { get; }

        public bool IsRegionSelectionEnabled => SelectedSortOrder != "BestLatency";

        private bool IsAutoSortOrder => SelectedSortOrder == "BestLatency";
        #endregion

        public RegionSelectorViewModel()
        {
            Servers.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(IsServerListEmpty));
                OnPropertyChanged(nameof(IsServerListEmptyAndNotLoading));
                LoadMoreCommand!.NotifyCanExecuteChanged(); // Update LoadMore button state when server count changes
            };

            SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsLoading && !string.IsNullOrWhiteSpace(PlaceId) && HasValidCookies);
            SearchGamesCommand = new AsyncRelayCommand(SearchGamesAsync, () => !IsLoading && !IsGameSearchLoading && !string.IsNullOrWhiteSpace(SearchQuery) && HasValidCookies);
            LoadMoreCommand = new AsyncRelayCommand(LoadMoreServersAsync, () => !IsLoading && !string.IsNullOrWhiteSpace(NextCursor) && Servers.Count < MaxServers);

            _ = InitializeCookiesAsync();
            SelectedSortOrderItem = SortOrderOptions.FirstOrDefault(x => x.Tag == "BestLatency");
        }

        private void OnSearchQueryChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                IsSearchFlyoutOpen = false;
                SearchResults.Clear();
            }

            if (long.TryParse(value, out _))
            {
                PlaceId = value;
            }

            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();
            _ = DebouncedSearchTriggerAsync(_searchDebounceCts.Token);
        }

        private void OnSelectedSearchResultChanged(OmniSearchContent? value)
        {
            if (value == null) return;

            PlaceId = value.RootPlaceId.ToString(CultureInfo.InvariantCulture);
            SearchQuery = value.RootPlaceId.ToString(CultureInfo.InvariantCulture);
            IsSearchFlyoutOpen = false;
        }

        private void OnSelectedSortOrderItemChanged(SortOrderComboBoxItem? value)
        {
            if (value != null)
            {
                SelectedSortOrder = value.Tag;
            }
        }

        public string? SelectedRegion
        {
            get => _selectedRegion;
            set
            {
                _selectedRegion = value ?? "";
                OnPropertyChanged();
                SearchCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task DebouncedSearchTriggerAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(600, token);
                if (!token.IsCancellationRequested && !IsLoading && !string.IsNullOrWhiteSpace(SearchQuery))
                {
                    await SearchGamesAsync(token);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task InitializeCookiesAsync()
        {
            try
            {
                _fetcher = new RobloxServerFetcher();
                Roblosecurity = await _fetcher.ResolveCookieAsync();

                HasValidCookies = !string.IsNullOrWhiteSpace(Roblosecurity);

                if (HasValidCookies)
                    await LoadRegionsAsync();
            }
            catch (Exception ex) { App.Logger.Error(ex); }
        }

        private async Task LoadRegionsAsync()
        {
            var cacheResult = await LoadDatacentersFromCacheAsync();
            if (cacheResult != null)
            {
                var (regions, dcMap) = cacheResult.Value;
                PopulateRegions(regions, dcMap);
                return;
            }

            IsLoading = true;
            LoadingMessage = Strings.Menu_RegionSelector_LoadingDatacenters;

            var apiResult = await _fetcher!.GetDatacentersAsync();
            if (apiResult != null)
            {
                var (regions, dcMap) = apiResult.Value;
                PopulateRegions(regions, dcMap);
                await SaveDatacentersToCacheAsync(dcMap);
                LoadingMessage = string.Format(CultureInfo.InvariantCulture, Strings.Menu_RegionSelector_LoadedRegions, Regions.Count);
                IsLoading = false;
                await Task.Delay(800);
                LoadingMessage = "";
                return;
            }

            var staleCache = await LoadDatacentersFromCacheAsync(allowExpired: true);
            if (staleCache != null)
            {
                var (regions, dcMap) = staleCache.Value;
                PopulateRegions(regions, dcMap);
                LoadingMessage = Strings.Menu_RegionSelector_UsingCachedData;
                IsLoading = false;
                await Task.Delay(1500);
                LoadingMessage = "";
                return;
            }

            LoadingMessage = Strings.Menu_RegionSelector_FailedToLoadDatacenters;
            IsLoading = false;
        }

        private void PopulateRegions(List<string> regions, Dictionary<int, string> dcMap)
        {
            var sorted = regions
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Regions.Clear();
            foreach (var r in sorted)
                Regions.Add(r);

            _dcMap = dcMap;

            Dispatcher.UIThread.Post(() =>
            {
                var preferred = Regions.FirstOrDefault(r => r.Equals(_selectedRegion, StringComparison.OrdinalIgnoreCase));
                SelectedRegion = preferred ?? Regions.FirstOrDefault();
            }, DispatcherPriority.Background);
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

        private async Task SearchAsync()
        {
            if (!IsAutoSortOrder && string.IsNullOrWhiteSpace(SelectedRegion))
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_RegionSelector_PleaseSelectRegion, MessageBoxImage.Warning);
                return;
            }

            HasSearched = true;
            IsLoading = true;
            LoadingMessage = Strings.Menu_RegionSelector_SearchingServers;
            Servers.Clear();
            _displayedServerIds.Clear();
            NextCursor = "";
            LastFetchProcessedCount = 0;

            int pagesChecked = 0;
            while (pagesChecked < 3)
            {
                await LoadServersAsync(pagesChecked == 0);
                pagesChecked++;
                if (string.IsNullOrWhiteSpace(NextCursor) || Servers.Count >= MaxServers)
                    break;
            }

            IsLoading = false;
            await Task.Delay(800);
            LoadingMessage = "";
        }

        private async Task LoadServersAsync(bool resetCursor = false)
        {
            if (string.IsNullOrWhiteSpace(PlaceId) || string.IsNullOrWhiteSpace(Roblosecurity)) return;
            if (!IsAutoSortOrder && string.IsNullOrWhiteSpace(SelectedRegion)) return;

            if (resetCursor) NextCursor = "";
            if (!long.TryParse(PlaceId, out var placeIdLong)) return;

            var result = await _fetcher!.FetchServerInstancesAsync(placeIdLong, NextCursor, SelectedSortOrder, Roblosecurity);
            if (result == null) return;

            int number = Servers.Count + 1;
            bool shouldFilterByRegion = !IsAutoSortOrder;

            foreach (var s in result.Servers)
            {
                // Stop adding if we've reached the max
                if (Servers.Count >= MaxServers)
                    break;

                if (_displayedServerIds.Add(s.Id) && s.DataCenterId.HasValue)
                {
                    bool regionMatches = true;
                    if (shouldFilterByRegion)
                    {
                        regionMatches = _dcMap!.TryGetValue(s.DataCenterId.Value, out var mappedRegion) && mappedRegion == SelectedRegion;
                    }

                    if (regionMatches)
                    {
                        var serverEntry = new ServerEntry
                        {
                            Number = number++,
                            ServerId = s.Id,
                            Players = $"{s.Playing}/{s.MaxPlayers}",
                            PlayingCount = s.Playing,
                            Region = s.Region,
                            DataCenterId = s.DataCenterId,
                            Uptime = s.UptimeDisplay,
                            PlayerTokens = s.PlayerTokens,
                            JoinCommand = new RelayCommand(() => JoinServer(s.Id))
                        };

                        Servers.Add(serverEntry);

                        _ = serverEntry.LoadThumbnailsAsync();
                    }
                }
            }

            LastFetchProcessedCount = result.Servers.Count;
            NextCursor = result.NextCursor;
        }

        private void JoinServer(string serverId)
        {
            if (!long.TryParse(PlaceId, out var placeId)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"roblox://experiences/start?placeId={placeId}&gameInstanceId={serverId}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { App.Logger.Error(ex); }
        }

        private async Task LoadMoreServersAsync()
        {
            if (Servers.Count >= MaxServers)
                return;

            IsLoading = true;

            for (int i = 0; i < 5 && !string.IsNullOrWhiteSpace(NextCursor) && Servers.Count < MaxServers; i++)
            {
                await LoadServersAsync();
            }

            IsLoading = false;
        }

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

                var sortedRegionDict = new Dictionary<string, List<int>>();
                foreach (var region in regionDict.Keys.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
                {
                    sortedRegionDict[region] = regionDict[region];
                }

                var cache = new DatacentersCache
                {
                    Regions = sortedRegionDict,
                    LastUpdated = DateTime.UtcNow
                };

                Directory.CreateDirectory(Paths.Cache);
                var json = JsonSerializer.Serialize(cache);
                await File.WriteAllTextAsync(GetCachePath(), json);
            }
            catch { /* ignore cache save errors */ }
        }

        private static async Task<(List<string> regions, Dictionary<int, string> datacenterMap)?> LoadDatacentersFromCacheAsync()
        {
            try
            {
                if (!File.Exists(GetCachePath())) return null;

                var json = await File.ReadAllTextAsync(GetCachePath());
                var cache = JsonSerializer.Deserialize<DatacentersCache>(json);

                if (cache == null || cache.LastUpdated < DateTime.UtcNow.AddDays(-7))
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

        private async Task SearchGamesAsync(CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(SearchQuery) || long.TryParse(SearchQuery, out _)) return;

            IsGameSearchLoading = true;
            try
            {
                var results = await GameSearching.GetGameSearchResultsAsync(SearchQuery);
                if (token.IsCancellationRequested || results == null || results.Count == 0) return;

                var thumbRequests = results.Select(r => new ThumbnailRequest
                {
                    Type = ThumbnailType.GameIcon,
                    TargetId = r.UniverseId,
                    Size = "128x128"
                }).ToList();

                var fetchedUrls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, token);
                if (token.IsCancellationRequested) return;

                for (int i = 0; i < results.Count; i++)
                {
                    if (fetchedUrls != null && i < fetchedUrls.Length && !string.IsNullOrEmpty(fetchedUrls[i]))
                    {
                        try
                        {
                            var response = await App.HttpClient.GetByteArrayAsync(new Uri(fetchedUrls[i]!), token);
                            using var ms = new MemoryStream(response);
                            results[i].ThumbnailBitmap = new Bitmap(ms);
                        }
                        catch { /* Handle failed image load silently */ }
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    SearchResults.Clear();
                    foreach (var res in results) SearchResults.Add(res);
                    IsSearchFlyoutOpen = SearchResults.Count > 0 && !string.IsNullOrWhiteSpace(SearchQuery);
                }, DispatcherPriority.Background);
            }
            catch (Exception ex) { App.Logger.Error($"Search error: {ex.Message}"); }
            finally { IsGameSearchLoading = false; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _fetcher?.Dispose();
                _fetcher = null;

                _searchDebounceCts?.Cancel();
                _searchDebounceCts?.Dispose();
                _searchDebounceCts = null;
            }

            _disposed = true;
        }
    }
}