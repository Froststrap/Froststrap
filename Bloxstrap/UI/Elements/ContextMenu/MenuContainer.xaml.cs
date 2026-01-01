using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

using Bloxstrap.Integrations;

namespace Bloxstrap.UI.Elements.ContextMenu
{
    public partial class MenuContainer
    {
        private readonly Watcher _watcher;
        private ActivityWatcher? _activityWatcher => _watcher.ActivityWatcher;

        private ServerInformation? _serverInformationWindow;
        private GameInformation? _gameInformationWindow;
        private ServerHistory? _gameHistoryWindow;

        private Stopwatch _totalPlaytimeStopwatch = new Stopwatch();
        private TimeSpan _accumulatedTotalPlaytime = TimeSpan.Zero;

        private DispatcherTimer? _playtimeTimer;
        private DateTime? _studioPlaceJoinTime = null;

        public MenuContainer(Watcher watcher)
        {
            InitializeComponent();

            _watcher = watcher;

            if (_activityWatcher is not null)
            {
                _activityWatcher.OnGameJoin += ActivityWatcher_OnGameJoin;
                _activityWatcher.OnGameLeave += ActivityWatcher_OnGameLeave;
                _activityWatcher.OnStudioPlaceOpened += ActivityWatcher_OnStudioPlaceOpened;
                _activityWatcher.OnStudioPlaceClosed += ActivityWatcher_OnStudioPlaceClosed;

                if (_activityWatcher.InRobloxStudio)
                {
                    InviteDeeplinkMenuItem.Visibility = Visibility.Collapsed;
                    ServerDetailsMenuItem.Visibility = Visibility.Collapsed;
                    GameInformationMenuItem.Visibility = Visibility.Collapsed;
                    GameHistoryMenuItem.Visibility = Visibility.Collapsed;
                    RegionJoinningMenuItem.Visibility = Visibility.Collapsed;

                    if (App.Settings.Prop.PlaytimeCounter)
                    {
                        StartTotalPlaytimeTimer();
                        PlaytimeMenuItem.Visibility = Visibility.Visible;

                        // Check if we're already in a Studio place when the menu opens
                        if (_activityWatcher.InStudioPlace)
                        {
                            _studioPlaceJoinTime = DateTime.Now;
                        }
                    }
                    else
                    {
                        PlaytimeMenuItem.Visibility = Visibility.Collapsed;
                    }

                    if (App.Settings.Prop.MemoryCleanerInterval != MemoryCleanerInterval.Never)
                        CleanMemoryMenuItem.Visibility = Visibility.Visible;
                    else
                        CleanMemoryMenuItem.Visibility = Visibility.Collapsed;

                }
                else
                {
                    if (App.Settings.Prop.PlaytimeCounter)
                    {
                        StartTotalPlaytimeTimer();
                        PlaytimeMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        PlaytimeMenuItem.Visibility = Visibility.Collapsed;
                    }

                    if (App.Settings.Prop.AllowCookieAccess)
                        UpdateRegionJoinText();

                    if (App.Settings.Prop.MemoryCleanerInterval != MemoryCleanerInterval.Never)
                        CleanMemoryMenuItem.Visibility = Visibility.Visible;
                    else
                        CleanMemoryMenuItem.Visibility = Visibility.Collapsed;

                    if (App.Settings.Prop.ShowGameHistoryMenu)
                        GameHistoryMenuItem.Visibility = Visibility.Visible;
                    else
                        GameHistoryMenuItem.Visibility = Visibility.Collapsed;
                }
            }

            if (_watcher.PlayerRichPresence is not null || _watcher.StudioRichPresence is not null)
                RichPresenceMenuItem.Visibility = Visibility.Visible;
            else
                RichPresenceMenuItem.Visibility = Visibility.Collapsed;

            VersionTextBlock.Text = $"{App.ProjectName} v{App.Version}";
        }

        private void StartTotalPlaytimeTimer()
        {
            _totalPlaytimeStopwatch.Start();

            _playtimeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _playtimeTimer.Tick += PlaytimeTimer_Tick;
            _playtimeTimer.Start();
        }

        private void StopTotalPlaytimeTimer()
        {
            _totalPlaytimeStopwatch.Stop();
            _accumulatedTotalPlaytime += _totalPlaytimeStopwatch.Elapsed;
            _totalPlaytimeStopwatch.Reset();

            if (_playtimeTimer != null)
            {
                _playtimeTimer.Tick -= PlaytimeTimer_Tick;
                _playtimeTimer.Stop();
                _playtimeTimer = null;
            }
        }

        private void PlaytimeTimer_Tick(object? sender, EventArgs e)
        {
            TimeSpan totalElapsed = _accumulatedTotalPlaytime + _totalPlaytimeStopwatch.Elapsed;

            if (_activityWatcher is null || (!_activityWatcher.InGame && !_activityWatcher.InStudioPlace))
            {
                PlaytimeTextBlock.Text = $"Total: {FormatTimeSpan(totalElapsed)}";
            }
            else if (_activityWatcher.InStudioPlace && _studioPlaceJoinTime.HasValue)
            {
                TimeSpan studioElapsed = DateTime.Now - _studioPlaceJoinTime.Value;
                PlaytimeTextBlock.Text = $"Total: {FormatTimeSpan(totalElapsed)} | Studio: {FormatTimeSpan(studioElapsed)}";
            }
            else if (_activityWatcher.InGame && !_activityWatcher.InRobloxStudio)
            {
                TimeSpan sessionElapsed = DateTime.Now - _activityWatcher!.Data.TimeJoined;
                PlaytimeTextBlock.Text = $"Total: {FormatTimeSpan(totalElapsed)} | Game: {FormatTimeSpan(sessionElapsed)}";
            }
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            else
                return $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        public void ShowServerInformationWindow()
        {
            if (_serverInformationWindow is null)
            {
                _serverInformationWindow = new(_watcher);
                _serverInformationWindow.Closed += (_, _) => _serverInformationWindow = null;
            }

            if (!_serverInformationWindow.IsVisible)
                _serverInformationWindow.ShowDialog();
            else
                _serverInformationWindow.Activate();
        }

        public void ShowGameInformationWindow(long placeId, long universeId)
        {
            if (_gameInformationWindow is null)
            {
                _gameInformationWindow = new GameInformation(placeId, universeId);
                _gameInformationWindow.Closed += (_, _) => _gameInformationWindow = null;
            }

            if (!_gameInformationWindow.IsVisible)
                _gameInformationWindow.ShowDialog();
            else
                _gameInformationWindow.Activate();
        }

        private void ActivityWatcher_OnGameJoin(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_activityWatcher?.Data.ServerType == ServerType.Public)
                    InviteDeeplinkMenuItem.Visibility = Visibility.Visible;

                ServerDetailsMenuItem.Visibility = Visibility.Visible;
                GameInformationMenuItem.Visibility = Visibility.Visible;

                if (App.Settings.Prop.AllowCookieAccess)
                    RegionJoinningMenuItem.Visibility = Visibility.Visible;
            });
        }

        private void ActivityWatcher_OnGameLeave(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                InviteDeeplinkMenuItem.Visibility = Visibility.Collapsed;
                ServerDetailsMenuItem.Visibility = Visibility.Collapsed;
                GameInformationMenuItem.Visibility = Visibility.Collapsed;

                if (App.Settings.Prop.AllowCookieAccess)
                    RegionJoinningMenuItem.Visibility = Visibility.Collapsed;

                _serverInformationWindow?.Close();
                _gameInformationWindow?.Close();
            });
        }

        private void ActivityWatcher_OnStudioPlaceOpened(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _studioPlaceJoinTime = DateTime.Now;
            });
        }

        private void ActivityWatcher_OnStudioPlaceClosed(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _studioPlaceJoinTime = null;
            });
        }

        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            HWND hWnd = (HWND)new WindowInteropHelper(this).Handle;
            int exStyle = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            exStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle);
        }

        private void Window_Closed(object sender, EventArgs e) => App.Logger.WriteLine("MenuContainer::Window_Closed", "Context menu container closed");
        private void CloseWatcheMenuItem_Click(object sender, RoutedEventArgs e) => _watcher.Dispose();

        private void RichPresenceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var isChecked = ((MenuItem)sender).IsChecked;

            _watcher.PlayerRichPresence?.SetVisibility(isChecked);
            _watcher.StudioRichPresence?.SetVisibility(isChecked);
        }

        private void InviteDeeplinkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string deeplink = _activityWatcher?.Data?.GetInviteDeeplink() ?? "No activity data available";
            Clipboard.SetDataObject(deeplink);
        }

        private void ServerDetailsMenuItem_Click(object sender, RoutedEventArgs e) => ShowServerInformationWindow();
        private void GameInformaionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            long placeId = _activityWatcher?.Data?.PlaceId ?? 0;
            long universeId = _activityWatcher?.Data?.UniverseId ?? 0;

            if (placeId == 0)
            {
                Frontend.ShowMessageBox(
                    "Not currently in a game. Please join a game first to view game information.",
                    MessageBoxImage.Error
                );
                return;
            }

            ShowGameInformationWindow(placeId, universeId);
        }

        private void CleanMemoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            const string LOG_IDENT = "MenuContainer::CleanMemoryMenuItem_Click";

            try
            {
                _watcher.MemoryCleaner?.CleanMemory();
                _watcher.MemoryCleaner?.TrimRobloxProcesses();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Exception during manual cleanup: {ex.Message}");
                Frontend.ShowMessageBox($"Failed to clean memory: {ex.Message}", MessageBoxImage.Error);
            }
        }

        private void CloseRobloxMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = Frontend.ShowMessageBox(
                Strings.ContextMenu_CloseRobloxMessage,
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            _watcher.KillRobloxProcess();
            StopTotalPlaytimeTimer();
        }

        private void JoinLastServerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_activityWatcher is null)
                throw new ArgumentNullException(nameof(_activityWatcher));

            if (_gameHistoryWindow is null)
            {
                _gameHistoryWindow = new(_activityWatcher);
                _gameHistoryWindow.Closed += (_, _) => _gameHistoryWindow = null;
            }

            if (!_gameHistoryWindow.IsVisible)
                _gameHistoryWindow.ShowDialog();
            else
                _gameHistoryWindow.Activate();
        }

        private async void AutoJoinRegionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            const string LOG_IDENT = "MenuContainer::AutoJoinRegionMenuItem_Click";

            try
            {
                if (_activityWatcher?.InGame != true || _activityWatcher?.Data == null)
                {
                    Frontend.ShowMessageBox("You need to be in a game to use this feature.", MessageBoxImage.Warning);
                    return;
                }

                long placeId = _activityWatcher.Data.PlaceId;

                string selectedRegion = App.Settings.Prop.SelectedRegion;
                if (string.IsNullOrEmpty(selectedRegion))
                {
                    Frontend.ShowMessageBox("Please select a region in Region Selector first.", MessageBoxImage.Warning);
                    return;
                }

                MessageBoxResult result = Frontend.ShowMessageBox($"Start searching for {selectedRegion}?", MessageBoxImage.Information, MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes)
                    return;

                await FindAndJoinServerInRegion(placeId, selectedRegion);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                Frontend.ShowMessageBox($"Failed to auto-join: {ex.Message}", MessageBoxImage.Error);
            }
        }

        private async Task FindAndJoinServerInRegion(long placeId, string selectedRegion)
        {
            const string LOG_IDENT = "MenuContainer::FindAndJoinServerInRegion";

            var fetcher = new RobloxServerFetcher();
            string? nextCursor = "";
            int pagesChecked = 0;
            const int maxPages = 20;

            var datacentersResult = await fetcher.GetDatacentersAsync();
            if (datacentersResult == null)
            {
                Frontend.ShowMessageBox("Failed to load regions.", MessageBoxImage.Error);
                return;
            }

            var (regions, dcMap) = datacentersResult.Value;

            string? cookie = null;

            try
            {
                await App.RemoteData.WaitUntilDataFetched();
                cookie = App.RemoteData.Prop.Dummy;

                bool isValid = await fetcher.ValidateCookieAsync(cookie);

                if (!isValid)
                {
                    Frontend.ShowMessageBox("Dummy cookie is invalid or expired. Please notify us in our discord server.", MessageBoxImage.Error);
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT, "Dummy cookie is valid, starting search...");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                Frontend.ShowMessageBox("Failed to validate cookie.", MessageBoxImage.Error);
                return;
            }

            while (pagesChecked < maxPages)
            {
                pagesChecked++;

                var result = await fetcher.FetchServerInstancesAsync(placeId, cookie, nextCursor, 2);

                if (result?.Servers != null)
                {
                    var matchingServer = result.Servers.FirstOrDefault(server =>
                        server.DataCenterId.HasValue &&
                        dcMap.TryGetValue(server.DataCenterId.Value, out var mappedRegion) &&
                        mappedRegion == selectedRegion &&
                        server.Playing < server.MaxPlayers);

                    if (matchingServer != null)
                    {
                        MessageBoxResult confirmResult = Frontend.ShowMessageBox(
                            $"Found server in {selectedRegion} with {matchingServer.Playing}/{matchingServer.MaxPlayers} players.\nDo you want to join?",
                            MessageBoxImage.Question,
                            MessageBoxButton.YesNo
                        );

                        if (confirmResult == MessageBoxResult.Yes)
                        {
                            string robloxUri = $"roblox://experiences/start?placeId={placeId}&gameInstanceId={matchingServer.Id}";
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = robloxUri,
                                UseShellExecute = true
                            });

                            _watcher.KillRobloxProcess();
                            return;
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                if (string.IsNullOrEmpty(result?.NextCursor))
                    break;

                nextCursor = result.NextCursor;
                await Task.Delay(250);
            }

            Frontend.ShowMessageBox($"No {selectedRegion} server found after checking {pagesChecked} pages, Please try another region.", MessageBoxImage.Information);
        }

        private void UpdateRegionJoinText()
        {
            if (RegionJoinTextBlock == null)
                return;

            string? selectedRegion = App.Settings.Prop.SelectedRegion;

            if (string.IsNullOrEmpty(selectedRegion))
            {
                RegionJoinTextBlock.Text = "Join Region";
            }
            else
            {
                RegionJoinTextBlock.Text = $"Join {selectedRegion}";
            }
        }
    }
}