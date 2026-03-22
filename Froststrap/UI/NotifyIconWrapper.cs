using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
		private ActivityWatcher? _activityWatcher => _watcher.ActivityWatcher;

        private DateTime _lastClickTime = DateTime.MinValue;
        private const int DoubleClickThresholdMs = 300;

        public NotifyIconWrapper(Watcher watcher)
        {
            App.Logger.WriteLine("NotifyIconWrapper::NotifyIconWrapper", "Initializing Avalonia TrayIcon");

            _watcher = watcher;
            _menuContainer = new MenuContainer(_watcher);

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Froststrap/Froststrap.ico"))),
                ToolTipText = "Froststrap"
            };

            _trayIcon.Clicked += OnTrayIconClicked;

            if (_activityWatcher is not null && (App.Settings.Prop.ShowServerDetails || App.Settings.Prop.ShowServerUptime))
            {
                _activityWatcher.OnGameJoin += OnGameJoin;
            }

            var trayIcons = TrayIcon.GetIcons(Application.Current!);
            trayIcons?.Add(_trayIcon);
        }

        private void OnTrayIconClicked(object? sender, EventArgs e)
        {
            if (e is PointerPressedEventArgs pointerArgs)
            {
                var pointerProperties = pointerArgs.GetCurrentPoint(null).Properties;

                if (pointerProperties.IsRightButtonPressed)
                {
                    HandleRightClickAction();
                    return;
                }

                HandleLeftClickLogic();
            }
            else
            {
                HandleLeftClickLogic();
            }
        }

        // TODO fix closing messagebox closes the watcher
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

        private void HandleRightClickAction()
        {
            App.Logger.WriteLine("NotifyIconWrapper", "Right click detected - Opening Context Menu");

            // TODO make right clicking open context menu
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
				TimeSpan _serverUptime = DateTime.UtcNow - serverTime.Value;

				if (_serverUptime.TotalSeconds > 60)
					serverUptime = Time.FormatTimeSpan(_serverUptime);
				else
					serverUptime = Strings.ContextMenu_ServerInformation_Notification_ServerNotTracked;
			}

			if (
				string.IsNullOrEmpty(serverLocation) && locationActive ||
				string.IsNullOrEmpty(serverUptime) && uptimeActive
				)
				return;

			string notifContent = Strings.Common_UnknownStatus;

			if (locationActive && !uptimeActive)
				notifContent = String.Format(Strings.ContextMenu_ServerInformation_Notification_Text, serverLocation);
			else if (!locationActive && uptimeActive)
				notifContent = String.Format(Strings.ContextMenu_ServerInformationUptime_Notification_Text, serverUptime);
			else if (locationActive && uptimeActive)
				notifContent = String.Format(Strings.ContextMenu_ServerInformationUptimeAndLocation_Notification_Text, serverLocation, serverUptime);

			ShowAlert(title, notifContent);
		}

		public void ShowAlert(string title, string message)
		{
			Frontend.ShowBalloonTip(title, message, Avalonia.Controls.Notifications.NotificationType.Information);
		}

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            App.Logger.WriteLine("NotifyIconWrapper::Dispose", "Cleaning up TrayIcon and MenuContainer");

            Dispatcher.UIThread.Post(() =>
            {
                var trayIcons = TrayIcon.GetIcons(Application.Current!);
                trayIcons?.Remove(_trayIcon);

                _menuContainer.Close();
                _trayIcon.Dispose();
            });

            GC.SuppressFinalize(this);
        }
    }
}