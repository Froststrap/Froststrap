using Avalonia.Controls.Notifications;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Notifications;
using Avalonia.Platform;
using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.UI.Elements.ContextMenu;

namespace Froststrap.UI
{
    public class NotifyIconWrapper : IDisposable
    {
        private bool _isDisposed = false;
        private readonly TrayIcon _trayIcon;
        private readonly MenuContainer _menuContainer;
        private readonly Watcher _watcher;
        private ActivityWatcher? ActivityWatcher => _watcher.ActivityWatcher;

        private DateTime _lastClickTime = DateTime.MinValue;
        private const int DoubleClickThresholdMs = 300;

        public NotifyIconWrapper(Watcher watcher)
        {
            App.Logger.WriteLine("NotifyIconWrapper::NotifyIconWrapper", "Initializing Avalonia TrayIcon");

            _watcher = watcher;
            _menuContainer = new MenuContainer(_watcher);

            var nativeMenu = NativeMenu.GetMenu(_menuContainer);

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Froststrap/Froststrap.ico"))),
                ToolTipText = "Froststrap",
                Menu = nativeMenu
            };

            _trayIcon.Clicked += OnTrayIconClicked;

            if (ActivityWatcher is not null && App.Settings.Prop.ShowServerDetails)
                ActivityWatcher.ShowNotif += ShowNotif;

            TrayIcon.GetIcons(Application.Current!)?.Add(_trayIcon);

            App.Logger.WriteLine("NotifyIconWrapper::NotifyIconWrapper", OperatingSystem.IsMacOS() ? "Running as macOS menu bar icon" : "Running as Windows system tray icon");
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

        public async void ShowNotif(object? sender, EventArgs e)
        {
            if (ActivityWatcher?.Data == null) return;

            string title = ActivityWatcher.Data.ServerType switch
            {
                ServerType.Public => Strings.ContextMenu_ServerInformation_Notification_Title_Public,
                ServerType.Private => Strings.ContextMenu_ServerInformation_Notification_Title_Private,
                ServerType.Reserved => Strings.ContextMenu_ServerInformation_Notification_Title_Reserved,
                _ => ""
            };

            string? serverLocation = await ActivityWatcher.Data.QueryServerLocation();
            string? serverUptime;
            DateTime? serverTime = ActivityWatcher.Data.StartTime;

            if (serverTime is not null)
            {
                TimeSpan _serverUptime = DateTime.UtcNow - serverTime.Value;

                if (_serverUptime.TotalMinutes < 1)
                    serverUptime = "0 minutes";
                else
                    serverUptime = Time.FormatTimeSpan(_serverUptime);
            }
            else
            {
                serverUptime = "0 minutes";
            }

            ShowAlert(
                title,
                string.Format(Strings.ContextMenu_ServerDetails_Notification_Text, serverLocation, serverUptime));
        }

        public void ShowAlert(string title, string message, NotificationType category = NotificationType.Information)
        {
            if (_isDisposed) return;

            var manager = NativeNotificationManager.Current;
            if (manager == null)
            {
                App.Logger.WriteLine("NotifyIconWrapper::ShowAlert", "NativeNotificationManager is null.");
                return;
            }

            string categoryString = category switch
            {
                NotificationType.Success => "success",
                NotificationType.Warning => "warning",
                NotificationType.Error => "error",
                _ => "info"
            };

            var notification = manager.CreateNotification(categoryString);

            if (notification == null) return;

            notification.Title = title;
            notification.Message = message;
            notification.Expiration = TimeSpan.FromSeconds(5);

            Dispatcher.UIThread.Post(() =>
            {
                if (_isDisposed) return;
                notification.Show();
            }, DispatcherPriority.Background);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            App.Logger.WriteLine("NotifyIconWrapper::Dispose", "Cleaning up TrayIcon and MenuContainer");

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _trayIcon.IsVisible = false;

                    var trayIcons = TrayIcon.GetIcons(Application.Current!);
                    trayIcons?.Remove(_trayIcon);

                    _menuContainer.Close();

                    _trayIcon.Dispose();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("NotifyIconWrapper::Dispose", $"Error during cleanup: {ex.Message}");
                }
            });

            GC.SuppressFinalize(this);
        }
    }
}
