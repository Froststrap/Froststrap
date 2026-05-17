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
using Froststrap.UI.ViewModels.AccountManagers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace Froststrap.UI.ViewModels.Settings
{
    public class SortOrderComboBoxItem
    {
        public string Content { get; set; } = "";
        public int Tag { get; set; }
    }

    public class RegionSelectorViewModel : NotifyPropertyChangedViewModel
    {
        private const string LOG_IDENT = "RegionSelectorViewModel";
        private readonly HashSet<string> _displayedServerIds = [];
        private RobloxServerFetcher? _fetcher;
        private Dictionary<int, string>? _dcMap;
        private CancellationTokenSource? _searchDebounceCts;

        #region Fields
        private bool _hasSearched;
        private string _placeId = "";
        private bool _isLoading;
        private bool _isGameSearchLoading;
        private string _loadingMessage = "";
        private string _nextCursor = "";
        private string? _roblosecurity;
        private bool _hasValidCookies;
        private string _searchQuery = "";
        private OmniSearchContent? _selectedSearchResult;
        private int _selectedSortOrder = 2;
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

        public int SelectedSortOrder
        {
            get => _selectedSortOrder;
            set => SetProperty(ref _selectedSortOrder, value);
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
            new() { Content = "Large Servers", Tag = 2 },
            new() { Content = "Small Servers", Tag = 1 }
        ];

        public bool IsServerListEmpty => Servers.Count == 0;
        public bool IsServerListEmptyAndNotLoading => IsServerListEmpty && !IsLoading;
        public bool ShowLoadingIndicator => IsLoading && !IsGameSearchLoading;

        public string ServerListMessage => !HasValidCookies ? "Log in using account manager or turn on 'Froststrap Account Permission' or report this to our discord server to use." :
            IsLoading ? "" :
            !HasSearched ? "Enter a Place ID and click Search to view servers." :
            IsServerListEmpty ? (LastFetchProcessedCount == 0 ? "No public servers found." : "No servers found for specified region.") : "";

        public IAsyncRelayCommand SearchCommand { get; }
        public IAsyncRelayCommand LoadMoreCommand { get; }
        public IAsyncRelayCommand SearchGamesCommand { get; }
        #endregion

        public RegionSelectorViewModel()
        {
            Servers.CollectionChanged += (_, _) => {
                OnPropertyChanged(nameof(IsServerListEmpty));
                OnPropertyChanged(nameof(IsServerListEmptyAndNotLoading));
            };

            SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsLoading && !string.IsNullOrWhiteSpace(PlaceId) && HasValidCookies);
            SearchGamesCommand = new AsyncRelayCommand(SearchGamesAsync, () => !IsLoading && !IsGameSearchLoading && !string.IsNullOrWhiteSpace(SearchQuery) && HasValidCookies);
            LoadMoreCommand = new AsyncRelayCommand(LoadMoreServersAsync, () => !IsLoading && !string.IsNullOrWhiteSpace(NextCursor));

            _ = InitializeCookiesAsync();
            SelectedSortOrderItem = SortOrderOptions.FirstOrDefault(x => x.Tag == 2);
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

            PlaceId = value.RootPlaceId.ToString();
            SearchQuery = value.RootPlaceId.ToString();
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
            get => App.Settings.Prop.SelectedRegion;
            set
            {
                App.Settings.Prop.SelectedRegion = value ?? "";
                OnPropertyChanged();
                SearchCommand.NotifyCanExecuteChanged();
                App.Settings.Save();
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
            catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
        }

        private async Task LoadRegionsAsync()
        {
            IsLoading = true;
            LoadingMessage = "Loading datacenters...";

            var result = await _fetcher!.GetDatacentersAsync() ?? await LoadDatacentersFromCacheAsync();

            if (result == null)
            {
                LoadingMessage = "Failed to load datacenters.";
                IsLoading = false;
                return;
            }

            if (result.Value.regions != null)
            {
                Regions.Clear();
                foreach (var r in result.Value.regions) Regions.Add(r);
                _dcMap = result.Value.datacenterMap;
                await SaveDatacentersToCacheAsync(result.Value);
            }

            SelectedRegion = Regions.FirstOrDefault(r => r.Equals(App.Settings.Prop.SelectedRegion, StringComparison.OrdinalIgnoreCase)) ?? Regions.FirstOrDefault();

            LoadingMessage = $"Loaded {Regions.Count} regions.";
            IsLoading = false;
            await Task.Delay(800);
            LoadingMessage = "";
        }

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedRegion))
            {
                _ = Frontend.ShowMessageBox("Please select a region first.", MessageBoxImage.Warning);
                return;
            }

            HasSearched = true;
            IsLoading = true;
            LoadingMessage = "Searching servers...";
            Servers.Clear();
            _displayedServerIds.Clear();
            NextCursor = "";
            LastFetchProcessedCount = 0;

            int pagesChecked = 0;
            while (pagesChecked < 3)
            {
                await LoadServersAsync(pagesChecked == 0);
                pagesChecked++;
                if (string.IsNullOrWhiteSpace(NextCursor)) break;
            }

            IsLoading = false;
            await Task.Delay(800);
            LoadingMessage = "";
        }

        private async Task LoadServersAsync(bool resetCursor = false)
        {
            if (string.IsNullOrWhiteSpace(PlaceId) || string.IsNullOrWhiteSpace(SelectedRegion) || string.IsNullOrWhiteSpace(Roblosecurity)) return;

            if (resetCursor) NextCursor = "";
            if (!long.TryParse(PlaceId, out var placeIdLong)) return;

            var result = await _fetcher!.FetchServerInstancesAsync(placeIdLong, NextCursor, SelectedSortOrder, Roblosecurity);
            if (result == null) return;

            int number = Servers.Count + 1;
            foreach (var s in result.Servers)
            {
                if (_displayedServerIds.Add(s.Id) && s.DataCenterId.HasValue &&
                    _dcMap!.TryGetValue(s.DataCenterId.Value, out var mappedRegion) && mappedRegion == SelectedRegion)
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
            catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
        }

        private static string GetCachePath() => Path.Combine(Paths.Cache, "DataCentersCache.json");

        private static async Task SaveDatacentersToCacheAsync((List<string> regions, Dictionary<int, string> datacenterMap) data)
        {
            try
            {
                Directory.CreateDirectory(Paths.Cache);
                var json = JsonSerializer.Serialize(new { data.regions, data.datacenterMap, LastUpdated = DateTime.UtcNow });
                await File.WriteAllTextAsync(GetCachePath(), json);
            }
            catch { /* Ignore cache save errors */ }
        }

        private static async Task<(List<string> regions, Dictionary<int, string> datacenterMap)?> LoadDatacentersFromCacheAsync()
        {
            try
            {
                if (!File.Exists(GetCachePath())) return null;
                var json = await File.ReadAllTextAsync(GetCachePath());
                var cache = JsonSerializer.Deserialize<DatacentersCache>(json);
                return (cache != null && cache.LastUpdated > DateTime.UtcNow.AddDays(-7)) ? (cache.Regions, cache.DatacenterMap) : null;
            }
            catch { return null; }
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
                            var response = await App.HttpClient.GetByteArrayAsync(fetchedUrls[i], token);
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
            catch (Exception ex) { App.Logger.WriteLine(LOG_IDENT, $"Search error: {ex.Message}"); }
            finally { IsGameSearchLoading = false; }
        }

        private async Task LoadMoreServersAsync()
        {
            IsLoading = true;
            _ = Servers.Count;
            for (int i = 0; i < 5 && !string.IsNullOrWhiteSpace(NextCursor); i++)
                await LoadServersAsync();
            IsLoading = false;
        }
    }
}