using Avalonia.Controls;
using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.UI.Elements.ContextMenu;

namespace Froststrap.UI
{
    public class NotifyIconWrapper : IDisposable
    {
        private bool _disposing = false;
        private TrayIcon? _trayIcon;
        private NativeMenu? _nativeMenu;
        private readonly MenuContainer _menuContainer;
        private readonly Watcher _watcher;
        private ActivityWatcher? _activityWatcher => _watcher.ActivityWatcher;
        private EventHandler? _alertClickHandler;

        // Timer for double-click detection
        private DateTime _lastClickTime = DateTime.MinValue;
        private const int DoubleClickMilliseconds = 500;

        public NotifyIconWrapper(Watcher watcher)
        {
            App.Logger.WriteLine("NotifyIconWrapper::NotifyIconWrapper", "Initializing notification area icon");

            _watcher = watcher;

            // Initialize tray icon on UI thread
            Dispatcher.UIThread.Invoke(() =>
            {
                InitializeTrayIcon();
            });

            if (_activityWatcher is not null && (App.Settings.Prop.ShowServerDetails || App.Settings.Prop.ShowServerUptime))
                _activityWatcher.OnGameJoin += OnGameJoin;

            _menuContainer = new(_watcher);
            _menuContainer.Show();
        }

        private void InitializeTrayIcon()
        {
            try
            {
                // Create tray icon
                _trayIcon = new TrayIcon
                {
                    ToolTipText = App.ProjectName,
                    IsVisible = true
                };

                // Load icon - assuming you have an icon resource
                // You'll need to add an .ico file to your project
                var iconPath = "avares://Froststrap/Resources/IconFroststrap.ico";

                // Alternative: Use a PNG if .ico isn't supported
                // var icon = new WindowIcon(iconPath);
                // _trayIcon.Icon = icon;

                // For now, we'll just show the tray icon without an icon
                // You'll need to implement proper icon loading

                // Create context menu
                CreateNativeMenu();

                // Set up click handlers
                _trayIcon.Clicked += OnTrayIconClicked;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("NotifyIconWrapper::InitializeTrayIcon",
                    $"Failed to initialize tray icon: {ex.Message}");
            }
        }

        private void CreateNativeMenu()
        {
            // Create a simple native menu
            _nativeMenu = new NativeMenu();

            // Add menu items
            var exitItem = new NativeMenuItem("Exit");
            exitItem.Click += (s, e) => App.SoftTerminate();
            _nativeMenu.Add(exitItem);

            // Set the menu
            if (_trayIcon != null)
            {
                _trayIcon.Menu = _nativeMenu;
            }
        }

        private void OnTrayIconClicked(object? sender, EventArgs e)
        {
            // Determine if this is a double-click
            var now = DateTime.Now;
            var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
            _lastClickTime = now;

            if (timeSinceLastClick <= DoubleClickMilliseconds)
            {
                // Double-click detected
                HandleDoubleClick();
            }
            else
            {
                // Single click - show context menu
                ShowContextMenu();
            }
        }

        private void HandleDoubleClick()
        {
            switch (App.Settings.Prop.DoubleClickAction)
            {
                case TrayDoubleClickAction.None:
                    Frontend.ShowMessageBox(
                        "You don't have the double-click action set to anything.",
                        MessageBoxImage.Information
                    );
                    break;

                case TrayDoubleClickAction.GameHistory:
                    if (!App.Settings.Prop.ShowGameHistoryMenu)
                    {
                        Frontend.ShowMessageBox(
                            "Enable 'Game History' in settings to use this feature.",
                            MessageBoxImage.Information
                        );
                        return;
                    }

                    new ServerHistory(_activityWatcher!).Show();
                    break;

                case TrayDoubleClickAction.ServerInfo:
                    if (!App.Settings.Prop.ShowServerDetails)
                    {
                        Frontend.ShowMessageBox(
                            "Enable 'Query Server Location' in settings to use this feature.",
                            MessageBoxImage.Information
                        );
                        return;
                    }

                    if (_activityWatcher is not null && _activityWatcher.InGame)
                    {
                        _menuContainer.ShowServerInformationWindow();
                    }
                    else
                    {
                        Frontend.ShowMessageBox(
                            "Join a game first to view server information.",
                            MessageBoxImage.Information
                        );
                    }
                    break;
            }
        }

        private void ShowContextMenu()
        {
            // Activate the menu container to show custom context menu
            _menuContainer.Activate();
            _menuContainer.ContextMenu?.Open();
        }

        #region Activity handlers
        public async void OnGameJoin(object? sender, EventArgs e)
        {
            if (_activityWatcher is null)
                return;

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

                    if (_serverUptime.TotalSeconds > 60)
                        serverUptime = Time.FormatTimeSpan(_serverUptime);
                    else
                        serverUptime = Strings.ContextMenu_ServerInformation_Notification_ServerNotTracked;
                }
            }

            if ((string.IsNullOrEmpty(serverLocation) && locationActive) ||
                (string.IsNullOrEmpty(serverUptime) && uptimeActive))
                return;

            string notifContent = Strings.Common_UnknownStatus;

            // Since we don't have an actual localization, this is probably the best way of doing that
            if (locationActive && !uptimeActive)
                notifContent = string.Format(Strings.ContextMenu_ServerInformation_Notification_Text, serverLocation);
            else if (!locationActive && uptimeActive)
                notifContent = string.Format(Strings.ContextMenu_ServerInformationUptime_Notification_Text, serverUptime);
            else if (locationActive && uptimeActive)
                notifContent = string.Format(Strings.ContextMenu_ServerInformationUptimeAndLocation_Notification_Text, serverLocation, serverUptime);

            ShowAlert(
                title,
                notifContent,
                10,
                (_, _) => _menuContainer.ShowServerInformationWindow()
            );
        }
        #endregion

        // Avalonia doesn't have built-in balloon tips, so we need to create our own notification system
        public void ShowAlert(string caption, string message, int duration, EventHandler? clickHandler)
        {
            string id = Guid.NewGuid().ToString()[..8];
            string LOG_IDENT = $"NotifyIconWrapper::ShowAlert.{id}";

            App.Logger.WriteLine(LOG_IDENT, $"Showing alert for {duration} seconds (clickHandler={clickHandler is not null})");
            App.Logger.WriteLine(LOG_IDENT, $"{caption}: {message.Replace("\n", "\\n")}");

            // For now, we'll just log the alert since Avalonia doesn't have built-in tray notifications
            // You could implement a custom notification window here

            // TODO: Implement custom notification window
            App.Logger.WriteLine(LOG_IDENT, "Notification shown (no UI implementation yet)");

            // Store the click handler for later use
            _alertClickHandler = clickHandler;

            // In a real implementation, you would show a custom notification window
            // and handle clicks on it
        }

        public void Dispose()
        {
            if (_disposing)
                return;

            _disposing = true;

            App.Logger.WriteLine("NotifyIconWrapper::Dispose", "Disposing NotifyIcon");

            // Clean up resources on UI thread
            Dispatcher.UIThread.Invoke(() =>
            {
                _menuContainer?.Close();

                if (_trayIcon != null)
                {
                    _trayIcon.Clicked -= OnTrayIconClicked;
                    _trayIcon.IsVisible = false;
                    _trayIcon.Dispose();
                }
            });

            GC.SuppressFinalize(this);
        }
    }
}