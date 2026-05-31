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
using FluentAvalonia.UI.Controls;
using Froststrap.Integrations;
using Froststrap.Models.APIs;
using Froststrap.UI.Elements.Settings;
using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels
{
    public partial class SearchBarViewModel : NotifyPropertyChangedViewModel
    {
        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    FilterSearchResults();

                    TriggerGameSearch(value);
                    GameSearchResults.Clear();
                    IsGameSearchLoading = false;
                }
            }
        }


        private ObservableCollection<OmniSearchContent> _gameSearchResults = [];
        public ObservableCollection<OmniSearchContent> GameSearchResults
        {
            get => _gameSearchResults;
            set => SetProperty(ref _gameSearchResults, value);
        }

        private bool _isGameSearchLoading;
        public bool IsGameSearchLoading
        {
            get => _isGameSearchLoading;
            set
            {
                if (SetProperty(ref _isGameSearchLoading, value))
                {
                    OnPropertyChanged(nameof(CanLoadMore));
                }
            }
        }

        private string _nextPageCursor = "";
        public string NextPageCursor
        {
            get => _nextPageCursor;
            set
            {
                if (SetProperty(ref _nextPageCursor, value))
                {
                    OnPropertyChanged(nameof(CanLoadMore));
                }
            }
        }

        private bool _isSearchFlyoutOpen;
        public bool IsSearchFlyoutOpen
        {
            get => _isSearchFlyoutOpen;
            set => SetProperty(ref _isSearchFlyoutOpen, value);
        }

        private string? _roblosecurity;
        public string? Roblosecurity
        {
            get => _roblosecurity;
            set => SetProperty(ref _roblosecurity, value);
        }

        public bool CanLoadMore => !string.IsNullOrEmpty(NextPageCursor) && !IsGameSearchLoading;

        private CancellationTokenSource? _searchDebounceCts;

        private ObservableCollection<SearchBarItem> _filteredSearchResults = [];
        public ObservableCollection<SearchBarItem> FilteredSearchResults
        {
            get => _filteredSearchResults;
            private set => SetProperty(ref _filteredSearchResults, value);
        }

        private List<SearchBarItem> _searchIndex = [];

        public IRelayCommand ClearSearchCommand { get; }
        public IRelayCommand<SearchBarItem> SearchResultSelectedCommand { get; }

        public event EventHandler<SearchBarItem>? SearchResultSelected;

        public SearchBarViewModel()
        {
            ClearSearchCommand = new RelayCommand(Clear);
            SearchResultSelectedCommand = new RelayCommand<SearchBarItem>(HandleSearchResultSelected);
        }

        private void TriggerGameSearch(string query)
        {
            if (!App.Settings.Prop.GameSearch)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    GameSearchResults.Clear();
                    IsSearchFlyoutOpen = FilteredSearchResults.Count > 0;
                    OnPropertyChanged(nameof(HasAnyResults));
                });
                return;
            }

            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;

            _ = SearchGamesAsync(query, token);
        }

        private async Task SearchGamesAsync(string query, CancellationToken token)
        {
            if (!App.Settings.Prop.GameSearch) return;

            if (string.IsNullOrWhiteSpace(query))
            {
                Dispatcher.UIThread.Post(() => {
                    GameSearchResults.Clear();
                    IsSearchFlyoutOpen = false;
                });
                return;
            }

            try
            {
                await Task.Delay(500, token);
                if (token.IsCancellationRequested) return;

                IsGameSearchLoading = true;
                IsSearchFlyoutOpen = true;

                List<OmniSearchContent> results = [];

                if (long.TryParse(query, out long placeId))
                {
                    try
                    {
                        var placeReq = new HttpRequestMessage(HttpMethod.Get, $"https://games.roblox.com/v1/games/multiget-place-details?placeIds={placeId}");
                        var account = AccountManager.Shared.ActiveAccount;
                        if (account != null)
                        {
                            var cookie = AccountManager.Shared.GetRoblosecurityForUser(account.UserId);
                            if (!string.IsNullOrEmpty(cookie))
                                placeReq.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
                        }

                        var placeResp = await App.HttpClient.SendAsync(placeReq, token);
                        if (placeResp.IsSuccessStatusCode)
                        {
                            var placeBody = await placeResp.Content.ReadAsStringAsync(token);
                            using var doc = JsonDocument.Parse(placeBody);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                            {
                                var placeElement = doc.RootElement[0];
                                results.Add(new OmniSearchContent
                                {
                                    RootPlaceId = placeId,
                                    UniverseId = (ulong)placeElement.GetProperty("universeId").GetInt64(),
                                    Name = placeElement.GetProperty("name").GetString() ?? $"Place {placeId}",
                                    PlayerCount = 0
                                });
                            }
                        }
                    }
                    catch { /* Fallback to standard search */ }
                }

                if (results.Count == 0)
                {
                    var (searchResults, nextCursor) = await GameSearching.GetDetailedGameSearchResultsAsync(query);
                    if (searchResults != null)
                        results.AddRange(searchResults);
                    NextPageCursor = nextCursor;
                }

                if (token.IsCancellationRequested) return;

                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested) return;

                    GameSearchResults.Clear();
                    foreach (var res in results)
                        GameSearchResults.Add(res);

                    IsSearchFlyoutOpen = HasAnyResults;

                    OnPropertyChanged(nameof(HasAnyResults));
                    OnPropertyChanged(nameof(CanLoadMore));
                }, DispatcherPriority.Background);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var thumbRequests = results.Select(r => new ThumbnailRequest
                        {
                            Type = ThumbnailType.GameIcon,
                            TargetId = r.UniverseId,
                            Size = "128x128"
                        }).ToList();

                        var fetchedUrls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, token);
                        if (fetchedUrls == null || token.IsCancellationRequested) return;

                        using var semaphore = new SemaphoreSlim(4);
                        var downloadTasks = new List<Task>();

                        for (int i = 0; i < results.Count; i++)
                        {
                            if (i < fetchedUrls.Length && !string.IsNullOrEmpty(fetchedUrls[i]))
                            {
                                int index = i;
                                downloadTasks.Add(Task.Run(async () =>
                                {
                                    await semaphore.WaitAsync(token);
                                    try
                                    {
                                        var response = await App.HttpClient.GetByteArrayAsync(fetchedUrls[index], token);
                                        using var ms = new MemoryStream(response);
                                        var bitmap = new Bitmap(ms);
                                        results[index].ThumbnailBitmap = bitmap;
                                    }
                                    catch { }
                                    finally { semaphore.Release(); }
                                }, token));
                            }
                        }
                        await Task.WhenAll(downloadTasks);
                    }
                    catch { }
                }, token);
            }
            catch (TaskCanceledException) { }
            finally
            {
                IsGameSearchLoading = false;
            }
        }

        [RelayCommand]
        public void ToggleSearchList()
        {
            if (HasAnyResults)
            {
                IsSearchFlyoutOpen = !IsSearchFlyoutOpen;
            }
        }

        [RelayCommand]
        private async Task LoadMoreGamesAsync()
        {
            if (!App.Settings.Prop.GameSearch || string.IsNullOrWhiteSpace(NextPageCursor) || string.IsNullOrWhiteSpace(SearchQuery) || IsGameSearchLoading)
                return;

            try
            {
                IsGameSearchLoading = true;
                OnPropertyChanged(nameof(CanLoadMore));

                var (results, nextCursor) = await GameSearching.GetDetailedGameSearchResultsAsync(SearchQuery, NextPageCursor);
                NextPageCursor = nextCursor;

                if (results != null && results.Count != 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var res in results)
                            GameSearchResults.Add(res);

                        OnPropertyChanged(nameof(CanLoadMore));
                    }, DispatcherPriority.Background);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var thumbRequests = results.Select(r => new ThumbnailRequest
                            {
                                Type = ThumbnailType.GameIcon,
                                TargetId = r.UniverseId,
                                Size = "128x128"
                            }).ToList();

                            var fetchedUrls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, CancellationToken.None);

                            var downloadTasks = new List<Task>();
                            using var semaphore = new SemaphoreSlim(4);

                            for (int i = 0; i < results.Count; i++)
                            {
                                if (fetchedUrls != null && i < fetchedUrls.Length && !string.IsNullOrEmpty(fetchedUrls[i]))
                                {
                                    int index = i;
                                    downloadTasks.Add(Task.Run(async () =>
                                    {
                                        await semaphore.WaitAsync();
                                        try
                                        {
                                            var response = await App.HttpClient.GetByteArrayAsync(fetchedUrls[index]);
                                            using var ms = new MemoryStream(response);
                                            var bitmap = new Bitmap(ms);
                                            results[index].ThumbnailBitmap = bitmap;
                                        }
                                        catch { /* Ignore image load failure */ }
                                        finally
                                        {
                                            semaphore.Release();
                                        }
                                    }));
                                }
                            }

                            await Task.WhenAll(downloadTasks);
                        }
                        catch { /* Ignore thumbnail fetch failures */ }
                    });
                }
                else
                {
                    NextPageCursor = "";
                    OnPropertyChanged(nameof(CanLoadMore));
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SearchBarViewModel", $"Load more error: {ex.Message}");
            }
            finally
            {
                IsGameSearchLoading = false;
                OnPropertyChanged(nameof(CanLoadMore));
            }
        }

        public void Clear()
        {
            SearchQuery = string.Empty;
            GameSearchResults.Clear();
        }

        public bool HasAnyResults => FilteredSearchResults.Count > 0 || GameSearchResults.Count > 0;

        public void SetSearchIndex(List<SearchBarItem> searchIndex)
        {
            _searchIndex = searchIndex ?? [];
        }

        public List<SearchBarItem> GetSearchIndex()
        {
            return _searchIndex;
        }

        private void HandleSearchResultSelected(SearchBarItem? item)
        {
            if (item == null)
                return;

            Clear();
            SearchResultSelected?.Invoke(this, item);
            item.NavigateAction?.Invoke();
        }

        private void FilterSearchResults()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                FilteredSearchResults = [];
                if (GameSearchResults.Count == 0) IsSearchFlyoutOpen = false;

                OnPropertyChanged(nameof(HasAnyResults));
                return;
            }

            var filtered = _searchIndex
                .Where(item => item.DisplayName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();

            FilteredSearchResults = new ObservableCollection<SearchBarItem>(filtered);

            if (FilteredSearchResults.Count > 0)
                IsSearchFlyoutOpen = true;

            OnPropertyChanged(nameof(HasAnyResults));
        }

        [RelayCommand]
        private async Task PlayGame(OmniSearchContent content)
        {
            if (content == null) return;
            Clear();

            Process.Start(new ProcessStartInfo
            {
                FileName = $"roblox://experiences/start?placeId={content.RootPlaceId}",
                UseShellExecute = true
            });

            MainWindow.ShowGlobalNotification(
            "Joining Game",
            $"Joinning {content.Name} using quick play.",
            InfoBarSeverity.Success,
            5000,
            FluentIcons.Common.Symbol.Globe
            );
        }

        [RelayCommand]
        private async Task RegionJoinGame(OmniSearchContent content)
        {
            if (content == null) return;

            bool joinSmallerServer = App.Settings.Prop.JoinSmallerServer;
            int bestRegionAmounts = App.Settings.Prop.BestRegionAmounts;
            int maxServerCheck = App.Settings.Prop.MaxServerCheck;

            Clear();

            MainWindow.ShowGlobalNotification(
                "Joining Game",
                $"Auto mode: Finding best region for you (checking {bestRegionAmounts} regions)",
                InfoBarSeverity.Informational,
                5000,
                FluentIcons.Common.Symbol.Globe
            );

            var fetcher = new RobloxServerFetcher();
            string? nextCursor = "";
            int serversChecked = 0;
            const int maxAttempts = 50;

            try
            {
                var datacentersResult = await fetcher.GetDatacentersAsync();
                if (datacentersResult == null) return;

                var (regions, dcMap) = datacentersResult.Value;

                Roblosecurity = await fetcher.ResolveCookieAsync();
                if (string.IsNullOrWhiteSpace(Roblosecurity))
                {
                    MainWindow.ShowGlobalNotification(
                        "No Valid Cookie Found",
                        "Log in using account manager or turn on 'Froststrap Account Permission' or report this to our discord server to use.",
                        InfoBarSeverity.Error,
                        5000,
                        FluentIcons.Common.Symbol.AccessibilityError
                    );
                    return;
                }

                List<string> topRegions = await GetClosestRegionsForAutoModeAsync(bestRegionAmounts);
                if (topRegions.Count == 0)
                {
                    MainWindow.ShowGlobalNotification(
                        "Auto Mode Failed",
                        "Could not determine your location. Please try again later.",
                        InfoBarSeverity.Warning,
                        5000,
                        FluentIcons.Common.Symbol.Warning
                    );
                    return;
                }

                var regionRank = new Dictionary<string, int>();
                for (int i = 0; i < topRegions.Count; i++)
                    regionRank[topRegions[i]] = i + 1;

                MainWindow.ShowGlobalNotification(
                    "Auto Mode",
                    $"Top {topRegions.Count} regions: {string.Join(", ", topRegions.Take(3))}",
                    InfoBarSeverity.Informational,
                    3000,
                    FluentIcons.Common.Symbol.Globe
                );

                string? bestServerId = null;
                string? bestServerRegion = null;
                int bestRank = int.MaxValue;
                int bestPlayers = int.MaxValue;
                int bestMaxPlayers = 0;

                while (serversChecked < maxServerCheck && serversChecked < maxAttempts)
                {
                    int sortOrder = joinSmallerServer ? 1 : 2;
                    var result = await fetcher.FetchServerInstancesAsync(content.RootPlaceId, nextCursor, sortOrder, Roblosecurity);

                    if (result?.Servers == null || result.Servers.Count == 0)
                    {
                        if (string.IsNullOrEmpty(nextCursor)) break;
                        await Task.Delay(500);
                        continue;
                    }

                    foreach (var server in result.Servers)
                    {
                        if (serversChecked >= maxServerCheck) break;

                        if (!server.DataCenterId.HasValue) continue;
                        if (!dcMap.TryGetValue(server.DataCenterId.Value, out var serverRegion)) continue;
                        if (server.Playing >= server.MaxPlayers) continue;

                        serversChecked++;

                        if (!regionRank.TryGetValue(serverRegion, out int rank)) continue;

                        bool isBetter = false;
                        if (rank < bestRank)
                        {
                            isBetter = true;
                        }
                        else if (rank == bestRank && joinSmallerServer && server.Playing < bestPlayers)
                        {
                            isBetter = true;
                        }
                        else if (rank == bestRank && !joinSmallerServer && server.Playing > bestPlayers)
                        {
                            isBetter = true;
                        }

                        if (isBetter)
                        {
                            bestRank = rank;
                            bestPlayers = server.Playing;
                            bestMaxPlayers = server.MaxPlayers;
                            bestServerId = server.Id;
                            bestServerRegion = serverRegion;
                            App.Logger.WriteLine("RegionJoinGame", $"Found better server in {serverRegion} (rank {rank}, players: {server.Playing}/{server.MaxPlayers})");

                            if (rank == 1)
                            {
                                App.Logger.WriteLine("RegionJoinGame", "Found rank 1 server, stopping early");
                                break;
                            }
                        }
                    }

                    if (bestRank == 1 && bestServerId != null)
                        break;

                    if (!string.IsNullOrEmpty(result.NextCursor))
                    {
                        nextCursor = result.NextCursor;
                    }
                    else
                    {
                        break;
                    }

                    await Task.Delay(200);
                }

                if (bestServerId != null)
                {
                    string playerCount = $"{bestPlayers}/{bestMaxPlayers}";
                    MainWindow.ShowGlobalNotification(
                        "Server Found",
                        $"Joining server in {bestServerRegion} (rank {bestRank}, {playerCount} players)",
                        InfoBarSeverity.Success,
                        5000,
                        FluentIcons.Common.Symbol.Checkmark
                    );

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"roblox://experiences/start?placeId={content.RootPlaceId}&gameInstanceId={bestServerId}",
                        UseShellExecute = true
                    });
                    return;
                }

                MainWindow.ShowGlobalNotification(
                    "Not Found",
                    $"Could not find a suitable server after checking {serversChecked} servers in {topRegions.Count} regions.",
                    InfoBarSeverity.Warning,
                    5000,
                    FluentIcons.Common.Symbol.Warning
                );
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SearchBarViewModel::RegionJoinGame", $"Exception: {ex.Message}");
                MainWindow.ShowGlobalNotification(
                    "Error",
                    $"Failed to join game: {ex.Message}",
                    InfoBarSeverity.Error,
                    5000,
                    FluentIcons.Common.Symbol.AccessibilityError
                );
            }
        }

        private async Task<List<string>> GetClosestRegionsForAutoModeAsync(int topCount)
        {
            try
            {
                var ipinfo = await Http.GetJson<IPInfoResponse>(new Uri("https://ipinfo.io/json"));
                if (string.IsNullOrEmpty(ipinfo?.Loc))
                    return new List<string>();

                string[] location = ipinfo.Loc.Split(',');
                double userLat = double.Parse(location[0], CultureInfo.InvariantCulture);
                double userLon = double.Parse(location[1], CultureInfo.InvariantCulture);

                var datacenters = await Http.GetJson<List<DatacenterEntry>>(new Uri("https://apis.rovalra.com/v1/datacenters/list"));
                if (datacenters == null || datacenters.Count == 0)
                    return new List<string>();

                var regionDistance = new Dictionary<string, double>();

                foreach (var dc in datacenters)
                {
                    if (dc.Location == null || dc.Location.LatLong == null || dc.Location.LatLong.Length < 2)
                        continue;

                    if (!double.TryParse(dc.Location.LatLong[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                        !double.TryParse(dc.Location.LatLong[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                        continue;

                    double distance = GetDistance(userLat, userLon, lat, lon);
                    string regionKey = $"{dc.Location.City}, {dc.Location.Country}".TrimStart(',').Trim();

                    if (!regionDistance.ContainsKey(regionKey) || distance < regionDistance[regionKey])
                        regionDistance[regionKey] = distance;
                }

                var closestRegions = regionDistance
                    .OrderBy(kvp => kvp.Value)
                    .Take(topCount)
                    .Select(kvp => kvp.Key)
                    .ToList();

                App.Logger.WriteLine("SearchBarViewModel::GetClosestRegionsForAutoMode", $"Top {closestRegions.Count} regions: {string.Join(", ", closestRegions)}");
                return closestRegions;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("SearchBarViewModel::GetClosestRegionsForAutoMode", ex);
                return new List<string>();
            }
        }

        private static double Deg2Rad(double deg) => deg * (Math.PI / 180.0);

        private static double GetDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            double dLat = Deg2Rad(lat2 - lat1);
            double dLon = Deg2Rad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                      Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
                      Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}