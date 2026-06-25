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
                        var uriBuilder = new UriBuilder(UrlBuilder.BuildApiUrl("games", "v1/games/multiget-place-details", secure: true))
                        {
                            Query = $"placeIds={placeId}"
                        };
                        var placeReq = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
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
            Strings.Menu_SearchBar_JoiningGame,
            string.Format(Strings.Menu_SearchBar_JoiningName, content.Name),
            InfoBarSeverity.Success,
            5000,
            FluentIcons.Common.Symbol.Globe
            );
        }

        [RelayCommand]
        private async Task RegionJoinGame(OmniSearchContent content)
        {
            if (content == null) return;

            Clear();
            IsSearchFlyoutOpen = false;

            MainWindow.ShowGlobalNotification(
                Strings.Menu_SearchBar_JoiningGame,
                Strings.Menu_SearchBar_AutoJoin,
                InfoBarSeverity.Informational,
                5000,
                FluentIcons.Common.Symbol.Globe
            );

            try
            {
                var fetcher = new RobloxServerFetcher();

                bool success = await fetcher.JoinBestServerAsync(
                    content.RootPlaceId,
                    App.Settings.Prop.JoinSmallerServer,
                    App.Settings.Prop.BestRegionAmounts,
                    App.Settings.Prop.MaxServerCheck,
                    showConfirmation: false
                );

                if (success)
                {
                    MainWindow.ShowGlobalNotification(
                        Strings.Menu_SearchBar_ServerFound,
                        Strings.Menu_SearchBar_JoiningBest,
                        InfoBarSeverity.Success,
                        3000,
                        FluentIcons.Common.Symbol.Checkmark
                    );
                }
                else
                {
                    MainWindow.ShowGlobalNotification(
                        Strings.Common_NotFound,
                        Strings.Menu_SearchBar_NoSuitableServer,
                        InfoBarSeverity.Warning,
                        5000,
                        FluentIcons.Common.Symbol.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SearchBarViewModel::RegionJoinGame", $"Exception: {ex.Message}");
                MainWindow.ShowGlobalNotification(
                    Strings.Common_Error,
                    string.Format(Strings.Menu_SearchBar_JoinError, ex.Message),
                    InfoBarSeverity.Error,
                    5000,
                    FluentIcons.Common.Symbol.AccessibilityError
                );
            }
        }
    }
}