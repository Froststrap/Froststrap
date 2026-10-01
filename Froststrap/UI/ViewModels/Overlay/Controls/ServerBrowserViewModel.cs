using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Enums.Overlay;
using Froststrap.Integrations;
using Froststrap.Models.Overlay;
using Froststrap.RobloxInterfaces;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class ServerBrowserViewModel : NotifyPropertyChangedViewModel
    {
        private const string RobloxWebDeeplinkBase = "https://www.roblox.com/games/start";

        private readonly ActivityWatcher? _activityWatcher;

        public ObservableCollection<GameServer> Servers { get; } = [];

        public IEnumerable<Order> Orders { get; } = Enum.GetValues<Order>();

        public ObservableCollection<ServerRegion> Regions { get; } = [];

        private ServerRegion? _selectedRegion;
        public ServerRegion? SelectedRegion
        {
            get => _selectedRegion;
            set
            {
                if (_selectedRegion == value) return;
                _selectedRegion = value;
                OnPropertyChanged(nameof(SelectedRegion));
                _ = LoadAsync();
            }
        }

        private Order _selectedOrder = Order.Descending;
        public Order SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                if (_selectedOrder == value) return;
                _selectedOrder = value;
                OnPropertyChanged(nameof(SelectedOrder));
                _ = LoadAsync();
            }
        }

        private bool _excludeFull;
        public bool ExcludeFull
        {
            get => _excludeFull;
            set
            {
                if (_excludeFull == value) return;
                _excludeFull = value;
                OnPropertyChanged(nameof(ExcludeFull));
                _ = LoadAsync();
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(CanRefresh));
                OnPropertyChanged(nameof(CanHop));
                OnPropertyChanged(nameof(CanJoinClosest));
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(EmptyText));
            }
        }

        public bool CanRefresh => !IsBusy;
        public bool ShowEmptyState => !IsBusy && Servers.Count == 0;

        public string EmptyText
        {
            get
            {
                if (!InGame) return Strings.Menu_Overlay_Servers_NotInGame;
                if (SelectedRegion is not null && !SelectedRegion.IsAll)
                    return String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Servers_EmptyRegion, SelectedRegion.Name);
                return Strings.Menu_Overlay_Servers_Empty;
            }
        }

        private string? _status;
        private DispatcherTimer? _statusTimer;

        public string SummaryText
        {
            get
            {
                if (_status is not null) return _status;
                return Servers.Count > 0
                    ? String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Servers_Summary, Servers.Count)
                    : String.Empty;
            }
        }

        private bool InGame => _activityWatcher?.InGame == true && _activityWatcher.Data.PlaceId != 0;

        public ICommand RefreshCommand { get; }
        public ICommand JoinCommand { get; }
        public ICommand CopyLinkCommand { get; }

        #region This server

        private readonly DispatcherTimer? _currentServerTimer;
        private readonly DispatcherTimer _uptimeTimer = new() { Interval = TimeSpan.FromSeconds(1) };

        private string? _currentLocation;

        public bool HasCurrentServer => InGame;
        public string CurrentServerType => _activityWatcher?.Data.ServerType.ToTranslatedString() ?? String.Empty;
        public string CurrentJobId => _activityWatcher?.Data.JobId ?? String.Empty;

        public string CurrentUptime => _activityWatcher?.Data.StartTime is DateTime started
            ? Time.FormatTimeSpan(DateTime.UtcNow - started)
            : Strings.Common_Loading;

        public string CurrentLocation => _currentLocation ?? Strings.Common_Loading;

        [SuppressMessage("Performance", "CA1822:Mark members as static",
            Justification = "Bound in XAML; must remain an instance member.")]
        public bool ShowCurrentLocation => App.Settings.Prop.ShowServerDetails;

        public ICommand CopyInstanceIdCommand { get; }

        private bool _isHopping;
        public bool CanHop => InGame && !_isHopping && !IsBusy;

        public ICommand HopCommand { get; }

        private bool _findingClosest;
        public bool CanJoinClosest => InGame && !_findingClosest && !IsBusy;

        public ICommand ClosestCommand { get; }

        private async void RefreshCurrentServer()
        {
            foreach (string name in new[]
            {
                nameof(HasCurrentServer),
                nameof(CurrentServerType),
                nameof(CurrentJobId),
                nameof(CurrentUptime),
                nameof(CanHop),
                nameof(CanJoinClosest)
            })
            {
                OnPropertyChanged(name);
            }

            if (!InGame || !ShowCurrentLocation || _currentLocation is not null)
                return;

            string? location = await _activityWatcher!.Data.QueryServerLocation();
            if (String.IsNullOrEmpty(location)) return;

            _currentLocation = location;
            OnPropertyChanged(nameof(CurrentLocation));
        }

        private async Task CopyInstanceIdAsync(Visual? visual)
        {
            try
            {
                var clipboard = TopLevel.GetTopLevel(visual)?.Clipboard;
                if (clipboard is null) return;

                await clipboard.SetTextAsync(CurrentJobId);
                Flash(Strings.Menu_Overlay_Servers_IdCopied);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to copy the instance id");
                App.Logger.Error(ex);
                Flash(Strings.Menu_Overlay_Servers_CopyFailed);
            }
        }

        private async Task HopAsync()
        {
            if (!CanHop) return;

            _isHopping = true;
            OnPropertyChanged(nameof(CanHop));

            try
            {
                if (Servers.Count == 0)
                    await LoadAsync();

                GameServer? target = GameServers.PickHopTarget(Servers);

                if (target is null)
                {
                    Flash(Strings.Menu_Overlay_Servers_NoHopTarget);
                    return;
                }

                App.Logger.Info($"Hopping to {target.JobId}");
                GameServers.Join(_activityWatcher!.Data.PlaceId, target.JobId);
                Flash(Strings.Menu_Overlay_Servers_Hopping);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to hop servers");
                App.Logger.Error(ex);
            }
            finally
            {
                _isHopping = false;
                OnPropertyChanged(nameof(CanHop));
            }
        }

        private async Task JoinClosestAsync()
        {
            const string LOG_IDENT = "ServerBrowserViewModel::JoinClosestAsync";

            if (!CanJoinClosest) return;

            if (!App.Settings.Prop.AllowCookieAccess)
            {
                Flash(Strings.Menu_Overlay_Servers_ClosestNeedsCookies);
                return;
            }

            _findingClosest = true;
            OnPropertyChanged(nameof(CanJoinClosest));

            try
            {
                if (!App.Cookies.Loaded)
                    await Task.Run(App.Cookies.LoadCookies);

                if (!App.Cookies.Loaded)
                {
                    Flash(Strings.Menu_Overlay_Servers_ClosestNeedsCookies);
                    return;
                }

                long placeId = _activityWatcher!.Data.PlaceId;
                var (server, alreadyClosest) = await GameServers.FindClosestAsync(placeId, _activityWatcher.Data.JobId);

                if (alreadyClosest) { Flash(Strings.Menu_Overlay_Servers_ClosestAlready); return; }
                if (server is null) { Flash(Strings.Menu_Overlay_Servers_ClosestNone); return; }

                App.Logger.Info($"{LOG_IDENT}: joining {server.JobId}");
                GameServers.Join(placeId, server.JobId);
                Flash(Strings.Menu_Overlay_Servers_ClosestJoining);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to find the closest server");
                App.Logger.Error(ex);
                Flash(Strings.Menu_Overlay_Servers_ClosestFailed);
            }
            finally
            {
                _findingClosest = false;
                OnPropertyChanged(nameof(CanJoinClosest));
            }
        }

        #endregion

        public ServerBrowserViewModel(ActivityWatcher? activityWatcher)
        {
            _activityWatcher = activityWatcher;

            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            JoinCommand = new RelayCommand<GameServer>(Join);
            CopyLinkCommand = new AsyncRelayCommand<Visual>(CopyLinkAsync);
            CopyInstanceIdCommand = new AsyncRelayCommand<Visual>(CopyInstanceIdAsync);
            HopCommand = new AsyncRelayCommand(HopAsync);
            ClosestCommand = new AsyncRelayCommand(JoinClosestAsync);

            _uptimeTimer.Tick += (_, _) =>
            {
                foreach (GameServer server in Servers)
                    server.Tick();
            };

            if (_activityWatcher is null)
                return;

            _currentServerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _currentServerTimer.Tick += (_, _) => RefreshCurrentServer();
            _currentServerTimer.Start();

            _activityWatcher.OnGameJoin += async (_, _) =>
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    _currentLocation = null;
                    await LoadAsync();
                });

            _activityWatcher.OnGameLeave += (_, _) =>
                Dispatcher.UIThread.Post(Clear);
        }

        public void SetVisible(bool visible)
        {
            if (visible) _uptimeTimer.Start();
            else _uptimeTimer.Stop();
        }

        public async Task InitialiseAsync()
        {
            if (Regions.Count == 0)
            {
                foreach (ServerRegion region in await GameServers.FetchRegionsAsync())
                    Regions.Add(region);

                _selectedRegion = Regions.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedRegion));
            }

            await LoadAsync();
        }

        public async Task LoadAsync()
        {
            if (IsBusy) return;

            if (!InGame)
            {
                Clear();
                return;
            }

            IsBusy = true;

            try
            {
                var servers = await GameServers.FetchAsync(
                    _activityWatcher!.Data.PlaceId,
                    _activityWatcher.Data.JobId,
                    SelectedOrder,
                    ExcludeFull,
                    SelectedRegion);

                if (servers.FirstOrDefault(x => x.IsCurrent) is GameServer current
                    && _activityWatcher.Data.StartTime is DateTime started)
                {
                    current.StartedAt = started;
                    current.UptimeIsEstimate = false;
                }

                await Task.WhenAll(
                    GameServers.PopulateIconsAsync(servers),
                    GameServers.PopulateDetailsAsync(_activityWatcher.Data.PlaceId, servers));

                Servers.Clear();
                foreach (GameServer server in servers)
                    Servers.Add(server);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load servers");
                App.Logger.Error(ex);
            }
            finally
            {
                IsBusy = false;
                Refreshed();
                RefreshCurrentServer();
            }
        }

        private void Join(GameServer? server)
        {
            if (server is null || server.IsCurrent || _activityWatcher is null) return;

            try
            {
                GameServers.Join(_activityWatcher.Data.PlaceId, server.JobId);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to join {server.JobId}");
                App.Logger.Error(ex);
            }
        }

        private async Task CopyLinkAsync(Visual? visual)
        {
            if (visual?.DataContext is not GameServer server || _activityWatcher is null) return;

            try
            {
                string link = $"{RobloxWebDeeplinkBase}?placeId={_activityWatcher.Data.PlaceId}&gameInstanceId={server.JobId}";

                var clipboard = TopLevel.GetTopLevel(visual)?.Clipboard;
                if (clipboard is null) return;

                await clipboard.SetTextAsync(link);
                Flash(Strings.Menu_Overlay_Servers_LinkCopied);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to copy a link to {server.JobId}");
                App.Logger.Error(ex);
                Flash(Strings.Menu_Overlay_Servers_CopyFailed);
            }
        }

        private void Flash(string message)
        {
            _status = message;
            OnPropertyChanged(nameof(SummaryText));

            _statusTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _statusTimer.Tick -= ClearStatus;
            _statusTimer.Tick += ClearStatus;

            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private void ClearStatus(object? sender, EventArgs e)
        {
            _statusTimer?.Stop();
            _status = null;
            OnPropertyChanged(nameof(SummaryText));
        }

        private void Clear()
        {
            Servers.Clear();
            Refreshed();
            RefreshCurrentServer();
        }

        private void Refreshed()
        {
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(EmptyText));
            OnPropertyChanged(nameof(SummaryText));
        }
    }
}