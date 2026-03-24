using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Froststrap.Integrations;

namespace Froststrap.UI.Elements.ContextMenu
{
    public partial class MenuContainer : Base.AvaloniaWindow
    {
        private readonly Watcher? _watcher;
        private ActivityWatcher? _activityWatcher => _watcher?.ActivityWatcher;

        private ServerInformation? _serverInformationWindow;
        private ServerHistory? _gameHistoryWindow;

        private Stopwatch _totalPlaytimeStopwatch = new Stopwatch();
        private TimeSpan _accumulatedTotalPlaytime = TimeSpan.Zero;

        private DispatcherTimer? _playtimeTimer;
        private DateTime? _studioPlaceJoinTime = null;

        private NativeMenuItem? VersionMenuItem;
        private NativeMenuItem? PlaytimeMenuItem;
        private NativeMenuItem? RichPresenceMenuItem;
        private NativeMenuItem? InviteDeeplinkMenuItem;
        private NativeMenuItem? AutoJoinRegionMenuItem;
        private NativeMenuItem? ServerDetailsMenuItem;
        private NativeMenuItem? GameHistoryMenuItem;

        public MenuContainer()
        {
            InitializeComponent();
            MapNativeMenuItems();

            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            this.Closing += (s, e) =>
            {
                e.Cancel = true;
                this.Hide();
            };
        }

        private void MapNativeMenuItems()
        {
            var menu = NativeMenu.GetMenu(this);
            if (menu == null) return;
            var items = menu.Items.OfType<NativeMenuItem>().ToList();

            VersionMenuItem = items.ElementAtOrDefault(0);
            PlaytimeMenuItem = items.ElementAtOrDefault(1);
            RichPresenceMenuItem = items.ElementAtOrDefault(3);
            InviteDeeplinkMenuItem = items.ElementAtOrDefault(4);
            AutoJoinRegionMenuItem = items.ElementAtOrDefault(5);
            ServerDetailsMenuItem = items.ElementAtOrDefault(6);
            GameHistoryMenuItem = items.ElementAtOrDefault(7);
        }

        public MenuContainer(Watcher watcher) : this()
        {
            _watcher = watcher;

            if (_activityWatcher is not null)
            {
                _activityWatcher.OnGameJoin += ActivityWatcher_OnGameJoin;
                _activityWatcher.OnGameLeave += ActivityWatcher_OnGameLeave;
                _activityWatcher.OnStudioPlaceOpened += ActivityWatcher_OnStudioPlaceOpened;
                _activityWatcher.OnStudioPlaceClosed += ActivityWatcher_OnStudioPlaceClosed;

                Dispatcher.UIThread.Post(() => {
                    if (_activityWatcher.InRobloxStudio)
                    {
                        if (InviteDeeplinkMenuItem != null) InviteDeeplinkMenuItem.IsVisible = false;
                        if (ServerDetailsMenuItem != null) ServerDetailsMenuItem.IsVisible = false;
                        if (GameHistoryMenuItem != null) GameHistoryMenuItem.IsVisible = false;
                        if (AutoJoinRegionMenuItem != null) AutoJoinRegionMenuItem.IsVisible = false;

                        if (App.Settings.Prop.PlaytimeCounter)
                        {
                            StartTotalPlaytimeTimer();
                            if (PlaytimeMenuItem != null) PlaytimeMenuItem.IsVisible = true;
                            if (_activityWatcher.InStudioPlace) _studioPlaceJoinTime = DateTime.Now;
                        }
                    }
                    else
                    {
                        if (App.Settings.Prop.PlaytimeCounter) StartTotalPlaytimeTimer();

                        UpdateRegionJoinUI();

                        if (GameHistoryMenuItem != null)
                            GameHistoryMenuItem.IsVisible = App.Settings.Prop.ShowGameHistoryMenu;
                    }

                    if (RichPresenceMenuItem != null)
                    {
                        RichPresenceMenuItem.IsVisible = (_watcher?.PlayerRichPresence is not null || _watcher?.StudioRichPresence is not null);

                        _watcher?.PlayerRichPresence?.SetVisibility(RichPresenceMenuItem.IsChecked);
                        _watcher?.StudioRichPresence?.SetVisibility(RichPresenceMenuItem.IsChecked);
                    }

                    if (VersionMenuItem != null)
                        VersionMenuItem.Header = $"{App.ProjectName} v{App.Version}";
                });
            }
        }

        private void StartTotalPlaytimeTimer()
        {
            _totalPlaytimeStopwatch.Start();
            _playtimeTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, PlaytimeTimer_Tick);
            _playtimeTimer.Start();
        }

        private void PlaytimeTimer_Tick(object? sender, EventArgs e)
        {
            TimeSpan total = _accumulatedTotalPlaytime + _totalPlaytimeStopwatch.Elapsed;
            if (_activityWatcher == null || PlaytimeMenuItem == null) return;

            string statusText;
            if (_activityWatcher.InStudioPlace && _studioPlaceJoinTime.HasValue)
                statusText = $"Total: {FormatTimeSpan(total)} | Studio: {FormatTimeSpan(DateTime.Now - _studioPlaceJoinTime.Value)}";
            else if (_activityWatcher.InGame)
                statusText = $"Total: {FormatTimeSpan(total)} | Game: {FormatTimeSpan(DateTime.Now - _activityWatcher.Data.TimeJoined)}";
            else
                statusText = $"Total: {FormatTimeSpan(total)}";

            PlaytimeMenuItem.Header = statusText;
        }

        private static string FormatTimeSpan(TimeSpan ts) =>
            ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}" : $"{ts.Minutes}:{ts.Seconds:D2}";

        public async void ShowServerInformationWindow()
        {
            if (_serverInformationWindow is null)
            {
                _serverInformationWindow = new(_watcher!);
                _serverInformationWindow.Closed += (_, _) => _serverInformationWindow = null;
            }

            if (!_serverInformationWindow.IsVisible) _serverInformationWindow.Show();
            else _serverInformationWindow.Activate();
        }

        private void ActivityWatcher_OnGameJoin(object? sender, EventArgs e) =>
            Dispatcher.UIThread.Invoke(() => {
                if (_activityWatcher?.Data.ServerType == ServerType.Public && InviteDeeplinkMenuItem != null)
                    InviteDeeplinkMenuItem.IsVisible = true;
                if (ServerDetailsMenuItem != null) ServerDetailsMenuItem.IsVisible = true;
                UpdateRegionJoinUI();
            });

        private void ActivityWatcher_OnGameLeave(object? sender, EventArgs e) =>
            Dispatcher.UIThread.Invoke(() => {
                if (InviteDeeplinkMenuItem != null) InviteDeeplinkMenuItem.IsVisible = false;
                if (ServerDetailsMenuItem != null) ServerDetailsMenuItem.IsVisible = false;
                UpdateRegionJoinUI();
                _serverInformationWindow?.Close();
            });

        private void ActivityWatcher_OnStudioPlaceOpened(object? sender, EventArgs e) => _studioPlaceJoinTime = DateTime.Now;
        private void ActivityWatcher_OnStudioPlaceClosed(object? sender, EventArgs e) => _studioPlaceJoinTime = null;

        private void CloseWatcheMenuItem_Click(object? sender, EventArgs e) => _watcher?.Dispose();

        private void RichPresenceMenuItem_Click(object? sender, EventArgs e)
        {
            if (sender is NativeMenuItem item)
            {
                bool isChecked = item.IsChecked;

                _watcher?.PlayerRichPresence?.SetVisibility(isChecked);
                _watcher?.StudioRichPresence?.SetVisibility(isChecked);
            }
        }

        private void InviteDeeplinkMenuItem_Click(object? sender, EventArgs e)
        {
            string deeplink = _activityWatcher?.Data?.GetInviteDeeplink() ?? "No activity data available";
            TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(deeplink);
        }

        private void ServerDetailsMenuItem_Click(object? sender, EventArgs e) => ShowServerInformationWindow();
        private void CloseRobloxMenuItem_Click(object? sender, EventArgs e) => _watcher?.KillRobloxProcess();

        private void JoinLastServerMenuItem_Click(object? sender, EventArgs e)
        {
            if (_activityWatcher is null) return;
            if (_gameHistoryWindow is null)
            {
                _gameHistoryWindow = new(_activityWatcher);
                _gameHistoryWindow.Closed += (_, _) => _gameHistoryWindow = null;
            }
            if (!_gameHistoryWindow.IsVisible) _gameHistoryWindow.Show();
            else _gameHistoryWindow.Activate();
        }

        private void UpdateRegionJoinUI()
        {
            if (AutoJoinRegionMenuItem == null) return;

            string region = App.Settings.Prop.SelectedRegion;
            bool hasRegion = !string.IsNullOrEmpty(region);

            bool inGame = _activityWatcher?.InGame ?? false;
            AutoJoinRegionMenuItem.IsVisible = hasRegion && inGame;

            AutoJoinRegionMenuItem.Header = hasRegion ? $"Join {region}" : "No Region Selected";
            AutoJoinRegionMenuItem.IsEnabled = hasRegion;
        }

        private async void AutoJoinRegionMenuItem_Click(object? sender, EventArgs e)
        {
            if (_activityWatcher?.InGame != true || _activityWatcher?.Data == null)
            {
                _ = Frontend.ShowMessageBox("You need to be in a game to use this feature.", MessageBoxImage.Warning);
                return;
            }
            string selectedRegion = App.Settings.Prop.SelectedRegion;
            if (string.IsNullOrEmpty(selectedRegion)) return;
            await FindAndJoinServerInRegion(_activityWatcher.Data.PlaceId, selectedRegion);
        }

        private async Task FindAndJoinServerInRegion(long placeId, string selectedRegion)
        {
            var fetcher = new RobloxServerFetcher();
            await App.RemoteData.WaitUntilDataFetched();
            string cookie = App.RemoteData.Prop.Dummy;

            if (!await fetcher.ValidateCookieAsync(cookie))
            {
                _ = Frontend.ShowMessageBox("Authentication failed. Please check your connection.", MessageBoxImage.Error);
                return;
            }

            string? nextCursor = "";
            for (int i = 0; i < 20; i++)
            {
                var result = await fetcher.FetchServerInstancesAsync(placeId, cookie, nextCursor);
                var match = result.Servers.FirstOrDefault(s => s.Region == selectedRegion && s.Playing < s.MaxPlayers);

                if (match != null)
                {
                    MessageBoxResult confirmResult = await Frontend.ShowMessageBox(
                            $"Found server in {selectedRegion} with {match.Playing}/{match.MaxPlayers} players.\nDo you want to join?",
                            MessageBoxImage.Question,
                            MessageBoxButton.YesNo
                        );

                    if (confirmResult == MessageBoxResult.Yes)
                    {
                        string robloxUri = $"roblox://experiences/start?placeId={placeId}&gameInstanceId={match.Id}";
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = robloxUri,
                            UseShellExecute = true
                        });

                        _watcher?.KillRobloxProcess();
                        return;
                    }
                    else return;
                }

                if (string.IsNullOrEmpty(result.NextCursor)) break;
                nextCursor = result.NextCursor;
                await Task.Delay(200);
            }

            _ = Frontend.ShowMessageBox($"No available {selectedRegion} servers found.", MessageBoxImage.Information);
        }
    }
}