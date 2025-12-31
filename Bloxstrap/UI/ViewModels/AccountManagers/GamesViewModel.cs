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

using Bloxstrap.Integrations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace Bloxstrap.UI.ViewModels.AccountManagers
{
    public record PlaceDetails(string name, string builder, bool hasVerifiedBadge, long universeId);
    public record PlaceInfo(long Id, long UniverseId, string Name, string? ThumbnailUrl);
    public record RecentGameInfo(long UniverseId, long RootPlaceId, string Name, int? Playing, long? Visits, string ThumbnailUrl);

    public class SortOrderComboBoxItem
    {
        public string Content { get; set; } = "";
        public int Tag { get; set; }
    }

    public partial class GamesViewModel : ObservableObject
    {
        private const string LOG_IDENT = "GamesViewModel";

        [ObservableProperty]
        private string _placeId = "";

        [ObservableProperty]
        private string _searchQuery = "";

        [ObservableProperty]
        private ObservableCollection<GameSearchResult> _searchResults = new();

        [ObservableProperty]
        private GameSearchResult? _selectedSearchResult;

        [ObservableProperty]
        private string _serverId = "";

        [ObservableProperty]
        private ObservableCollection<RecentGameInfo> _discoveryGames = new();

        [ObservableProperty]
        private ObservableCollection<RecentGameInfo> _continuePlayingGames = new();

        [ObservableProperty]
        private bool _isLoadingContinuePlaying = false;

        [ObservableProperty]
        private ObservableCollection<PlaceInfo> _subplaces = new();

        [ObservableProperty]
        private bool _isLoadingSubplaces = false;

        [ObservableProperty]
        private ObservableCollection<string> _regions = new();

        [ObservableProperty]
        private string? _selectedRegion;

        [ObservableProperty]
        private int _selectedSortOrder = 2;

        [ObservableProperty]
        private string? _selectedGameThumbnail;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private ObservableCollection<RecentGameInfo> _favoriteGames = new();

        [ObservableProperty]
        private bool _isLoadingFavorites = false;

        [ObservableProperty]
        private string _selectedGameName = "";

        [ObservableProperty]
        private string _selectedGameCreator = "";

        [ObservableProperty]
        private bool _isSelectedGameCreatorVerified = false;

        [ObservableProperty]
        private long? _selectedGameVisits = 0;

        [ObservableProperty]
        private int? _selectedGamePlaying = 0;

        [ObservableProperty]
        private bool _isAutoSearching;

        [ObservableProperty]
        private string _autoSearchStatus = "";

        [ObservableProperty]
        private bool _isPrivateServersModalOpen;

        [ObservableProperty]
        private ObservableCollection<PrivateServerInfo> _privateServers = new();

        [ObservableProperty]
        private bool _arePrivateServersEmpty;

        public bool HasAccounts => AccountManager.Shared.Accounts.Any();
        public bool HasActiveAccount => AccountManager.Shared.ActiveAccount != null;
        public bool ShouldShowGames => HasAccounts && HasActiveAccount;
        public bool HasSubplaces => Subplaces.Any();

        public List<SortOrderComboBoxItem> SortOrderOptions => new()
        {
            new SortOrderComboBoxItem { Content = "Large Servers", Tag = 2 },
            new SortOrderComboBoxItem { Content = "Small Servers", Tag = 1 }
        };

        public record PrivateServerInfo(long VipServerId, string AccessCode, string Name, long OwnerId, string OwnerName, string? OwnerAvatarUrl, int MaxPlayers, int CurrentPlayers);

        private CancellationTokenSource? _searchDebounceCts;
        private static readonly HttpClient _http = new();

        private Elements.AccountManagers.MainWindow? GetMainWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is Elements.AccountManagers.MainWindow accountManagerWindow)
                {
                    return accountManagerWindow;
                }
            }
            return null;
        }

        public GamesViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            AccountManager.Shared.ActiveAccountChanged += OnActiveAccountChanged;

            _ = InitializeDataAsync();
        }

        private async void OnActiveAccountChanged(AltAccount? newAccount)
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    if (newAccount == null)
                    {
                        DiscoveryGames.Clear();
                        FavoriteGames.Clear();
                        ContinuePlayingGames.Clear();
                        SearchResults.Clear();
                        Subplaces.Clear();
                        ResetGameDetails();

                        OnPropertyChanged(nameof(ShouldShowGames));
                        OnPropertyChanged(nameof(HasActiveAccount));
                        return;
                    }

                    IsLoading = true;

                    try
                    {
                        var tasks = new List<Task>
                {
                    RefreshDiscoveryGames(),
                    RefreshFavoriteGames(),
                    RefreshContinuePlaying()
                };

                        await Task.WhenAll(tasks);

                        OnPropertyChanged(nameof(ShouldShowGames));
                        OnPropertyChanged(nameof(HasActiveAccount));
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::OnActiveAccountChanged", $"Exception: {ex.Message}");
            }
        }

        private async Task InitializeDataAsync()
        {
            try
            {
                await LoadDataAsync();
                await LoadRegionsAsync();
                App.Logger.WriteLine($"{LOG_IDENT}::InitializeDataAsync", "Loaded");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::InitializeDataAsync", $"Exception: {ex.Message}");
            }
        }

        private async Task LoadDataAsync()
        {
            var mgr = AccountManager.Shared;

            PlaceId = mgr.CurrentPlaceId ?? "";
            ServerId = mgr.CurrentServerInstanceId ?? "";
            SelectedRegion = mgr.SelectedRegion;

            if (mgr.ActiveAccount is not null)
            {
                _ = RefreshDiscoveryGames();
                await RefreshFavoriteGames();
                await RefreshContinuePlaying();

                if (!string.IsNullOrEmpty(PlaceId) && long.TryParse(PlaceId, out long currentPlaceId))
                {
                    _ = LoadGameThumbnailAsync(currentPlaceId);
                    _ = LoadSubplacesForSelectedGameAsync();
                }
            }
        }

        private async Task LoadRegionsAsync()
        {
            const string LOG_IDENT_REGIONS = $"{LOG_IDENT}::LoadRegions";

            Regions.Clear();

            var fetcher = new RobloxServerFetcher();

            var datacentersResult = await fetcher.GetDatacentersAsync();

            if (datacentersResult != null)
            {
                App.Logger.WriteLine(LOG_IDENT_REGIONS, "Successfully loaded datacenters from API, saving to cache...");
                await SaveDatacentersToCacheAsync(datacentersResult.Value);
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT_REGIONS, "Failed to load datacenters from API, trying cache...");
                datacentersResult = await LoadDatacentersFromCacheAsync();

                if (datacentersResult == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_REGIONS, "Failed to load datacenters from cache.");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_REGIONS, "Successfully loaded datacenters from cache");
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var region in datacentersResult.Value.regions)
                    Regions.Add(region);

                var savedRegion = App.Settings.Prop.SelectedRegion;
                if (!string.IsNullOrEmpty(savedRegion) && Regions.Contains(savedRegion))
                {
                    SelectedRegion = savedRegion;
                }
                else if (string.IsNullOrEmpty(SelectedRegion) && datacentersResult.Value.regions.Count > 0)
                {
                    SelectedRegion = datacentersResult.Value.regions[0];
                }
            });

            App.Logger.WriteLine(LOG_IDENT_REGIONS, $"Loaded {datacentersResult.Value.regions.Count} regions. Selected: {SelectedRegion}");
        }

        private async Task DebouncedSearchTriggerAsync(CancellationToken token)
        {
            const string LOG_IDENT_SEARCH_DEBOUNCE = $"{LOG_IDENT}::DebouncedSearchTrigger";

            try
            {
                await Task.Delay(600, token);
                if (token.IsCancellationRequested)
                    return;

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    await SearchGamesAsync();
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_SEARCH_DEBOUNCE, $"Exception: {ex.Message}");
            }
        }

        private async Task SearchGamesAsync()
        {
            const string LOG_IDENT_SEARCH = $"{LOG_IDENT}::SearchGames";

            SearchResults.Clear();

            try
            {
                var results = await GameSearching.GetGameSearchResultsAsync(SearchQuery);
                foreach (var r in results)
                    SearchResults.Add(r);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_SEARCH, $"Exception: {ex.Message}");
            }
        }

        private async Task LoadGameDetailsAsync(long placeId)
        {
            const string LOG_IDENT_GAME_DETAILS = $"{LOG_IDENT}::LoadGameDetails";

            if (placeId == 0)
            {
                ResetGameDetails();
                return;
            }

            try
            {
                var placeDetails = await FetchPlaceDetailsAsync(placeId);
                if (placeDetails != null)
                {
                    SelectedGameName = placeDetails.name ?? "Unknown Game";
                    SelectedGameCreator = placeDetails.builder ?? "Unknown Creator";
                    IsSelectedGameCreatorVerified = placeDetails.hasVerifiedBadge;
                }
                else
                {
                    SelectedGameName = "Unknown Game";
                    SelectedGameCreator = "Unknown Creator";
                    IsSelectedGameCreatorVerified = false;
                }

                await LoadUniverseStatsAsync(placeId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_GAME_DETAILS, $"Exception: {ex.Message}");
                ResetGameDetails();
            }
        }

        private async Task LoadUniverseStatsAsync(long placeId)
        {
            try
            {
                var placeDetails = await FetchPlaceDetailsAsync(placeId);
                if (placeDetails == null)
                {
                    await UniverseDetails.FetchBulk(placeId.ToString());
                    var fallbackUniverseDetails = UniverseDetails.LoadFromCache(placeId);

                    if (fallbackUniverseDetails?.Data != null)
                    {
                        SelectedGameVisits = fallbackUniverseDetails.Data.Visits;
                        SelectedGamePlaying = (int?)fallbackUniverseDetails.Data.Playing;
                    }
                    else
                    {
                        SelectedGameVisits = 0;
                        SelectedGamePlaying = 0;
                    }
                    return;
                }

                long universeId = placeDetails.universeId;

                await UniverseDetails.FetchBulk(universeId.ToString());
                var universeDetails = UniverseDetails.LoadFromCache(universeId);

                if (universeDetails?.Data != null)
                {
                    SelectedGameVisits = universeDetails.Data.Visits;
                    SelectedGamePlaying = (int?)universeDetails.Data.Playing;
                }
                else
                {
                    SelectedGameVisits = 0;
                    SelectedGamePlaying = 0;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::LoadUniverseStats", $"Exception: {ex.Message}");
                SelectedGameVisits = 0;
                SelectedGamePlaying = 0;
            }
        }

        private void ResetGameDetails()
        {
            SelectedGameName = "";
            SelectedGameCreator = "";
            IsSelectedGameCreatorVerified = false;
            SelectedGameVisits = 0;
            SelectedGamePlaying = 0;
            SelectedGameThumbnail = null;
        }

        private async Task<PlaceDetails?> FetchPlaceDetailsAsync(long placeId)
        {
            const string LOG_IDENT_PLACE_DETAILS = $"{LOG_IDENT}::FetchPlaceDetails";

            try
            {
                using var client = new HttpClient();
                string url = $"https://games.roblox.com/v1/games/multiget-place-details?placeIds={placeId}";

                var mgr = AccountManager.Shared;
                if (mgr?.ActiveAccount != null)
                {
                    string? cookie = mgr.GetRoblosecurityForUser(mgr.ActiveAccount.UserId);
                    if (!string.IsNullOrEmpty(cookie))
                    {
                        client.DefaultRequestHeaders.Add("Cookie", $".ROBLOSECURITY={cookie}");
                    }
                }

                var response = await client.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    App.Logger.WriteLine(LOG_IDENT_PLACE_DETAILS, $"Unauthorized access to place details for {placeId}. Authentication may be required.");
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_PLACE_DETAILS, $"Failed to fetch place details: {response.StatusCode}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var placeDetailsArray = JsonSerializer.Deserialize<List<PlaceDetails>>(responseContent);

                return placeDetailsArray?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_PLACE_DETAILS, $"Exception: {ex.Message}");
                return null;
            }
        }

        private async Task LoadGameThumbnailAsync(long placeId)
        {
            const string LOG_IDENT_THUMBNAIL = $"{LOG_IDENT}::LoadGameThumbnail";

            if (placeId == 0)
            {
                ResetGameDetails();
                return;
            }

            try
            {
                var thumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(
                    $"https://thumbnails.roblox.com/v1/places/gameicons?placeIds={placeId}&returnPolicy=PlaceHolder&size=256x256&format=Png&isCircular=false");

                if (thumbnailResponse?.Data != null && thumbnailResponse.Data.Any())
                {
                    var thumbnail = thumbnailResponse.Data.First();
                    SelectedGameThumbnail = thumbnail.ImageUrl;
                }
                else
                {
                    SelectedGameThumbnail = null;
                }

                await LoadGameDetailsAsync(placeId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_THUMBNAIL, $"Exception: {ex.Message}");
                ResetGameDetails();
            }
        }

        private async Task LoadSubplacesForSelectedGameAsync()
        {
            const string LOG_IDENT_SUBPLACES = $"{LOG_IDENT}::LoadSubplacesForSelectedGame";

            try
            {
                var placeDetails = await FetchPlaceDetailsAsync(long.Parse(PlaceId));
                if (placeDetails == null || placeDetails.universeId == 0)
                {
                    Subplaces.Clear();
                    return;
                }

                await FetchSubplacesAsync(placeDetails.universeId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_SUBPLACES, $"Exception: {ex.Message}");
                Subplaces.Clear();
            }
        }

        private async Task FetchSubplacesAsync(long universeId)
        {
            const string LOG_IDENT_FETCH_SUBPLACES = $"{LOG_IDENT}::FetchSubplaces";

            if (universeId == 0)
            {
                Subplaces.Clear();
                return;
            }

            try
            {
                IsLoadingSubplaces = true;

                using var client = new HttpClient();
                string url = $"https://develop.roblox.com/v1/universes/{universeId}/places?isUniverseCreation=false&limit=100&sortOrder=Asc";

                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_FETCH_SUBPLACES, $"Failed to fetch subplaces: {response.StatusCode}");
                    return;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var subplacesResponse = JsonSerializer.Deserialize<SubplacesResponse>(responseContent);

                if (subplacesResponse?.Data == null || !subplacesResponse.Data.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_FETCH_SUBPLACES, "No subplaces found in response");
                    Subplaces.Clear();
                    return;
                }

                var subplacesList = new List<PlaceInfo>();

                foreach (var place in subplacesResponse.Data)
                {
                    string thumbnailUrl = await GetPlaceThumbnailUrlAsync(place.Id);
                    subplacesList.Add(new PlaceInfo(place.Id, place.UniverseId, place.Name, thumbnailUrl));
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Subplaces.Clear();
                    foreach (var subplace in subplacesList)
                        Subplaces.Add(subplace);
                });

                App.Logger.WriteLine(LOG_IDENT_FETCH_SUBPLACES, $"Loaded {subplacesList.Count} subplaces for universe {universeId}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_FETCH_SUBPLACES, $"Exception: {ex.Message}");
                Subplaces.Clear();
            }
            finally
            {
                IsLoadingSubplaces = false;
            }
        }

        private async Task<string> GetPlaceThumbnailUrlAsync(long placeId)
        {
            try
            {
                var thumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(
                    $"https://thumbnails.roblox.com/v1/places/gameicons?placeIds={placeId}&returnPolicy=PlaceHolder&size=50x50&format=Png&isCircular=false");

                return thumbnailResponse?.Data?.FirstOrDefault()?.ImageUrl ?? "";
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::GetPlaceThumbnailUrl", $"Exception: {ex.Message}");
                return "";
            }
        }

        private async Task FindAndJoinServer(long placeId)
        {
            const string LOG_IDENT_FIND_SERVER = $"{LOG_IDENT}::FindAndJoinServer";

            var mainWindow = GetMainWindow();
            var fetcher = new RobloxServerFetcher();
            string? nextCursor = "";
            int attemptCount = 0;
            const int maxAttempts = 20;

            mainWindow?.ShowLoading("Loading regions...");

            try
            {
                var datacentersResult = await fetcher.GetDatacentersAsync();

                if (datacentersResult == null)
                {
                    Frontend.ShowMessageBox("Failed to load server regions. Please try again later.", MessageBoxImage.Error);
                    return;
                }

                var (regions, dcMap) = datacentersResult.Value;

                if (!Regions.Any())
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Regions.Clear();
                        foreach (var region in regions)
                            Regions.Add(region);
                    });
                }

                while (attemptCount < maxAttempts)
                {
                    attemptCount++;
                    mainWindow?.ShowLoading($"Searching for {SelectedRegion} server... (Page {attemptCount})");

                    var result = await fetcher.FetchServerInstancesAsync(placeId, nextCursor, SelectedSortOrder);

                    if (result?.Servers == null || !result.Servers.Any())
                    {
                        mainWindow?.ShowLoading("No servers found, checking next page...");
                        await Task.Delay(1000);
                        continue;
                    }

                    var matchingServer = result.Servers.FirstOrDefault(server =>
                        server.DataCenterId.HasValue &&
                        dcMap.TryGetValue(server.DataCenterId.Value, out var mappedRegion) &&
                        mappedRegion == SelectedRegion);

                    if (matchingServer != null)
                    {
                        mainWindow?.ShowLoading($"Found server in {SelectedRegion}! Joining...");

                        string robloxUri = $"roblox://experiences/start?placeId={placeId}&gameInstanceId={matchingServer.Id}";
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = robloxUri,
                                UseShellExecute = true
                            });

                            mainWindow?.ShowLoading("Successfully joined server!");
                            await Task.Delay(750);
                            return;
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteException(LOG_IDENT_FIND_SERVER, ex);
                            Frontend.ShowMessageBox($"Failed to launch game: {ex.Message}", MessageBoxImage.Error);
                            return;
                        }
                    }

                    if (!string.IsNullOrEmpty(result.NextCursor))
                    {
                        nextCursor = result.NextCursor;
                    }
                    else
                    {
                        Frontend.ShowMessageBox($"Could not find a server in {SelectedRegion} after searching all available servers.", MessageBoxImage.Information);
                        return;
                    }

                    await Task.Delay(500);
                }

                Frontend.ShowMessageBox($"Could not find a server in {SelectedRegion} after {maxAttempts} pages. Try selecting a different region.", MessageBoxImage.Information);
            }
            finally
            {
                mainWindow?.HideLoading();
            }
        }

        private async Task AutoFindAndJoinGameAsync(long placeId)
        {
            const string LOG_IDENT_AUTO_JOIN = $"{LOG_IDENT}::AutoFindAndJoinGameAsync";

            if (placeId == 0)
            {
                Frontend.ShowMessageBox("Invalid Place ID.", MessageBoxImage.Warning);
                return;
            }

            PlaceId = placeId.ToString();

            if (string.IsNullOrWhiteSpace(SelectedRegion))
            {
                Frontend.ShowMessageBox("Please select a region first.", MessageBoxImage.Warning);
                return;
            }

            IsAutoSearching = true;

            try
            {
                await FindAndJoinServer(placeId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_AUTO_JOIN, $"Exception: {ex.Message}");
                Frontend.ShowMessageBox($"Failed to find and join server: {ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                IsAutoSearching = false;
            }
        }

        private async Task LaunchGameAsync(long placeId)
        {
            const string LOG_IDENT_LAUNCH = $"{LOG_IDENT}::LaunchGameAsync";

            var mgr = AccountManager.Shared;
            if (mgr?.ActiveAccount is null)
            {
                Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                return;
            }

            if (placeId == 0)
            {
                Frontend.ShowMessageBox("Invalid Place ID.", MessageBoxImage.Warning);
                return;
            }

            PlaceId = placeId.ToString();
            mgr.SetCurrentPlaceId(PlaceId);
            mgr.SetCurrentServerInstanceId(ServerId);

            await mgr.LaunchAccountToPlaceAsync(mgr.ActiveAccount, placeId, ServerId);
        }

        private async Task FetchDiscoveryPageGamesAsync(long userId, CancellationToken token = default)
        {
            const string LOG_IDENT_DISCOVERY = $"{LOG_IDENT}::FetchDiscoveryPageGames";

            try
            {
                DiscoveryGames.Clear();

                if (!ShouldShowGames)
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, "No accounts available, skipping Discovery games fetch.");
                    return;
                }

                var mgr = AccountManager.Shared;
                if (mgr == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, "Account manager unavailable.");
                    return;
                }

                if (userId == 0 && mgr.ActiveAccount is null)
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, "No active account.");
                    return;
                }

                string? cookie = mgr.GetRoblosecurityForUser(userId);
                if (string.IsNullOrEmpty(cookie))
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, ".ROBLOSECURITY not available for user; aborting discovery fetch.");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Starting discovery fetch for user {userId}.");

                var postReq = new HttpRequestMessage(HttpMethod.Post, "https://apis.roblox.com/discovery-api/omni-recommendation");
                postReq.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
                postReq.Content = new StringContent("{\"pageType\":\"Home\",\"sessionId\":\"1\"}", Encoding.UTF8, "application/json");

                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, "Sending discovery POST...");
                using var resp = await _http.SendAsync(postReq, token).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Discovery response status={(int)resp.StatusCode}");

                if (!resp.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Discovery POST failed: {(int)resp.StatusCode}. Body: {body}");
                    return;
                }

                JObject jo;
                try
                {
                    jo = JObject.Parse(body);
                }
                catch (Exception parseEx)
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Parse exception: {parseEx.Message}");
                    return;
                }

                var orderedContentIds = new List<long>();
                var sorts = jo["sorts"] as JArray;
                if (sorts != null)
                {
                    foreach (var sort in sorts)
                    {
                        var recs = sort["recommendationList"] as JArray;
                        if (recs == null) continue;
                        foreach (var rec in recs)
                        {
                            if (rec["contentType"]?.Value<string>() != "Game") continue;
                            long? cid = rec["contentId"]?.Value<long?>();
                            if (cid.HasValue) orderedContentIds.Add(cid.Value);
                        }
                    }
                }

                if (!orderedContentIds.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, "No game recommendations found in discovery response.");
                    return;
                }

                const int MaxTotalContentIds = 50;
                orderedContentIds = orderedContentIds.Take(MaxTotalContentIds).ToList();
                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Found {orderedContentIds.Count} recommended contentIds (taking first {MaxTotalContentIds}).");

                var universeOrdered = new List<long>();
                var seenUniverses = new HashSet<long>();

                foreach (var cid in orderedContentIds)
                {
                    if (token.IsCancellationRequested) break;

                    var node = jo["contentMetadata"]?["Game"]?[cid.ToString()];
                    if (node == null)
                    {
                        App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Metadata missing for contentId {cid} - skipping.");
                        continue;
                    }

                    long? universeId = node["universeId"]?.Value<long?>();
                    long? rootPlaceId = node["rootPlaceId"]?.Value<long?>();

                    if (!universeId.HasValue)
                    {
                        App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"UniverseId missing for contentId {cid} - skipping.");
                        continue;
                    }

                    long uId = universeId.Value;

                    if (seenUniverses.Contains(uId))
                    {
                        App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Universe {uId} already queued, skipping duplicate (content {cid}).");
                        continue;
                    }

                    seenUniverses.Add(uId);
                    universeOrdered.Add(uId);
                }

                if (!universeOrdered.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, "No universe ids extracted from metadata.");
                    return;
                }

                var uniqUniverseIds = universeOrdered.Take(50).ToList();

                await UniverseDetails.FetchBulk(string.Join(',', uniqUniverseIds));

                var fetchedGamesOrdered = new List<RecentGameInfo>();

                foreach (var uId in uniqUniverseIds)
                {
                    if (token.IsCancellationRequested) break;

                    var universeDetails = UniverseDetails.LoadFromCache(uId);
                    if (universeDetails?.Data != null)
                    {
                        var gameInfo = new RecentGameInfo(
                            universeDetails.Data.Id,
                            universeDetails.Data.RootPlaceId,
                            universeDetails.Data.Name,
                            (int?)universeDetails.Data.Playing,
                            universeDetails.Data.Visits,
                            universeDetails.Thumbnail?.ImageUrl ?? ""
                        );
                        fetchedGamesOrdered.Add(gameInfo);
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Game details missing for universe {uId}");
                    }
                }

                await Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    DiscoveryGames.Clear();
                    foreach (var g in fetchedGamesOrdered)
                        DiscoveryGames.Add(g);
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Finished — added {fetchedGamesOrdered.Count} games to UI (from universeIds).");
                });
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, "Cancelled.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Exception: {ex.Message}");
            }
        }

        private async Task FetchContinuePlayingGamesAsync(CancellationToken token = default)
        {
            const string LOG_IDENT_CONTINUE = $"{LOG_IDENT}::FetchContinuePlayingGames";

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ContinuePlayingGames.Clear();
                    IsLoadingContinuePlaying = true;
                });

                if (!ShouldShowGames)
                {
                    App.Logger.WriteLine(LOG_IDENT_CONTINUE, "No active account available, skipping continue playing fetch.");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        IsLoadingContinuePlaying = false;
                    });
                    return;
                }

                await Task.Delay(100, token);

                var recentGames = await LoadRecentGamesFromCacheAsync();

                if (!recentGames.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_CONTINUE, "No recent games found in cache");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        IsLoadingContinuePlaying = false;
                    });
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_CONTINUE, $"Found {recentGames.Count} recent games");

                var uniqueRecentGames = recentGames
                    .OrderByDescending(g => g.TimeLeft)
                    .GroupBy(g => g.UniverseId)
                    .Select(g => g.First())
                    .Take(50)
                    .ToList();

                App.Logger.WriteLine(LOG_IDENT_CONTINUE, $"After removing duplicates: {uniqueRecentGames.Count} unique games");

                var continuePlayingList = new List<RecentGameInfo>();
                var universeIdsToFetch = new List<long>();
                var processedUniverseIds = new HashSet<long>();

                var universeIds = uniqueRecentGames.Select(g => g.UniverseId).Distinct().ToList();

                if (universeIds.Any())
                {
                    await UniverseDetails.FetchBulk(string.Join(',', universeIds.Take(50)));
                }

                foreach (var activity in uniqueRecentGames)
                {
                    if (token.IsCancellationRequested) break;

                    if (processedUniverseIds.Contains(activity.UniverseId))
                        continue;

                    processedUniverseIds.Add(activity.UniverseId);

                    var universeDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
                    if (universeDetails?.Data != null)
                    {
                        var gameInfo = new RecentGameInfo(
                            activity.UniverseId,
                            universeDetails.Data.RootPlaceId,
                            universeDetails.Data.Name ?? "Unknown Game",
                            (int?)universeDetails.Data.Playing,
                            universeDetails.Data.Visits,
                            universeDetails.Thumbnail?.ImageUrl ?? ""
                        );
                        continuePlayingList.Add(gameInfo);
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT_CONTINUE, $"No universe details found for universe {activity.UniverseId}");
                    }
                }

                continuePlayingList = continuePlayingList
                    .OrderByDescending(g => uniqueRecentGames.First(r => r.UniverseId == g.UniverseId).TimeLeft)
                    .Take(50)
                    .ToList();

                await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ContinuePlayingGames.Clear();
                    foreach (var game in continuePlayingList)
                        ContinuePlayingGames.Add(game);

                    IsLoadingContinuePlaying = false;
                }));

                App.Logger.WriteLine(LOG_IDENT_CONTINUE, $"Loaded {continuePlayingList.Count} unique continue playing games");
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT_CONTINUE, "Cancelled.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_CONTINUE, ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsLoadingContinuePlaying = false;
                });
            }
        }

        private async Task<List<ActivityData>> LoadRecentGamesFromCacheAsync()
        {
            const string LOG_IDENT_CACHE = $"{LOG_IDENT}::LoadRecentGamesFromCache";

            try
            {
                var cachePath = Path.Combine(Paths.Cache, "GameHistory.json");

                if (!File.Exists(cachePath))
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE, "Game history cache file does not exist");
                    return new List<ActivityData>();
                }

                var json = await File.ReadAllTextAsync(cachePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                };

                var gameHistory = JsonSerializer.Deserialize<List<GameHistoryData>>(json, options);

                if (gameHistory == null || !gameHistory.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE, "No game history found in cache");
                    return new List<ActivityData>();
                }

                var mgr = AccountManager.Shared;

                App.Logger.WriteLine(LOG_IDENT_CACHE, $"Raw game history count: {gameHistory.Count}");

                var recentGames = gameHistory
                    .Where(activity =>
                        activity.ServerType == 0 &&
                        activity.UniverseId != 0 &&
                        activity.PlaceId != 0 &&
                        activity.TimeLeft.HasValue)
                    .OrderByDescending(activity => activity.TimeLeft)
                    .Take(15)
                    .Select(history => new ActivityData
                    {
                        UniverseId = history.UniverseId,
                        PlaceId = history.PlaceId,
                        JobId = history.JobId,
                        UserId = history.UserId,
                        ServerType = (ServerType)history.ServerType,
                        TimeJoined = history.TimeJoined,
                        TimeLeft = history.TimeLeft
                    })
                    .ToList();

                App.Logger.WriteLine(LOG_IDENT_CACHE, $"Loaded {recentGames.Count} recent public server games from cache");
                return recentGames;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_CACHE, ex);
                return new List<ActivityData>();
            }
        }

        private async Task FetchFavoriteGamesAsync(long userId, CancellationToken token = default)
        {
            const string LOG_IDENT_FAVORITES = $"{LOG_IDENT}::FetchFavoriteGames";

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    FavoriteGames.Clear();
                });

                if (!ShouldShowGames)
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, "No accounts available, skipping favorites fetch.");
                    return;
                }

                var mgr = AccountManager.Shared;
                if (mgr == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, "Account manager unavailable.");
                    return;
                }

                if (userId == 0 && mgr.ActiveAccount is null)
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, "No active account.");
                    return;
                }

                string? cookie = mgr.GetRoblosecurityForUser(userId);
                if (string.IsNullOrEmpty(cookie))
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, ".ROBLOSECURITY not available for user; aborting favorites fetch.");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Starting favorites fetch for user {userId}.");

                var favoritesUrl = $"https://games.roblox.com/v2/users/{userId}/favorite/games?limit=50&sortOrder=Desc";

                var favoritesReq = new HttpRequestMessage(HttpMethod.Get, favoritesUrl);
                favoritesReq.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");

                using var favoritesResp = await _http.SendAsync(favoritesReq, token).ConfigureAwait(false);
                var favoritesBody = await favoritesResp.Content.ReadAsStringAsync().ConfigureAwait(false);

                App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Favorites response status={(int)favoritesResp.StatusCode}");

                if (!favoritesResp.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Favorites API failed: {(int)favoritesResp.StatusCode}. Body: {favoritesBody}");
                    return;
                }

                JObject favoritesJson;
                try
                {
                    favoritesJson = JObject.Parse(favoritesBody);
                }
                catch (Exception parseEx)
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Parse exception: {parseEx.Message}");
                    return;
                }

                var favoritesData = favoritesJson["data"] as JArray;
                if (favoritesData == null || !favoritesData.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, "No favorite games found.");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Found {favoritesData.Count} favorite games.");

                var universeIds = new List<long>();

                foreach (var game in favoritesData)
                {
                    if (token.IsCancellationRequested) break;

                    long universeId = game["id"]?.Value<long>() ?? 0;
                    if (universeId > 0 && !universeIds.Contains(universeId))
                        universeIds.Add(universeId);
                }

                if (!universeIds.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, "No valid universe IDs found in favorites.");
                    return;
                }

                await UniverseDetails.FetchBulk(string.Join(',', universeIds.Take(50)));

                var favoriteGamesList = new List<RecentGameInfo>();

                foreach (var game in favoritesData)
                {
                    if (token.IsCancellationRequested) break;

                    long universeId = game["id"]?.Value<long>() ?? 0;
                    var rootPlace = game["rootPlace"];
                    long rootPlaceId = rootPlace?["id"]?.Value<long>() ?? 0;

                    var universeDetails = UniverseDetails.LoadFromCache(universeId);
                    if (universeDetails?.Data != null)
                    {
                        var gameInfo = new RecentGameInfo(
                            universeDetails.Data.Id,
                            universeDetails.Data.RootPlaceId,
                            universeDetails.Data.Name,
                            (int?)universeDetails.Data.Playing,
                            universeDetails.Data.Visits,
                            universeDetails.Thumbnail?.ImageUrl ?? ""
                        );
                        favoriteGamesList.Add(gameInfo);
                    }
                    else
                    {
                        var gameInfo = new RecentGameInfo(
                            universeId,
                            rootPlaceId,
                            game["name"]?.Value<string>() ?? "",
                            null,
                            game["placeVisits"]?.Value<long?>(),
                            ""
                        );
                        favoriteGamesList.Add(gameInfo);
                    }
                }

                await Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    FavoriteGames.Clear();
                    foreach (var game in favoriteGamesList)
                        FavoriteGames.Add(game);

                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Finished — added {favoriteGamesList.Count} favorite games to UI with full details.");
                });
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT_FAVORITES, "Cancelled.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Exception: {ex.Message}");
            }
        }

        private async Task PopulatePrivateServersAsync()
        {
            const string LOG_IDENT_PRIVATE = $"{LOG_IDENT}::PopulatePrivateServers";

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                PrivateServers.Clear();
                ArePrivateServersEmpty = false;
            });

            if (!long.TryParse(PlaceId, out long placeId) || placeId == 0)
            {
                App.Logger.WriteLine(LOG_IDENT_PRIVATE, "PlaceId invalid or not set; cannot populate private servers.");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PrivateServers.Clear();
                    ArePrivateServersEmpty = true;
                });
                return;
            }

            try
            {
                string url = $"https://games.roblox.com/v1/games/{placeId}/private-servers?excludeFriendServers=false&sortOrder=Asc";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                var mgr = AccountManager.Shared;
                if (mgr?.ActiveAccount != null)
                {
                    string? cookie = mgr.GetRoblosecurityForUser(mgr.ActiveAccount.UserId);
                    if (!string.IsNullOrEmpty(cookie))
                    {
                        req.Headers.Add("Origin", "https://www.roblox.com");
                        req.Headers.Add("Referrer", "https://www.roblox.com");
                        req.Headers.Remove("Cookie");
                        req.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
                    }
                }

                using var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_PRIVATE, $"Private servers request failed: {(int)resp.StatusCode}");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        PrivateServers.Clear();
                        ArePrivateServersEmpty = true;
                    });
                    return;
                }

                var body = await resp.Content.ReadAsStringAsync();
                var jo = JObject.Parse(body);
                var data = jo["data"] as JArray;

                if (data == null || !data.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_PRIVATE, "No private servers returned by API.");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        PrivateServers.Clear();
                        ArePrivateServersEmpty = true;
                    });
                    return;
                }

                var tempList = new List<(long VipServerId, string AccessCode, string Name, long OwnerId, string OwnerName, int MaxPlayers, int CurrentPlayers)>();
                var ownerIds = new HashSet<long>();

                foreach (var srv in data)
                {
                    try
                    {
                        int maxPlayers = srv["maxPlayers"]?.Value<int>() ?? 0;
                        var playersArray = srv["players"] as JArray;
                        int currentPlayers = playersArray?.Count ?? 0;
                        string name = srv["name"]?.Value<string>() ?? "";
                        long vipServerId = srv["vipServerId"]?.Value<long>() ?? 0;
                        string accessCode = srv["accessCode"]?.Value<string>() ?? vipServerId.ToString();

                        var owner = srv["owner"];
                        long ownerId = owner?["id"]?.Value<long>() ?? 0;
                        string ownerName = owner?["name"]?.Value<string>() ?? "";

                        tempList.Add((vipServerId, accessCode, name, ownerId, ownerName, maxPlayers, currentPlayers));

                        if (ownerId != 0)
                            ownerIds.Add(ownerId);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine($"{LOG_IDENT_PRIVATE}::ParseItem", $"Exception parsing server item: {ex.Message}");
                    }
                }

                var avatarUrls = new Dictionary<long, string?>();
                if (ownerIds.Any())
                {
                    try
                    {
                        foreach (var id in ownerIds)
                            avatarUrls[id] = null;

                        var tasks = ownerIds.Select(async id =>
                        {
                            try
                            {
                                var img = await GetAvatarUrl(id).ConfigureAwait(false);
                                lock (avatarUrls)
                                {
                                    avatarUrls[id] = img;
                                }
                            }
                            catch (Exception ex)
                            {
                                App.Logger.WriteLine($"{LOG_IDENT_PRIVATE}::GetAvatarUrl", $"Failed to fetch avatar for {id}: {ex.Message}");
                                lock (avatarUrls)
                                {
                                    avatarUrls[id] = null;
                                }
                            }
                        }).ToArray();

                        await Task.WhenAll(tasks).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT_PRIVATE, $"Failed fetching avatars individually: {ex.Message}");
                        foreach (var id in ownerIds)
                            if (!avatarUrls.ContainsKey(id))
                                avatarUrls[id] = null;
                    }
                }

                var list = new List<PrivateServerInfo>();
                foreach (var t in tempList)
                {
                    string? ownerAvatar = null;
                    if (t.OwnerId != 0 && avatarUrls.TryGetValue(t.OwnerId, out var au))
                        ownerAvatar = au;

                    var entry = new PrivateServerInfo(
                        t.VipServerId,
                        t.AccessCode,
                        t.Name,
                        t.OwnerId,
                        t.OwnerName,
                        ownerAvatar,
                        t.MaxPlayers,
                        t.CurrentPlayers
                    );

                    list.Add(entry);
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PrivateServers.Clear();
                    foreach (var p in list)
                        PrivateServers.Add(p);

                    ArePrivateServersEmpty = list.Count == 0;
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_PRIVATE, $"Exception: {ex.Message}");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PrivateServers.Clear();
                    ArePrivateServersEmpty = true;
                });
            }
        }

        private async Task<string?> GetAvatarUrl(long userId)
        {
            try
            {
                var request = new ThumbnailRequest
                {
                    TargetId = (ulong)userId,
                    Type = "AvatarHeadShot",
                    Size = "75x75",
                    Format = "Png",
                    IsCircular = true
                };

                return await Thumbnails.GetThumbnailUrlAsync(request, CancellationToken.None);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::GetAvatarUrl", $"Exception: {ex.Message}");
                return null;
            }
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
                App.Logger.WriteLine(LOG_IDENT_CACHE_SAVE, $"Exception: {ex.Message}");
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
                App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, $"Exception: {ex.Message}");
                return null;
            }
        }

        [RelayCommand]
        private async Task AutoFindAndJoinSelectedGameAsync()
        {
            const string LOG_IDENT_AUTO_JOIN_SELECTED = $"{LOG_IDENT}::AutoFindAndJoinSelectedGame";

            if (SelectedSearchResult == null)
            {
                if (string.IsNullOrWhiteSpace(PlaceId) || !long.TryParse(PlaceId, out long placeId) || placeId == 0)
                {
                    Frontend.ShowMessageBox("Please select a game first or enter a valid Place ID.", MessageBoxImage.Warning);
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_AUTO_JOIN_SELECTED, $"Using PlaceId from input field: {PlaceId}");
                await AutoFindAndJoinGameAsync(placeId);
                return;
            }

            App.Logger.WriteLine(LOG_IDENT_AUTO_JOIN_SELECTED, $"Using selected game: {SelectedSearchResult.Name} (PlaceId: {SelectedSearchResult.RootPlaceId})");
            await AutoFindAndJoinGameAsync(SelectedSearchResult.RootPlaceId);
        }

        [RelayCommand]
        private async Task LaunchRoblox()
        {
            const string LOG_IDENT_LAUNCH = $"{LOG_IDENT}::LaunchRoblox";

            var mgr = AccountManager.Shared;
            if (mgr?.ActiveAccount is null)
            {
                Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(PlaceId))
            {
                Frontend.ShowMessageBox("Please enter a Place ID.", MessageBoxImage.Warning);
                return;
            }

            if (!long.TryParse(PlaceId, out long placeId))
            {
                Frontend.ShowMessageBox("Please enter a valid Place ID.", MessageBoxImage.Warning);
                return;
            }

            PlaceId = placeId.ToString();
            mgr.SetCurrentPlaceId(PlaceId);
            mgr.SetCurrentServerInstanceId(ServerId);

            await mgr.LaunchAccountToPlaceAsync(mgr.ActiveAccount, placeId, ServerId);
        }

        [RelayCommand]
        private async Task LaunchSubplace(long placeId)
        {
            const string LOG_IDENT_SUBPLACE = $"{LOG_IDENT}::LaunchSubplace";

            var mgr = AccountManager.Shared;
            if (mgr?.ActiveAccount is null)
            {
                Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                return;
            }

            try
            {
                PlaceId = placeId.ToString();
                mgr.SetCurrentPlaceId(PlaceId);
                mgr.SetCurrentServerInstanceId(ServerId);

                await mgr.LaunchAccountToPlaceAsync(mgr.ActiveAccount, placeId, ServerId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_SUBPLACE, $"Exception: {ex.Message}");
                Frontend.ShowMessageBox($"Failed to launch subplace: {ex.Message}", MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private Task LaunchDiscoveryPageGame(long placeId)
            => LaunchGameAsync(placeId);

        [RelayCommand]
        private Task AutoFindAndJoinServer(long placeId)
            => AutoFindAndJoinGameAsync(placeId);

        [RelayCommand]
        private Task LaunchFavoriteGame(long placeId)
            => LaunchGameAsync(placeId);

        [RelayCommand]
        private Task AutoFindAndJoinFavoriteGame(long placeId)
            => AutoFindAndJoinGameAsync(placeId);

        [RelayCommand]
        private Task LaunchContinuePlayingGame(long placeId)
            => LaunchGameAsync(placeId);

        [RelayCommand]
        private Task AutoFindAndJoinContinuePlayingGame(long placeId)
            => AutoFindAndJoinGameAsync(placeId);

        [RelayCommand]
        private Task RefreshDiscoveryGames()
            => ExecuteWithCancellationSupport(
                token => FetchDiscoveryPageGamesAsync(AccountManager.Shared.ActiveAccount?.UserId ?? 0, token),
                new CancellationTokenSource(),
                "RefreshDiscoveryGames");

        [RelayCommand]
        private Task RefreshFavoriteGames()
            => ExecuteWithCancellationSupport(
                token => FetchFavoriteGamesAsync(AccountManager.Shared.ActiveAccount?.UserId ?? 0, token),
                new CancellationTokenSource(),
                "RefreshFavoriteGames");

        [RelayCommand]
        private Task RefreshContinuePlaying()
            => ExecuteWithCancellationSupport(
                token => FetchContinuePlayingGamesAsync(token),
                new CancellationTokenSource(),
                "RefreshContinuePlaying");

        [RelayCommand]
        private async Task ShowPrivateServers()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() => IsPrivateServersModalOpen = true);
                await PopulatePrivateServersAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::ShowPrivateServers", $"Exception: {ex.Message}");
                await Application.Current.Dispatcher.InvokeAsync(() => IsPrivateServersModalOpen = false);
            }
        }

        [RelayCommand]
        private void HidePrivateServers()
        {
            Application.Current?.Dispatcher?.Invoke(() => IsPrivateServersModalOpen = false);
        }

        [RelayCommand]
        private async Task JoinPrivateServer(string accessCode)
        {
            const string LOG_IDENT_JOIN = $"{LOG_IDENT}::JoinPrivateServer";

            try
            {
                if (string.IsNullOrWhiteSpace(accessCode))
                    return;

                var mgr = AccountManager.Shared;
                if (mgr?.ActiveAccount is null)
                {
                    Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                    return;
                }

                if (!long.TryParse(PlaceId, out long placeId) || placeId == 0)
                {
                    Frontend.ShowMessageBox("Please select a game first (set Place ID).", MessageBoxImage.Warning);
                    return;
                }

                PlaceId = placeId.ToString();
                mgr.SetCurrentPlaceId(PlaceId);
                mgr.SetCurrentServerInstanceId(accessCode);

                await mgr.LaunchAccountToPlaceAsync(mgr.ActiveAccount, placeId, accessCode);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_JOIN, $"Exception: {ex.Message}");
                Frontend.ShowMessageBox($"Failed to join server: {ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                Application.Current?.Dispatcher?.Invoke(() => IsPrivateServersModalOpen = false);
            }
        }

        [RelayCommand]
        private void PersistPlaceId()
        {
            var mgr = AccountManager.Shared;
            if (mgr != null)
            {
                mgr.SetCurrentPlaceId(PlaceId);
            }
        }

        [RelayCommand]
        private void PersistServerId()
        {
            var mgr = AccountManager.Shared;
            if (mgr != null)
            {
                mgr.SetCurrentServerInstanceId(ServerId);
            }
        }

        private async Task ExecuteWithCancellationSupport(
            Func<CancellationToken, Task> action,
            CancellationTokenSource? cts,
            string operationName)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            try
            {
                await action(token);
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::{operationName}", "Cancelled.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::{operationName}", $"Exception: {ex.Message}");
            }
        }

        partial void OnSearchQueryChanged(string value)
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;
            _ = DebouncedSearchTriggerAsync(token);

            if (long.TryParse(value, out long placeId))
            {
                PlaceId = value;
                _ = LoadGameThumbnailAsync(placeId);
                _ = LoadSubplacesForSelectedGameAsync();
            }
        }

        partial void OnSelectedSearchResultChanged(GameSearchResult? value)
        {
            const string LOG_IDENT_SEARCH_RESULT = $"{LOG_IDENT}::OnSelectedSearchResultChanged";

            if (value != null)
            {
                PlaceId = value.RootPlaceId.ToString();
                SearchQuery = value.RootPlaceId.ToString();

                _ = LoadGameThumbnailAsync(value.RootPlaceId);
                _ = LoadSubplacesForSelectedGameAsync();

                _searchDebounceCts?.Cancel();
                App.Logger.WriteLine(LOG_IDENT_SEARCH_RESULT, $"Selected game: {value.Name} ({PlaceId})");
            }
            else
            {
                ResetGameDetails();
                Subplaces.Clear();
            }
        }

        partial void OnSelectedRegionChanged(string? value)
        {
            const string LOG_IDENT_REGION = $"{LOG_IDENT}::OnSelectedRegionChanged";

            var mgr = AccountManager.Shared;
            if (mgr != null && value != null)
            {
                mgr.SetSelectedRegion(value);
                App.Logger.WriteLine(LOG_IDENT_REGION, $"Selected region changed to: {value}");
            }
        }

        partial void OnPlaceIdChanged(string value)
        {
            const string LOG_IDENT_PLACEID = $"{LOG_IDENT}::OnPlaceIdChanged";

            var mgr = AccountManager.Shared;
            if (mgr != null)
            {
                mgr.SetCurrentPlaceId(value);
            }
        }

        partial void OnServerIdChanged(string value)
        {
            const string LOG_IDENT_SERVERID = $"{LOG_IDENT}::OnServerIdChanged";

            var mgr = AccountManager.Shared;
            if (mgr != null)
            {
                mgr.SetCurrentServerInstanceId(value);
            }
        }
    }
}