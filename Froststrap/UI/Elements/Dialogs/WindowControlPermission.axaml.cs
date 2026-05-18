using Avalonia.Interactivity;
using Froststrap.Integrations;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.ViewModels.Dialogs;
using System.Collections.ObjectModel;

namespace Froststrap.UI.Elements.Dialogs;

/// <summary>
/// Interaction logic for WindowControlPermission.xaml
/// </summary>
public partial class WindowControlPermission : AvaloniaWindow
{
    public MessageBoxResult Result = MessageBoxResult.Cancel;

    public ActivityWatcher _activityWatcher = null!;

    private readonly WindowControlPermissionViewModel viewModel = null!;

    public WindowControlPermission()
    {
        InitializeComponent();
    }

    public WindowControlPermission(ActivityWatcher activityWatcher) : this()
    {
        _activityWatcher = activityWatcher;
        viewModel = new WindowControlPermissionViewModel(activityWatcher);

        viewModel.RequestCloseEvent += (_, _) => Close();

        DataContext = viewModel;
    }

    private void OKButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activityWatcher == null) return;

        Result = MessageBoxResult.OK;
        if (!WindowAllowedUniverses.Contains(_activityWatcher.Data.UniverseId))
        {
            WindowAllowedUniverses.Add(_activityWatcher.Data.UniverseId);
            App.Settings.Save();

            _activityWatcher.watcher.WindowController?.UpdateExposedPerms();
        }
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel == null || _activityWatcher == null) return;

        if (viewModel.BlacklistFromAsking)
        {
            if ((!WindowAllowedUniverses.Contains(_activityWatcher.Data.UniverseId)) && (!WindowBlacklistedUniverses.Contains(_activityWatcher.Data.UniverseId)))
            {
                WindowBlacklistedUniverses.Add(_activityWatcher.Data.UniverseId);
                App.Settings.Save();
            }
        }
        Close();
    }

    public static ObservableCollection<long> WindowAllowedUniverses
    {
        get => App.Settings.Prop.WindowAllowedUniverses;
        set => App.Settings.Prop.WindowAllowedUniverses = value;
    }

    public static ObservableCollection<long> WindowBlacklistedUniverses
    {
        get => App.Settings.Prop.WindowBlacklistedUniverses;
        set => App.Settings.Prop.WindowBlacklistedUniverses = value;
    }
}