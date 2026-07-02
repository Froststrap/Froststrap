using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using Froststrap.Integrations;

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
                        CloseRobloxMenuItem?.SetValue(MenuItem.HeaderProperty, Strings.Menu_ContextMenu_CloseStudio);

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
                statusText = $"{Strings.Common_Total}: {FormatTimeSpan(total)} | {Strings.Common_Studio}: {FormatTimeSpan(DateTime.Now - _studioPlaceJoinTime.Value)}";
            else if (ActivityWatcher.InGame)
                statusText = $"{Strings.Common_Total}: {FormatTimeSpan(total)} | {Strings.Common_Game}: {FormatTimeSpan(DateTime.Now - ActivityWatcher.Data.TimeJoined)}";
            else
                statusText = $"{Strings.Common_Total}: {FormatTimeSpan(total)}";

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

        private async void InviteDeeplinkMenuItem_Click(object? sender, EventArgs e)
        {
            string deeplink = ActivityWatcher?.Data?.GetInviteDeeplink(true, DeeplinkType.RobloxWeb)
                               ?? Strings.Menu_ContextMenu_NoData;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is IClipboard clipboard)
            {
                await clipboard.SetTextAsync(deeplink);
            }
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

            var fetcher = new RobloxServerFetcher();
            await fetcher.JoinBestServerAsync(
                ActivityWatcher.Data.PlaceId,
                App.Settings.Prop.JoinSmallerServer,
                App.Settings.Prop.BestRegionAmounts,
                App.Settings.Prop.MaxServerCheck,
                showConfirmation: true
            );
        }
    }
}