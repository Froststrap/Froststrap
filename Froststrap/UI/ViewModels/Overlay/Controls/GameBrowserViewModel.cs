using System.Collections.ObjectModel;
using System.Windows.Input;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.Input;

using Froststrap.Integrations;
using Froststrap.Models.Overlay;
using Froststrap.RobloxInterfaces;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class GameBrowserViewModel : NotifyPropertyChangedViewModel
    {
        private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(400);

        private static readonly TimeSpan ContinueRefresh = TimeSpan.FromMinutes(1);

        private readonly ActivityWatcher? _activityWatcher;
        private readonly DispatcherTimer _searchTimer;
        private readonly FlashMessage _flash;

        private int _searchGeneration;
        private bool _favoritesLoaded;
        private bool _favoritesFailed;
        private bool _searchFailed;

        private bool _loadingContinue;
        private DateTime _continueLoaded = DateTime.MinValue;

        public ObservableCollection<GameTile> SearchResults { get; } = [];
        public ObservableCollection<GameTile> Favorites { get; } = [];
        public ObservableCollection<GameTile> ContinueTiles { get; } = [];

        public ObservableCollection<GameTile> ActiveTiles =>
            ShowingFavorites ? Favorites :
            ShowingContinue ? ContinueTiles :
            SearchResults;

        public bool ShowingContinue => !ShowingFavorites && String.IsNullOrWhiteSpace(Query);

        public bool ShowContinueLabel => ShowingContinue && ContinueTiles.Count > 0;

        private bool _showingFavorites;
        public bool ShowingFavorites
        {
            get => _showingFavorites;
            set
            {
                if (_showingFavorites == value)
                    return;

                _showingFavorites = value;

                OnPropertyChanged(nameof(ShowingFavorites));
                OnPropertyChanged(nameof(ShowingSearch));
                OnPropertyChanged(nameof(ShowingContinue));
                OnPropertyChanged(nameof(ShowContinueLabel));
                OnPropertyChanged(nameof(ActiveTiles));

                Refreshed();

                if (value && !_favoritesLoaded)
                    _ = LoadFavoritesAsync();
            }
        }

        public bool ShowingSearch
        {
            get => !ShowingFavorites;
            set => ShowingFavorites = !value;
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
                OnPropertyChanged(nameof(ShowingContinue));
                OnPropertyChanged(nameof(ShowContinueLabel));
                OnPropertyChanged(nameof(ActiveTiles));

                _searchTimer.Stop();

                if (String.IsNullOrWhiteSpace(value))
                {
                    _searchGeneration++;
                    _searchFailed = false;

                    SearchResults.Clear();

                    IsBusy = false;
                    Refreshed();
                    return;
                }

                _searchTimer.Start();
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

        public string StatusText => _flash.Text ?? String.Empty;
        public bool HasStatus => _flash.Text is not null;

        public bool ShowEmptyState => !IsBusy && ActiveTiles.Count == 0;

        public string EmptyText
        {
            get
            {
                if (ShowingFavorites)
                {
                    if (_favoritesFailed)
                        return Strings.Menu_Overlay_Games_Failed;

                    return UserId == 0
                        ? Strings.Menu_Overlay_Games_NoAccount
                        : Strings.Menu_Overlay_Games_NoFavorites;
                }

                if (ShowingContinue)
                    return Strings.Menu_Overlay_Games_SearchPrompt;

                if (String.IsNullOrWhiteSpace(Query))
                    return Strings.Menu_Overlay_Games_SearchPrompt;

                return _searchFailed
                    ? Strings.Menu_Overlay_Games_Failed
                    : String.Format(
                        CultureInfo.InvariantCulture,
                        Strings.Menu_Overlay_Games_NoResults,
                        Query.Trim());
            }
        }

        private long UserId
        {
            get
            {
                if (_activityWatcher?.Data.UserId is long playing and > 0)
                    return playing;

                return App.Cookies.CurrentUser?.Id ?? 0;
            }
        }

        public ICommand JoinCommand { get; }

        public GameBrowserViewModel(ActivityWatcher? activityWatcher)
        {
            _activityWatcher = activityWatcher;

            _flash = new FlashMessage(TimeSpan.FromSeconds(4), () =>
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(HasStatus));
            });

            JoinCommand = new RelayCommand<GameTile>(Join);

            _searchTimer = new DispatcherTimer { Interval = SearchDelay };
            _searchTimer.Tick += async (_, _) =>
            {
                _searchTimer.Stop();
                await SearchAsync(Query);
            };

            App.Cookies.WatchAccount(this, static vm => vm.OnAccountChanged());
        }

        private void OnAccountChanged() => Dispatcher.UIThread.Post(() =>
        {
            _favoritesLoaded = false;
            _favoritesFailed = false;
            _continueLoaded = DateTime.MinValue;

            Favorites.Clear();
            ContinueTiles.Clear();

            OnPropertyChanged(nameof(ShowContinueLabel));
            OnPropertyChanged(nameof(ActiveTiles));

            Refreshed();

            if (ShowingFavorites)
                _ = LoadFavoritesAsync();
            else if (ShowingContinue)
                _ = LoadContinueAsync(true);
        });

        public async Task LoadContinueAsync(bool force = false)
        {
            if (_loadingContinue || (!force && DateTime.UtcNow - _continueLoaded < ContinueRefresh))
                return;

            _loadingContinue = true;

            bool showBusy = ShowingContinue && ContinueTiles.Count == 0;

            if (showBusy)
            {
                IsBusy = true;
                Refreshed();
            }

            try
            {
                List<GameTile> tiles = await Experiences.ContinueAsync();

                ContinueTiles.Clear();

                foreach (GameTile tile in tiles)
                {
                    ContinueTiles.Add(tile);
                    _ = tile.LoadIconAsync();
                }

                _continueLoaded = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load the Continue list");
                App.Logger.Error(ex);
            }
            finally
            {
                _loadingContinue = false;

                if (showBusy)
                    IsBusy = false;

                OnPropertyChanged(nameof(ShowContinueLabel));
                OnPropertyChanged(nameof(ActiveTiles));
                Refreshed();
            }
        }

        private async Task SearchAsync(string query)
        {
            int generation = ++_searchGeneration;

            IsBusy = true;
            _searchFailed = false;
            Refreshed();

            try
            {
                List<GameTile> results = await Experiences.SearchAsync(query.Trim());

                if (generation != _searchGeneration)
                    return;

                SearchResults.Clear();

                foreach (GameTile tile in results)
                {
                    SearchResults.Add(tile);
                    _ = tile.LoadIconAsync();
                }
            }
            catch (Exception ex)
            {
                if (generation != _searchGeneration)
                    return;

                App.Logger.Error("Game search failed");
                App.Logger.Error(ex);

                SearchResults.Clear();
                _searchFailed = true;
            }
            finally
            {
                if (generation == _searchGeneration)
                {
                    IsBusy = false;
                    Refreshed();
                }
            }
        }

        public async Task LoadFavoritesAsync()
        {
            long userId = UserId;

            if (userId == 0)
            {
                Refreshed();
                return;
            }

            IsBusy = true;
            _favoritesFailed = false;
            Refreshed();

            try
            {
                List<GameTile> favorites = await Experiences.FavoritesAsync(userId);

                Favorites.Clear();

                foreach (GameTile tile in favorites)
                {
                    Favorites.Add(tile);
                    _ = tile.LoadIconAsync();
                }

                _favoritesLoaded = true;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load favorites");
                App.Logger.Error(ex);

                _favoritesFailed = true;
            }
            finally
            {
                IsBusy = false;
                Refreshed();
            }
        }

        private void Join(GameTile? tile)
        {
            if (tile is null)
                return;

            try
            {
                Experiences.Join(tile);

                Flash(String.Format(
                    CultureInfo.InvariantCulture,
                    Strings.Menu_Overlay_Games_Joining,
                    tile.Name));
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to join {tile.PlaceId}");
                App.Logger.Error(ex);
            }
        }

        private void Flash(string message) => _flash.Show(message);

        private void Refreshed()
        {
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(EmptyText));
            OnPropertyChanged(nameof(ShowContinueLabel));
        }
    }
}