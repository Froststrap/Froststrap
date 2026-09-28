// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Enums.Overlay;
using Froststrap.Integrations;
using Froststrap.UI.Elements.Overlay;
using LucideAvalonia.Enum;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
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
            set => Set(ref _profileIcon, value, nameof(ProfileIcon));
        }

        private string _displayName = String.Empty;
        public string DisplayName
        {
            get => _displayName;
            set => Set(ref _displayName, value, nameof(DisplayName));
        }

        public Controls.OnlineStatusViewModel OnlineStatus { get; } = new();

        private long _shownUserId;

        private string _username = String.Empty;
        public string Username
        {
            get => _username;
            set => Set(ref _username, value, nameof(Username));
        }

        #endregion

        #region Current experience

        private string _gameIcon = String.Empty;
        public string GameIcon
        {
            get => _gameIcon;
            set => Set(ref _gameIcon, value, nameof(GameIcon));
        }

        private string _game = Strings.Menu_Overlay_NotInGame;
        public string Game
        {
            get => _game;
            set => Set(ref _game, value, nameof(Game));
        }

        private string _timePlayed = String.Empty;
        public string TimePlayed
        {
            get => _timePlayed;
            set => Set(ref _timePlayed, value, nameof(TimePlayed));
        }

        private string _currentRegion = String.Empty;
        public string CurrentRegion
        {
            get => _currentRegion;
            set
            {
                if (_currentRegion == value)
                    return;

                _currentRegion = value;

                OnPropertyChanged(nameof(CurrentRegion));
                OnPropertyChanged(nameof(HasCurrentRegion));
            }
        }

        public bool HasCurrentRegion => !String.IsNullOrEmpty(_currentRegion);

        private bool _isInGame;
        public bool IsInGame
        {
            get => _isInGame;
            set => Set(ref _isInGame, value, nameof(IsInGame));
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
            set => Set(ref _shareIcon, value, nameof(ShareIcon));
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
            set => Set(ref _scrimCornerRadius, value, nameof(ScrimCornerRadius));
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

            await LoadAccountAsync();
        }

        private void OnAccountChanged() => Dispatcher.UIThread.Post(() => _ = SyncAccountAsync());

        public async Task SyncAccountAsync()
        {
            await App.Cookies.RefreshAsync();

            if ((App.Cookies.CurrentUser?.Id ?? 0) != _shownUserId)
                await LoadAccountAsync();
        }

        private async Task LoadAccountAsync()
        {
            if (!App.Settings.Prop.AllowCookieAccess)
                return;

            try
            {
                if (!App.Cookies.Loaded)
                    await Task.Run(App.Cookies.LoadCookies);

                AuthenticatedUser? current = App.Cookies.CurrentUser;

                if (current is null)
                {
                    _shownUserId = 0;
                    DisplayName = String.Empty;
                    Username = String.Empty;
                    ProfileIcon = String.Empty;
                    return;
                }

                UserDetails details = await UserDetails.Fetch(current.Id);

                DisplayName = details.Data.DisplayName;
                Username = $"@{details.Data.Name}";
                ProfileIcon = details.Thumbnail.ImageUrl ?? String.Empty;
                _shownUserId = current.Id;

                App.Logger.Info($"Showing account {current.Id}");
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load the signed-in user");
                App.Logger.Error(ex);
            }
        }

        private async void OnGameJoin()
        {
            IsInGame = true;

            UpdateSession();

            _sessionTimer.Start();

            ActivityData? activity = _activityWatcher?.Data;

            if (activity is null)
                return;

            try
            {
                if (activity.UniverseDetails is null)
                {
                    await UniverseDetails.FetchSingle(activity.UniverseId);

                    activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
                }

                Game = activity.UniverseDetails?.Data.Name ?? String.Empty;
                GameIcon = activity.UniverseDetails?.Thumbnail.ImageUrl ?? String.Empty;

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

        private void Set<T>(ref T field, T value, string name)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;

            OnPropertyChanged(name);
        }
    }
}