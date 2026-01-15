using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.UI.Elements.ContextMenu;

namespace Froststrap.UI
{
    public class NotifyIconWrapper : IDisposable
    {
        private bool _disposing = false;
        private TrayIcon? _trayIcon;
        private readonly MenuContainer _menuContainer;
        private readonly Watcher _watcher;
        private ActivityWatcher? _activityWatcher => _watcher.ActivityWatcher;

        public NotifyIconWrapper(Watcher watcher)
        {
            App.Logger.WriteLine("NotifyIconWrapper::NotifyIconWrapper", "Initializing notification area icon");

            _watcher = watcher;

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
                _trayIcon = new TrayIcon
                {
                    Icon = new WindowIcon(AssetLoader.Open((new Uri("avares://Froststrap/Assets/Icons/Froststrap.ico")))),
                    ToolTipText = App.ProjectName,
                    IsVisible = true
                };

                _trayIcon.Clicked += OnTrayIconClicked;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("NotifyIconWrapper::InitializeTrayIcon", $"Failed to initialize tray icon: {ex.Message}");
            }
        }

        private void OnTrayIconClicked(object? sender, EventArgs e)
        {
            ShowCustomContextMenu();
        }

        private void ShowCustomContextMenu()
        {
            try
            {
                _menuContainer.Activate();

                if (_menuContainer.ContextMenu != null)
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                    {
                        var mainWindow = desktopLifetime.MainWindow;
                        if (mainWindow != null && mainWindow.IsVisible)
                        {
                            _menuContainer.ContextMenu.Open(mainWindow);
                        }
                        else
                        {
                            mainWindow?.Show();
                            _menuContainer.ContextMenu.Open(mainWindow);
                            mainWindow?.Hide();
                        }
                    }
                }
                else
                {
                    App.Logger.WriteLine("NotifyIconWrapper::ShowCustomContextMenu", "MenuContainer.ContextMenu is null - MenuContainer should handle its own display");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("NotifyIconWrapper::ShowCustomContextMenu",
                    $"Failed to show context menu: {ex.Message}");

                BringMainWindowToFront();
            }
        }

        private void BringMainWindowToFront()
        {
            try
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                    {
                        var mainWindow = desktopLifetime.MainWindow;
                        if (mainWindow != null)
                        {
                            if (mainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
                                mainWindow.WindowState = Avalonia.Controls.WindowState.Normal;

                            mainWindow.Show();
                            mainWindow.Activate();
                            mainWindow.Topmost = true;
                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                mainWindow.Topmost = false;
                            }, DispatcherPriority.Background);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("NotifyIconWrapper::BringMainWindowToFront",
                    $"Failed to bring window to front: {ex.Message}");
            }
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

            if (locationActive && !uptimeActive)
                notifContent = string.Format(Strings.ContextMenu_ServerInformation_Notification_Text, serverLocation);
            else if (!locationActive && uptimeActive)
                notifContent = string.Format(Strings.ContextMenu_ServerInformationUptime_Notification_Text, serverUptime);
            else if (locationActive && uptimeActive)
                notifContent = string.Format(Strings.ContextMenu_ServerInformationUptimeAndLocation_Notification_Text, serverLocation, serverUptime);

            ShowSimpleNotification(title, notifContent);
        }
        #endregion

        private void ShowSimpleNotification(string title, string message)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                Frontend.ShowMessageBox($"{title}\n\n{message}", MessageBoxImage.Information);
            });
        }

        public void ShowAlert(string caption, string message, int duration, EventHandler? clickHandler)
        {
            string id = Guid.NewGuid().ToString()[..8];
            string LOG_IDENT = $"NotifyIconWrapper::ShowAlert.{id}";

            App.Logger.WriteLine(LOG_IDENT, $"Showing alert for {duration} seconds");
            App.Logger.WriteLine(LOG_IDENT, $"{caption}: {message.Replace("\n", "\\n")}");

            Dispatcher.UIThread.Invoke(() =>
            {
                Frontend.ShowMessageBox($"{caption}\n\n{message}", MessageBoxImage.Information);
                clickHandler?.Invoke(null, EventArgs.Empty);
            });
        }

        public void Dispose()
        {
            if (_disposing)
                return;

            _disposing = true;

            App.Logger.WriteLine("NotifyIconWrapper::Dispose", "Disposing NotifyIcon");

            try
            {
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
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("NotifyIconWrapper::Dispose",
                    $"Error during disposal: {ex.Message}");
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }
    }
}