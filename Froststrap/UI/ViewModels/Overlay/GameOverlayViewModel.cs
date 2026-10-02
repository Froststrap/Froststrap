// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Enums.Overlay;
using Froststrap.Integrations;
using Froststrap.UI.Elements.Overlay;
using Froststrap.Utility;
using LucideAvalonia.Enum;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Overlay
{
    internal class GameOverlayViewModel : NotifyPropertyChangedViewModel
    {
        private readonly GameOverlay _window;
        private readonly Integrations.Overlay? _overlay;
        private readonly ActivityWatcher? _activityWatcher;

        private readonly DispatcherTimer _sessionTimer;

        #region Account

        private string _profileIcon = String.Empty;
        public string ProfileIcon
        {
            get => _profileIcon;
            set
            {
                if (!SetProperty(ref _profileIcon, value))
                    return;

                _ = LoadProfileBitmapAsync();
            }
        }

        private Bitmap? _profileBitmap;
        public Bitmap? ProfileBitmap
        {
            get => _profileBitmap;
            private set => SetProperty(ref _profileBitmap, value);
        }

        private bool _loadingProfileBitmap;
        private string? _loadedProfileBitmapUrl;

        private async Task LoadProfileBitmapAsync()
        {
            string url = _profileIcon;

            if (_loadingProfileBitmap || String.IsNullOrEmpty(url) || _loadedProfileBitmapUrl == url)
                return;

            _loadingProfileBitmap = true;

            try
            {
                Bitmap? bitmap = await ImageLoader.LoadAsync(url, 80);

                if (bitmap is null)
                    return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_profileIcon == url)
                    {
                        ProfileBitmap = bitmap;
                        _loadedProfileBitmapUrl = url;
                    }
                });
            }
            finally
            {
                _loadingProfileBitmap = false;
            }
        }

        private string _displayName = String.Empty;
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (!SetProperty(ref _displayName, value))
                    return;

                OnPropertyChanged(nameof(HasProfileName));
            }
        }

        public Controls.OnlineStatusViewModel OnlineStatus { get; } = new();

        private string _username = String.Empty;
        public string Username
        {
            get => _username;
            set
            {
                if (!SetProperty(ref _username, value))
                    return;

                OnPropertyChanged(nameof(HasProfileName));
            }
        }

        public bool HasProfileName => !String.IsNullOrEmpty(_displayName) || !String.IsNullOrEmpty(_username);

        private long _profileId;
        private bool _profileLoaded;
        private bool _profileLoading;

        #endregion

        #region Current experience

        private string _gameIcon = String.Empty;
        public string GameIcon
        {
            get => _gameIcon;
            set
            {
                if (!SetProperty(ref _gameIcon, value))
                    return;

                _ = LoadGameBitmapAsync();
            }
        }

        private Bitmap? _gameBitmap;
        public Bitmap? GameBitmap
        {
            get => _gameBitmap;
            private set => SetProperty(ref _gameBitmap, value);
        }

        private bool _loadingGameBitmap;
        private string? _loadedGameBitmapUrl;

        private async Task LoadGameBitmapAsync()
        {
            string url = _gameIcon;

            if (_loadingGameBitmap || String.IsNullOrEmpty(url) || _loadedGameBitmapUrl == url)
                return;

            _loadingGameBitmap = true;

            try
            {
                Bitmap? bitmap = await ImageLoader.LoadAsync(url, 72);

                if (bitmap is null)
                    return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_gameIcon == url)
                    {
                        GameBitmap = bitmap;
                        _loadedGameBitmapUrl = url;
                    }
                });
            }
            finally
            {
                _loadingGameBitmap = false;
            }
        }

        private string _game = Strings.Menu_Overlay_NotInGame;
        public string Game
        {
            get => _game;
            set => SetProperty(ref _game, value);
        }

        private string _timePlayed = String.Empty;
        public string TimePlayed
        {
            get => _timePlayed;
            set => SetProperty(ref _timePlayed, value);
        }

        private string _currentRegion = String.Empty;
        public string CurrentRegion
        {
            get => _currentRegion;
            set
            {
                if (!SetProperty(ref _currentRegion, value))
                    return;

                OnPropertyChanged(nameof(HasCurrentRegion));
            }
        }

        public bool HasCurrentRegion => !String.IsNullOrEmpty(_currentRegion);

        private bool _isInGame;
        public bool IsInGame
        {
            get => _isInGame;
            set => SetProperty(ref _isInGame, value);
        }

        #endregion

        #region Panels

        private readonly HashSet<OverlayPanelKind> _open = [];

        public event EventHandler<OverlayPanelKind>? PanelOpened;

        public bool IsMessagesVisible => _open.Contains(OverlayPanelKind.Messages);
        public bool IsBadgesVisible => _open.Contains(OverlayPanelKind.Badges);
        public bool IsServersVisible => _open.Contains(OverlayPanelKind.Servers);
        public bool IsGamesVisible => _open.Contains(OverlayPanelKind.Games);
        public bool IsHistoryVisible => _open.Contains(OverlayPanelKind.History);
        public bool IsNotesVisible => _open.Contains(OverlayPanelKind.Notes);

        public ICommand TogglePanelCommand { get; }
        public ICommand ClosePanelCommand { get; }

        private void TogglePanel(string? name)
        {
            if (!Enum.TryParse(name, out OverlayPanelKind panel))
                return;

            if (_open.Add(panel))
                PanelOpened?.Invoke(this, panel);
            else
                _open.Remove(panel);

            NotifyPanels();
        }

        private void ClosePanel(string? name)
        {
            if (!Enum.TryParse(name, out OverlayPanelKind panel))
                return;

            _open.Remove(panel);
            NotifyPanels();
        }

        private void NotifyPanels()
        {
            OnPropertyChanged(nameof(IsMessagesVisible));
            OnPropertyChanged(nameof(IsBadgesVisible));
            OnPropertyChanged(nameof(IsServersVisible));
            OnPropertyChanged(nameof(IsGamesVisible));
            OnPropertyChanged(nameof(IsHistoryVisible));
            OnPropertyChanged(nameof(IsNotesVisible));
        }

        #endregion

        public ICommand CloseCommand { get; }

        #region Sharing

        private LucideIconNames _shareIcon = LucideIconNames.Share2;
        public LucideIconNames ShareIcon
        {
            get => _shareIcon;
            set => SetProperty(ref _shareIcon, value);
        }

        private DispatcherTimer? _shareTimer;
        public ICommand ShareCommand { get; }

        private async Task ShareAsync()
        {
            if (_activityWatcher?.InGame != true)
                return;

            try
            {
                var clipboard = _window.Clipboard;
                if (clipboard is null)
                    return;

                await clipboard.SetTextAsync(_activityWatcher.Data.GetInviteDeeplink());

                ShareIcon = LucideIconNames.Check;

                _shareTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _shareTimer.Tick -= ResetShareIcon;
                _shareTimer.Tick += ResetShareIcon;

                _shareTimer.Stop();
                _shareTimer.Start();
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to copy the invite link");
                App.Logger.Error(ex);
            }
        }

        private void ResetShareIcon(object? sender, EventArgs e)
        {
            _shareTimer?.Stop();
            ShareIcon = LucideIconNames.Share2;
        }

        #endregion

        private CornerRadius _scrimCornerRadius = new(0, 0, 8, 8);
        public CornerRadius ScrimCornerRadius
        {
            get => _scrimCornerRadius;
            set => SetProperty(ref _scrimCornerRadius, value);
        }

        public GameOverlayViewModel(GameOverlay window, Integrations.Overlay? overlay)
        {
            _window = window;
            _overlay = overlay;
            _activityWatcher = overlay?.ActivityWatcher;

            TogglePanelCommand = new RelayCommand<string>(TogglePanel);
            ClosePanelCommand = new RelayCommand<string>(ClosePanel);
            CloseCommand = new RelayCommand(window.Dismiss);
            ShareCommand = new AsyncRelayCommand(ShareAsync);

            App.Cookies.WatchAccount(this, static vm => vm.OnAccountChanged());

            foreach ((string name, OverlayPanelLayout panel) in App.OverlayLayout.Prop.Panels)
            {
                if (panel.Open && Enum.TryParse(name, out OverlayPanelKind kind))
                    _open.Add(kind);
            }

            _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _sessionTimer.Tick += (_, _) => UpdateSession();

            if (_overlay is not null)
            {
                _overlay.BoundsChanged += OnBoundsChanged;
                _overlay.GameVisibilityChanged += OnGameVisibilityChanged;
                _overlay.WindowClosed += OnWindowClosed;
            }

            if (_activityWatcher is null)
                return;

            _activityWatcher.OnGameJoin += (_, _) => Dispatcher.UIThread.Post(OnGameJoin);
            _activityWatcher.OnGameLeave += (_, _) => Dispatcher.UIThread.Post(OnGameLeave);
        }

        public async Task OnLoaded()
        {
            if (_activityWatcher?.InGame == true)
                OnGameJoin();

            await LoadProfileAsync();
        }

        private void OnAccountChanged() => Dispatcher.UIThread.Post(() => _ = SyncAccountAsync());

        public async Task SyncAccountAsync()
        {
            await App.Cookies.RefreshAsync();
            await LoadProfileAsync();
        }

        public async Task LoadProfileAsync()
        {
            const string LOG_IDENT = "GameOverlayViewModel::LoadProfileAsync";

            long playing = _activityWatcher?.InGame == true ? _activityWatcher.Data.UserId : 0;

            if (_profileLoading || (_profileLoaded && (playing == 0 || playing == _profileId)))
                return;

            _profileLoading = true;

            try
            {
                var local = await Task.Run(AppStorageManager.ReadAccount);
                long id = playing;

                if (id == 0 && await App.Cookies.EnsureLoadedAsync())
                    id = App.Cookies.CurrentUser?.Id ?? 0;

                if (id == 0)
                    id = local?.Id ?? 0;

                if (id == 0)
                {
                    App.Logger.Info($"{LOG_IDENT}: Couldn't tell which account is signed in");
                    return;
                }

                if (id != _profileId)
                {
                    _profileId = id;
                    _profileLoaded = false;

                    var known = local?.Id == id ? local : null;

                    DisplayName = known?.DisplayName ?? known?.Name ?? String.Empty;
                    Username = known?.Name is string name ? $"@{name}" : String.Empty;
                    ProfileIcon = String.Empty;
                    ProfileBitmap = null;
                }

                UserDetails details = await UserDetails.Fetch(id);

                DisplayName = details.Data.DisplayName;
                Username = $"@{details.Data.Name}";
                ProfileIcon = details.Thumbnail.ImageUrl ?? String.Empty;

                _profileLoaded = true;

                App.Logger.Info($"{LOG_IDENT}: Showing account {id}");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"{LOG_IDENT}: Failed to load the signed-in user");
                App.Logger.Error(ex);
            }
            finally
            {
                _profileLoading = false;
            }
        }

        private async void OnGameJoin()
        {
            IsInGame = true;

            UpdateSession();
            _sessionTimer.Start();

            _ = LoadProfileAsync();

            ActivityData? activity = _activityWatcher?.Data;

            if (activity is null)
                return;

            try
            {
                UniverseDetails? universe = await activity.EnsureUniverseDetailsAsync();

                Game = universe?.Data.Name ?? String.Empty;
                GameIcon = universe?.Thumbnail.ImageUrl ?? String.Empty;

                _ = LoadCurrentRegionAsync(activity);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load the current experience");
                App.Logger.Error(ex);
            }
        }

        private async Task LoadCurrentRegionAsync(ActivityData activity)
        {
            try
            {
                if (!activity.MachineAddressValid)
                    return;

                string? location = await activity.QueryServerLocation();

                if (String.IsNullOrEmpty(location))
                    return;

                await Dispatcher.UIThread.InvokeAsync(() => CurrentRegion = location);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load the current server region");
                App.Logger.Error(ex);
            }
        }

        private void OnGameLeave()
        {
            _sessionTimer.Stop();

            Game = Strings.Menu_Overlay_NotInGame;
            GameIcon = String.Empty;
            GameBitmap = null;
            _loadedGameBitmapUrl = null;
            TimePlayed = String.Empty;
            CurrentRegion = String.Empty;
            IsInGame = false;
        }

        private void UpdateSession()
        {
            DateTime? joined = _activityWatcher?.Data.TimeJoined;

            if (joined is null || joined == default(DateTime))
            {
                TimePlayed = String.Empty;
                return;
            }

            TimeSpan elapsed = DateTime.Now - joined.Value;

            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            TimePlayed = elapsed.ToString(
                elapsed.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss",
                CultureInfo.InvariantCulture);
        }

        private OverlayBounds? _pendingBounds;
        private bool _boundsQueued;

        private void OnBoundsChanged(object? sender, OverlayBounds bounds)
        {
            if (bounds.Rect.Width <= 0 || bounds.Rect.Height <= 0)
                return;

            _pendingBounds = bounds;

            if (_boundsQueued)
                return;

            _boundsQueued = true;

            Dispatcher.UIThread.Post(() =>
            {
                _boundsQueued = false;

                if (_pendingBounds is not null)
                    ApplyBounds(_pendingBounds);
            }, DispatcherPriority.Render);
        }

        private void ApplyBounds(OverlayBounds bounds)
        {
            double radius = bounds.IsMaximised ? 0 : 8;

            ScrimCornerRadius = new CornerRadius(0, 0, radius, radius);

            Rect rect = bounds.Rect;

            _window.Position = new PixelPoint((int)rect.Left, (int)rect.Top);

            double scaling = _window.RenderScaling;
            if (scaling <= 0)
                scaling = 1;

            _window.Width = Math.Max(rect.Width / scaling, 0);
            _window.Height = Math.Max(rect.Height / scaling, 0);
        }

        private void OnGameVisibilityChanged(object? sender, bool visible)
        {
            if (!visible)
                _window.Dismiss();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _sessionTimer.Stop();
            _window.Close();
        }
    }
}