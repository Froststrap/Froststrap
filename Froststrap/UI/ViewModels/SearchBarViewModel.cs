/*
*  Froststrap
*  Copyright (c) Froststrap Team
*
*  This file is part of Froststrap and is distributed under the terms of the
*  GNU Affero General Public License, version 3 or later.
*
*  SPDX-License-Identifier: AGPL-3.0-or-later
*/

using Avalonia.Threading;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using System.Collections.ObjectModel;
using Froststrap.UI.Elements.Settings;
using FluentIcons.Common;
using Avalonia.Controls.Notifications;
using FluentAvalonia.UI.Controls;
using System.Text.Json;

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

                    if (!DisableGameSearch)
                    {
                        TriggerGameSearch(value);
                    }
                    else
                    {
                        GameSearchResults.Clear();
                        IsGameSearchLoading = false;
                    }
                }
            }
        }

        public bool DisableGameSearch
        {
            get => App.Settings.Prop.DisableGameSearch;
            set
            {
                if (App.Settings.Prop.DisableGameSearch != value)
                {
                    App.Settings.Prop.DisableGameSearch = value;
                    OnPropertyChanged(nameof(DisableGameSearch));

                    if (value)
                    {
                        GameSearchResults.Clear();
                        IsGameSearchLoading = false;
                    }
                    else if (!string.IsNullOrWhiteSpace(SearchQuery))
                    {
                        TriggerGameSearch(SearchQuery);
                    }
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
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;

            _ = SearchGamesAsync(query, token);
        }

        private async Task SearchGamesAsync(string query, CancellationToken token)
        {
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
            if (string.IsNullOrWhiteSpace(NextPageCursor) || string.IsNullOrWhiteSpace(SearchQuery) || IsGameSearchLoading) return;

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

            string selectedRegion = App.Settings.Prop.SelectedRegion ?? "";
            if (string.IsNullOrWhiteSpace(selectedRegion)) return;

            Clear();

            MainWindow.ShowGlobalNotification(
                "Joining Game",
                $"Searching for region {selectedRegion}",
                InfoBarSeverity.Informational,
                5000,
                FluentIcons.Common.Symbol.Globe
            );

            var fetcher = new RobloxServerFetcher();
            string? nextCursor = "";
            int attemptCount = 0;
            const int maxAttempts = 20;

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
                    $"Log in using account manager or turn on 'Froststrap Account Permission' or report this to our discord server to use.",
                    InfoBarSeverity.Error,
                    5000,
                    FluentIcons.Common.Symbol.AccessibilityError
                    );
                    return;
                }

                while (attemptCount < maxAttempts)
                {
                    attemptCount++;
                    var result = await fetcher.FetchServerInstancesAsync(content.RootPlaceId, nextCursor, 2, Roblosecurity);

                    if (result?.Servers == null || result.Servers.Count == 0)
                    {
                        await Task.Delay(1000);
                        continue;
                    }

                    var matchingServer = result.Servers.FirstOrDefault(server =>
                    {
                        if (server.DataCenterId.HasValue && dcMap.TryGetValue(server.DataCenterId.Value, out var mappedRegion))
                        {
                            return mappedRegion == selectedRegion;
                        }
                        return false;
                    });

                    if (matchingServer != null)
                    {
                        MainWindow.ShowGlobalNotification(
                            "Server Found",
                            $"Joining server in {selectedRegion}",
                            InfoBarSeverity.Success,
                            5000,
                            FluentIcons.Common.Symbol.Checkmark
                        );

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = $"roblox://experiences/start?placeId={content.RootPlaceId}&gameInstanceId={matchingServer.Id}",
                            UseShellExecute = true
                        });
                        return;
                    }

                    if (!string.IsNullOrEmpty(result.NextCursor))
                    {
                        nextCursor = result.NextCursor;
                    }
                    else
                    {
                        MainWindow.ShowGlobalNotification(
                            "Not Found",
                            $"Could not find a server in {selectedRegion}.",
                            InfoBarSeverity.Warning,
                            5000,
                            FluentIcons.Common.Symbol.Warning
                        );
                        return;
                    }

                    await Task.Delay(500);
                }

                MainWindow.ShowGlobalNotification(
                    "Search Timeout",
                    $"Failed to find a server in {selectedRegion} after {maxAttempts} attempts.",
                    InfoBarSeverity.Warning,
                    5000,
                    FluentIcons.Common.Symbol.Warning
                );
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SearchBarViewModel::RegionJoinGame", $"Exception: {ex.Message}");
            }
        }
    }
}