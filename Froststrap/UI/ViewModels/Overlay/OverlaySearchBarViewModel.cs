// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using Froststrap.Integrations.Overlay;
using Froststrap.Models.Overlay;
using Froststrap.RobloxInterfaces;
using Froststrap.UI.ViewModels.Overlay.Controls;
using Froststrap.UI.ViewModels.Overlay.SearchTabs;
using System.Windows.Input;
using static Froststrap.RobloxInterfaces.Experiences;

namespace Froststrap.UI.ViewModels.Overlay
{
    internal class OverlaySearchBarViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);
        private const int PageSize = 10;

        private readonly DispatcherTimer _debounce;
        private readonly CancellationTokenSource _disposeCts = new();
        private readonly HashSet<long> _joiningUniverses = [];
        private readonly List<GameTile> _searchBuffer = [];
        private string? _nextCursor;
        private bool _hasMorePages;
        private int _visibleCount;
        private bool _loadingMore;
        private int _generation;

        private bool _shown;
        private bool _showingContinue;
        private bool _disposed;

        public BulkObservableCollection<OverlaySearchTabItem> Tabs { get; } = [];
        public BulkObservableCollection<GameTile> Results { get; } = [];

        public PeopleBrowserViewModel People { get; } = new();

        private PrivateServersViewModel? _privateServers;
        public PrivateServersViewModel? PrivateServers
        {
            get => _privateServers;
            private set
            {
                if (_privateServers == value)
                    return;

                _privateServers = value;

                OnPropertyChanged(nameof(PrivateServers));
                OnPropertyChanged(nameof(ShowPrivateServers));
                OnPropertyChanged(nameof(ShowGamesTab));
                OnPropertyChanged(nameof(ShowPeopleTab));
            }
        }

        public bool ShowPrivateServers => _privateServers is not null;

        private OverlaySearchTab _selectedTab = OverlaySearchTab.Games;
        public OverlaySearchTab SelectedTab
        {
            get => _selectedTab;
            private set
            {
                if (_selectedTab == value)
                    return;

                _selectedTab = value;

                foreach (OverlaySearchTabItem tab in Tabs)
                    tab.IsSelected = tab.Kind == value;

                if (ShowPrivateServers)
                    ClosePrivateServers();

                OnPropertyChanged(nameof(SelectedTab));
                OnPropertyChanged(nameof(ShowGamesTab));
                OnPropertyChanged(nameof(ShowPeopleTab));

                if (value == OverlaySearchTab.People)
                {
                    People.SetQuery(_query);
                }
                else if (value == OverlaySearchTab.Games)
                {
                    if (String.IsNullOrWhiteSpace(_query))
                    {
                        if (Results.Count == 0 && _shown)
                            _ = LoadContinueAsync();
                    }
                    else
                    {
                        _debounce.Start();
                    }
                }

                FocusInputRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool ShowGamesTab => _selectedTab == OverlaySearchTab.Games && !ShowPrivateServers;
        public bool ShowPeopleTab => _selectedTab == OverlaySearchTab.People && !ShowPrivateServers;

        private string _query = String.Empty;
        public string Query
        {
            get => _query;
            set
            {
                if (_query == value)
                    return;

                _query = value;
                OnPropertyChanged(nameof(Query));

                if (ShowPrivateServers)
                    ClosePrivateServers();

                if (_selectedTab == OverlaySearchTab.People)
                {
                    People.SetQuery(value);
                    return;
                }

                _debounce.Stop();

                if (String.IsNullOrWhiteSpace(value))
                {
                    _generation++;
                    _showingContinue = false;
                    Clear();

                    if (_shown)
                        _ = LoadContinueAsync();

                    return;
                }

                _showingContinue = false;
                _debounce.Start();
            }
        }

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value)
                    return;

                _selectedIndex = value;
                OnPropertyChanged(nameof(SelectedIndex));
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsGamesLoading));
                Refreshed();
            }
        }

        private bool _isLoadingMore;
        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            private set
            {
                if (_isLoadingMore == value)
                    return;

                _isLoadingMore = value;
                OnPropertyChanged(nameof(IsLoadingMore));
                OnPropertyChanged(nameof(IsGamesLoading));
            }
        }

        public bool IsGamesLoading => IsBusy || IsLoadingMore;

        public bool ShowResults => Results.Count > 0;

        public bool ShowEmpty => !IsBusy && Results.Count == 0;

        public bool ShowContinueHeader => _showingContinue && Results.Count > 0;

        public string EmptyText
        {
            get
            {
                if (_selectedTab != OverlaySearchTab.Games)
                    return Strings.Menu_Overlay_SearchBar_ComingSoon;

                if (String.IsNullOrWhiteSpace(_query))
                    return Strings.Menu_Overlay_SearchBar_Prompt;

                return String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_SearchBar_NoResults, _query.Trim());
            }
        }

        public ICommand CloseCommand { get; }
        public ICommand SelectTabCommand { get; }
        public ICommand JoinCommand { get; }
        public ICommand JoinRegionCommand { get; }
        public ICommand OpenPrivateServersCommand { get; }
        public ICommand ClosePrivateServersCommand { get; }

        public event EventHandler? CloseRequested;
        public event EventHandler? FocusInputRequested;

        public OverlaySearchBarViewModel()
        {
            Tabs.Add(new OverlaySearchTabItem { Kind = OverlaySearchTab.Games, Title = Strings.Menu_Overlay_SearchBar_Tab_Games, IsSelected = true });
            Tabs.Add(new OverlaySearchTabItem { Kind = OverlaySearchTab.People, Title = Strings.Menu_Overlay_SearchBar_Tab_People });

            _debounce = new DispatcherTimer { Interval = SearchDelay };
            _debounce.Tick += async (_, _) =>
            {
                _debounce.Stop();

                if (_selectedTab == OverlaySearchTab.Games)
                    await SearchAsync(Query);
            };

            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

            SelectTabCommand = new RelayCommand<OverlaySearchTabItem>(tab =>
            {
                if (tab?.IsEnabled != true)
                    return;

                if (tab.Kind == _selectedTab)
                {
                    FocusInputRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                SelectedTab = tab.Kind;
            });

            JoinCommand = new RelayCommand<GameTile>(Join);
            JoinRegionCommand = new AsyncRelayCommand<GameTile>(JoinRegion);
            OpenPrivateServersCommand = new RelayCommand<GameTile>(OpenPrivateServers);
            ClosePrivateServersCommand = new RelayCommand(ClosePrivateServers);
        }

        public static void Attach(IOverlayHost? host)
        {
            _ = host;
        }

        public void Prewarm()
        {
            _ = LoadContinueAsync();
            People.Prewarm();
        }

        public void OnShown()
        {
            _shown = true;
            Refreshed();

            SelectedTab = OverlaySearchTab.Games;

            Query = String.Empty;

            ApplyJoiningState();

            if (!IsBusy && String.IsNullOrWhiteSpace(_query) && Results.Count == 0)
                _ = LoadContinueAsync();
        }

        public void OnHidden()
        {
            _shown = false;
        }

        public void MoveSelection(int delta)
        {
            if (_selectedTab == OverlaySearchTab.People)
            {
                People.MoveSelection(delta);
                return;
            }

            if (Results.Count == 0)
                return;

            int next = SelectedIndex + delta;

            if (next < 0)
            {
                next = Results.Count - 1;
            }
            else if (next >= Results.Count)
            {
                AppendBuffered();

                if (next >= Results.Count)
                    next = 0;
            }

            SelectedIndex = next;
        }

        public void JoinSelected()
        {
            if (_selectedTab == OverlaySearchTab.People)
            {
                People.JoinSelected();
                return;
            }

            if (SelectedIndex < 0 || SelectedIndex >= Results.Count)
                return;

            Join(Results[SelectedIndex]);
        }

        private void Join(GameTile? tile)
        {
            if (tile is null)
                return;

            try
            {
                App.Logger.Info($"Search bar: joining {tile.Name} ({tile.PlaceId})");
                Experiences.Join(tile);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Search bar failed to join a game");
                App.Logger.Error(ex);
            }

            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task JoinRegion(GameTile? tile)
        {
            if (tile is null || _joiningUniverses.Contains(tile.UniverseId))
                return;

            _joiningUniverses.Add(tile.UniverseId);
            ApplyJoiningState();

            bool success = false;
            bool cancelled = false;

            try
            {
                App.Logger.Info($"Search bar: looking for best region server for {tile.Name} ({tile.PlaceId})");

                using var fetcher = new RobloxServerFetcher();

                success = await fetcher.JoinBestServerAsync(
                    tile.PlaceId,
                    showConfirmation: false,
                    _disposeCts.Token);

                if (success)
                    App.Logger.Info($"Search bar: joined best region server for {tile.Name}");
                else
                    App.Logger.Warn($"Search bar: no region server found for {tile.Name}");
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                App.Logger.Info($"Search bar: region join cancelled for {tile.Name}");
            }
            catch (Exception ex)
            {
                App.Logger.Error("Search bar failed to join best region");
                App.Logger.Error(ex);
            }
            finally
            {
                _joiningUniverses.Remove(tile.UniverseId);
                ApplyJoiningState();
            }

            if (cancelled)
                return;

            if (success)
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            bool stillVisible = Results.Any(t => t.UniverseId == tile.UniverseId);

            if (!stillVisible)
                return;

            try
            {
                await Frontend.ShowMessageBox(
                    String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_SearchBar_JoinRegionFailed, tile.Name),
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to show region join failure message");
                App.Logger.Error(ex);
            }
        }

        private void ApplyJoiningState()
        {
            foreach (GameTile tile in Results)
                tile.IsJoiningRegion = _joiningUniverses.Contains(tile.UniverseId);
        }

        private void OpenPrivateServers(GameTile? tile)
        {
            if (tile is null || tile.PlaceId == 0)
                return;

            _privateServers?.SetVisible(false);

            var vm = new PrivateServersViewModel(
                activityWatcher: null,
                placeIdOverride: tile.PlaceId,
                onBack: () =>
                {
                    ClosePrivateServers();
                    FocusInputRequested?.Invoke(this, EventArgs.Empty);
                });

            PrivateServers = vm;
            vm.SetVisible(true);
        }

        private void ClosePrivateServers()
        {
            if (_privateServers is null)
                return;

            _privateServers.SetVisible(false);
            PrivateServers = null;
        }

        private async Task LoadContinueAsync()
        {
            if (!String.IsNullOrWhiteSpace(_query))
                return;

            int generation = ++_generation;

            IsBusy = true;
            Refreshed();

            try
            {
                List<GameTile> tiles = await Experiences.ContinueAsync();

                if (generation != _generation || !String.IsNullOrWhiteSpace(_query))
                    return;

                _searchBuffer.Clear();
                _searchBuffer.AddRange(tiles);
                _nextCursor = null;
                _hasMorePages = false;

                ResetVisible();

                _showingContinue = Results.Count > 0;
                SelectedIndex = Results.Count > 0 ? 0 : -1;
            }
            catch (Exception ex)
            {
                if (generation != _generation)
                    return;

                App.Logger.Error("Continue list failed");
                App.Logger.Error(ex);
            }
            finally
            {
                if (generation == _generation)
                {
                    IsBusy = false;
                    Refreshed();
                }
            }
        }

        private async Task SearchAsync(string query)
        {
            int generation = ++_generation;

            IsBusy = true;
            Refreshed();

            try
            {
                GameSearchPage page = await Experiences.SearchPageAsync(query.Trim(), null);

                if (generation != _generation)
                    return;

                _searchBuffer.Clear();
                _searchBuffer.AddRange(page.Tiles);
                _nextCursor = page.NextCursor;
                _hasMorePages = !String.IsNullOrEmpty(page.NextCursor);

                ResetVisible();

                _showingContinue = false;
                SelectedIndex = Results.Count > 0 ? 0 : -1;
            }
            catch (Exception ex)
            {
                if (generation != _generation)
                    return;

                App.Logger.Error("Search bar query failed");
                App.Logger.Error(ex);

                Results.ReplaceAll([]);
                SelectedIndex = -1;
            }
            finally
            {
                if (generation == _generation)
                {
                    IsBusy = false;
                    Refreshed();
                }
            }
        }

        public void ShowMoreGames()
        {
            if (_selectedTab != OverlaySearchTab.Games || ShowPrivateServers || IsBusy)
                return;

            int appended = AppendBuffered();

            if (appended == 0 && _hasMorePages && !_loadingMore && !String.IsNullOrWhiteSpace(_query))
                _ = LoadMoreSearchAsync();
        }

        private int AppendBuffered()
        {
            if (_visibleCount >= _searchBuffer.Count)
                return 0;

            int next = Math.Min(_searchBuffer.Count, _visibleCount + PageSize);
            List<GameTile> chunk = _searchBuffer.GetRange(_visibleCount, next - _visibleCount);

            Results.AddRange(chunk);
            _visibleCount = next;

            ApplyJoiningState();

            foreach (GameTile tile in chunk)
                _ = tile.LoadIconAsync();

            return chunk.Count;
        }

        private async Task LoadMoreSearchAsync()
        {
            _loadingMore = true;
            IsLoadingMore = true;

            int generation = _generation;

            try
            {
                GameSearchPage page = await Experiences.SearchPageAsync(_query.Trim(), _nextCursor);

                if (generation != _generation)
                    return;

                int added = 0;

                foreach (GameTile tile in page.Tiles)
                {
                    if (_searchBuffer.Any(x => x.UniverseId == tile.UniverseId))
                        continue;

                    _searchBuffer.Add(tile);
                    added++;
                }

                _nextCursor = page.NextCursor;
                _hasMorePages = added > 0 && !String.IsNullOrEmpty(page.NextCursor);

                AppendBuffered();
            }
            catch (Exception ex)
            {
                App.Logger.Error("Search bar failed to load more results");
                App.Logger.Error(ex);

                _hasMorePages = false;
            }
            finally
            {
                _loadingMore = false;
                IsLoadingMore = false;
            }
        }

        private void ResetVisible()
        {
            _visibleCount = Math.Min(PageSize, _searchBuffer.Count);

            Results.ReplaceAll([.. _searchBuffer.Take(_visibleCount)]);
            ApplyJoiningState();

            foreach (GameTile tile in Results)
                _ = tile.LoadIconAsync();
        }

        private void Clear()
        {
            _searchBuffer.Clear();
            _nextCursor = null;
            _hasMorePages = false;
            _visibleCount = 0;

            Results.ReplaceAll([]);
            SelectedIndex = -1;
            Refreshed();
        }

        private void Refreshed()
        {
            OnPropertyChanged(nameof(ShowResults));
            OnPropertyChanged(nameof(ShowEmpty));
            OnPropertyChanged(nameof(ShowContinueHeader));
            OnPropertyChanged(nameof(EmptyText));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _disposeCts.Cancel();
            _disposeCts.Dispose();

            _debounce.Stop();

            People.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}