// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Froststrap.Integrations;
using Froststrap.Integrations.AccountManager;
using Froststrap.RobloxInterfaces;
using Froststrap.UI.Elements.Settings;
using LucideAvalonia.Enum;
using System.Collections.ObjectModel;
using System.Windows.Input;

using BadgesApi = Froststrap.RobloxInterfaces.Badges;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal sealed class GamePageViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        internal enum GamePageTab
        {
            About,
            Badges,
            Subplaces,
            Servers
        }

        private readonly QuickPlayGameItem _item;
        private readonly Action? _onBack;
        private readonly Action<long>? _onPlaceResolved;
        private readonly Action? _onFavoriteChanged;
        private readonly CancellationTokenSource _cts = new();

        private GameDetailResponse? _details;
        private bool _disposed;
        private bool _placeResolved;
        private bool _subplacesLoaded;
        private bool _badgesLoaded;
        private bool _favoriteInFlight;

        public GamePageViewModel(QuickPlayGameItem item, Action? onBack = null, Action<long>? onPlaceResolved = null, Action? onFavoriteChanged = null)
        {
            _item = item;
            _onBack = onBack;
            _onPlaceResolved = onPlaceResolved;
            _onFavoriteChanged = onFavoriteChanged;

            PlaceId = item.PlaceId;
            UniverseId = item.UniverseId;
            Name = item.Name;
            Creator = item.Creator;
            IsVerified = item.IsVerified;

            PlayCommand = new RelayCommand(Play);
            JoinBestRegionCommand = new AsyncRelayCommand(JoinBestRegionAsync, () => CanJoinBestRegion);
            VisitPageCommand = new RelayCommand(() => GameServers.OpenGamePage(PlaceId));
            BackCommand = new RelayCommand(() => _onBack?.Invoke());
            JoinSubplaceCommand = new RelayCommand<PlaceInfo>(subplace =>
            {
                if (subplace is not null)
                    GameServers.Join(subplace.Id);
            });

            ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync);

            SelectTabCommand = new RelayCommand<string>(SelectTab);
        }

        private void NotifyPlaceResolved()
        {
            if (_placeResolved || PlaceId == 0)
                return;

            _placeResolved = true;
            _onPlaceResolved?.Invoke(PlaceId);
        }

        private void SelectTab(string? tab)
        {
            if (Enum.TryParse(tab, out GamePageTab parsed))
                SelectedTab = parsed;
        }

        public long PlaceId { get; private set; }
        public long UniverseId { get; private set; }

        private string _name = String.Empty;
        public string Name
        {
            get => _name;
            private set => SetProperty(ref _name, value);
        }

        private string _creator = String.Empty;
        public string Creator
        {
            get => _creator;
            private set => SetProperty(ref _creator, value);
        }

        private bool _isVerified;
        public bool IsVerified
        {
            get => _isVerified;
            private set => SetProperty(ref _isVerified, value);
        }

        private string _description = String.Empty;
        public string Description
        {
            get => _description;
            private set
            {
                if (SetProperty(ref _description, value))
                    OnPropertyChanged(nameof(HasDescription));
            }
        }

        public bool HasDescription => !String.IsNullOrWhiteSpace(Description);

        private string _genre = String.Empty;
        public string Genre
        {
            get => _genre;
            private set => SetProperty(ref _genre, value);
        }

        private int _maxPlayers;
        public int MaxPlayers
        {
            get => _maxPlayers;
            private set => SetProperty(ref _maxPlayers, value);
        }

        private long _playing;
        public long Playing
        {
            get => _playing;
            private set => SetProperty(ref _playing, value);
        }

        private long _visits;
        public long Visits
        {
            get => _visits;
            private set => SetProperty(ref _visits, value);
        }

        private int _favoritedCount;
        public int FavoritedCount
        {
            get => _favoritedCount;
            private set => SetProperty(ref _favoritedCount, value);
        }

        private string _createdText = String.Empty;
        public string CreatedText
        {
            get => _createdText;
            private set => SetProperty(ref _createdText, value);
        }

        private string _updatedText = String.Empty;
        public string UpdatedText
        {
            get => _updatedText;
            private set => SetProperty(ref _updatedText, value);
        }

        private Bitmap? _heroBitmap;
        public Bitmap? HeroBitmap
        {
            get => _heroBitmap;
            private set => SetProperty(ref _heroBitmap, value);
        }

        private Bitmap? _iconBitmap;
        public Bitmap? IconBitmap
        {
            get => _iconBitmap;
            private set => SetProperty(ref _iconBitmap, value);
        }

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (SetProperty(ref _isLoading, value))
                    OnPropertyChanged(nameof(IsContentVisible));
            }
        }

        private bool _isUnavailable;
        public bool IsUnavailable
        {
            get => _isUnavailable;
            private set
            {
                if (SetProperty(ref _isUnavailable, value))
                    OnPropertyChanged(nameof(IsContentVisible));
            }
        }

        public bool IsContentVisible => !IsLoading && !IsUnavailable;

        private bool _isFavorited;
        public bool IsFavorited
        {
            get => _isFavorited;
            set
            {
                if (SetProperty(ref _isFavorited, value))
                    OnPropertyChanged(nameof(FavoriteText));
            }
        }

        public string FavoriteText => IsFavorited
            ? Strings.Menu_QuickPlay_GamePage_Favorited
            : Strings.Menu_QuickPlay_GamePage_Favorite;

        public ObservableCollection<SocialLinkItem> SocialLinks { get; } = [];
        public bool HasSocialLinks => SocialLinks.Count > 0;

        public ObservableCollection<PlaceInfo> Subplaces { get; } = [];
        public bool HasSubplaces => Subplaces.Count > 0;
        public bool ShowSubplacesEmpty => _subplacesLoaded && !IsLoadingSubplaces && !HasSubplaces;

        private bool _isLoadingSubplaces;
        public bool IsLoadingSubplaces
        {
            get => _isLoadingSubplaces;
            private set
            {
                if (SetProperty(ref _isLoadingSubplaces, value))
                    OnPropertyChanged(nameof(ShowSubplacesEmpty));
            }
        }

        public ObservableCollection<Badge> Badges { get; } = [];
        public bool HasBadges => Badges.Count > 0;
        public bool ShowBadgesEmpty => _badgesLoaded && !IsLoadingBadges && !HasBadges;

        private bool _isLoadingBadges;
        public bool IsLoadingBadges
        {
            get => _isLoadingBadges;
            private set
            {
                if (SetProperty(ref _isLoadingBadges, value))
                    OnPropertyChanged(nameof(ShowBadgesEmpty));
            }
        }

        private GamePageTab _selectedTab = GamePageTab.About;
        public GamePageTab SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (!SetProperty(ref _selectedTab, value))
                    return;

                if (value == GamePageTab.Subplaces && !_subplacesLoaded)
                    _ = LoadSubplacesAsync();
                else if (value == GamePageTab.Badges && !_badgesLoaded)
                    _ = LoadBadgesAsync();
            }
        }

        public int SelectedTabIndex
        {
            get => (int)SelectedTab;
            set => SelectedTab = (GamePageTab)value;
        }

        private bool _isJoiningBestRegion;
        public bool IsJoiningBestRegion
        {
            get => _isJoiningBestRegion;
            private set
            {
                if (SetProperty(ref _isJoiningBestRegion, value))
                {
                    OnPropertyChanged(nameof(CanJoinBestRegion));
                    JoinBestRegionCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool CanJoinBestRegion => !IsJoiningBestRegion;

        public ICommand PlayCommand { get; }
        public IAsyncRelayCommand JoinBestRegionCommand { get; }
        public ICommand VisitPageCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand JoinSubplaceCommand { get; }
        public IAsyncRelayCommand ToggleFavoriteCommand { get; }
        public ICommand SelectTabCommand { get; }

        public async Task LoadAsync()
        {
            IsLoading = true;

            try
            {
                await ResolveUniverseAsync();
                NotifyPlaceResolved();
                await LoadDetailsAsync();
                _ = LoadImagesAsync();
                _ = LoadSocialLinksAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to load the experience page for {PlaceId}");
                App.Logger.Error(ex);
            }
            finally
            {
                if (!_cts.IsCancellationRequested)
                {
                    IsUnavailable = _details is null && String.IsNullOrEmpty(Name);
                    IsLoading = false;
                }
            }
        }

        private async Task ResolveUniverseAsync()
        {
            if (UniverseId != 0 || PlaceId == 0)
                return;

            long? resolved = await UniverseDetails.ResolveUniverseId(PlaceId);

            if (resolved is > 0)
            {
                UniverseId = resolved.Value;
                OnPropertyChanged(nameof(UniverseId));
            }
        }

        private async Task LoadDetailsAsync()
        {
            GameDetailResponse? details = _item.OriginalDetails?.Data;

            if (details is null && UniverseId != 0)
            {
                await UniverseDetails.FetchSingle(UniverseId);
                details = UniverseDetails.LoadFromCache(UniverseId)?.Data;
            }

            if (details is null)
                return;

            _details = details;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Name = details.Name ?? Strings.Menu_QuickPlay_UnknownGame;
                Creator = details.Creator?.Name ?? Strings.Common_Unknown;
                IsVerified = details.Creator?.HasVerifiedBadge ?? false;
                Description = details.Description ?? String.Empty;
                Genre = String.IsNullOrWhiteSpace(details.Genre) ? Strings.Common_Unknown : details.Genre;
                MaxPlayers = details.MaxPlayers;
                Playing = details.Playing;
                Visits = details.Visits;
                FavoritedCount = details.FavoritedCount;
                IsFavorited = details.IsFavoritedByUser;
                CreatedText = FormatDate(details.Created);
                UpdatedText = FormatDate(details.Updated);

                if (PlaceId == 0)
                    PlaceId = details.RootPlaceId;

                NotifyPlaceResolved();
            });
        }

        private static string FormatDate(DateTime value) =>
            value == default ? String.Empty : value.ToLocalTime().ToString("d MMM yyyy", Locale.CurrentCulture);

        private async Task LoadImagesAsync()
        {
            CancellationToken token = _cts.Token;

            try
            {
                if (UniverseId == 0)
                    return;

                string? heroUrl = await Thumbnails.GetGameThumbnailUrlAsync(UniverseId, token);

                if (token.IsCancellationRequested)
                    return;

                var requests = new List<ThumbnailRequest>
                {
                    new()
                    {
                        TargetId = (ulong)UniverseId,
                        Type = ThumbnailType.GameIcon,
                        Size = ThumbnailSize.Detailed,
                        Format = ThumbnailFormat.Png
                    }
                };

                string?[] urls = await Thumbnails.GetThumbnailUrlsAsync(requests, token);

                if (token.IsCancellationRequested)
                    return;

                Bitmap? hero = await ImageLoader.LoadAsync(heroUrl, 900);
                Bitmap? icon = await ImageLoader.LoadAsync(urls.ElementAtOrDefault(0), 300);

                if (token.IsCancellationRequested)
                    return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    if (hero is not null)
                        HeroBitmap = hero;

                    if (icon is not null)
                        IconBitmap = icon;
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to load the experience thumbnails for {UniverseId}");
                App.Logger.Error(ex);
            }
        }

        private async Task LoadSocialLinksAsync()
        {
            CancellationToken token = _cts.Token;

            try
            {
                if (UniverseId == 0)
                    return;

                Uri url = UrlBuilder.BuildApiUrl("games", $"v1/games/{UniverseId}/social-links/list");

                ApiArrayResponse<SocialLink> response = App.Cookies.Loaded
                    ? await Http.AuthGetJson<ApiArrayResponse<SocialLink>>(url)
                    : await Http.GetJson<ApiArrayResponse<SocialLink>>(url);

                if (token.IsCancellationRequested)
                    return;

                List<SocialLinkItem> links = (response?.Data ?? [])
                    .Where(link => !String.IsNullOrWhiteSpace(link.Url))
                    .Select(SocialLinkItem.From)
                    .ToList();

                if (links.Count == 0)
                    return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    SocialLinks.Clear();

                    foreach (SocialLinkItem link in links)
                        SocialLinks.Add(link);

                    OnPropertyChanged(nameof(HasSocialLinks));
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                App.Logger.Warn($"Failed to load the social links for {UniverseId}");
                App.Logger.Error(ex);
            }
        }

        private async Task LoadSubplacesAsync()
        {
            CancellationToken token = _cts.Token;

            _subplacesLoaded = true;
            IsLoadingSubplaces = true;

            try
            {
                if (UniverseId == 0)
                    return;

                Uri url = UrlBuilder.BuildApiUrl(
                    "develop",
                    $"v1/universes/{UniverseId}/places?isUniverseCreation=false&limit=100&sortOrder=Asc");

                ApiPageResponse<SubplaceData> response = await Http.GetJson<ApiPageResponse<SubplaceData>>(url);

                if (token.IsCancellationRequested || response?.Data is null)
                    return;

                List<PlaceInfo> places = response.Data
                    .Select(place => new PlaceInfo(place.Id, place.UniverseId, place.Name, "", thumbnailDecodeWidth: 180))
                    .ToList();

                if (places.Count > 0)
                {
                    try
                    {
                        var thumbRequests = places
                            .Select(place => new ThumbnailRequest
                            {
                                TargetId = (ulong)place.Id,
                                Type = ThumbnailType.PlaceIcon,
                                Size = ThumbnailSize.Cyberpunk,
                                Format = ThumbnailFormat.Png
                            })
                            .ToList();

                        string?[] urls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, token);

                        if (token.IsCancellationRequested)
                            return;

                        for (int i = 0; i < places.Count; i++)
                            places[i].ThumbnailUrl = urls.ElementAtOrDefault(i) ?? "";
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"Subplace thumbnail fetch failed: {ex.Message}");
                    }
                }

                if (token.IsCancellationRequested)
                    return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    Subplaces.Clear();

                    foreach (PlaceInfo place in places)
                        Subplaces.Add(place);

                    OnPropertyChanged(nameof(HasSubplaces));
                    OnPropertyChanged(nameof(ShowSubplacesEmpty));
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                App.Logger.Error($"Subplace fetch failed: {ex.Message}");
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsLoadingSubplaces = false;
                    OnPropertyChanged(nameof(ShowSubplacesEmpty));
                }
            }
        }

        private async Task LoadBadgesAsync()
        {
            CancellationToken token = _cts.Token;

            _badgesLoaded = true;
            IsLoadingBadges = true;

            try
            {
                if (UniverseId == 0)
                    return;

                long userId = AccountManager.Shared?.ActiveAccount?.UserId ?? 0;

                List<Badge> badges = await BadgesApi.FetchAsync(UniverseId, userId);

                if (token.IsCancellationRequested)
                    return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    Badges.Clear();

                    foreach (Badge badge in badges)
                        Badges.Add(badge);

                    OnPropertyChanged(nameof(HasBadges));
                    OnPropertyChanged(nameof(ShowBadgesEmpty));
                });

                foreach (Badge badge in badges)
                    _ = badge.LoadIconAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                App.Logger.Error($"Badge fetch failed: {ex.Message}");
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsLoadingBadges = false;
                    OnPropertyChanged(nameof(ShowBadgesEmpty));
                }
            }
        }

        private void Play()
        {
            if (PlaceId != 0)
                GameServers.Join(PlaceId);
        }

        private async Task ToggleFavoriteAsync()
        {
            if (UniverseId == 0 || _favoriteInFlight || _disposed)
                return;

            if (AccountManager.Shared?.ActiveAccount is null || !App.Cookies.Loaded)
            {
                await Frontend.ShowMessageBox(Strings.Menu_QuickPlay_PleaseSelectAccount, MessageBoxImage.Warning);
                return;
            }

            bool target = !IsFavorited;
            _favoriteInFlight = true;

            try
            {
                bool success = await Experiences.SetFavoriteAsync(UniverseId, target);

                if (_disposed)
                    return;

                if (!success)
                {
                    await Frontend.ShowMessageBox(Strings.Menu_QuickPlay_GamePage_FavoriteFailed, MessageBoxImage.Warning);
                    return;
                }

                IsFavorited = target;
                FavoritedCount = Math.Max(0, FavoritedCount + (target ? 1 : -1));
                _onFavoriteChanged?.Invoke();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to toggle favorite for {UniverseId}: {ex.Message}");

                if (!_disposed)
                    await Frontend.ShowMessageBox(Strings.Menu_QuickPlay_GamePage_FavoriteFailed, MessageBoxImage.Warning);
            }
            finally
            {
                _favoriteInFlight = false;
            }
        }

        private async Task JoinBestRegionAsync()
        {
            if (PlaceId == 0 || IsJoiningBestRegion || _disposed)
                return;

            if (AccountManager.Shared?.ActiveAccount is null)
            {
                await Frontend.ShowMessageBox(Strings.Menu_QuickPlay_PleaseSelectAccount, MessageBoxImage.Warning);
                return;
            }

            IsJoiningBestRegion = true;

            try
            {
                using var fetcher = new RobloxServerFetcher();

                bool success = await fetcher.JoinBestServerAsync(PlaceId, showConfirmation: false);

                if (_disposed)
                    return;

                if (success)
                {
                    MainWindow.ShowGlobalNotification(
                        Strings.Menu_QuickPlay_BestRegionJoined_Title,
                        String.Format(
                            Locale.CurrentCulture,
                            Strings.Menu_QuickPlay_BestRegionJoined_Message,
                            Name),
                        FAInfoBarSeverity.Success,
                        4000,
                        LucideIconNames.Globe);
                }
            }
            finally
            {
                if (!_disposed)
                    IsJoiningBestRegion = false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _cts.Cancel();
            _cts.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
