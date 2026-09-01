using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.ContextMenu;

internal class ServerInformationViewModel : NotifyPropertyChangedViewModel
{
    private ActivityWatcher? _activityWatcher;
    private string _serverType = string.Empty;
    private string _instanceId = string.Empty;
    private string _accessCode = string.Empty;
    private string _location = string.Empty;
    private string _uptime = string.Empty;
    private bool _hasServerData;

    private DispatcherTimer? _uptimeTimer;
    private bool _isTimerRunning;

    public string ServerType
    {
        get => _serverType;
        set => SetProperty(ref _serverType, value);
    }

    public string InstanceId
    {
        get => _instanceId;
        set
        {
            if (SetProperty(ref _instanceId, value))
                OnPropertyChanged(nameof(ShowInstanceId));
        }
    }

    public string AccessCode
    {
        get => _accessCode;
        set
        {
            if (SetProperty(ref _accessCode, value))
            {
                OnPropertyChanged(nameof(AccessCodeVisibility));
                OnPropertyChanged(nameof(CopyButtonText));
            }
        }
    }

    public string ServerLocation
    {
        get => _location;
        set
        {
            if (SetProperty(ref _location, value))
                OnPropertyChanged(nameof(ServerLocationVisibility));
        }
    }

    public string ServerUptime
    {
        get => _uptime;
        set
        {
            if (SetProperty(ref _uptime, value))
                OnPropertyChanged(nameof(ServerUptimeVisibility));
        }
    }

    public bool HasServerData
    {
        get => _hasServerData;
        set
        {
            if (SetProperty(ref _hasServerData, value))
            {
                OnPropertyChanged(nameof(ShowInstanceId));
                OnPropertyChanged(nameof(AccessCodeVisibility));
                OnPropertyChanged(nameof(ServerLocationVisibility));
                OnPropertyChanged(nameof(ServerUptimeVisibility));
                OnPropertyChanged(nameof(ShowCopyButton));
                OnPropertyChanged(nameof(CopyButtonText));
            }
        }
    }

    public bool ShowInstanceId => HasServerData && !string.IsNullOrEmpty(InstanceId);
    public bool AccessCodeVisibility => HasServerData && !string.IsNullOrEmpty(AccessCode);
    public bool ServerLocationVisibility => HasServerData && !string.IsNullOrEmpty(ServerLocation) && ServerLocation != Strings.Common_NotAvailable;
    public bool ServerUptimeVisibility => HasServerData && !string.IsNullOrEmpty(ServerUptime) && ServerUptime != Strings.Common_NotAvailable && App.Settings.Prop.ShowServerUptime;
    public bool ShowCopyButton => HasServerData;

    public ICommand CopyCommand { get; }
    public ICommand CloseCommand { get; }

    public string CopyButtonText => AccessCodeVisibility
        ? Strings.ContextMenu_ServerInformation_CopyAccessCode
        : Strings.ContextMenu_ServerInformation_CopyInstanceId;

    public ServerInformationViewModel()
    {
        CopyCommand = new RelayCommand<Visual?>(CopyToClipboard);
        CloseCommand = new RelayCommand<Window>(window => window?.Close());

        // Initialize the timer for live uptime updates
        _uptimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uptimeTimer.Tick += (s, e) => RefreshUptime();
    }

    public void SetWatcher(ActivityWatcher? watcher)
    {
        _activityWatcher = watcher;
    }

    public void UpdateData(ActivityData data)
    {
        ServerType = data.ServerType.ToString();
        InstanceId = data.JobId;
        AccessCode = data.AccessCode;
        ServerLocation = data.Region ?? Strings.Common_NotAvailable;
        ServerUptime = data.StartTime.HasValue
            ? FormatUptime(DateTime.UtcNow - data.StartTime.Value)
            : Strings.Common_NotAvailable;
        HasServerData = true;

        StartUptimeUpdates();
    }

    public void ClearData()
    {
        StopUptimeUpdates();

        ServerType = string.Empty;
        InstanceId = string.Empty;
        AccessCode = string.Empty;
        ServerLocation = string.Empty;
        ServerUptime = string.Empty;
        HasServerData = false;
    }

    public void RefreshUptime()
    {
        if (_activityWatcher == null || !_activityWatcher.InGame) return;
        var data = _activityWatcher.Data;
        if (data.StartTime.HasValue)
        {
            ServerUptime = FormatUptime(DateTime.UtcNow - data.StartTime.Value);
        }
        else
        {
            ServerUptime = Strings.Common_NotAvailable;
        }
    }

    public async void RefreshLocation()
    {
        if (_activityWatcher == null || !_activityWatcher.InGame) return;
        var data = _activityWatcher.Data;
        if (!string.IsNullOrEmpty(data.Region))
        {
            ServerLocation = data.Region;
        }
        else if (data.MachineAddressValid)
        {
            string? location = await data.QueryServerLocation();
            if (!string.IsNullOrEmpty(location))
            {
                ServerLocation = location;
            }
            else
            {
                ServerLocation = Strings.Common_NotAvailable;
            }
        }
        else
        {
            ServerLocation = Strings.Common_NotAvailable;
        }
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    private async void CopyToClipboard(Visual? visual)
    {
        if (visual is null) return;
        var topLevel = TopLevel.GetTopLevel(visual);
        if (topLevel?.Clipboard is not null)
        {
            string textToCopy = AccessCodeVisibility ? AccessCode : InstanceId;
            await topLevel.Clipboard.SetTextAsync(textToCopy);
        }
    }

    private void StartUptimeUpdates()
    {
        if (!_isTimerRunning && HasServerData && _activityWatcher != null && _activityWatcher.InGame)
        {
            _uptimeTimer?.Start();
            _isTimerRunning = true;
        }
    }

    public void StopUptimeUpdates()
    {
        if (_isTimerRunning)
        {
            _uptimeTimer?.Stop();
            _isTimerRunning = false;
        }
    }
}