using Froststrap.Integrations;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Dialogs;

internal class WindowControlPermissionViewModel : NotifyPropertyChangedViewModel
{
    private readonly ActivityWatcher _activityWatcher;

    public List<ActivityData>? GameHistory { get; private set; }

    public GenericTriState LoadState { get; private set; } = GenericTriState.Unknown;

    public string Error { get; private set; } = String.Empty;

    public ICommand CloseWindowCommand => new RelayCommand(RequestClose);

    public bool BlacklistFromAsking { get; set; } = false;

    public EventHandler? RequestCloseEvent;

    public WindowControlPermissionViewModel(ActivityWatcher activityWatcher)
    {
        _activityWatcher = activityWatcher;

        LoadData();
    }

    private async void LoadData()
    {
        LoadState = GenericTriState.Unknown;
        OnPropertyChanged(nameof(LoadState));

        UniverseDetails? universe = UniverseDetails.LoadFromCache(_activityWatcher.Data.UniverseId);
        if (universe == null)
            await UniverseDetails.FetchSingle(_activityWatcher.Data.UniverseId);
        universe = UniverseDetails.LoadFromCache(_activityWatcher.Data.UniverseId);

        GameHistory = [new() { UniverseDetails = universe }];

        OnPropertyChanged(nameof(GameHistory));

        LoadState = GenericTriState.Successful;
        OnPropertyChanged(nameof(LoadState));
    }

    private void RequestClose() => RequestCloseEvent?.Invoke(this, EventArgs.Empty);
}