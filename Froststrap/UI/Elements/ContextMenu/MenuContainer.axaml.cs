using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.Models.APIs;
using Froststrap.UI.Elements.Dialogs;

namespace Froststrap.UI.Elements.ContextMenu
{
    public partial class MenuContainer : Base.AvaloniaWindow
    {
        private readonly Watcher? _watcher;
        private ActivityWatcher? ActivityWatcher => _watcher?.ActivityWatcher;

        private ServerInformation? _serverInformationWindow;
        private ServerHistory? _gameHistoryWindow;

        private readonly Stopwatch _totalPlaytimeStopwatch = new();
        private readonly TimeSpan _accumulatedTotalPlaytime = TimeSpan.Zero;

        private DispatcherTimer? _playtimeTimer;
        private DateTime? _studioPlaceJoinTime = null;

        private WindowControlPermission? _windowPermissionWindow;

        private NativeMenuItem? VersionMenuItem;
        private NativeMenuItem? PlaytimeMenuItem;
        private NativeMenuItem? RichPresenceMenuItem;
        private NativeMenuItem? InviteDeeplinkMenuItem;
        private NativeMenuItem? AutoJoinRegionMenuItem;
        private NativeMenuItem? ServerDetailsMenuItem;
        private NativeMenuItem? GameHistoryMenuItem;
        private NativeMenuItem? CloseRobloxMenuItem;

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
            CloseRobloxMenuItem = items.ElementAtOrDefault(9);
        }

        public MenuContainer(Watcher watcher) : this()
        {
            _watcher = watcher;

            if (ActivityWatcher is not null)
            {
                ActivityWatcher.OnGameJoin += ActivityWatcher_OnGameJoin;
                ActivityWatcher.OnGameLeave += ActivityWatcher_OnGameLeave;
                ActivityWatcher.OnStudioPlaceOpened += ActivityWatcher_OnStudioPlaceOpened;
                ActivityWatcher.OnStudioPlaceClosed += ActivityWatcher_OnStudioPlaceClosed;

                Dispatcher.UIThread.Post(() => {
                    if (ActivityWatcher.InRobloxStudio)
                    {
                        InviteDeeplinkMenuItem?.SetValue(MenuItem.IsVisibleProperty, false);
                        ServerDetailsMenuItem?.SetValue(MenuItem.IsVisibleProperty, false);
                        GameHistoryMenuItem?.SetValue(MenuItem.IsVisibleProperty, false);
                        AutoJoinRegionMenuItem?.SetValue(MenuItem.IsVisibleProperty, false);
                        CloseRobloxMenuItem?.SetValue(MenuItem.HeaderProperty, "Close Studio");

                        if (App.Settings.Prop.PlaytimeCounter)
                        {
                            StartTotalPlaytimeTimer();
                            PlaytimeMenuItem?.SetValue(MenuItem.IsVisibleProperty, true);
                            if (ActivityWatcher.InStudioPlace) _studioPlaceJoinTime = DateTime.Now;
                        }
                    }
                    else
                    {
                        if (App.Settings.Prop.PlaytimeCounter) StartTotalPlaytimeTimer();

                        GameHistoryMenuItem?.SetValue(MenuItem.IsVisibleProperty, App.Settings.Prop.ShowGameHistoryMenu);
                    }

                    if (RichPresenceMenuItem != null)
                    {
                        RichPresenceMenuItem.IsVisible = (_watcher?.PlayerRichPresence is not null || _watcher?.StudioRichPresence is not null);

                        _watcher?.PlayerRichPresence?.SetVisibility(RichPresenceMenuItem.IsChecked);
                        _watcher?.StudioRichPresence?.SetVisibility(RichPresenceMenuItem.IsChecked);
                    }

                    VersionMenuItem?.SetValue(NativeMenuItem.HeaderProperty, $"{App.ProjectName} v{App.Version}");
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
            if (ActivityWatcher == null || PlaytimeMenuItem == null) return;

            string statusText;
            if (ActivityWatcher.InStudioPlace && _studioPlaceJoinTime.HasValue)
                statusText = $"Total: {FormatTimeSpan(total)} | Studio: {FormatTimeSpan(DateTime.Now - _studioPlaceJoinTime.Value)}";
            else if (ActivityWatcher.InGame)
                statusText = $"Total: {FormatTimeSpan(total)} | Game: {FormatTimeSpan(DateTime.Now - ActivityWatcher.Data.TimeJoined)}";
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

        public void ShowWindowPermissionWindow()
        {
            if (_windowPermissionWindow is null)
            {
                _windowPermissionWindow = new(_watcher?.ActivityWatcher!);
                _windowPermissionWindow.Closed += (_, _) => _windowPermissionWindow = null;
            }

            if (!_windowPermissionWindow.IsVisible)
            {
                _windowPermissionWindow.Show();
                _windowPermissionWindow.Topmost = true;
                _windowPermissionWindow.Activate();
                _windowPermissionWindow.Topmost = false;
                _windowPermissionWindow.Focus();
            }
            else
                _windowPermissionWindow.Activate();
        }

        private void ActivityWatcher_OnGameJoin(object? sender, EventArgs e) =>
            Dispatcher.UIThread.Invoke(() => {
                if (ActivityWatcher?.Data.ServerType == ServerType.Public && InviteDeeplinkMenuItem != null)
                    InviteDeeplinkMenuItem.IsVisible = true;
                ServerDetailsMenuItem?.SetValue(MenuItem.IsVisibleProperty, true);
                AutoJoinRegionMenuItem?.SetValue(MenuItem.IsVisibleProperty, true);
            });

        private void ActivityWatcher_OnGameLeave(object? sender, EventArgs e) =>
            Dispatcher.UIThread.Invoke(() => {
                InviteDeeplinkMenuItem?.SetValue(MenuItem.IsVisibleProperty, false);
                ServerDetailsMenuItem?.SetValue(MenuItem.IsVisibleProperty, false);
                AutoJoinRegionMenuItem?.SetValue(MenuItem.IsVisibleProperty, false);
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
            string deeplink = ActivityWatcher?.Data?.GetInviteDeeplink(true, DeeplinkType.RobloxWeb) ?? "No activity data available";
            TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(deeplink);
        }

        private void ServerDetailsMenuItem_Click(object? sender, EventArgs e) => ShowServerInformationWindow();
        private void CloseRobloxMenuItem_Click(object? sender, EventArgs e) => _watcher?.KillRobloxProcess();

        private void JoinLastServerMenuItem_Click(object? sender, EventArgs e)
        {
            if (ActivityWatcher is null) return;
            if (_gameHistoryWindow is null)
            {
                _gameHistoryWindow = new(ActivityWatcher);
                _gameHistoryWindow.Closed += (_, _) => _gameHistoryWindow = null;
            }
            if (!_gameHistoryWindow.IsVisible) _gameHistoryWindow.Show();
            else _gameHistoryWindow.Activate();
        }

        private async void AutoJoinRegionMenuItem_Click(object? sender, EventArgs e)
        {
            if (ActivityWatcher is null) return;

            await FindAndJoinServerInRegion(ActivityWatcher.Data.PlaceId);
        }

        private async Task FindAndJoinServerInRegion(long placeId)
        {
            bool joinSmallerServer = App.Settings.Prop.JoinSmallerServer;
            int bestRegionAmounts = App.Settings.Prop.BestRegionAmounts;
            int maxServerCheck = App.Settings.Prop.MaxServerCheck;

            var fetcher = new RobloxServerFetcher();

            string? resolvedCookie = await fetcher.ResolveCookieAsync();
            if (string.IsNullOrWhiteSpace(resolvedCookie))
            {
                _ = Frontend.ShowMessageBox("No valid cookie found. Log in using account manager or turn on 'Froststrap Account Permission' to use this feature.", MessageBoxImage.Error);
                return;
            }

            var datacentersResult = await fetcher.GetDatacentersAsync();
            if (datacentersResult == null) return;
            var (_, dcMap) = datacentersResult.Value;

            List<string> topRegions = await GetClosestRegionsForAutoModeAsync(bestRegionAmounts);
            if (topRegions.Count == 0)
            {
                _ = Frontend.ShowMessageBox("Could not determine your location for Auto mode. Please try again later.", MessageBoxImage.Warning);
                return;
            }

            var regionRank = new Dictionary<string, int>();
            for (int i = 0; i < topRegions.Count; i++)
                regionRank[topRegions[i]] = i + 1;

            string? nextCursor = "";
            int serversChecked = 0;
            const int maxAttempts = 50;

            string? bestServerId = null;
            string? bestServerRegion = null;
            int bestRank = int.MaxValue;
            int bestPlayers = int.MaxValue;
            int bestMaxPlayers = 0;

            while (serversChecked < maxServerCheck && serversChecked < maxAttempts)
            {
                int sortOrder = joinSmallerServer ? 1 : 2;
                var result = await fetcher.FetchServerInstancesAsync(placeId, nextCursor, sortOrder, resolvedCookie);

                if (result?.Servers == null || result.Servers.Count == 0)
                {
                    if (string.IsNullOrEmpty(nextCursor)) break;
                    await Task.Delay(500);
                    continue;
                }

                foreach (var server in result.Servers)
                {
                    if (serversChecked >= maxServerCheck) break;

                    if (!server.DataCenterId.HasValue) continue;
                    if (!dcMap.TryGetValue(server.DataCenterId.Value, out var serverRegion)) continue;
                    if (server.Playing >= server.MaxPlayers) continue;

                    serversChecked++;

                    if (!regionRank.TryGetValue(serverRegion, out int rank)) continue;

                    bool isBetter = false;
                    if (rank < bestRank)
                    {
                        isBetter = true;
                    }
                    else if (rank == bestRank && joinSmallerServer && server.Playing < bestPlayers)
                    {
                        isBetter = true;
                    }
                    else if (rank == bestRank && !joinSmallerServer && server.Playing > bestPlayers)
                    {
                        isBetter = true;
                    }

                    if (isBetter)
                    {
                        bestRank = rank;
                        bestPlayers = server.Playing;
                        bestMaxPlayers = server.MaxPlayers;
                        bestServerId = server.Id;
                        bestServerRegion = serverRegion;
                        App.Logger.WriteLine("AutoJoin", $"Found better server in {serverRegion} (rank {rank}, players: {server.Playing}/{server.MaxPlayers})");

                        if (rank == 1)
                        {
                            App.Logger.WriteLine("AutoJoin", "Found rank 1 server, stopping early");
                            break;
                        }
                    }
                }

                if (bestRank == 1 && bestServerId != null)
                    break;

                if (!string.IsNullOrEmpty(result.NextCursor))
                {
                    nextCursor = result.NextCursor;
                }
                else
                {
                    break;
                }

                await Task.Delay(200);
            }

            if (bestServerId != null)
            {
                string playerCount = $"{bestPlayers}/{bestMaxPlayers}";

                MessageBoxResult confirmResult = await Frontend.ShowMessageBox(
                    $"Found server in {bestServerRegion} with {playerCount} players.\nDo you want to join?",
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo
                );

                if (confirmResult == MessageBoxResult.Yes)
                {
                    string robloxUri = $"roblox://experiences/start?placeId={placeId}&gameInstanceId={bestServerId}";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = robloxUri,
                        UseShellExecute = true
                    });

                    _watcher?.KillRobloxProcess();
                    return;
                }
            }

            string errorMessage = $"Could not find a suitable server after checking {serversChecked} servers in {topRegions.Count} regions.";
            _ = Frontend.ShowMessageBox(errorMessage, MessageBoxImage.Information);
        }

        private static async Task<List<string>> GetClosestRegionsForAutoModeAsync(int topCount)
        {
            try
            {
                var ipinfo = await Http.GetJson<IPInfoResponse>(new Uri("https://ipinfo.io/json"));
                if (string.IsNullOrEmpty(ipinfo?.Loc))
                    return [];

                string[] location = ipinfo.Loc.Split(',');
                double userLat = double.Parse(location[0], CultureInfo.InvariantCulture);
                double userLon = double.Parse(location[1], CultureInfo.InvariantCulture);

                var datacenters = await Http.GetJson<List<DatacenterEntry>>(new Uri("https://apis.rovalra.com/v1/datacenters/list"));
                if (datacenters == null || datacenters.Count == 0)
                    return [];

                var regionDistance = new Dictionary<string, double>();

                foreach (var dc in datacenters)
                {
                    if (dc.Location == null || dc.Location.LatLong == null || dc.Location.LatLong.Length < 2)
                        continue;

                    if (!double.TryParse(dc.Location.LatLong[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                        !double.TryParse(dc.Location.LatLong[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                        continue;

                    double distance = GetDistance(userLat, userLon, lat, lon);
                    string regionKey = $"{dc.Location.City}, {dc.Location.Country}".TrimStart(',').Trim();

                    if (!regionDistance.TryGetValue(regionKey, out double existingDistance) || distance < existingDistance)
                        regionDistance[regionKey] = distance;
                }

                var closestRegions = regionDistance
                    .OrderBy(kvp => kvp.Value)
                    .Take(topCount)
                    .Select(kvp => kvp.Key)
                    .ToList();

                App.Logger.WriteLine("AutoJoin", $"Top {closestRegions.Count} regions: {string.Join(", ", closestRegions)}");
                return closestRegions;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AutoJoin::GetClosestRegionsForAutoMode", ex);
                return [];
            }
        }

        private static double Deg2Rad(double deg) => deg * (Math.PI / 180.0);

        private static double GetDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            double dLat = Deg2Rad(lat2 - lat1);
            double dLon = Deg2Rad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                      Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
                      Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}