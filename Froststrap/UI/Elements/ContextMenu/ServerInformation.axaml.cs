using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.ContextMenu;

namespace Froststrap.UI.Elements.ContextMenu;

internal partial class ServerInformation : Base.AvaloniaWindow
{
    private readonly Watcher? _watcher;
    private readonly ServerInformationViewModel _viewModel;

    public ServerInformation()
    {
        InitializeComponent();
        _viewModel = new ServerInformationViewModel();
        DataContext = _viewModel;
    }

    public ServerInformation(Watcher watcher) : this()
    {
        _watcher = watcher;
        if (_watcher?.ActivityWatcher is ActivityWatcher aw)
        {
            _viewModel.SetWatcher(aw);
            aw.OnGameJoin += OnGameJoin;
            aw.OnGameLeave += OnGameLeave;
            aw.ShowNotif += OnShowNotif;

            if (aw.InGame)
            {
                UpdateViewModel(aw);
                _viewModel.RefreshLocation();
                _viewModel.RefreshUptime();
            }
        }
    }

    private void OnGameJoin(object? sender, EventArgs e)
    {
        if (sender is ActivityWatcher aw)
            Dispatcher.UIThread.Invoke(() =>
            {
                UpdateViewModel(aw);
                _viewModel.RefreshLocation();
                _viewModel.RefreshUptime();
            });
    }

    private void OnGameLeave(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(() => _viewModel.ClearData());
    }

    private void OnShowNotif(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            _viewModel.RefreshUptime();
            _viewModel.RefreshLocation();
        });
    }

    private void UpdateViewModel(ActivityWatcher aw)
    {
        _viewModel.UpdateData(aw.Data);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_watcher?.ActivityWatcher is ActivityWatcher aw)
        {
            aw.OnGameJoin -= OnGameJoin;
            aw.OnGameLeave -= OnGameLeave;
            aw.ShowNotif -= OnShowNotif;
        }

        _viewModel.StopUptimeUpdates();
        base.OnClosed(e);
    }
}