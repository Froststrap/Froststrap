/*
 *  Froststrap
 *  Copyright (c) Froststrap Team
 *
 *  This file is part of Froststrap and is distributed under the terms of the
 *  GNU Affero General Public License, version 3 or later.
 *
 *  SPDX-License-Identifier: AGPL-3.0-or-later
 *
 *  Description: Nix flake for shipping for Nix-darwin, Nix, NixOS, and modules
 *               of the Nix ecosystem. 
 */

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Bloxstrap.Integrations;
using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class RegionSelectorViewModel : INotifyPropertyChanged
    {
        private const string LOG_IDENT = "RegionSelectorViewModel";

        private bool _hasSearched = false;
        private string _placeId = "";
        private string? _selectedRegion;
        private bool _isLoading;
        private bool _isGameSearchLoading;
        private string _loadingMessage = "";
        private ObservableCollection<string> _regions = new();
        private ObservableCollection<ServerEntry> _servers = new();
        private string _nextCursor = "";

        private string? _roblosecurity;
        private bool _hasValidCookies = false;

        private string _searchQuery = "";
        private ObservableCollection<GameSearchResult> _searchResults = new();
        private GameSearchResult? _selectedSearchResult;

        private ObservableCollection<object> _searchItems = new();

        private CancellationTokenSource? _searchDebounceCts;

        public EventHandler? RequestCloseWindowEvent;

        private int _lastFetchProcessedCount = 0;

        private readonly HashSet<string> _displayedServerIds = new();

        private int _selectedSortOrder = 2;
        public int SelectedSortOrder
        {
            get => _selectedSortOrder;
            set
            {
                if (_selectedSortOrder != value)
                {
                    _selectedSortOrder = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<SortOrderComboBoxItem> SortOrderOptions { get; } = new List<SortOrderComboBoxItem>
        {
            new SortOrderComboBoxItem { Content = "Large Servers", Tag = 2 },
            new SortOrderComboBoxItem { Content = "Small Servers", Tag = 1 }
        };

        public bool IsServerListEmpty => Servers.Count == 0;

        public bool IsServerListEmptyAndNotLoading => IsServerListEmpty && !IsLoading;

        // expose search state so view can decide Enter behavior
        public bool HasSearched => _hasSearched;

        public string PlaceId
        {
            get => _placeId;
            set
            {
                if (_placeId != value)
                {
                    _placeId = value;
                    OnPropertyChanged();
                    (SearchCommand as RelayCommand)?.NotifyCanExecuteChanged();

                    if (string.IsNullOrWhiteSpace(SelectedRegion) && Regions.Count > 0)
                    {
                        SelectedRegion = Regions[0];
                    }
                }
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    OnPropertyChanged();
                    (SearchGamesCommand as RelayCommand)?.NotifyCanExecuteChanged();

                    if (long.TryParse(value, out _))
                    {
                        PlaceId = value;
                    }

                    _searchDebounceCts?.Cancel();
                    _searchDebounceCts = new CancellationTokenSource();
                    var token = _searchDebounceCts.Token;
                    _ = DebouncedSearchTriggerAsync(token);
                }
            }
        }

        private async Task DebouncedSearchTriggerAsync(CancellationToken token)
        {
            const string LOG_IDENT_DEBOUNCE = $"{LOG_IDENT}::DebouncedSearchTrigger";

            try
            {
                await Task.Delay(600, token);
                if (token.IsCancellationRequested)
                    return;

                if (IsLoading)
                    return;

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    App.Logger.WriteLine(LOG_IDENT_DEBOUNCE, "DebouncedSearchTrigger invoking SearchGamesAsync");
                    await SearchGamesAsync();
                }
            }
            catch (TaskCanceledException) { /* expected on rapid typing */ }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_DEBOUNCE, ex);
            }
        }

        public ObservableCollection<GameSearchResult> SearchResults
        {
            get => _searchResults;
            set
            {
                if (_searchResults != value)
                {
                    _searchResults = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<object> SearchItems
        {
            get => _searchItems;
            set
            {
                if (_searchItems != value)
                {
                    _searchItems = value;
                    OnPropertyChanged();
                }
            }
        }

        public GameSearchResult? SelectedSearchResult
        {
            get => _selectedSearchResult;
            set
            {
                if (_selectedSearchResult != value)
                {
                    _selectedSearchResult = value;
                    OnPropertyChanged();

                    if (_selectedSearchResult != null)
                    {
                        PlaceId = _selectedSearchResult.RootPlaceId.ToString();

                        _searchDebounceCts?.Cancel();
                        _searchQuery = _selectedSearchResult.RootPlaceId.ToString();
                        OnPropertyChanged(nameof(SearchQuery));

                        App.Logger.WriteLine(LOG_IDENT, $"SelectedSearchResult changed to: {_selectedSearchResult.Name} ({PlaceId})");
                    }
                }
            }
        }

        public ObservableCollection<string> Regions
        {
            get => _regions;
            set
            {
                if (_regions != value)
                {
                    _regions = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? SelectedRegion
        {
            get => _selectedRegion;
            set
            {
                if (_selectedRegion != value)
                {
                    _selectedRegion = value;
                    OnPropertyChanged();
                    (SearchCommand as RelayCommand)?.NotifyCanExecuteChanged();

                    try
                    {
                        SaveSelectedRegionToSettings();
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }
            }
        }

        public ObservableCollection<ServerEntry> Servers
        {
            get => _servers;
            set
            {
                if (_servers != value)
                {
                    _servers = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ServerListMessage));
                    OnPropertyChanged(nameof(IsServerListEmpty));
                    OnPropertyChanged(nameof(IsServerListEmptyAndNotLoading));
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ServerListMessage));
                    OnPropertyChanged(nameof(IsServerListEmptyAndNotLoading));
                    OnPropertyChanged(nameof(ShowLoadingIndicator));
                    App.Logger.WriteLine(LOG_IDENT, $"IsLoading changed to: {_isLoading}");
                    (SearchCommand as RelayCommand)?.NotifyCanExecuteChanged();
                    (LoadMoreCommand as RelayCommand)?.NotifyCanExecuteChanged();
                    (SearchGamesCommand as RelayCommand)?.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsGameSearchLoading
        {
            get => _isGameSearchLoading;
            set
            {
                if (_isGameSearchLoading != value)
                {
                    _isGameSearchLoading = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowLoadingIndicator));
                    App.Logger.WriteLine(LOG_IDENT, $"IsGameSearchLoading changed to: {_isGameSearchLoading}");
                    (SearchGamesCommand as RelayCommand)?.NotifyCanExecuteChanged();
                }
            }
        }
        public bool ShowLoadingIndicator => IsLoading && !IsGameSearchLoading;

        public string LoadingMessage
        {
            get => _loadingMessage;
            set
            {
                if (_loadingMessage != value)
                {
                    _loadingMessage = value;
                    OnPropertyChanged();
                    App.Logger.WriteLine(LOG_IDENT, $"LoadingMessage: {_loadingMessage}");
                }
            }
        }

        public string ServerListMessage
        {
            get
            {
                if (!_hasValidCookies)
                    return "Please enable 'Allow Froststrap to access your Roblox account' in Bootstrapper page";

                if (IsLoading)
                    return "";
                if (!_hasSearched)
                    return "Enter a Place ID and click Search to view servers.";
                if (IsServerListEmpty)
                {
                    if (!string.IsNullOrWhiteSpace(PlaceId) && Servers.Count == 0)
                    {
                        if (_lastFetchProcessedCount == 0)
                            return "No public servers found for the Place ID.";
                        return "No servers found for specified region.";
                    }
                    return "No servers in the specified region were found.";
                }
                return "";
            }
        }

        public ICommand SearchCommand { get; }
        public ICommand LoadMoreCommand { get; }
        public ICommand SearchGamesCommand { get; }
        public ICommand RefreshCookiesCommand { get; }

        private RobloxServerFetcher? _fetcher;
        private Dictionary<int, string>? _dcMap;

        public RegionSelectorViewModel()
        {
            App.Logger.WriteLine(LOG_IDENT, "Constructor called");

            Servers.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsServerListEmpty));
                OnPropertyChanged(nameof(IsServerListEmptyAndNotLoading));
            };

            SearchItems = new ObservableCollection<object>();
            SearchResults = new ObservableCollection<GameSearchResult>();

            SearchCommand = new RelayCommand(async () => await SearchAsync(),
                () => !IsLoading && !string.IsNullOrWhiteSpace(PlaceId) && _hasValidCookies);

            SearchGamesCommand = new RelayCommand(async () => await SearchGamesAsync(),
                () => !IsLoading && !IsGameSearchLoading && !string.IsNullOrWhiteSpace(SearchQuery) && _hasValidCookies);

            LoadMoreCommand = new RelayCommand(async () => await LoadMoreServersAsync(),
                () => !IsLoading && !string.IsNullOrWhiteSpace(_nextCursor));

            RefreshCookiesCommand = new RelayCommand(async () =>
            {
                var refreshed = await RefreshCookiesAsync();
                if (refreshed)
                {
                    Frontend.ShowMessageBox("Authentication refreshed successfully!", MessageBoxImage.Information);
                }
                else
                {
                    Frontend.ShowMessageBox("Failed to refresh authentication. Please check your settings.", MessageBoxImage.Warning);
                }
            });

            (SearchCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (SearchGamesCommand as RelayCommand)?.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(ServerListMessage));

            _ = InitializeCookiesAsync();
        }

        public async Task<bool> RefreshCookiesAsync()
        {
            try
            {
                await App.Cookies.LoadCookies();

                if (App.Cookies.Loaded)
                {
                    _roblosecurity = App.Cookies.GetAuthCookie();
                    _hasValidCookies = !string.IsNullOrWhiteSpace(_roblosecurity);

                    (SearchCommand as RelayCommand)?.NotifyCanExecuteChanged();
                    (SearchGamesCommand as RelayCommand)?.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(ServerListMessage));

                    return _hasValidCookies;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return false;
        }

        private async Task InitializeCookiesAsync()
        {
            const string LOG_IDENT_INIT_COOKIES = $"{LOG_IDENT}::InitializeCookies";

            try
            {
                // First try cookie manager
                await App.Cookies.LoadCookies();

                if (App.Cookies.Loaded)
                {
                    _roblosecurity = App.Cookies.GetAuthCookie();
                    _hasValidCookies = !string.IsNullOrWhiteSpace(_roblosecurity);

                    if (_hasValidCookies)
                    {
                        var user = await App.Cookies.GetAuthenticated();
                        if (user != null)
                        {
                            App.Logger.WriteLine(LOG_IDENT_INIT_COOKIES, $"Authenticated via cookie manager as: {user.Username} (ID: {user.Id})");
                        }
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT_INIT_COOKIES, "No valid roblosecurity cookie found in cookie manager");
                    }
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_INIT_COOKIES, $"Cookie manager was not loaded. State: {App.Cookies.State}");
                    _hasValidCookies = false;
                }

                // Fallback to account manager if cookie manager dosent have valid cookies
                if (!_hasValidCookies)
                {
                    App.Logger.WriteLine(LOG_IDENT_INIT_COOKIES, "Falling back to AccountManager for authentication...");

                    var mgr = AccountManager.Shared;
                    if (mgr?.ActiveAccount != null)
                    {
                        _roblosecurity = mgr.GetRoblosecurityForUser(mgr.ActiveAccount.UserId);
                        _hasValidCookies = !string.IsNullOrWhiteSpace(_roblosecurity);

                        if (_hasValidCookies)
                        {
                            App.Logger.WriteLine(LOG_IDENT_INIT_COOKIES, $"Authenticated via AccountManager as: {mgr.ActiveAccount.Username} (ID: {mgr.ActiveAccount.UserId})");
                        }
                        else
                        {
                            App.Logger.WriteLine(LOG_IDENT_INIT_COOKIES, "AccountManager also doesn't have valid cookies");
                        }
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT_INIT_COOKIES, "No active account in AccountManager");
                    }
                }

                if (_hasValidCookies)
                {
                    App.Logger.WriteLine(LOG_IDENT_INIT_COOKIES, "Authentication successful, loading regions...");
                    await LoadRegionsAsync();
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_INIT_COOKIES, "No authentication available");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_INIT_COOKIES, ex);
                _hasValidCookies = false;
            }
        }

        private async Task LoadRegionsAsync()
        {
            const string LOG_IDENT_LOAD_REGIONS = $"{LOG_IDENT}::LoadRegions";

            IsLoading = true;
            LoadingMessage = "Loading datacenters...";
            Regions.Clear();
            SelectedRegion = null;
            Servers.Clear();

            _fetcher = new RobloxServerFetcher();

            LoadingMessage = "Loading datacenters...";

            var datacentersResult = await _fetcher.GetDatacentersAsync();

            if (datacentersResult != null)
            {
                App.Logger.WriteLine(LOG_IDENT_LOAD_REGIONS, "Successfully loaded datacenters from API, saving to cache...");
                await SaveDatacentersToCacheAsync(datacentersResult.Value);
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT_LOAD_REGIONS, "Failed to load datacenters from API, trying cache...");
                LoadingMessage = "Trying cached datacenters...";

                datacentersResult = await LoadDatacentersFromCacheAsync();

                if (datacentersResult == null)
                {
                    LoadingMessage = "Failed to load datacenters.";
                    IsLoading = false;
                    await Task.Delay(1200);
                    LoadingMessage = "";
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_LOAD_REGIONS, "Successfully loaded datacenters from cache");
            }

            foreach (var region in datacentersResult.Value.regions)
                Regions.Add(region);

            _dcMap = datacentersResult.Value.datacenterMap;

            try
            {
                var saved = LoadSelectedRegionFromSettings();
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    var matched = Regions.FirstOrDefault(r => string.Equals(r, saved, StringComparison.OrdinalIgnoreCase));
                    if (matched != null)
                        SelectedRegion = matched;
                    else if (Regions.Count > 0)
                        SelectedRegion = Regions[0];
                }
                else if (Regions.Count > 0)
                {
                    SelectedRegion = Regions[0];
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_LOAD_REGIONS, ex);
                if (Regions.Count > 0)
                    SelectedRegion = Regions[0];
            }

            LoadingMessage = $"Loaded {Regions.Count} regions.";
            IsLoading = false;
            await Task.Delay(800);
            LoadingMessage = "";

            (SearchCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (SearchGamesCommand as RelayCommand)?.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(ServerListMessage));
        }

        private string GetDatacentersCachePath()
        {
            string cacheDir = Paths.Cache;
            Directory.CreateDirectory(cacheDir);
            return Path.Combine(cacheDir, "datacenters_cache.json");
        }

        private async Task SaveDatacentersToCacheAsync((List<string> regions, Dictionary<int, string> datacenterMap) datacenters)
        {
            const string LOG_IDENT_CACHE_SAVE = $"{LOG_IDENT}::SaveDatacentersToCache";

            try
            {
                var cacheData = new DatacentersCache
                {
                    Regions = datacenters.regions,
                    DatacenterMap = datacenters.datacenterMap,
                    LastUpdated = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(GetDatacentersCachePath(), json);

                App.Logger.WriteLine(LOG_IDENT_CACHE_SAVE, "Successfully saved datacenters to cache");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_CACHE_SAVE, ex);
            }
        }

        private async Task<(List<string> regions, Dictionary<int, string> datacenterMap)?> LoadDatacentersFromCacheAsync()
        {
            const string LOG_IDENT_CACHE_LOAD = $"{LOG_IDENT}::LoadDatacentersFromCache";

            try
            {
                var cachePath = GetDatacentersCachePath();

                if (!File.Exists(cachePath))
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, "Cache file does not exist");
                    return null;
                }

                var json = await File.ReadAllTextAsync(cachePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, "Cache file is empty");
                    return null;
                }

                var cacheData = JsonSerializer.Deserialize<DatacentersCache>(json);
                if (cacheData == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, "Failed to deserialize cache JSON");
                    return null;
                }

                if (cacheData.LastUpdated < DateTime.UtcNow.AddDays(-7))
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, "Cache is too old, ignoring");
                    return null;
                }

                App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, $"Loaded datacenters from cache (last updated: {cacheData.LastUpdated})");
                return (cacheData.Regions, cacheData.DatacenterMap);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_CACHE_LOAD, ex);
                return null;
            }
        }

        private async Task SearchGamesAsync()
        {
            const string LOG_IDENT_SEARCH_GAMES = $"{LOG_IDENT}::SearchGames";

            App.Logger.WriteLine(LOG_IDENT_SEARCH_GAMES, "SearchGamesAsync called");
            _hasSearched = true;

            IsGameSearchLoading = true;
            LoadingMessage = "Searching games...";
            SearchResults.Clear();

            try
            {
                var results = await GameSearching.GetGameSearchResultsAsync(SearchQuery);
                foreach (var r in results)
                    SearchResults.Add(r);

                if (SearchResults.Count == 0)
                {
                    LoadingMessage = "No games found.";
                    await Task.Delay(900);
                    LoadingMessage = "";
                }
                else
                {
                    LoadingMessage = $"Found {SearchResults.Count} result(s). Select one to set Place ID.";
                    await Task.Delay(900);
                    LoadingMessage = "";
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_SEARCH_GAMES, ex);
                LoadingMessage = "Search failed.";
                await Task.Delay(900);
                LoadingMessage = "";
            }
            finally
            {
                IsGameSearchLoading = false;
                (SearchGamesCommand as RelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        private async Task SearchAsync()
        {
            const string LOG_IDENT_SEARCH = $"{LOG_IDENT}::Search";

            if (string.IsNullOrWhiteSpace(SelectedRegion) && Regions.Count > 0)
            {
                SelectedRegion = Regions[0];
            }

            if (string.IsNullOrWhiteSpace(SelectedRegion))
            {
                Frontend.ShowMessageBox("Please select a region first.", MessageBoxImage.Warning);
                return;
            }

            App.Logger.WriteLine(LOG_IDENT_SEARCH, "SearchAsync called");
            _hasSearched = true;
            IsLoading = true;
            LoadingMessage = "Searching servers...";
            Servers.Clear();
            _displayedServerIds.Clear();
            _nextCursor = "";

            _lastFetchProcessedCount = 0;
            OnPropertyChanged(nameof(ServerListMessage));

            if (_fetcher == null || _dcMap == null)
            {
                LoadingMessage = "Fetcher not initialized.";
                IsLoading = false;
                await Task.Delay(800);
                LoadingMessage = "";
                return;
            }

            int pagesChecked = 0;
            const int maxPages = 3;

            do
            {
                await LoadServersAsync(_fetcher, _dcMap, resetCursor: pagesChecked == 0);
                pagesChecked++;

                if (pagesChecked < maxPages && !string.IsNullOrWhiteSpace(_nextCursor))
                {
                    App.Logger.WriteLine(LOG_IDENT_SEARCH, $"Checked {pagesChecked} pages, found {Servers.Count} servers...");
                }
            }
            while (pagesChecked < maxPages && !string.IsNullOrWhiteSpace(_nextCursor));

            IsLoading = false;

            if (Servers.Count > 0)
            {
                App.Logger.WriteLine(LOG_IDENT_SEARCH, $"Search completed. Checked {pagesChecked} pages, found {Servers.Count} servers.");
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT_SEARCH, $"Search completed. Checked {pagesChecked} pages, no servers found.");
            }

            App.Logger.WriteLine(LOG_IDENT_SEARCH, $"SearchAsync completed after {pagesChecked} pages. Found {Servers.Count} servers");
            await Task.Delay(800);
            LoadingMessage = "";
        }

        private async Task LoadServersAsync(RobloxServerFetcher fetcher, Dictionary<int, string> dcMap, bool resetCursor = false)
        {
            const string LOG_IDENT_LOAD_SERVERS = $"{LOG_IDENT}::LoadServers";

            App.Logger.WriteLine(LOG_IDENT_LOAD_SERVERS, "LoadServersAsync called");

            if (string.IsNullOrWhiteSpace(PlaceId) || string.IsNullOrWhiteSpace(SelectedRegion) || string.IsNullOrWhiteSpace(_roblosecurity))
            {
                App.Logger.WriteLine(LOG_IDENT_LOAD_SERVERS, "PlaceId, SelectedRegion, or roblosecurity is null/empty");
                LoadingMessage = "Authentication required.";
                await Task.Delay(800);
                LoadingMessage = "";
                return;
            }

            if (resetCursor)
                _nextCursor = "";

            string cursor = _nextCursor;
            int number = Servers.Count + 1;

            if (!long.TryParse(PlaceId, out var placeIdLong))
            {
                App.Logger.WriteLine(LOG_IDENT_LOAD_SERVERS, "PlaceId is not a valid long");
                LoadingMessage = "Invalid Place ID.";
                await Task.Delay(800);
                LoadingMessage = "";
                return;
            }

            App.Logger.WriteLine(LOG_IDENT_LOAD_SERVERS, $"Starting server fetch with cursor: '{cursor}' and sortOrder: {SelectedSortOrder}");

            LoadingMessage = "Fetching server list...";

            var result = await fetcher.FetchServerInstancesAsync(placeIdLong, _roblosecurity, cursor, SelectedSortOrder);

            if (result == null)
            {
                LoadingMessage = "Failed to fetch servers.";
                App.Logger.WriteLine(LOG_IDENT_LOAD_SERVERS, "FetchServerInstancesAsync returned null");
                await Task.Delay(800);
                LoadingMessage = "";
                return;
            }

            App.Logger.WriteLine(LOG_IDENT_LOAD_SERVERS, $"FetchServerInstancesAsync returned {result.Servers.Count} servers, NextCursor: '{result.NextCursor}'");

            if (result.Servers.Count > 0)
            {
                foreach (var server in result.Servers.Take(3))
                {
                    App.Logger.WriteLine(LOG_IDENT_LOAD_SERVERS, $"Server: ID={server.Id}, DC={server.DataCenterId}, Region={server.Region}, Players={server.Playing}/{server.MaxPlayers}");
                }
            }

            int processed = 0;
            int addedThisBatch = 0;
            int totalInBatch = result.Servers.Count;

            foreach (var s in result.Servers)
            {
                processed++;
                App.Logger.WriteLine(LOG_IDENT_LOAD_SERVERS, $"Processing server {processed}/{totalInBatch} (added {addedThisBatch}), - Server ID: {s.Id}, - Server Uptime: {s.UptimeDisplay} - DataCenterId: {s.DataCenterId}, Region: {s.Region}, Players: {s.Playing}/{s.MaxPlayers}");

                if (_displayedServerIds.Add(s.Id) && s.DataCenterId.HasValue && dcMap.TryGetValue(s.DataCenterId.Value, out var mappedRegion) && mappedRegion == SelectedRegion)
                {
                    Servers.Add(new ServerEntry
                    {
                        Number = number++,
                        ServerId = s.Id,
                        Players = $"{s.Playing}/{s.MaxPlayers}",
                        Region = s.Region,
                        DataCenterId = s.DataCenterId,
                        Uptime = s.UptimeDisplay,
                        JoinCommand = new RelayCommand(() => JoinServer(s.Id))
                    });
                    addedThisBatch++;
                }
            }

            _lastFetchProcessedCount = processed;
            OnPropertyChanged(nameof(ServerListMessage));

            _nextCursor = result.NextCursor;
            (LoadMoreCommand as RelayCommand)?.NotifyCanExecuteChanged();

            int totalServersFromFetcher = result.Servers.Count;
            int newlyFetchedCount = result.NewlyFetchedCount;
            int cachedCount = Math.Max(0, totalServersFromFetcher - newlyFetchedCount);

            if (addedThisBatch > 0)
            {
                App.Logger.WriteLine(LOG_IDENT_LOAD_SERVERS, $"Processed {totalServersFromFetcher} servers ({cachedCount} from cache). Added {addedThisBatch} matching your region. Total: {Servers.Count}.");
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT_LOAD_SERVERS, $"Processed {totalServersFromFetcher} servers ({cachedCount} from cache). None matched your region. Total: {Servers.Count}.");
            }

            App.Logger.WriteLine(LOG_IDENT_LOAD_SERVERS, $"LoadServersAsync finished. Servers count: {Servers.Count}, NextCursor: {_nextCursor}");

            if (string.IsNullOrWhiteSpace(_nextCursor))
            {
                await Task.Delay(900);
                if (LoadingMessage.StartsWith("Processed"))
                    LoadingMessage = "";
            }
        }

        private async Task LoadMoreServersAsync()
        {
            const string LOG_IDENT_LOAD_MORE = $"{LOG_IDENT}::LoadMoreServers";

            if (_fetcher == null || _dcMap == null || string.IsNullOrWhiteSpace(_nextCursor))
                return;

            IsLoading = true;
            LoadingMessage = "Loading more servers...";

            int initialCount = Servers.Count;
            int pagesChecked = 0;
            const int maxPages = 5;

            do
            {
                await LoadServersAsync(_fetcher, _dcMap, resetCursor: false);
                pagesChecked++;

                if (pagesChecked < maxPages && !string.IsNullOrWhiteSpace(_nextCursor))
                {
                    App.Logger.WriteLine(LOG_IDENT_LOAD_MORE, $"Checked {pagesChecked} pages, loaded {Servers.Count - initialCount} additional servers...");
                }
            }
            while (pagesChecked < maxPages && !string.IsNullOrWhiteSpace(_nextCursor));

            IsLoading = false;

            int serversAdded = Servers.Count - initialCount;
            if (serversAdded > 0)
            {
                App.Logger.WriteLine(LOG_IDENT_LOAD_MORE, $"Loaded {serversAdded} additional servers from {pagesChecked} pages. Total: {Servers.Count}.");
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT_LOAD_MORE, $"No additional servers found after checking {pagesChecked} pages.");
            }

            await Task.Delay(800);
            LoadingMessage = "";
        }

        private async void JoinServer(string serverId)
        {
            const string LOG_IDENT_JOIN = $"{LOG_IDENT}::JoinServer";

            App.Logger.WriteLine(LOG_IDENT_JOIN, $"JoinServer called with serverId: {serverId}");

            if (!long.TryParse(PlaceId, out var placeId))
            {
                App.Logger.WriteLine(LOG_IDENT_JOIN, "PlaceId is not a valid long");
                LoadingMessage = "Invalid Place ID.";
                return;
            }

            // Fallback to system protocol.
            string robloxUri = $"roblox://experiences/start?placeId={placeId}&gameInstanceId={serverId}";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = robloxUri,
                    UseShellExecute = true
                });
                App.Logger.WriteLine(LOG_IDENT_JOIN, "Launched game via system protocol");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_JOIN, ex);
                LoadingMessage = "Failed to launch game";
            }
        }

        private void SaveSelectedRegionToSettings()
        {
            const string LOG_IDENT_SAVE_REGION = $"{LOG_IDENT}::SaveSelectedRegionToSettings";

            try
            {
                if (string.IsNullOrWhiteSpace(_selectedRegion))
                {
                    App.Settings.Prop.SelectedRegion = string.Empty;
                }
                else
                {
                    App.Settings.Prop.SelectedRegion = _selectedRegion;
                }

                App.Settings.Save();
                App.Logger.WriteLine(LOG_IDENT_SAVE_REGION, "Persisted SelectedRegion to settings.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_SAVE_REGION, ex);
            }
        }

        private string? LoadSelectedRegionFromSettings()
        {
            const string LOG_IDENT_LOAD_REGION = $"{LOG_IDENT}::LoadSelectedRegionFromSettings";

            try
            {
                var region = App.Settings.Prop.SelectedRegion;
                return string.IsNullOrWhiteSpace(region) ? null : region.Trim();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_LOAD_REGION, ex);
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    public class SortOrderComboBoxItem
    {
        public string Content { get; set; } = string.Empty;
        public object Tag { get; set; } = new object();
    }
}