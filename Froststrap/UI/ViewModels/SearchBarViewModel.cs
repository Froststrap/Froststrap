using Avalonia.Threading;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Models;
using Froststrap.Models.APIs.Roblox;
using Froststrap.Integrations;
using Froststrap.Utility;
using Froststrap.Models.Entities;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Froststrap.UI.ViewModels
{
    public partial class SearchBarViewModel : ObservableObject
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
                }
            }
        }

        [ObservableProperty]
        private ObservableCollection<OmniSearchContent> _gameSearchResults = [];

        [ObservableProperty]
        private bool _isGameSearchLoading = false;

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
                GameSearchResults.Clear();
                return;
            }

            try
            {
                await Task.Delay(500, token);
                if (token.IsCancellationRequested) return;

                IsGameSearchLoading = true;

                List<OmniSearchContent> results = new();

                if (long.TryParse(query, out long placeId))
                {
                    try
                    {
                        var placeReq = new HttpRequestMessage(HttpMethod.Get, $"https://games.roblox.com/v1/games/multiget-place-details?placeIds={placeId}");
                        if (AccountManager.Shared.ActiveAccount != null)
                        {
                            var cookie = AccountManager.Shared.GetRoblosecurityForUser(AccountManager.Shared.ActiveAccount.UserId);
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
                                long universeId = placeElement.GetProperty("universeId").GetInt64();
                                string name = placeElement.GetProperty("name").GetString() ?? $"Place {placeId}";

                                results.Add(new OmniSearchContent
                                {
                                    RootPlaceId = placeId,
                                    UniverseId = (ulong)universeId,
                                    Name = name,
                                    PlayerCount = 0
                                });
                            }
                        }
                    }
                    catch { /* Fallback to standard search if this fails */ }
                }

                if (!results.Any())
                {
                    var searchResults = await GameSearching.GetGameSearchResultsAsync(query);
                    if (searchResults != null)
                        results.AddRange(searchResults);
                }

                if (token.IsCancellationRequested) return;

                if (results != null && results.Any())
                {
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
                                var response = await App.HttpClient.GetByteArrayAsync(fetchedUrls[i], token);
                                using var ms = new MemoryStream(response);
                                results[i].ThumbnailBitmap = new Bitmap(ms);
                            }
                            catch { /* Ignore image load failure */ }
                        }
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        GameSearchResults.Clear();
                        foreach (var res in results)
                            GameSearchResults.Add(res);

                        OnPropertyChanged(nameof(HasAnyResults));
                    }, DispatcherPriority.Background);
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        GameSearchResults.Clear();
                        OnPropertyChanged(nameof(HasAnyResults));
                    });
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SearchBarViewModel", $"Search error: {ex.Message}");
            }
            finally
            {
                IsGameSearchLoading = false;
                OnPropertyChanged(nameof(HasAnyResults));
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
                OnPropertyChanged(nameof(HasAnyResults));
                return;
            }

            var filtered = _searchIndex
                .Where(item => item.DisplayName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();

            FilteredSearchResults = new ObservableCollection<SearchBarItem>(filtered);
            OnPropertyChanged(nameof(HasAnyResults));
        }

        [RelayCommand]
        private async Task PlayGame(OmniSearchContent content)
        {
            if (content == null) return;
            var account = AccountManager.Shared.ActiveAccount;
            if (account == null) return;

            Clear();
            await AccountManager.Shared.LaunchAccountAsync(account, content.RootPlaceId);
        }

        [RelayCommand]
        private async Task RegionJoinGame(OmniSearchContent content)
        {
            if (content == null) return;

            var account = AccountManager.Shared.ActiveAccount;
            if (account == null) 
            {
                return; 
            }

            string selectedRegion = App.Settings.Prop.SelectedRegion ?? "";
            if (string.IsNullOrWhiteSpace(selectedRegion))
            {
                return; 
            }

            Clear();

            var fetcher = new RobloxServerFetcher();
            string? nextCursor = "";
            int attemptCount = 0;
            const int maxAttempts = 20;

            try
            {
                var datacentersResult = await fetcher.GetDatacentersAsync();
                if (datacentersResult == null) return;

                var (regions, dcMap) = datacentersResult.Value;

                string? cookie = AccountManager.Shared.GetRoblosecurityForUser(account.UserId);
                if (string.IsNullOrWhiteSpace(cookie)) return;

                while (attemptCount < maxAttempts)
                {
                    attemptCount++;
                    // Assume SortOrder 2 (Large servers)
                    var result = await fetcher.FetchServerInstancesAsync(content.RootPlaceId, cookie, nextCursor, 2);

                    if (result?.Servers == null || !result.Servers.Any())
                    {
                        await Task.Delay(1000);
                        continue;
                    }

                    var matchingServer = result.Servers.FirstOrDefault(server =>
                        server.DataCenterId.HasValue &&
                        dcMap.TryGetValue(server.DataCenterId.Value, out var mappedRegion) &&
                        mappedRegion == selectedRegion);

                    if (matchingServer != null)
                    {
                        await AccountManager.Shared.LaunchAccountAsync(account, content.RootPlaceId, matchingServer.Id);
                        return;
                    }

                    if (!string.IsNullOrEmpty(result.NextCursor))
                    {
                        nextCursor = result.NextCursor;
                    }
                    else
                    {
                        return; // Searched all servers
                    }

                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SearchBarViewModel::RegionJoinGame", $"Exception: {ex.Message}");
            }
        }
    }
}
