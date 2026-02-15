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
using Bloxstrap.UI.ViewModels.AccountManagers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public partial class RegionSelectorViewModel : ObservableObject
    {
        private const string LOG_IDENT = "RegionSelectorViewModel";
        private readonly HashSet<string> _displayedServerIds = new();
        private RobloxServerFetcher? _fetcher;
        private Dictionary<int, string>? _dcMap;
        private CancellationTokenSource? _searchDebounceCts;

        [ObservableProperty] private bool _hasSearched;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SearchCommand))] private string _placeId = "";
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ServerListMessage), nameof(IsServerListEmptyAndNotLoading), nameof(ShowLoadingIndicator))]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand), nameof(LoadMoreCommand), nameof(SearchGamesCommand))] private bool _isLoading;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowLoadingIndicator))]
        [NotifyCanExecuteChangedFor(nameof(SearchGamesCommand))] private bool _isGameSearchLoading;
        [ObservableProperty] private string _loadingMessage = "";
        [ObservableProperty] private string _nextCursor = "";
        [ObservableProperty] private string? _roblosecurity;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ServerListMessage))]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand), nameof(SearchGamesCommand))] private bool _hasValidCookies;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SearchGamesCommand))] private string _searchQuery = "";
        [ObservableProperty] private GameSearchResult? _selectedSearchResult;
        [ObservableProperty] private int _selectedSortOrder = 2;
        [ObservableProperty] private int _lastFetchProcessedCount;

        public ObservableCollection<string> Regions { get; } = new();
        public ObservableCollection<ServerEntry> Servers { get; } = new();
        public ObservableCollection<GameSearchResult> SearchResults { get; } = new();

        public List<SortOrderComboBoxItem> SortOrderOptions { get; } = new()
        {
            new() { Content = "Large Servers", Tag = 2 },
            new() { Content = "Small Servers", Tag = 1 }
        };

        public bool IsServerListEmpty => Servers.Count == 0;
        public bool IsServerListEmptyAndNotLoading => IsServerListEmpty && !IsLoading;
        public bool ShowLoadingIndicator => IsLoading && !IsGameSearchLoading;

        public string ServerListMessage => !HasValidCookies ? "Dummy not found, Please notify us in our discord server." :
            IsLoading ? "" :
            !HasSearched ? "Enter a Place ID and click Search to view servers." :
            IsServerListEmpty ? (LastFetchProcessedCount == 0 ? "No public servers found." : "No servers found for specified region.") : "";

        public IAsyncRelayCommand SearchCommand { get; }
        public IAsyncRelayCommand LoadMoreCommand { get; }
        public IAsyncRelayCommand SearchGamesCommand { get; }

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
        }

        partial void OnSearchQueryChanged(string value)
        {
            if (long.TryParse(value, out _)) PlaceId = value;

            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();
            _ = DebouncedSearchTriggerAsync(_searchDebounceCts.Token);
        }

        partial void OnSelectedSearchResultChanged(GameSearchResult? value)
        {
            if (value == null) return;
            PlaceId = value.RootPlaceId.ToString();
            SearchQuery = value.RootPlaceId.ToString();
        }

        public string? SelectedRegion
        {
            get => App.Settings.Prop.SelectedRegion;
            set
            {
                App.Settings.Prop.SelectedRegion = value!;
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
                    await SearchGamesAsync();
            }
            catch (OperationCanceledException) { }
        }

        private async Task InitializeCookiesAsync()
        {
            try
            {
                await App.RemoteData.WaitUntilDataFetched();
                Roblosecurity = App.RemoteData.Prop.Dummy;

                if (!string.IsNullOrWhiteSpace(Roblosecurity))
                {
                    _fetcher = new RobloxServerFetcher();
                    HasValidCookies = await _fetcher.ValidateCookieAsync(Roblosecurity);
                }

                if (HasValidCookies) await LoadRegionsAsync();
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
                Frontend.ShowMessageBox("Please select a region first.", MessageBoxImage.Warning);
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

            var result = await _fetcher!.FetchServerInstancesAsync(placeIdLong, Roblosecurity, NextCursor, SelectedSortOrder);
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

        private string GetCachePath() => Path.Combine(Paths.Cache, "datacenters_cache.json");

        private async Task SaveDatacentersToCacheAsync((List<string> regions, Dictionary<int, string> datacenterMap) data)
        {
            try
            {
                Directory.CreateDirectory(Paths.Cache);
                var json = JsonSerializer.Serialize(new { data.regions, data.datacenterMap, LastUpdated = DateTime.UtcNow });
                await File.WriteAllTextAsync(GetCachePath(), json);
            }
            catch { /* Ignore cache save errors */ }
        }

        private async Task<(List<string> regions, Dictionary<int, string> datacenterMap)?> LoadDatacentersFromCacheAsync()
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

        private async Task SearchGamesAsync()
        {
            IsGameSearchLoading = true;
            LoadingMessage = "Searching games...";
            try
            {
                var results = await GameSearching.GetGameSearchResultsAsync(SearchQuery);
                foreach (var r in results) SearchResults.Add(r);
                LoadingMessage = SearchResults.Count == 0 ? "No games found." : "";
            }
            catch { LoadingMessage = "Search failed."; }
            finally { IsGameSearchLoading = false; }
        }

        private async Task LoadMoreServersAsync()
        {
            IsLoading = true;
            int initial = Servers.Count;
            for (int i = 0; i < 5 && !string.IsNullOrWhiteSpace(NextCursor); i++)
                await LoadServersAsync();
            IsLoading = false;
        }
    }
}