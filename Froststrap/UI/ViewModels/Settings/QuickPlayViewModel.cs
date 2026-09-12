// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using Froststrap.Integrations.AccountManager;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings;

internal record PrivateServerInfo(
    long VipServerId,
    string AccessCode,
    string Name,
    long OwnerId,
    string OwnerName,
    string? OwnerAvatarUrl,
    int MaxPlayers,
    int CurrentPlayers);

internal class QuickPlayViewModel : NotifyPropertyChangedViewModel, IDisposable
{
    internal enum QuickPlayTab
    {
        Continue,
        Recommended,
        Favorites
    }

    private bool _isLoading;
    private bool _isOverlayVisible;
    private bool _isSubplacesOverlayVisible;
    private bool _isLoadingSubplaces;
    private readonly ObservableCollection<PlaceInfo> _subplaces = [];
    private UniverseDetails? _selectedUniverseDetails;
    private readonly string _cachePath = Path.Combine(Paths.Cache, "GameHistory.json");
    private static readonly JsonSerializerOptions HistoryLoadOptions = new() { PropertyNameCaseInsensitive = true };
    private List<GameHistoryEntry> _allHistory = [];

    private bool _isPrivateServersOverlayVisible;
    private bool _arePrivateServersEmpty;
    private bool _isLoadingPrivateServers;
    private long _currentPrivateServersPlaceId;
    private bool _isCurrentGameApi;
    private bool _isLoadingServers;
    private bool _isJoiningBestRegion;
    private bool _isFavoritesLoading;
    private QuickPlayTab _selectedTab = QuickPlayTab.Continue;
    private bool _favoritesLoaded;
    private bool _isRecommendedLoading;
    private bool _recommendationsLoaded;
    private bool _recentGamesLoaded;

    private string _searchQuery = "";
    private string _serverId = "";
    private bool _isSearchFlyoutOpen;
    private bool _isGameSearchLoading;
    private CancellationTokenSource? _searchDebounceCts;
    private CancellationTokenSource? _gameInfoCts;
    private QuickPlayGameItem? _selectedGame;
    private bool _disposed;

    private readonly ObservableCollection<PrivateServerInfo> _privateServers = [];

    public ObservableCollection<QuickPlayGameItem> RecentGames { get; } = [];
    public ObservableCollection<QuickPlayGameItem> FavoriteGames { get; } = [];
    public ObservableCollection<QuickPlayGameItem> RecommendedGames { get; } = [];
    public ObservableCollection<ServerInfo> SelectedGameServers { get; } = [];
    public ObservableCollection<OmniSearchContent> SearchResults { get; } = [];

    public UniverseDetails? SelectedUniverseDetails
    {
        get => _selectedUniverseDetails;
        set => SetProperty(ref _selectedUniverseDetails, value);
    }

    public bool IsOverlayVisible
    {
        get => _isOverlayVisible;
        set => SetProperty(ref _isOverlayVisible, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(ShowRecentEmpty));
        }
    }

    public bool IsSubplacesOverlayVisible
    {
        get => _isSubplacesOverlayVisible;
        set => SetProperty(ref _isSubplacesOverlayVisible, value);
    }

    public bool IsLoadingSubplaces
    {
        get => _isLoadingSubplaces;
        set => SetProperty(ref _isLoadingSubplaces, value);
    }

    public bool IsPrivateServersOverlayVisible
    {
        get => _isPrivateServersOverlayVisible;
        set => SetProperty(ref _isPrivateServersOverlayVisible, value);
    }

    public bool ArePrivateServersEmpty
    {
        get => _arePrivateServersEmpty;
        set => SetProperty(ref _arePrivateServersEmpty, value);
    }

    public bool IsLoadingPrivateServers
    {
        get => _isLoadingPrivateServers;
        set => SetProperty(ref _isLoadingPrivateServers, value);
    }

    public bool IsLoadingServers
    {
        get => _isLoadingServers;
        set => SetProperty(ref _isLoadingServers, value);
    }

    public bool IsCurrentGameApi
    {
        get => _isCurrentGameApi;
        set
        {
            if (SetProperty(ref _isCurrentGameApi, value))
            {
                OnPropertyChanged(nameof(IsTrackedGame));
                OnPropertyChanged(nameof(OverlayMinWidth));
                OnPropertyChanged(nameof(OverlayMaxWidth));
            }
        }
    }

    public double OverlayMinWidth => IsCurrentGameApi ? 750 : 450;
    public double OverlayMaxWidth => IsCurrentGameApi ? 1000 : 550;

    public bool IsJoiningBestRegion
    {
        get => _isJoiningBestRegion;
        set => SetProperty(ref _isJoiningBestRegion, value);
    }

    public bool IsFavoritesLoading
    {
        get => _isFavoritesLoading;
        set
        {
            if (SetProperty(ref _isFavoritesLoading, value))
                OnPropertyChanged(nameof(ShowFavoritesEmpty));
        }
    }

    public bool IsRecommendedLoading
    {
        get => _isRecommendedLoading;
        set
        {
            if (SetProperty(ref _isRecommendedLoading, value))
                OnPropertyChanged(nameof(ShowRecommendedEmpty));
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                OnSearchQueryChanged(value);
        }
    }

    public string ServerId
    {
        get => _serverId;
        set => SetProperty(ref _serverId, value);
    }

    public bool IsSearchFlyoutOpen
    {
        get => _isSearchFlyoutOpen;
        set => SetProperty(ref _isSearchFlyoutOpen, value);
    }

    public bool IsGameSearchLoading
    {
        get => _isGameSearchLoading;
        set => SetProperty(ref _isGameSearchLoading, value);
    }

    public QuickPlayGameItem? SelectedGame
    {
        get => _selectedGame;
        private set
        {
            if (SetProperty(ref _selectedGame, value))
                OnPropertyChanged(nameof(HasSelectedGame));
        }
    }

    public bool HasSelectedGame => SelectedGame != null;

    public QuickPlayTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                if (value == QuickPlayTab.Continue && !_recentGamesLoaded)
                {
                    IsLoading = true;
                    RecentGames.Clear();
                    OnPropertyChanged(nameof(HasRecentGames));
                    OnPropertyChanged(nameof(ShowRecentEmpty));
                    _ = LoadRecentGamesAsync();
                }
                else if (value == QuickPlayTab.Recommended && !_recommendationsLoaded)
                {
                    IsRecommendedLoading = true;
                    RecommendedGames.Clear();
                    OnPropertyChanged(nameof(HasRecommendedGames));
                    OnPropertyChanged(nameof(ShowRecommendedEmpty));
                    _ = LoadRecommendedGamesAsync();
                }
                else if (value == QuickPlayTab.Favorites && !_favoritesLoaded)
                {
                    IsFavoritesLoading = true;
                    FavoriteGames.Clear();
                    OnPropertyChanged(nameof(HasFavoriteGames));
                    _ = LoadFavoriteGamesAsync();
                }
            }
        }
    }

    public int SelectedTabIndex
    {
        get => (int)SelectedTab;
        set => SelectedTab = (QuickPlayTab)value;
    }

    public bool HasRecentGames => RecentGames.Count > 0;
    public bool HasFavoriteGames => FavoriteGames.Count > 0;
    public bool HasRecommendedGames => RecommendedGames.Count > 0;
    public bool ShowRecentEmpty => !IsLoading && !HasRecentGames;
    public bool ShowRecommendedEmpty => !IsRecommendedLoading && !HasRecommendedGames;
    public bool ShowFavoritesEmpty => !IsFavoritesLoading && !HasFavoriteGames;

#pragma warning disable CA1822
    public bool IsLoggedIn => AccountManager.Shared?.ActiveAccount != null;
#pragma warning restore CA1822

    public ObservableCollection<PlaceInfo> Subplaces => _subplaces;
    public ObservableCollection<PrivateServerInfo> PrivateServers => _privateServers;

    public static bool HasActiveAccount => AccountManager.Shared?.ActiveAccount != null;
    public bool IsTrackedGame => !IsCurrentGameApi;
    public bool CanJoinBestRegion => !IsJoiningBestRegion;

    public ICommand JoinGameCommand { get; }
    public ICommand RejoinLastServerCommand { get; }
    public ICommand ViewServersCommand { get; }
    public ICommand ViewRobloxServersCommand { get; }
    public ICommand CloseOverlayCommand { get; }
    public ICommand CloseSubplacesCommand { get; }
    public ICommand VisitPageCommand { get; }
    public ICommand ViewSubplacesCommand { get; }
    public ICommand JoinSubplaceCommand { get; }
    public ICommand ShowPrivateServersCommand { get; }
    public ICommand JoinPrivateServerCommand { get; }
    public ICommand ClosePrivateServersCommand { get; }
    public ICommand JoinBestRegionCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public IRelayCommand JoinServerByIdCommand { get; }
    public IAsyncRelayCommand JoinBestRegionFromSearchCommand { get; }

    public QuickPlayViewModel()
    {
        JoinGameCommand = new RelayCommand<QuickPlayGameItem>(item =>
        {
            if (item != null) LaunchRoblox(item.PlaceId);
        });

        RejoinLastServerCommand = new RelayCommand<object>(param =>
        {
            if (param is QuickPlayGameItem item)
            {
                LaunchRoblox(item.PlaceId, item.LastJobId);
            }
            else if (param is ServerInfo server)
            {
                var entry = _allHistory.FirstOrDefault(x => x.UniverseId == SelectedUniverseDetails?.Data?.Id);
                if (entry != null) LaunchRoblox(entry.PlaceId, server.JobId);
            }
        });

        ViewSubplacesCommand = new RelayCommand<QuickPlayGameItem>(async item =>
        {
            if (item == null || item.UniverseId == 0) return;

            SelectedUniverseDetails = item.OriginalDetails;
            IsSubplacesOverlayVisible = true;
            await FetchSubplacesAsync(item.UniverseId);
        });

        JoinSubplaceCommand = new RelayCommand<PlaceInfo>(subplace =>
        {
            if (subplace != null) LaunchRoblox(subplace.Id);
        });

        ViewServersCommand = new RelayCommand<QuickPlayGameItem>(async item =>
        {
            if (item == null || item.Source != GameSource.Tracked) return;

            SelectedUniverseDetails = item.OriginalDetails;
            IsOverlayVisible = true;
            SelectedGameServers.Clear();
            IsLoadingServers = true;
            IsCurrentGameApi = false;

            try
            {
                var entry = _allHistory.FirstOrDefault(x => x.UniverseId == item.UniverseId);
                if (entry != null)
                {
                    var sortedServers = entry.Servers.OrderByDescending(x => x.JoinedAt).ToList();
                    foreach (var s in sortedServers) s.IsLatest = false;
                    if (sortedServers.Count > 0) sortedServers[0].IsLatest = true;
                    foreach (var s in sortedServers)
                        SelectedGameServers.Add(s);
                }
            }
            finally
            {
                IsLoadingServers = false;
            }
        });

        CloseOverlayCommand = new RelayCommand(() => IsOverlayVisible = false);
        CloseSubplacesCommand = new RelayCommand(() => IsSubplacesOverlayVisible = false);

        VisitPageCommand = new RelayCommand<QuickPlayGameItem>(item =>
        {
            if (item != null) Process.Start(new ProcessStartInfo($"https://www.roblox.com/games/{item.PlaceId}") { UseShellExecute = true });
        });

        ShowPrivateServersCommand = new RelayCommand<QuickPlayGameItem>(async item =>
        {
            if (item == null || item.PlaceId == 0) return;
            _currentPrivateServersPlaceId = item.PlaceId;
            await ShowPrivateServersForGameAsync();
        });

        JoinPrivateServerCommand = new RelayCommand<string>(accessCode =>
        {
            if (string.IsNullOrWhiteSpace(accessCode)) return;
            LaunchRoblox(_currentPrivateServersPlaceId, accessCode: accessCode);
            IsPrivateServersOverlayVisible = false;
        });

        JoinBestRegionCommand = new RelayCommand<QuickPlayGameItem>(async item =>
        {
            if (item == null || item.PlaceId == 0 || IsJoiningBestRegion) return;

            IsJoiningBestRegion = true;
            try
            {
                using var fetcher = new RobloxServerFetcher();
                bool success = await fetcher.JoinBestServerAsync(
                    item.PlaceId,
                    showConfirmation: false
                );

                if (!success)
                {
                    await Frontend.ShowMessageBox(Strings.Menu_QuickPlay_NoSuitableServer, MessageBoxImage.Information);
                }
            }
            finally
            {
                IsJoiningBestRegion = false;
            }
        });

        ViewRobloxServersCommand = new RelayCommand<QuickPlayGameItem>(async item =>
        {
            if (item == null) return;

            SelectedUniverseDetails = item.OriginalDetails;
            IsOverlayVisible = true;
            SelectedGameServers.Clear();
            IsLoadingServers = true;
            IsCurrentGameApi = true;

            try
            {
                var servers = await FetchServersForGameAsync(item.PlaceId);
                if (servers.Count > 0)
                {
                    servers = [.. servers.OrderByDescending(s => s.JoinedAt)];
                    foreach (var s in servers)
                        SelectedGameServers.Add(s);
                }
                item.ServerCount = servers.Count;
                OnPropertyChanged(nameof(RecentGames));
            }
            finally
            {
                IsLoadingServers = false;
            }
        });

        ClosePrivateServersCommand = new RelayCommand(() => IsPrivateServersOverlayVisible = false);

        ClearSearchCommand = new RelayCommand(ClearSearch);
        JoinServerByIdCommand = new RelayCommand(JoinServerById, () => CanJoinServerById);
        JoinBestRegionFromSearchCommand = new AsyncRelayCommand(JoinBestRegionFromSearchAsync, () => CanJoinBestRegionFromSearch);

        AccountManager.Shared.ActiveAccountChanged += _ =>
        {
            Dispatcher.UIThread.InvokeAsync(() => OnPropertyChanged(nameof(HasActiveAccount)));
        };

        AccountManager.Shared.ActiveAccountChanged += OnActiveAccountChanged;
        _ = Initialize();
    }

    private async Task Initialize()
    {
        _allHistory = await Task.Run(() => LoadLocalHistory(_cachePath));
        _recentGamesLoaded = false;
        if (SelectedTab == QuickPlayTab.Continue)
        {
            IsLoading = true;
            await LoadRecentGamesAsync();
        }
    }

    private void ClearSearch()
    {
        SearchQuery = string.Empty;
        SearchResults.Clear();
        IsSearchFlyoutOpen = false;
    }

    private void OnSearchQueryChanged(string value)
    {
        JoinServerByIdCommand.NotifyCanExecuteChanged();
        JoinBestRegionFromSearchCommand.NotifyCanExecuteChanged();

        if (string.IsNullOrWhiteSpace(value))
        {
            IsSearchFlyoutOpen = false;
            SearchResults.Clear();
            ClearSelectedGame();
            return;
        }

        if (long.TryParse(value, out var placeId))
        {
            IsSearchFlyoutOpen = false;
            SearchResults.Clear();
            _ = LoadSelectedGameInfoAsync(placeId);
            return;
        }

        ClearSelectedGame();

        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = new CancellationTokenSource();
        _ = DebouncedSearchTriggerAsync(_searchDebounceCts.Token);
    }

    private bool CanJoinServerById =>
        long.TryParse(SearchQuery, out _);

    private bool CanJoinBestRegionFromSearch =>
        long.TryParse(SearchQuery, out _);

    private void JoinServerById()
    {
        if (!long.TryParse(SearchQuery, out var placeId)) return;

        var jobId = string.IsNullOrWhiteSpace(ServerId) ? null : ServerId.Trim();
        LaunchRoblox(placeId, jobId);
    }

    private async Task JoinBestRegionFromSearchAsync()
    {
        if (!long.TryParse(SearchQuery, out var placeId)) return;
        if (IsJoiningBestRegion) return;

        IsJoiningBestRegion = true;
        try
        {
            using var fetcher = new RobloxServerFetcher();
            bool success = await fetcher.JoinBestServerAsync(
                placeId,
                showConfirmation: false
            );

            if (!success)
            {
                await Frontend.ShowMessageBox(Strings.Menu_QuickPlay_NoSuitableServer, MessageBoxImage.Information);
            }
        }
        finally
        {
            IsJoiningBestRegion = false;
        }
    }

    private void ClearSelectedGame()
    {
        _gameInfoCts?.Cancel();
        _gameInfoCts?.Dispose();
        _gameInfoCts = null;
        SelectedGame = null;
    }

    private async Task LoadSelectedGameInfoAsync(long placeId)
    {
#pragma warning disable CA1849
        _gameInfoCts?.Cancel();
#pragma warning restore CA1849
        _gameInfoCts?.Dispose();
        _gameInfoCts = new CancellationTokenSource();
        var token = _gameInfoCts.Token;

        SelectedGame = null;

        try
        {
            var url = UrlBuilder.BuildApiUrl("apis", $"universes/v1/places/{placeId}/universe");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await App.HttpClient.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("universeId", out var universeIdElement) ||
                !universeIdElement.TryGetInt64(out var universeId))
                return;

            if (token.IsCancellationRequested) return;

            await UniverseDetails.FetchBulk(universeId.ToString(CultureInfo.InvariantCulture));
            if (token.IsCancellationRequested) return;

            var details = UniverseDetails.LoadFromCache(universeId);

            var item = new QuickPlayGameItem
            {
                UniverseId = universeId,
                PlaceId = placeId,
                Name = details?.Data?.Name ?? Strings.Menu_QuickPlay_UnknownGame,
                Creator = details?.Data?.Creator?.Name ?? Strings.Common_Unknown,
                Playing = details?.Data?.Playing ?? 0,
                Visits = details?.Data?.Visits ?? 0,
                OriginalDetails = details,
                Source = GameSource.None,
                IsVerified = details?.Data?.Creator?.HasVerifiedBadge ?? false
            };

            var thumbRequests = new List<ThumbnailRequest>
            {
                new()
                {
                    TargetId = (ulong)universeId,
                    Type = ThumbnailType.GameIcon,
                    Size = "150x150",
                    Format = ThumbnailFormat.Png
                }
            };

            var urls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, token);
            if (token.IsCancellationRequested) return;
            if (urls.Length > 0 && !string.IsNullOrEmpty(urls[0]))
                item.ThumbnailUrl = urls[0]!;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!token.IsCancellationRequested)
                    SelectedGame = item;
            }, DispatcherPriority.Background, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to load selected game info: {ex.Message}");
        }
    }

    private async Task DebouncedSearchTriggerAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(600, token);
            if (!token.IsCancellationRequested && !string.IsNullOrWhiteSpace(SearchQuery))
            {
                await SearchGamesAsync(token);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsSearchFlyoutOpen = SearchResults.Count > 0 && !string.IsNullOrWhiteSpace(SearchQuery);
                }, DispatcherPriority.Background, token);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task SearchGamesAsync(CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;
        if (long.TryParse(SearchQuery, out _)) return;

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

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SearchResults.Clear();
                foreach (var res in results) SearchResults.Add(res);
                IsSearchFlyoutOpen = SearchResults.Count > 0 && !string.IsNullOrWhiteSpace(SearchQuery);
            }, DispatcherPriority.Background, token);
        }
        catch (Exception ex) { App.Logger.Error($"Search error: {ex.Message}"); }
        finally { IsGameSearchLoading = false; }
    }

    private async Task LoadRecentGamesAsync()
    {
        try
        {
            var localGames = await LoadLocalGamesAsync();

            List<QuickPlayGameItem> apiGames = [];
            if (HasActiveAccount)
            {
                apiGames = await FetchRecentlyVisitedFromApiAsync();
                _ = Task.Delay(2000).ContinueWith(_ => RefreshApiGamesInBackground(), TaskScheduler.Default);
            }

            await SetRecentGamesFromSources(localGames, apiGames);
            _recentGamesLoaded = true;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to load recent games: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<List<QuickPlayGameItem>> LoadLocalGamesAsync()
    {
        var universeIds = _allHistory.Select(x => x.UniverseId).Where(id => id > 0).Distinct().ToList();
        if (universeIds.Count > 0)
            await UniverseDetails.FetchBulk(string.Join(",", universeIds));

        var history = _allHistory;
        var localGames = await Task.Run(() =>
        {
            var list = new List<QuickPlayGameItem>();
            foreach (var entry in history)
            {
                if (entry.UniverseId == 0) continue;

                var details = UniverseDetails.LoadFromCache(entry.UniverseId);
                var lastSession = entry.Servers.OrderByDescending(s => s.JoinedAt).FirstOrDefault();
                list.Add(new QuickPlayGameItem
                {
                    UniverseId = entry.UniverseId,
                    PlaceId = entry.PlaceId,
                    Name = details?.Data?.Name ?? Strings.Menu_QuickPlay_UnknownGame,
                    Creator = details?.Data?.Creator?.Name ?? Strings.Common_Unknown,
                    Playing = details?.Data?.Playing ?? 0,
                    Visits = details?.Data?.Visits ?? 0,
                    ServerCount = entry.Servers.Count,
                    LastJobId = lastSession?.JobId,
                    OriginalDetails = details,
                    Source = GameSource.Tracked,
                    LastPlayedTicks = lastSession?.JoinedAt.Ticks ?? 0,
                    IsVerified = details?.Data?.Creator?.HasVerifiedBadge ?? false
                });
            }
            return list;
        });

        return localGames;
    }

    private static List<GameHistoryEntry> LoadLocalHistory(string cachePath)
    {
        try
        {
            if (!File.Exists(cachePath)) return [];

            string json = File.ReadAllText(cachePath);
            var entries = JsonSerializer.Deserialize<List<GameHistoryEntry>>(json, HistoryLoadOptions) ?? [];

            var validEntries = entries.Where(e => e.UniverseId > 0).ToList();
            if (validEntries.Count != entries.Count)
            {
                try
                {
                    var cleanJson = JsonSerializer.Serialize(validEntries, HistoryLoadOptions);
                    File.WriteAllText(cachePath, cleanJson);
                    App.Logger.Info("Cleaned GameHistory.json by removing entries with UniverseId = 0");
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to rewrite cleaned cache: {ex.Message}");
                }
            }
            return validEntries;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Error loading history: {ex.Message}");
            return [];
        }
    }

    private static async Task<List<QuickPlayGameItem>> FetchRecentlyVisitedFromApiAsync()
    {
        var accountManager = AccountManager.Shared;
        if (accountManager?.ActiveAccount == null) return [];

        string? cookie = accountManager.GetRoblosecurityForUser(accountManager.ActiveAccount.UserId);
        if (string.IsNullOrEmpty(cookie)) return [];

        var url = UrlBuilder.BuildApiUrl("apis", "search-landing-page-api/v1?sessionId=Meddsam");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");

        try
        {
            using var client = new HttpClient();
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<SearchLandingResponse>(json);
            var recentSort = result?.Sorts?.FirstOrDefault(s => s.SortId == "RecentlyVisited");
            if (recentSort?.Games == null) return [];

            var games = new List<QuickPlayGameItem>();
            long baseTicks = DateTime.UtcNow.Ticks;
            for (int i = 0; i < recentSort.Games.Count; i++)
            {
                var apiGame = recentSort.Games[i];
                if (apiGame.UniverseId == 0) continue;
                games.Add(new QuickPlayGameItem
                {
                    UniverseId = apiGame.UniverseId,
                    PlaceId = apiGame.RootPlaceId,
                    Name = apiGame.Name ?? Strings.Menu_QuickPlay_UnknownGame,
                    Playing = apiGame.PlayerCount,
                    Source = GameSource.RobloxApi,
                    LastPlayedTicks = baseTicks - i
                });
            }
            return games;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to fetch recent games API: {ex.Message}");
            return [];
        }
    }

    private static List<QuickPlayGameItem> MergeByApiOrder(List<QuickPlayGameItem> localGames, List<QuickPlayGameItem> apiGames)
    {
        var validLocal = localGames.Where(g => g.UniverseId != 0).ToList();
        var validApi = apiGames.Where(g => g.UniverseId != 0).ToList();

        var localByUniverse = validLocal
            .GroupBy(g => g.UniverseId)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<QuickPlayGameItem>(validApi.Count);

        foreach (var apiGame in validApi)
        {
            if (localByUniverse.TryGetValue(apiGame.UniverseId, out var localGame))
            {
                localGame.LastPlayedTicks = Math.Max(localGame.LastPlayedTicks, apiGame.LastPlayedTicks);
                result.Add(localGame);
                localByUniverse.Remove(apiGame.UniverseId);
            }
            else
            {
                result.Add(apiGame);
            }
        }

        var apiUniverseIds = new HashSet<long>(validApi.Select(g => g.UniverseId));
        result.AddRange(validLocal.Where(g => !apiUniverseIds.Contains(g.UniverseId)));

        return result;
    }

    private static async Task EnrichGamesWithDetails(List<QuickPlayGameItem> games)
    {
        var needDetails = games.Where(g => g.OriginalDetails == null && g.UniverseId > 0).ToList();
        if (needDetails.Count > 0)
        {
            var ids = needDetails.Select(g => g.UniverseId.ToString(CultureInfo.InvariantCulture)).ToList();
            await UniverseDetails.FetchBulk(string.Join(",", ids));
            foreach (var game in needDetails)
            {
                var details = UniverseDetails.LoadFromCache(game.UniverseId);
                if (details?.Data != null)
                {
                    game.OriginalDetails = details;
                    game.Creator = details.Data.Creator?.Name ?? Strings.Common_Unknown;
                    if (string.IsNullOrEmpty(game.Name)) game.Name = details.Data.Name ?? Strings.Menu_QuickPlay_UnknownGame;
                    game.Playing = details.Data.Playing;
                    game.Visits = details.Data.Visits;
                    if (game.PlaceId == 0) game.PlaceId = details.Data.RootPlaceId;
                    game.IsVerified = details.Data.Creator?.HasVerifiedBadge ?? false;
                }
            }
        }

        var needThumb = games.Where(g => string.IsNullOrEmpty(g.ThumbnailUrl)).ToList();
        if (needThumb.Count > 0)
            await FetchThumbnailsForGames(needThumb);
    }

    private static async Task FetchThumbnailsForGames(List<QuickPlayGameItem> games)
    {
        var thumbRequests = games
            .Where(item => item.UniverseId != 0)
            .Select(item => new ThumbnailRequest
            {
                TargetId = (ulong)item.UniverseId,
                Type = ThumbnailType.GameIcon,
                Size = "150x150",
                Format = ThumbnailFormat.Png
            }).ToList();

        try
        {
            var urls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, CancellationToken.None);
            for (int i = 0; i < games.Count; i++)
            {
                string? url = urls.ElementAtOrDefault(i);
                if (!string.IsNullOrEmpty(url))
                    games[i].ThumbnailUrl = url;
            }
        }
        catch (Exception ex) { App.Logger.Error($"Thumbnail fetch failed: {ex.Message}"); }
    }

    private async Task SetRecentGamesFromSources(List<QuickPlayGameItem> localGames, List<QuickPlayGameItem> apiGames)
    {
        var merged = await Task.Run(() => MergeByApiOrder(localGames, apiGames));
        await EnrichGamesWithDetails(merged);

        const int batchSize = 6;

        await Dispatcher.UIThread.InvokeAsync(RecentGames.Clear, DispatcherPriority.Background);

        for (int i = 0; i < merged.Count; i += batchSize)
        {
            var batch = merged.Skip(i).Take(batchSize).ToList();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var game in batch)
                    RecentGames.Add(game);
            }, DispatcherPriority.Background);

            await Task.Yield();
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(HasRecentGames));
            OnPropertyChanged(nameof(ShowRecentEmpty));
        }, DispatcherPriority.Background);
    }

    private async Task RefreshApiGamesInBackground()
    {
        if (!HasActiveAccount) return;

        try
        {
            var freshApiGames = await FetchRecentlyVisitedFromApiAsync();
            if (freshApiGames.Count == 0) return;

            if (SelectedTab == QuickPlayTab.Continue && _recentGamesLoaded)
            {
                var localGames = await LoadLocalGamesAsync();
                await SetRecentGamesFromSources(localGames, freshApiGames);
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Background refresh failed: {ex.Message}");
        }
    }

    private async void OnActiveAccountChanged(AccountManagerAccount? account)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            _recentGamesLoaded = false;
            _favoritesLoaded = false;
            _recommendationsLoaded = false;

            RecentGames.Clear();
            FavoriteGames.Clear();
            RecommendedGames.Clear();
            OnPropertyChanged(nameof(HasRecentGames));
            OnPropertyChanged(nameof(HasFavoriteGames));
            OnPropertyChanged(nameof(HasRecommendedGames));
            OnPropertyChanged(nameof(ShowRecentEmpty));
            OnPropertyChanged(nameof(ShowRecommendedEmpty));

            _allHistory = await Task.Run(() => LoadLocalHistory(_cachePath));

            if (account != null)
            {
                _ = Task.Delay(2000).ContinueWith(_ => RefreshApiGamesInBackground(), TaskScheduler.Default);

                if (SelectedTab == QuickPlayTab.Continue)
                {
                    IsLoading = true;
                    await LoadRecentGamesAsync();
                }
                else if (SelectedTab == QuickPlayTab.Favorites)
                {
                    IsFavoritesLoading = true;
                    await LoadFavoriteGamesAsync();
                }
                else if (SelectedTab == QuickPlayTab.Recommended)
                {
                    IsRecommendedLoading = true;
                    await LoadRecommendedGamesAsync();
                }
            }
            else
            {
                if (SelectedTab == QuickPlayTab.Continue)
                {
                    IsLoading = true;
                    await LoadRecentGamesAsync();
                }
            }

            OnPropertyChanged(nameof(IsLoggedIn));
        });
    }

    private async Task LoadFavoriteGamesAsync()
    {
        if (!HasActiveAccount)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                FavoriteGames.Clear();
                OnPropertyChanged(nameof(HasFavoriteGames));
            });
            _favoritesLoaded = true;
            IsFavoritesLoading = false;
            return;
        }

        try
        {
            var games = await FetchFavoritesFromApiAsync(AccountManager.Shared!.ActiveAccount!.UserId);
            await EnrichGamesWithDetails(games);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                FavoriteGames.Clear();
                foreach (var g in games)
                    FavoriteGames.Add(g);
                OnPropertyChanged(nameof(HasFavoriteGames));
                OnPropertyChanged(nameof(ShowFavoritesEmpty));
            });
            _favoritesLoaded = true;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to load favorite games: {ex.Message}");
            _favoritesLoaded = false;
        }
        finally
        {
            IsFavoritesLoading = false;
        }
    }

    private static async Task<List<QuickPlayGameItem>> FetchFavoritesFromApiAsync(long userId)
    {
        var accountManager = AccountManager.Shared;
        if (accountManager?.ActiveAccount == null) return [];

        string? cookie = accountManager.GetRoblosecurityForUser(accountManager.ActiveAccount.UserId);
        if (string.IsNullOrEmpty(cookie)) return [];

        var url = UrlBuilder.BuildApiUrl("games", $"v2/users/{userId}/favorite/games?accessFilter=0&limit=100&sortOrder=Desc");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");

        var response = await Http.SendJson<FavoriteGamesResponse>(request);
        if (response?.Data == null) return [];

        var games = new List<QuickPlayGameItem>();
        foreach (var fav in response.Data)
        {
            if (fav.Id == 0 || fav.RootPlace == null || fav.RootPlace.Id == 0) continue;
            games.Add(new QuickPlayGameItem
            {
                UniverseId = fav.Id,
                PlaceId = fav.RootPlace.Id,
                Name = fav.Name ?? Strings.Menu_QuickPlay_UnknownGame,
                Creator = fav.Creator?.Name ?? Strings.Common_Unknown,
                Visits = fav.PlaceVisits,
                Source = GameSource.None
            });
        }
        return games;
    }

    private async Task LoadRecommendedGamesAsync()
    {
        if (!HasActiveAccount)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RecommendedGames.Clear();
                OnPropertyChanged(nameof(HasRecommendedGames));
                OnPropertyChanged(nameof(ShowRecommendedEmpty));
            });
            _recommendationsLoaded = true;
            IsRecommendedLoading = false;
            return;
        }

        try
        {
            var games = await FetchRecommendedFromApiAsync();
            if (games.Count > 50)
                games = [.. games.Take(50)];

            await EnrichGamesWithDetails(games);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RecommendedGames.Clear();
                foreach (var g in games)
                    RecommendedGames.Add(g);
                OnPropertyChanged(nameof(HasRecommendedGames));
                OnPropertyChanged(nameof(ShowRecommendedEmpty));
            });
            _recommendationsLoaded = true;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to load recommended games: {ex.Message}");
            _recommendationsLoaded = false;
        }
        finally
        {
            IsRecommendedLoading = false;
        }
    }

    private static async Task<List<QuickPlayGameItem>> FetchRecommendedFromApiAsync()
    {
        var accountManager = AccountManager.Shared;
        if (accountManager?.ActiveAccount == null) return [];

        string? cookie = accountManager.GetRoblosecurityForUser(accountManager.ActiveAccount.UserId);
        if (string.IsNullOrEmpty(cookie)) return [];

        var url = "https://apis.roblox.com/discovery-api/omni-recommendation";

        var payload = new
        {
            pageType = "Home",
            sessionId = Guid.NewGuid().ToString(),
            supportedTreatmentTypes = new[] { "SortlessGrid" },
            sduiTreatmentTypes = new[] { "Carousel", "HeroUnit" },
            cpuCores = Environment.ProcessorCount,
            maxResolution = "2560x1440",
            maxMemory = 32768,
            networkType = "4g"
        };

        var jsonPayload = JsonSerializer.Serialize(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
        request.Headers.Add("User-Agent", "Roblox/WinInet");
        request.Headers.Add("Referer", "https://www.roblox.com/home");

        var response = await Http.SendJson<OmniRecommendationResponse>(request);
        if (response?.Sorts == null) return [];

        var recommendedSort = response.Sorts
            .FirstOrDefault(s => s.TopicId == 100000000 &&
                                 s.TreatmentType == "SortlessGrid" &&
                                 s.RecommendationList != null);

        if (recommendedSort?.RecommendationList == null) return [];

        var gameItems = new List<QuickPlayGameItem>();
        foreach (var rec in recommendedSort.RecommendationList)
        {
            if (rec.ContentType != "Game") continue;
            if (response.ContentMetadata?.Game != null &&
                response.ContentMetadata.Game.TryGetValue(rec.ContentId.ToString(CultureInfo.InvariantCulture), out var details))
            {
                gameItems.Add(new QuickPlayGameItem
                {
                    UniverseId = rec.ContentId,
                    PlaceId = details.RootPlaceId,
                    Name = details.Name ?? Strings.Menu_QuickPlay_UnknownGame,
                    Creator = "Unknown",
                    Playing = details.PlayerCount,
                    Visits = details.TotalUpVotes + details.TotalDownVotes,
                    Source = GameSource.None
                });
            }
            else
            {
                gameItems.Add(new QuickPlayGameItem
                {
                    UniverseId = rec.ContentId,
                    PlaceId = rec.ContentId,
                    Name = Strings.Menu_QuickPlay_UnknownGame,
                    Source = GameSource.None
                });
            }
        }

        return gameItems;
    }

    private async Task FetchSubplacesAsync(long universeId)
    {
        try
        {
            IsLoadingSubplaces = true;
            Subplaces.Clear();

            Uri url = UrlBuilder.BuildApiUrl(
                "develop",
                $"v1/universes/{universeId}/places?isUniverseCreation=false&limit=100&sortOrder=Asc"
            );

            var subplacesResponse = await Http.GetJson<SubplacesResponse>(url);

            if (subplacesResponse?.Data != null && subplacesResponse.Data.Count > 0)
            {
                var tempSubplaces = subplacesResponse.Data
                    .Select(place => new PlaceInfo(place.Id, place.UniverseId, place.Name, ""))
                    .ToList();

                var thumbRequests = tempSubplaces.Select(p => new ThumbnailRequest
                {
                    TargetId = (ulong)p.Id,
                    Type = ThumbnailType.PlaceIcon,
                    Size = "150x150",
                    Format = ThumbnailFormat.Png
                }).ToList();

                try
                {
                    var urls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, CancellationToken.None);
                    for (int i = 0; i < tempSubplaces.Count; i++)
                    {
                        tempSubplaces[i].ThumbnailUrl = urls.ElementAtOrDefault(i) ?? "";
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Subplace thumbnail fetch failed: {ex.Message}");
                }

                foreach (var p in tempSubplaces) Subplaces.Add(p);
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Subplace fetch failed: {ex.Message}");
        }
        finally
        {
            IsLoadingSubplaces = false;
        }
    }

    private async Task ShowPrivateServersForGameAsync()
    {
        if (_currentPrivateServersPlaceId == 0) return;

        var accountManager = AccountManager.Shared;
        if (accountManager is null)
        {
            _ = Frontend.ShowMessageBox(Strings.Menu_QuickPlay_AccountManagerNotAvailable, MessageBoxImage.Error);
            return;
        }

        var activeAccount = accountManager.ActiveAccount;
        if (activeAccount == null)
        {
            _ = Frontend.ShowMessageBox(Strings.Menu_QuickPlay_PleaseSelectAccount, MessageBoxImage.Warning);
            return;
        }

        IsLoadingPrivateServers = true;
        IsPrivateServersOverlayVisible = true;
        PrivateServers.Clear();
        ArePrivateServersEmpty = false;

        try
        {
            string? cookie = accountManager.GetRoblosecurityForUser(activeAccount.UserId);
            if (string.IsNullOrEmpty(cookie))
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_QuickPlay_UnableToAuthenticate, MessageBoxImage.Warning);
                return;
            }

            Uri url = UrlBuilder.BuildApiUrl(
                "games",
                $"v1/games/{_currentPrivateServersPlaceId}/private-servers?excludeFriendServers=false&sortOrder=Asc"
            );

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
            request.Headers.Add("Origin", "https://www.roblox.com");
            request.Headers.Add("Referrer", "https://www.roblox.com");

            var response = await Http.SendJson<PrivateServersResponse>(request);
            if (response?.Data == null || response.Data.Count == 0)
            {
                ArePrivateServersEmpty = true;
                return;
            }

            var ownerIds = response.Data
                .Select(s => s.Owner.Id)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            var avatarUrls = new Dictionary<long, string?>();
            if (ownerIds.Count > 0)
            {
                var results = await accountManager.GetAvatarUrlsBulkAsync(ownerIds);
                avatarUrls = results;
            }

            var servers = new List<PrivateServerInfo>();
            foreach (var server in response.Data)
            {
                string? avatarUrl = avatarUrls.GetValueOrDefault(server.Owner.Id);
                servers.Add(new PrivateServerInfo(
                    server.VipServerId,
                    server.AccessCode,
                    server.Name,
                    server.Owner.Id,
                    server.Owner.Name,
                    avatarUrl,
                    server.MaxPlayers,
                    server.Players.Count
                ));
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var server in servers)
                    PrivateServers.Add(server);
                ArePrivateServersEmpty = servers.Count == 0;
            });
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Exception in ShowPrivateServersForGameAsync: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() => ArePrivateServersEmpty = true);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoadingPrivateServers = false);
        }
    }

    private static async Task<List<ServerInfo>> FetchServersForGameAsync(long placeId)
    {
        using var fetcher = new RobloxServerFetcher();
        var result = await fetcher.FetchServerInstancesAsync(placeId, maxServers: 15);
        if (result.Servers == null || result.Servers.Count == 0)
            return [];

        var servers = new List<ServerInfo>(result.Servers.Count);
        foreach (var s in result.Servers)
        {
            if (string.IsNullOrEmpty(s.Region) ||
                s.Region.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                continue;

            var si = new ServerInfo
            {
                JobId = s.Id,
                Region = s.Region,
                JoinedAt = s.FirstSeen ?? DateTime.UtcNow,
                IsLatest = false,
                Playing = s.Playing,
                MaxPlayers = s.MaxPlayers,
                Uptime = s.UptimeDisplay,
            };

            if (s.PlayerTokens is { Count: > 0 })
                foreach (var t in s.PlayerTokens)
                    si.PlayerTokens.Add(t);

            servers.Add(si);
        }

        await LoadPlayerThumbnailsAsync(servers);
        return servers;
    }

    private static async Task LoadPlayerThumbnailsAsync(List<ServerInfo> servers)
    {
        var allTokens = servers
            .SelectMany(s => s.PlayerTokens)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .ToList();

        if (allTokens.Count == 0) return;

        const int batchSize = 100;
        var tokenToUrl = new Dictionary<string, string?>();

        var chunks = allTokens
            .Select((t, i) => new { Token = t, Index = i })
            .GroupBy(x => x.Index / batchSize)
            .Select(g => g.Select(x => x.Token).ToList())
            .ToList();

        foreach (var chunk in chunks)
        {
            var requests = chunk.Select(token => new ThumbnailRequest
            {
                Token = token,
                Type = ThumbnailType.AvatarHeadShot,
                Size = "60x60",
                Format = ThumbnailFormat.Png,
                IsCircular = true
            }).ToList();

            var urls = await Thumbnails.GetThumbnailUrlsAsync(requests, CancellationToken.None);
            for (int i = 0; i < chunk.Count && i < urls.Length; i++)
                tokenToUrl[chunk[i]] = urls[i];
        }

        using var semaphore = new SemaphoreSlim(15);
        var tasks = servers.Select(async server =>
        {
            await semaphore.WaitAsync();
            try
            {
                var bitmaps = new List<Bitmap>();
                foreach (var playerToken in server.PlayerTokens)
                {
                    if (tokenToUrl.TryGetValue(playerToken, out var url) &&
                        !string.IsNullOrEmpty(url))
                    {
                        try
                        {
                            var bytes = await App.HttpClient.GetByteArrayAsync(new Uri(url));
                            using var ms = new MemoryStream(bytes);
                            bitmaps.Add(new Bitmap(ms));
                        }
                        catch { }
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    server.PlayerAvatarThumbnails.Clear();
                    foreach (var bmp in bitmaps)
                        server.PlayerAvatarThumbnails.Add(bmp);

                    int extra = server.Playing - bitmaps.Count;
                    if (extra > 0)
                    {
                        server.ExtraPlayersText = $"+{extra}";
                        server.HasExtraPlayers = true;
                    }
                    else
                    {
                        server.HasExtraPlayers = false;
                    }
                }, DispatcherPriority.Background);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private static void LaunchRoblox(long placeId, string? jobId = null, string? accessCode = null)
    {
        if (placeId == 0) return;
        string deeplink = $"roblox://experiences/start?placeId={placeId}";

        if (!string.IsNullOrEmpty(accessCode))
            deeplink += "&accessCode=" + Uri.EscapeDataString(accessCode);
        else if (!string.IsNullOrEmpty(jobId))
            deeplink += "&gameInstanceId=" + Uri.EscapeDataString(jobId);

        Process.Start(new ProcessStartInfo(deeplink) { UseShellExecute = true });
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
#pragma warning disable CA1849
            _searchDebounceCts?.Cancel();
            _gameInfoCts?.Cancel();
#pragma warning restore CA1849
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = null;

            _gameInfoCts?.Dispose();
            _gameInfoCts = null;

            AccountManager.Shared.ActiveAccountChanged -= OnActiveAccountChanged;
        }

        _disposed = true;
    }
}