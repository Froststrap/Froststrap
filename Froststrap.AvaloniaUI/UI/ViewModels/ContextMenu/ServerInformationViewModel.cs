using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using System.Windows;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.ContextMenu
{
    internal class ServerInformationViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ActivityWatcher _activityWatcher;

        public string InstanceId => _activityWatcher.Data.JobId;

        public string ServerType => _activityWatcher.Data.ServerType.ToTranslatedString();

        public string ServerLocation { get; private set; } = Strings.Common_Loading;

        public string ServerUptime { get; private set; } = Strings.Common_Loading;

        public bool ServerLocationVisibility => App.Settings.Prop.ShowServerDetails;
        public bool ServerUptimeVisibility => App.Settings.Prop.ShowServerUptime;

        public ICommand CopyInstanceIdCommand => new RelayCommand(CopyInstanceId);

        public ServerInformationViewModel(Watcher watcher)
        {
            _activityWatcher = watcher.ActivityWatcher!;

            if (ServerLocationVisibility)
                QueryServerLocation();

            if (ServerUptimeVisibility)
                QueryServerUptime();
        }

        public async void QueryServerLocation()
        {
            string? location = await _activityWatcher.Data.QueryServerLocation();

            if (String.IsNullOrEmpty(location))
                ServerLocation = Strings.Common_NotAvailable;
            else
                ServerLocation = location;

            OnPropertyChanged(nameof(ServerLocation));
        }

        public async void QueryServerUptime()
        {
            DateTime? serverTime = await _activityWatcher.Data.QueryServerTime();
            TimeSpan _serverUptime = DateTime.UtcNow - serverTime.Value;

            string? serverUptime = Strings.ContextMenu_ServerInformation_Notification_ServerNotTracked;
            if (_serverUptime.TotalSeconds > 60)
                serverUptime = Time.FormatTimeSpan(_serverUptime);

            ServerUptime = serverUptime;

            OnPropertyChanged(nameof(ServerUptime));
        }

        private void CopyInstanceId() => TopLevel.GetTopLevel(null)?.Clipboard?.SetTextAsync(InstanceId);
    }
}
