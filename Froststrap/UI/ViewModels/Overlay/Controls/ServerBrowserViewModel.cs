﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Enums.Overlay;
using Froststrap.Integrations;
using Froststrap.RobloxInterfaces;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class ServerBrowserViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private const string RobloxWebDeeplinkBase = "https://www.roblox.com/games/start";
        private const int PageSize = 60;

        private static readonly string[] CurrentServerProperties =
        [
            nameof(HasCurrentServer),
            nameof(CurrentServerType),
            nameof(CurrentJobId),
            nameof(CurrentUptime),
            nameof(CanHop),
            nameof(CanJoinClosest)
        ];

        private readonly ActivityWatcher? _activityWatcher;
        private readonly FlashMessage _flash;

        private List<GameServer> _allServers = [];
        private int _shown;
        private int _generation;
        private bool _reloadPending;

        private bool _visible;
        private bool _initialised;
        private bool _stale;
        private bool _loadingMore;
        private bool _disposed;

        public BulkObservableCollection<GameServer> Servers { get; } = new();

        public IEnumerable<Order> Orders { get; } = Enum.GetValues<Order>();

        public ObservableCollection<ServerRegion> Regions { get; } = [];

        public bool HasMore => _shown < _allServers.Count;

        private ServerRegion? _selectedRegion;
        public ServerRegion? SelectedRegion
        {
            get => _selectedRegion;
            set
            {
                if (_selectedRegion == value) return;
                _selectedRegion = value;
                OnPropertyChanged(nameof(SelectedRegion));
                RequestReload();
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
                RequestReload();
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
                RequestReload();
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

        public string SummaryText
        {
            get
            {
                if (_flash.Text is not null) return _flash.Text;
                return Servers.Count > 0
                    ? String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Servers_Summary, Servers.Count)
                    : String.Empty;
            }
        }

        private bool InGame => _activityWatcher?.InGame == true && _activityWatcher.Data.PlaceId != 0;

        public ICommand RefreshCommand { get; }
        public ICommand LoadMoreCommand { get; }
        public ICommand JoinCommand { get; }
        public ICommand CopyLinkCommand { get; }

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

        private async Task RefreshCurrentServerAsync()
        {
            foreach (string name in CurrentServerProperties)
                OnPropertyChanged(name);

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

            _findingClosest = true;
            OnPropertyChanged(nameof(CanJoinClosest));

            try
            {
                if (!await App.Cookies.EnsureLoadedAsync())
                {
                    Flash(Strings.Menu_Overlay_Servers_ClosestNeedsCookies);
                    return;
                }

                long placeId = _activityWatcher!.Data.PlaceId;
                var (jobId, alreadyClosest) = await GameServers.FindClosestAsync(placeId, _activityWatcher.Data.JobId);

                if (alreadyClosest) { Flash(Strings.Menu_Overlay_Servers_ClosestAlready); return; }
                if (jobId is null) { Flash(Strings.Menu_Overlay_Servers_ClosestNone); return; }

                App.Logger.Info($"{LOG_IDENT}: joining {jobId}");
                GameServers.Join(placeId, jobId);
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

        public ServerBrowserViewModel(ActivityWatcher? activityWatcher)
        {
            _activityWatcher = activityWatcher;

            _flash = new FlashMessage(TimeSpan.FromSeconds(2), () => OnPropertyChanged(nameof(SummaryText)));

            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync);
            JoinCommand = new RelayCommand<GameServer>(Join);
            CopyLinkCommand = new AsyncRelayCommand<Visual>(CopyLinkAsync);
            CopyInstanceIdCommand = new AsyncRelayCommand<Visual>(CopyInstanceIdAsync);
            HopCommand = new AsyncRelayCommand(HopAsync);
            ClosestCommand = new AsyncRelayCommand(JoinClosestAsync);

            _uptimeTimer.Tick += OnUptimeTick;

            if (_activityWatcher is null)
                return;

            _currentServerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _currentServerTimer.Tick += OnCurrentServerTick;

            _activityWatcher.OnGameJoin += OnActivityGameJoin;
            _activityWatcher.OnGameLeave += OnActivityGameLeave;
        }

        private void RequestReload()
        {
            if (IsBusy)
            {
                _reloadPending = true;
                return;
            }

            _ = LoadAsync();
        }

        private void OnUptimeTick(object? sender, EventArgs e)
        {
            foreach (GameServer server in Servers)
                server.Tick();
        }

        private void OnCurrentServerTick(object? sender, EventArgs e) => _ = RefreshCurrentServerAsync();

        private void OnActivityGameJoin(object? sender, EventArgs e) => Dispatcher.UIThread.Post(OnGameJoin);

        private void OnActivityGameLeave(object? sender, EventArgs e) => Dispatcher.UIThread.Post(OnGameLeave);

        public void SetVisible(bool visible)
        {
            _visible = visible;

            if (!visible)
            {
                _uptimeTimer.Stop();
                _currentServerTimer?.Stop();
                return;
            }

            _uptimeTimer.Start();

            if (InGame)
                _currentServerTimer?.Start();

            if (!_initialised)
            {
                _initialised = true;
                _ = InitialiseAsync();
            }
            else if (_stale)
            {
                _ = LoadAsync();
            }
            else
            {
                _ = RefreshCurrentServerAsync();
            }
        }

        private void OnGameJoin()
        {
            _currentLocation = null;
            _stale = true;

            if (_visible && _initialised)
                _ = LoadAsync();
        }

        private void OnGameLeave()
        {
            _generation++;
            _stale = false;

            _currentLocation = null;
            Clear();
        }

        private async Task InitialiseAsync()
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

            _stale = false;
            int generation = ++_generation;
            IsBusy = true;

            try
            {
                long placeId = _activityWatcher!.Data.PlaceId;

                var servers = await GameServers.FetchAsync(
                    placeId,
                    _activityWatcher.Data.JobId,
                    SelectedOrder,
                    ExcludeFull,
                    SelectedRegion);

                if (generation != _generation)
                    return;

                if (servers.FirstOrDefault(x => x.IsCurrent) is GameServer current
                    && _activityWatcher.Data.StartTime is DateTime started)
                {
                    current.StartedAt = started;
                    current.UptimeIsEstimate = false;
                }

                _allServers = servers;
                _shown = Math.Min(PageSize, servers.Count);

                List<GameServer> visible = [.. servers.Take(_shown)];

                await EnrichAsync(placeId, visible);

                if (generation != _generation)
                    return;

                Servers.ReplaceAll(visible);

                OnPropertyChanged(nameof(HasMore));
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load servers");
                App.Logger.Error(ex);
            }
            finally
            {
                if (generation == _generation)
                {
                    IsBusy = false;
                    Refreshed();
                    _ = RefreshCurrentServerAsync();

                    if (_reloadPending)
                    {
                        _reloadPending = false;
                        _ = LoadAsync();
                    }
                }
            }
        }

        private async Task LoadMoreAsync()
        {
            if (_loadingMore || !HasMore) return;

            _loadingMore = true;

            try
            {
                int next = Math.Min(_shown + PageSize, _allServers.Count);

                List<GameServer> more = [.. _allServers.Skip(_shown).Take(next - _shown)];

                _shown = next;

                Servers.AddRange(more);

                OnPropertyChanged(nameof(HasMore));
                OnPropertyChanged(nameof(SummaryText));

                await EnrichAsync(_activityWatcher!.Data.PlaceId, more);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load more servers");
                App.Logger.Error(ex);
            }
            finally
            {
                _loadingMore = false;
            }
        }

        private static async Task EnrichAsync(long placeId, List<GameServer> servers)
        {
            if (servers.Count == 0)
                return;

            await Task.WhenAll(
                GameServers.PopulateIconsAsync(servers),
                GameServers.PopulateDetailsAsync(placeId, servers));
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

        private void Flash(string message) => _flash.Show(message);

        private void Clear()
        {
            _allServers = [];
            _shown = 0;

            Servers.ReplaceAll([]);

            OnPropertyChanged(nameof(HasMore));
            Refreshed();
            _ = RefreshCurrentServerAsync();
        }

        private void Refreshed()
        {
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(EmptyText));
            OnPropertyChanged(nameof(SummaryText));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _uptimeTimer.Stop();
            _uptimeTimer.Tick -= OnUptimeTick;

            if (_currentServerTimer is not null)
            {
                _currentServerTimer.Stop();
                _currentServerTimer.Tick -= OnCurrentServerTick;
            }

            if (_activityWatcher is not null)
            {
                _activityWatcher.OnGameJoin -= OnActivityGameJoin;
                _activityWatcher.OnGameLeave -= OnActivityGameLeave;
            }

            GC.SuppressFinalize(this);
        }
    }
}