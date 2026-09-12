using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels.Settings
{
    internal partial class RegionSelectorViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private const int ApiPageSize = 100;
        private const int SpecificRegionBatchSize = 12;
        private const int AutoRegionBatchSize = 4;
        private const int AutoMaxRegions = 3;
        private const int AlivenessConcurrency = 4;

        private readonly HashSet<string> _displayedServerIds = [];
        private readonly Dictionary<string, int?> _regionCursors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Queue<ServerInstance>> _regionBuffers = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _activeAutoRegions = [];
        private readonly CancellationTokenSource _disposeCts = new();
        private RobloxServerFetcher? _fetcher;
        private Dictionary<int, string>? _dcMap;
        private CancellationTokenSource? _searchDebounceCts;
        private CancellationTokenSource? _searchCts;
        private List<string> _sortedAutoRegions = [];
        private string? _resolvedCookie;
        private bool _isAutoMode;
        private bool _disposed;

        #region Fields
        private bool _hasSearched;
        private string _placeId = "";
        private string _selectedRegion = Strings.Common_Auto;
        private bool _isLoading;
        private bool _isGameSearchLoading;
        private string _loadingMessage = "";
        private bool _hasValidCookies;
        private string _searchQuery = "";
        private OmniSearchContent? _selectedSearchResult;
        private int _lastFetchProcessedCount;
        private string? _thumbnailUrl;
        private string? _selectedRegionInput;
        private bool _isSearchFlyoutOpen;
        private List<string> _regions = [];
        private bool _hasMoreServers;
        private bool _isLoadingMore;
        #endregion

        #region Properties
        public bool HasSearched
        {
            get => _hasSearched;
            set
            {
                if (SetProperty(ref _hasSearched, value))
                {
                    OnPropertyChanged(nameof(ShowLoadMoreButton));
                    LoadMoreCommand.NotifyCanExecuteChanged();
                }
            }
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
                    OnPropertyChanged(nameof(ShowLoadMoreButton));
                    SearchCommand.NotifyCanExecuteChanged();
                    SearchGamesCommand.NotifyCanExecuteChanged();
                    LoadMoreCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            private set
            {
                if (SetProperty(ref _isLoadingMore, value))
                {
                    OnPropertyChanged(nameof(ShowLoadMoreButton));
                    SearchCommand.NotifyCanExecuteChanged();
                    LoadMoreCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasMoreServers
        {
            get => _hasMoreServers;
            private set
            {
                if (SetProperty(ref _hasMoreServers, value))
                {
                    OnPropertyChanged(nameof(ShowLoadMoreButton));
                    LoadMoreCommand.NotifyCanExecuteChanged();
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

        public List<string> Regions
        {
            get => _regions;
            private set => SetProperty(ref _regions, value);
        }

        public ObservableCollection<ServerEntry> Servers { get; } = [];
        public ObservableCollection<OmniSearchContent> SearchResults { get; } = [];

        public bool IsServerListEmpty => Servers.Count == 0;
        public bool IsServerListEmptyAndNotLoading => IsServerListEmpty && !IsLoading;
        public bool ShowLoadingIndicator => IsLoading && !IsGameSearchLoading;

        public bool ShowLoadMoreButton =>
            HasSearched && !IsLoading && !IsLoadingMore && Servers.Count > 0 && HasMoreServers;

        public string ServerListMessage => !HasValidCookies ? Strings.Menu_RegionSelector_LoginRequired :
            IsLoading ? "" :
            !HasSearched ? Strings.Menu_RegionSelector_EnterPlaceId :
            IsServerListEmpty ? (LastFetchProcessedCount == 0 ? Strings.Menu_RegionSelector_NoPublicServers : Strings.Menu_RegionSelector_NoServersForRegion) : "";

        public IAsyncRelayCommand SearchCommand { get; }
        public IAsyncRelayCommand SearchGamesCommand { get; }
        public IAsyncRelayCommand LoadMoreCommand { get; }
        public IRelayCommand ClearSearchCommand { get; }
        #endregion

        public RegionSelectorViewModel()
        {
            Servers.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(IsServerListEmpty));
                OnPropertyChanged(nameof(IsServerListEmptyAndNotLoading));
                OnPropertyChanged(nameof(ShowLoadMoreButton));
                LoadMoreCommand?.NotifyCanExecuteChanged();
            };

            SearchCommand = new AsyncRelayCommand(SearchAsync,
                () => !IsLoading && !IsLoadingMore && !string.IsNullOrWhiteSpace(PlaceId) && HasValidCookies);
            SearchGamesCommand = new AsyncRelayCommand(SearchGamesAsync,
                () => !IsLoading && !IsGameSearchLoading && !string.IsNullOrWhiteSpace(SearchQuery) && HasValidCookies);
            LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync, () => ShowLoadMoreButton);

            ClearSearchCommand = new RelayCommand(ClearSearch);

            _ = InitializeAsync();
        }

        private void ClearSearch()
        {
            SearchQuery = string.Empty;
            SearchResults.Clear();
            IsSearchFlyoutOpen = false;
        }

        private void OnSearchQueryChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                IsSearchFlyoutOpen = false;
                SearchResults.Clear();
                return;
            }

            if (long.TryParse(value, out _))
                PlaceId = value;

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
                    Dispatcher.UIThread.Post(() =>
                    {
                        IsSearchFlyoutOpen = SearchResults.Count > 0 && !string.IsNullOrWhiteSpace(SearchQuery);
                    });
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task InitializeAsync()
        {
            try
            {
                _fetcher = new RobloxServerFetcher();
                HasValidCookies = true;
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

                _ = Task.Run(async () =>
                {
                    try { await _fetcher!.GetDatacentersAsync(_disposeCts.Token); }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { App.Logger.Error(ex); }
                });

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
            var sorted = regions.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
            var list = new List<string> { Strings.Common_Auto };
            list.AddRange(sorted);

            _dcMap = dcMap;

            var desired = string.IsNullOrEmpty(_selectedRegion) ? Strings.Common_Auto : _selectedRegion;
            var match = list.FirstOrDefault(r => r.Equals(desired, StringComparison.OrdinalIgnoreCase))
                        ?? Strings.Common_Auto;

            SelectedRegion = "";
            Regions = list;
            Dispatcher.UIThread.Post(() => SelectedRegion = match, DispatcherPriority.Background);
        }

        private async Task SearchAsync()
        {
#pragma warning disable CA1849
            _searchCts?.Cancel();
#pragma warning restore CA1849
            _searchCts?.Dispose();
            _searchCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
            var token = _searchCts.Token;

            HasSearched = true;
            IsLoading = true;
            LoadingMessage = Strings.Menu_RegionSelector_SearchingServers;
            Servers.Clear();
            _displayedServerIds.Clear();
            _regionCursors.Clear();
            _regionBuffers.Clear();
            _activeAutoRegions.Clear();
            _sortedAutoRegions.Clear();
            LastFetchProcessedCount = 0;
            HasMoreServers = false;

            if (!long.TryParse(PlaceId, out var placeId))
            {
                IsLoading = false;
                await Frontend.ShowMessageBox("Invalid place ID.", MessageBoxImage.Error);
                return;
            }

            try
            {
                _isAutoMode = string.IsNullOrEmpty(SelectedRegion) ||
                              SelectedRegion.Equals(Strings.Common_Auto, StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrEmpty(_resolvedCookie))
                {
                    try { _resolvedCookie = await _fetcher!.ResolveCookieAsync(); }
                    catch (Exception ex) { App.Logger.Error("Failed to resolve cookie:", ex); }
                }

                List<ServerInstance> servers;

                if (_isAutoMode)
                {
                    _sortedAutoRegions = await _fetcher!.GetClosestRegionsForAutoModeAsync(token) ?? [];
                    if (_sortedAutoRegions.Count == 0)
                    {
                        await Frontend.ShowMessageBox(
                            "Could not determine your location for Auto mode. Please try again later.",
                            MessageBoxImage.Warning);
                        IsLoading = false;
                        return;
                    }

                    servers = await FetchAutoBatchAsync(placeId, isLoadMore: false, token);

                    if (servers.Count == 0)
                    {
                        await Frontend.ShowMessageBox(
                            "Could not find any servers in nearby regions. Please select a region manually.",
                            MessageBoxImage.Warning);
                        IsLoading = false;
                        return;
                    }
                }
                else
                {
                    servers = await TakeFromRegionAsync(placeId, SelectedRegion ?? "", SpecificRegionBatchSize, token);
                }

                AppendServers(servers);
                LastFetchProcessedCount = servers.Count;
                HasMoreServers = ComputeHasMoreServers();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { App.Logger.Error($"Search error: {ex}"); }
            finally
            {
                IsLoading = false;
                await Task.Delay(500);
                LoadingMessage = "";
            }
        }

        private async Task LoadMoreAsync()
        {
            if (!HasSearched || IsLoading || IsLoadingMore || !HasMoreServers) return;
            if (_searchCts == null || _searchCts.IsCancellationRequested) return;
            if (!long.TryParse(PlaceId, out var placeId)) return;

            var token = _searchCts.Token;
            IsLoadingMore = true;

            try
            {
                var servers = _isAutoMode
                    ? await FetchAutoBatchAsync(placeId, isLoadMore: true, token)
                    : await TakeFromRegionAsync(placeId, SelectedRegion ?? "", SpecificRegionBatchSize, token);

                AppendServers(servers);
                HasMoreServers = ComputeHasMoreServers();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { App.Logger.Error($"Load more error: {ex}"); }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private async Task<List<ServerInstance>> FetchAutoBatchAsync(long placeId, bool isLoadMore, CancellationToken token)
        {
            var result = new List<ServerInstance>();
            var seen = new HashSet<string>();

            if (!isLoadMore)
            {
                foreach (var region in _sortedAutoRegions)
                {
                    if (token.IsCancellationRequested) break;
                    if (_activeAutoRegions.Count >= AutoMaxRegions) break;

                    var taken = await TakeFromRegionAsync(placeId, region, AutoRegionBatchSize, token);
                    if (taken.Count == 0) continue;

                    _activeAutoRegions.Add(region);
                    foreach (var s in taken)
                        if (seen.Add(s.Id)) result.Add(s);
                }
            }
            else
            {
                foreach (var region in _activeAutoRegions)
                {
                    if (token.IsCancellationRequested) break;

                    var taken = await TakeFromRegionAsync(placeId, region, AutoRegionBatchSize, token);
                    foreach (var s in taken)
                        if (seen.Add(s.Id)) result.Add(s);
                }
            }

            return result;
        }

        private async Task<List<ServerInstance>> TakeFromRegionAsync(long placeId, string region, int wanted, CancellationToken token)
        {
            bool hasCookie = !string.IsNullOrEmpty(_resolvedCookie);
            int effectiveWanted = hasCookie ? wanted : ApiPageSize;

            if (!_regionBuffers.TryGetValue(region, out var buffer))
                _regionBuffers[region] = buffer = new Queue<ServerInstance>();

            var taken = new List<ServerInstance>();

            while (!token.IsCancellationRequested && taken.Count < effectiveWanted)
            {
                if (buffer.Count > 0)
                {
                    if (!hasCookie)
                    {
                        while (buffer.Count > 0 && taken.Count < effectiveWanted)
                            taken.Add(buffer.Dequeue());
                    }
                    else
                    {
                        int needed = effectiveWanted - taken.Count;
                        var candidates = new List<ServerInstance>();
                        while (buffer.Count > 0 && candidates.Count < needed)
                            candidates.Add(buffer.Dequeue());

                        var alive = await FilterAliveAsync(placeId, candidates, token);
                        taken.AddRange(alive);
                    }
                    continue;
                }

                bool cursorInitialized = _regionCursors.TryGetValue(region, out int? cursor);
                if (cursorInitialized && cursor == null) break;

                var (pageServers, nextCursor) = await _fetcher!.FetchServersByRegionAsync(
                    placeId, region, cursorInitialized ? cursor : null, limit: ApiPageSize, cancellationToken: token);

                _regionCursors[region] = nextCursor;

                if (pageServers.Count == 0) break;

                foreach (var s in pageServers)
                    buffer.Enqueue(s);
            }

            return taken;
        }

        private bool ComputeHasMoreServers()
        {
            if (_isAutoMode)
            {
                return _activeAutoRegions.Any(r =>
                {
                    if (_regionBuffers.TryGetValue(r, out var buf) && buf.Count > 0) return true;
                    if (_regionCursors.TryGetValue(r, out var c) && c != null) return true;
                    return false;
                });
            }

            var region = SelectedRegion ?? "";
            if (_regionBuffers.TryGetValue(region, out var buffer) && buffer.Count > 0) return true;
            if (_regionCursors.TryGetValue(region, out var cursor) && cursor != null) return true;
            return false;
        }

        private async Task<List<ServerInstance>> FilterAliveAsync(long placeId, List<ServerInstance> servers, CancellationToken token)
        {
            if (servers.Count == 0) return servers;

            var aliveFlags = new bool[servers.Count];
            using var semaphore = new SemaphoreSlim(AlivenessConcurrency);

            var tasks = Enumerable.Range(0, servers.Count).Select(async i =>
            {
                await semaphore.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    aliveFlags[i] = await _fetcher!
                        .IsServerAliveAsync(placeId, servers[i].Id, _resolvedCookie!, token)
                        .ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return [.. servers.Where((_, i) => aliveFlags[i])];
        }

        private void AppendServers(List<ServerInstance> servers)
        {
            int number = Servers.Count + 1;
            foreach (var s in servers)
            {
                if (_displayedServerIds.Add(s.Id))
                {
                    var entry = new ServerEntry
                    {
                        Number = number++,
                        ServerId = s.Id,
                        Region = s.Region,
                        DataCenterId = s.DataCenterId,
                        Uptime = s.UptimeDisplay,
                        JoinCommand = new RelayCommand(() => JoinServer(s.Id))
                    };
                    Servers.Add(entry);
                }
            }
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
                        catch { }
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
                    sortedRegionDict[region] = regionDict[region];

                var cache = new DatacentersCache
                {
                    Regions = sortedRegionDict,
                    LastUpdated = DateTime.UtcNow
                };

                Directory.CreateDirectory(Paths.Cache);
                var json = JsonSerializer.Serialize(cache);
                await File.WriteAllTextAsync(GetCachePath(), json);
            }
            catch { }
        }

        private static async Task<(List<string> regions, Dictionary<int, string> datacenterMap)?> LoadDatacentersFromCacheAsync(bool allowExpired = false)
        {
            try
            {
                if (!File.Exists(GetCachePath())) return null;

                var json = await File.ReadAllTextAsync(GetCachePath());
                var cache = JsonSerializer.Deserialize<DatacentersCache>(json);

                if (cache == null) return null;
                if (!allowExpired && cache.LastUpdated < DateTime.UtcNow.AddDays(-7)) return null;

                var map = new Dictionary<int, string>();
                var regions = cache.Regions.Keys.ToList();

                foreach (var kvp in cache.Regions)
                    foreach (var id in kvp.Value)
                        map[id] = kvp.Key;

                return (regions, map);
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _disposeCts.Cancel();
                _disposeCts.Dispose();

                _fetcher?.Dispose();
                _fetcher = null;

                _searchDebounceCts?.Cancel();
                _searchDebounceCts?.Dispose();
                _searchDebounceCts = null;

                _searchCts?.Cancel();
                _searchCts?.Dispose();
                _searchCts = null;
            }

            _disposed = true;
        }
    }
}