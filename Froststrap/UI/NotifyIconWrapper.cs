using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.UI.Elements.ContextMenu;
using Froststrap.UI.Elements.Dialogs;

namespace Froststrap.UI
{
	public class NotifyIconWrapper : IDisposable
	{
		private bool _isDisposed = false;
		private readonly TrayIcon _trayIcon;
		private readonly MenuContainer _menuContainer;
		private readonly Watcher _watcher;
		private ActivityWatcher? _activityWatcher => _watcher.ActivityWatcher;

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

            if (_activityWatcher is not null && (App.Settings.Prop.ShowServerDetails || App.Settings.Prop.ShowServerUptime))
            {
                _activityWatcher.OnGameJoin += OnGameJoin;
            }

            TrayIcon.GetIcons(Application.Current!)?.Add(_trayIcon);
        }

        private void OnTrayIconClicked(object? sender, EventArgs e)
        {
            HandleLeftClickLogic();
        }

        private void HandleLeftClickLogic()
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
                    _ = Frontend.ShowMessageBox("You don’t have the double-click action set to anything.", MessageBoxImage.Information);
                    break;

                case TrayDoubleClickAction.GameHistory:
                    if (!App.Settings.Prop.ShowGameHistoryMenu)
                    {
                        _ = Frontend.ShowMessageBox("Enable 'Game History' in settings to use this feature.", MessageBoxImage.Information);
                        return;
                    }
                    var history = new ServerHistory(_activityWatcher!);
                    history.Show();
                    break;

                case TrayDoubleClickAction.ServerInfo:
                    if (!App.Settings.Prop.ShowServerDetails)
                    {
                        _ = Frontend.ShowMessageBox("Enable 'Query Server Location' in settings to use this feature.", MessageBoxImage.Information);
                        return;
                    }

                    if (_activityWatcher?.InGame == true)
                        _menuContainer.ShowServerInformationWindow();
                    else
                        _ = Frontend.ShowMessageBox("Join a game first to view server information.", MessageBoxImage.Information);
                    break;
            }
        }

        public async void OnGameJoin(object? sender, EventArgs e)
        {
            if (_activityWatcher?.Data == null) return;

            Task<string?>? thumbnailTask = null;
            if (App.Settings.Prop.ShowJoinNotification)
            {
                thumbnailTask = Thumbnails.GetThumbnailUrlAsync(new ThumbnailRequest
                {
                    TargetId = (ulong)_activityWatcher.Data.UniverseId,
                    Type = ThumbnailType.GameIcon,
                    Size = "150x150",
                    Format = ThumbnailFormat.Png,
                    IsCircular = false
                }, CancellationToken.None);
            }

            string title = _activityWatcher.Data.ServerType switch
            {
                ServerType.Public => Strings.ContextMenu_ServerInformation_Notification_Title_Public,
                ServerType.Private => Strings.ContextMenu_ServerInformation_Notification_Title_Private,
                ServerType.Reserved => Strings.ContextMenu_ServerInformation_Notification_Title_Reserved,
                _ => ""
            };

            bool locationActive = App.Settings.Prop.ShowServerDetails;
            bool uptimeActive = App.Settings.Prop.ShowServerUptime;

            string? serverLocation = "";
            if (locationActive)
                serverLocation = await _activityWatcher.Data.QueryServerLocation();

            string? serverUptime = "";
            if (uptimeActive)
            {
                DateTime? serverTime = await _activityWatcher.Data.QueryServerTime();
                if (serverTime.HasValue)
                {
                    TimeSpan _serverUptime = DateTime.UtcNow - serverTime.Value;
                    serverUptime = _serverUptime.TotalSeconds > 60
                        ? Time.FormatTimeSpan(_serverUptime)
                        : Strings.ContextMenu_ServerInformation_Notification_ServerNotTracked;
                }
            }

            if ((string.IsNullOrEmpty(serverLocation) && locationActive) ||
                (string.IsNullOrEmpty(serverUptime) && uptimeActive))
                return;

            string notifContent = Strings.Common_UnknownStatus;
            if (locationActive && !uptimeActive)
                notifContent = String.Format(Strings.ContextMenu_ServerInformation_Notification_Text, serverLocation);
            else if (!locationActive && uptimeActive)
                notifContent = String.Format(Strings.ContextMenu_ServerInformationUptime_Notification_Text, serverUptime);
            else if (locationActive && uptimeActive)
                notifContent = String.Format(Strings.ContextMenu_ServerInformationUptimeAndLocation_Notification_Text, serverLocation, serverUptime);

            string? thumbnailUrl = null;
            if (thumbnailTask != null)
            {
                try { thumbnailUrl = await thumbnailTask; } catch { /* Fallback handled below */ }
            }

            thumbnailUrl ??= "avares://Froststrap/Froststrap/Resources/MessageBox/FullQuality/Information.png";

            if (App.Settings.Prop.ShowJoinNotification)
                ShowAlertWithImage(title, notifContent, thumbnailUrl);
        }

        public void ShowAlertWithImage(string title, string message, string imagePath)
        {
            if (_isDisposed) return;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_isDisposed) return;

                var notification = new NotificationDialog(title, message, imagePath, timeoutMs: 7000);

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