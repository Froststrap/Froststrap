using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public record PrivateServerInfo(
        long VipServerId,
        string AccessCode,
        string Name,
        long OwnerId,
        string OwnerName,
        string? OwnerAvatarUrl,
        int MaxPlayers,
        int CurrentPlayers);

    public class QuickPlayViewModel : NotifyPropertyChangedViewModel
    {
        private bool _isLoading;
        private bool _isOverlayVisible;
        private bool _isSubplacesOverlayVisible;
        private bool _isLoadingSubplaces;
        private readonly ObservableCollection<PlaceInfo> _subplaces = [];
        private UniverseDetails? _selectedUniverseDetails;
        private readonly string _cachePath = Path.Combine(Paths.Cache, "GameHistory.json");
        private List<GameHistoryEntry> _allHistory = [];

        private bool _isPrivateServersOverlayVisible;
        private bool _arePrivateServersEmpty;
        private bool _isLoadingPrivateServers;
        private long _currentPrivateServersPlaceId;
        private bool _isCurrentGameApi;
        private bool _isLoadingServers;
        private bool _isJoiningBestRegion;

        private readonly ObservableCollection<PrivateServerInfo> _privateServers = [];

        public ObservableCollection<QuickPlayGameItem> RecentGames { get; } = [];
        public ObservableCollection<ServerInfo> SelectedGameServers { get; } = [];

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
            set => SetProperty(ref _isLoading, value);
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
            set => SetProperty(ref _isCurrentGameApi, value);
        }

        public bool IsJoiningBestRegion
        {
            get => _isJoiningBestRegion;
            set => SetProperty(ref _isJoiningBestRegion, value);
        }

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
                    var fetcher = new RobloxServerFetcher();
                    bool success = await fetcher.JoinBestServerAsync(
                        item.PlaceId,
                        App.Settings.Prop.JoinSmallerServer,
                        App.Settings.Prop.BestRegionAmounts,
                        App.Settings.Prop.MaxServerCheck,
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

            AccountManager.Shared.ActiveAccountChanged += _ =>
            {
                Dispatcher.UIThread.InvokeAsync(() => OnPropertyChanged(nameof(HasActiveAccount)));
            };

            AccountManager.Shared.ActiveAccountChanged += OnActiveAccountChanged;
            _ = Initialize();
        }

        private async Task Initialize()
        {
            IsLoading = true;

            _allHistory = LoadLocalHistory(_cachePath);
            await LoadLocalGamesIntoList();

            if (HasActiveAccount)
            {
                await LoadApiGamesAndMerge();
            }

            IsLoading = false;

            if (HasActiveAccount)
            {
                _ = RefreshApiGamesInBackground();
            }
        }

        private async Task LoadLocalGamesIntoList()
        {
            var universeIds = _allHistory.Select(x => x.UniverseId).Where(id => id > 0).Distinct().ToList();
            if (universeIds.Count > 0)
                await UniverseDetails.FetchBulk(string.Join(",", universeIds));

            var localGames = new List<QuickPlayGameItem>();
            foreach (var entry in _allHistory)
            {
                if (entry.UniverseId == 0) continue;

                var details = UniverseDetails.LoadFromCache(entry.UniverseId);
                var lastSession = entry.Servers.OrderByDescending(s => s.JoinedAt).FirstOrDefault();
                localGames.Add(new QuickPlayGameItem
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
                    LastPlayedTicks = lastSession?.JoinedAt.Ticks ?? 0
                });
            }

            await FetchThumbnailsForGames(localGames);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RecentGames.Clear();
                foreach (var game in localGames) RecentGames.Add(game);
            });
        }

        private async Task LoadApiGamesAndMerge()
        {
            var apiGames = await GetCachedApiGamesAsync();
            if (apiGames.Count == 0)
            {
                apiGames = await FetchRecentlyVisitedFromApiAsync();
                if (apiGames.Count > 0)
                    await SetCachedApiGamesAsync(apiGames);
            }

            var currentLocal = RecentGames.ToList();
            var merged = MergeByApiOrder(currentLocal, apiGames);
            await EnrichGamesWithDetails(merged);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RecentGames.Clear();
                foreach (var game in merged) RecentGames.Add(game);
            });
        }

        private static string GetApiGamesCachePath()
        {
            var activeUserId = AccountManager.Shared?.ActiveAccount?.UserId;
            if (activeUserId.HasValue)
                return Path.Combine(Paths.Cache, $"ApiRecentGames_{activeUserId.Value}.json");
            else
                return Path.Combine(Paths.Cache, "ApiRecentGames_empty.json");
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
            catch (Exception ex) { Debug.WriteLine($"Thumbnail fetch failed: {ex.Message}"); }
        }

        private static async Task<List<QuickPlayGameItem>> GetCachedApiGamesAsync()
        {
            try
            {
                string cachePath = GetApiGamesCachePath();
                if (!File.Exists(cachePath)) return [];
                var json = await File.ReadAllTextAsync(cachePath);
                return JsonSerializer.Deserialize<List<QuickPlayGameItem>>(json) ?? [];
            }
            catch { return []; }
        }

        private static async Task SetCachedApiGamesAsync(List<QuickPlayGameItem> games)
        {
            try
            {
                string cachePath = GetApiGamesCachePath();
                var json = JsonSerializer.Serialize(games);
                await File.WriteAllTextAsync(cachePath, json);
            }
            catch (Exception ex) { App.Logger.WriteLine("QuickPlayViewModel", $"Failed to cache API games: {ex.Message}"); }
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
                    result.Add(localGame);
                else
                    result.Add(apiGame);
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
                var ids = needDetails.Select(g => g.UniverseId.ToString()).ToList();
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
                    }
                }
            }

            var needThumb = games.Where(g => string.IsNullOrEmpty(g.ThumbnailUrl)).ToList();
            if (needThumb.Count > 0)
                await FetchThumbnailsForGames(needThumb);
        }


        private async Task RefreshApiGamesInBackground()
        {
            if (!HasActiveAccount) return;

            try
            {
                var freshApiGames = await FetchRecentlyVisitedFromApiAsync();
                if (freshApiGames.Count == 0) return;

                await SetCachedApiGamesAsync(freshApiGames);

                _allHistory = LoadLocalHistory(_cachePath);
                var universeIds = _allHistory.Select(x => x.UniverseId).Where(id => id > 0).Distinct().ToList();
                if (universeIds.Count > 0)
                    await UniverseDetails.FetchBulk(string.Join(",", universeIds));

                var localGames = new List<QuickPlayGameItem>();
                foreach (var entry in _allHistory)
                {
                    if (entry.UniverseId == 0) continue;

                    var details = UniverseDetails.LoadFromCache(entry.UniverseId);
                    var lastSession = entry.Servers.OrderByDescending(s => s.JoinedAt).FirstOrDefault();
                    localGames.Add(new QuickPlayGameItem
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
                        LastPlayedTicks = lastSession?.JoinedAt.Ticks ?? 0
                    });
                }

                var merged = MergeByApiOrder(localGames, freshApiGames);
                await EnrichGamesWithDetails(merged);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RecentGames.Clear();
                    foreach (var game in merged) RecentGames.Add(game);
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("QuickPlayViewModel", $"Background refresh failed: {ex.Message}");
            }
        }

        private static List<GameHistoryEntry> LoadLocalHistory(string cachePath)
        {
            try
            {
                if (!File.Exists(cachePath)) return [];

                string json = File.ReadAllText(cachePath);
                var entries = JsonSerializer.Deserialize<List<GameHistoryEntry>>(json) ?? [];

                var validEntries = entries.Where(e => e.UniverseId > 0).ToList();

                if (validEntries.Count != entries.Count)
                {
                    try
                    {
                        var cleanJson = JsonSerializer.Serialize(validEntries);
                        File.WriteAllText(cachePath, cleanJson);
                        App.Logger.WriteLine("QuickPlayViewModel", "Cleaned GameHistory.json by removing entries with UniverseId = 0");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("QuickPlayViewModel", $"Failed to rewrite cleaned cache: {ex.Message}");
                    }
                }

                return validEntries;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("QuickPlayViewModel", $"Error loading history: {ex.Message}");
                return [];
            }
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
                        App.Logger.WriteLine("QuickPlayViewModel", $"Subplace thumbnail fetch failed: {ex.Message}");
                    }

                    foreach (var p in tempSubplaces) Subplaces.Add(p);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("QuickPlayViewModel", $"Subplace fetch failed: {ex.Message}");
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
                App.Logger.WriteLine("QuickPlayViewModel", $"Exception in ShowPrivateServersForGameAsync: {ex.Message}");
                await Dispatcher.UIThread.InvokeAsync(() => ArePrivateServersEmpty = true);
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsLoadingPrivateServers = false);
            }
        }

        private static async Task<List<QuickPlayGameItem>> FetchRecentlyVisitedFromApiAsync()
        {
            var accountManager = AccountManager.Shared;
            if (accountManager == null) return [];
            var activeAccount = accountManager.ActiveAccount;
            if (activeAccount == null) return [];

            string? cookie = accountManager.GetRoblosecurityForUser(activeAccount.UserId);
            if (string.IsNullOrEmpty(cookie)) return [];

            var url = UrlBuilder.BuildApiUrl("apis", "search-landing-page-api/v1?sessionId=Meddsam");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");

            var response = await Http.SendJson<RecentlyVisitedResponse>(request);
            var recentSort = response?.Sorts?.FirstOrDefault(s => s.SortId == "RecentlyVisited");
            if (recentSort?.Games == null) return [];

            var games = new List<QuickPlayGameItem>(capacity: recentSort.Games.Count);
            long baseTicks = DateTime.UtcNow.Ticks;

            for (int i = 0; i < recentSort.Games.Count; i++)
            {
                var apiGame = recentSort.Games[i];
                if (apiGame.UniverseId == 0) continue;
                games.Add(new QuickPlayGameItem
                {
                    UniverseId = apiGame.UniverseId,
                    PlaceId = apiGame.RootPlaceId,
                    Name = apiGame.Name,
                    Playing = apiGame.PlayerCount,
                    Source = GameSource.RobloxApi,
                    LastPlayedTicks = baseTicks - i
                });
            }
            return games;
        }

        private static async Task<List<ServerInfo>> FetchServersForGameAsync(long placeId)
        {
            var fetcher = new RobloxServerFetcher();
            var result = await fetcher.FetchServerInstancesAsync(placeId);
            if (result.Servers == null || result.Servers.Count == 0)
                return [];

            return [.. result.Servers.Select(s => new ServerInfo
            {
                JobId = s.Id,
                Region = s.Region,
                JoinedAt = s.FirstSeen ?? DateTime.UtcNow,
                IsLatest = false
            })];
        }

        private async void OnActiveAccountChanged(AccountManagerAccount? account)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                IsLoading = true;
                try
                {
                    _allHistory = LoadLocalHistory(_cachePath);
                    await LoadLocalGamesIntoList();

                    if (account != null)
                    {
                        await LoadApiGamesAndMerge();
                        _ = RefreshApiGamesInBackground();
                    }
                }
                finally
                {
                    IsLoading = false;
                }
            });
        }

        private static void LaunchRoblox(long placeId, string? jobId = null, string? accessCode = null)
        {
            if (placeId == 0) return;

            string deeplink = $"roblox://experiences/start?placeId={placeId}";

            if (!string.IsNullOrEmpty(accessCode))
            {
                deeplink += "&accessCode=" + Uri.EscapeDataString(accessCode);
            }
            else if (!string.IsNullOrEmpty(jobId))
            {
                deeplink += "&gameInstanceId=" + Uri.EscapeDataString(jobId);
            }

            Process.Start(new ProcessStartInfo(deeplink) { UseShellExecute = true });
        }
    }
}