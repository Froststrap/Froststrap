// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Models.Overlay;
using Froststrap.RobloxInterfaces;
using System.Windows.Input;
using static Froststrap.RobloxInterfaces.Experiences;

namespace Froststrap.UI.ViewModels.Overlay
{
    internal class OverlaySearchBarViewModel : NotifyPropertyChangedViewModel
    {
        private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);
        private const int MaxResults = 30;

        private readonly DispatcherTimer _debounce;
        private int _generation;

        public BulkObservableCollection<OverlaySearchTabItem> Tabs { get; } = [];
        public BulkObservableCollection<GameTile> Results { get; } = [];

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

                OnPropertyChanged(nameof(SelectedTab));
                Clear();
            }
        }

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

                _debounce.Stop();

                if (String.IsNullOrWhiteSpace(value))
                {
                    _generation++;
                    Clear();
                    return;
                }

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
                Refreshed();
            }
        }

        public bool ShowResults => Results.Count > 0;

        public bool ShowEmpty => !IsBusy && Results.Count == 0;

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

        public event EventHandler? CloseRequested;

        public OverlaySearchBarViewModel()
        {
            Tabs.Add(new OverlaySearchTabItem { Kind = OverlaySearchTab.Games, Title = Strings.Menu_Overlay_SearchBar_Tab_Games, IsSelected = true });
            Tabs.Add(new OverlaySearchTabItem { Kind = OverlaySearchTab.Profiles, Title = Strings.Menu_Overlay_SearchBar_Tab_Profiles, IsEnabled = false });
            Tabs.Add(new OverlaySearchTabItem { Kind = OverlaySearchTab.Items, Title = Strings.Menu_Overlay_SearchBar_Tab_Items, IsEnabled = false });
            Tabs.Add(new OverlaySearchTabItem { Kind = OverlaySearchTab.Groups, Title = Strings.Menu_Overlay_SearchBar_Tab_Groups, IsEnabled = false });
            Tabs.Add(new OverlaySearchTabItem { Kind = OverlaySearchTab.Market, Title = Strings.Menu_Overlay_SearchBar_Tab_Market, IsEnabled = false });
            Tabs.Add(new OverlaySearchTabItem { Kind = OverlaySearchTab.Docs, Title = Strings.Menu_Overlay_SearchBar_Tab_Docs, IsEnabled = false });
            Tabs.Add(new OverlaySearchTabItem { Kind = OverlaySearchTab.DevForum, Title = Strings.Menu_Overlay_SearchBar_Tab_DevForum, IsEnabled = false });

            _debounce = new DispatcherTimer { Interval = SearchDelay };
            _debounce.Tick += async (_, _) =>
            {
                _debounce.Stop();
                await SearchAsync(Query);
            };

            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

            SelectTabCommand = new RelayCommand<OverlaySearchTabItem>(tab =>
            {
                if (tab?.IsEnabled == true)
                    SelectedTab = tab.Kind;
            });

            JoinCommand = new RelayCommand<GameTile>(Join);
        }

        public void OnShown() => Refreshed();

        public void OnHidden()
        {
            _debounce.Stop();
            _generation++;
            Query = String.Empty;
            Clear();
        }

        public void MoveSelection(int delta)
        {
            if (Results.Count == 0)
                return;

            int next = SelectedIndex + delta;

            if (next < 0)
                next = Results.Count - 1;
            else if (next >= Results.Count)
                next = 0;

            SelectedIndex = next;
        }

        public void JoinSelected()
        {
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

        private async Task SearchAsync(string query)
        {
            int generation = ++_generation;

            IsBusy = true;
            Refreshed();

            try
            {
                GameSearchPage page = await Experiences.SearchPageAsync(query.Trim(), null, MaxResults);

                if (generation != _generation)
                    return;

                Results.ReplaceAll(page.Tiles);

                foreach (GameTile tile in page.Tiles)
                    _ = tile.LoadIconAsync();

                SelectedIndex = page.Tiles.Count > 0 ? 0 : -1;
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

        private void Clear()
        {
            Results.ReplaceAll([]);
            SelectedIndex = -1;
            Refreshed();
        }

        private void Refreshed()
        {
            OnPropertyChanged(nameof(ShowResults));
            OnPropertyChanged(nameof(ShowEmpty));
            OnPropertyChanged(nameof(EmptyText));
        }
    }
}