using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.UI.Elements.ContextMenu;

namespace Froststrap.UI
{
    internal class NotifyIconWrapper : IDisposable
    {
        private bool _isDisposed;
        private readonly TrayIcon _trayIcon;
        private readonly MenuContainer _menuContainer;
        private readonly Watcher _watcher;
        private ActivityWatcher? ActivityWatcher => _watcher.ActivityWatcher;

        private DateTime _lastClickTime = DateTime.MinValue;
        private const int DoubleClickThresholdMs = 300;

        public NotifyIconWrapper(Watcher watcher)
        {
            App.Logger.Info("Initializing Avalonia TrayIcon");

            _watcher = watcher;
            _menuContainer = new MenuContainer(_watcher);

            var nativeMenu = NativeMenu.GetMenu(_menuContainer);

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Froststrap/FroststrapTray.ico"))),
                ToolTipText = "Froststrap",
                Menu = nativeMenu
            };

            _trayIcon.Clicked += OnTrayIconClicked;

            if (ActivityWatcher is not null && App.Settings.Prop.ShowServerDetails)
            {
                if (App.Settings.Prop.ShowServerUptime)
                    ActivityWatcher.ShowNotif += ShowNotification;
                else
                    ActivityWatcher.OnGameJoin += ShowNotification;
            }

            TrayIcon.GetIcons(Application.Current!)?.Add(_trayIcon);
        }


        // On macos simply clicking the icon instantly opens the menu so double click action isnt possible
        private void OnTrayIconClicked(object? sender, EventArgs e)
        {
            if (OperatingSystem.IsMacOS())
                return;

            HandleWindowsDoubleClickLogic();
        }

        private void HandleWindowsDoubleClickLogic()
        {
            DateTime now = DateTime.Now;
            double elapsed = (now - _lastClickTime).TotalMilliseconds;

            if (elapsed <= DoubleClickThresholdMs)
            {
                _lastClickTime = DateTime.MinValue;
                HandleDoubleClickAction();
            }
            else
            {
                _lastClickTime = now;
            }
        }

        private void HandleDoubleClickAction()
        {
            switch (App.Settings.Prop.DoubleClickAction)
            {
                case TrayDoubleClickAction.None:
                    _ = Frontend.ShowMessageBox("You don't have the double-click action set to anything.", MessageBoxImage.Information);
                    break;

                case TrayDoubleClickAction.GameHistory:
                    if (!App.Settings.Prop.ShowGameHistoryMenu)
                    {
                        _ = Frontend.ShowMessageBox("Enable 'Game History' in settings to use this feature.", MessageBoxImage.Information);
                        return;
                    }
                    var history = new ServerHistory(ActivityWatcher!);
                    history.Show();
                    break;

                case TrayDoubleClickAction.ServerInfo:
                    if (!App.Settings.Prop.ShowServerDetails)
                    {
                        _ = Frontend.ShowMessageBox("Enable 'Query Server Location' in settings to use this feature.", MessageBoxImage.Information);
                        return;
                    }

                    if (ActivityWatcher?.InGame == true)
                        _menuContainer.ShowServerInformationWindow();
                    else
                        _ = Frontend.ShowMessageBox("Join a game first to view server information.", MessageBoxImage.Information);
                    break;
            }
        }

        public async void ShowNotification(object? sender, EventArgs e)
        {
            App.Logger.Debug("Dispatching event notfification");
            if (ActivityWatcher?.Data == null) return;

            string title = ActivityWatcher.Data.ServerType switch
            {
                ServerType.Public => Strings.ContextMenu_ServerInformation_Notification_Title_Public,
                ServerType.Private => Strings.ContextMenu_ServerInformation_Notification_Title_Private,
                ServerType.Reserved => Strings.ContextMenu_ServerInformation_Notification_Title_Reserved,
                _ => ""
            };

            string? serverLocation = await ActivityWatcher.Data.QueryServerLocation();
            if (string.IsNullOrEmpty(serverLocation))
            {
                App.Logger.Error("Couldn't connect to ipinfo.io");
                ShowAlert(
                    string.Format(CultureInfo.InvariantCulture, Strings.Dialog_Connectivity_UnableToConnect, "ipinfo.io"),
                    Strings.ActivityWatcher_LocationQueryFailed,
                    5
                );
                return;
            }

            if (!App.Settings.Prop.ShowServerUptime)
            {
                string? serverID = ActivityWatcher.Data.JobId;
                ShowAlert(title, string.Format(CultureInfo.InvariantCulture, Strings.ContextMenu_ServerDetails_Notification_Text_ServerID, serverLocation, serverID));
            }
            else
            {
                TimeSpan _serverUptime = DateTime.UtcNow - (ActivityWatcher.Data.StartTime ?? DateTime.UtcNow);
                string serverUptime = _serverUptime.TotalMinutes < 1
                    ? Strings.Common_JustStarted
                    : Time.FormatTimeSpan(_serverUptime);

                ShowAlert(title, string.Format(CultureInfo.InvariantCulture, Strings.ContextMenu_ServerDetails_Notification_Text, serverLocation, serverUptime));
            }
        }

        private void ShowAlert(string title, string message, int duration = 5)
        {
            App.Logger.Debug("Dispatching Alert");
            if (_isDisposed) return;

            Backend.NativeNotify.SendMessage(
                title,
                message,
                duration
            );
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            App.Logger.Info("Cleaning up TrayIcon and MenuContainer");

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _trayIcon.IsVisible = false;

                    var trayIcons = TrayIcon.GetIcons(Application.Current!);
                    trayIcons?.Remove(_trayIcon);

                    _menuContainer.Close();
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Error during cleanup: {ex.Message}");
                }
            });

            _trayIcon.Dispose();

            if (ActivityWatcher is not null)
            {
                ActivityWatcher.ShowNotif -= ShowNotification;
                ActivityWatcher.OnGameJoin -= ShowNotification;
            }

            GC.SuppressFinalize(this);
        }
    }
}
