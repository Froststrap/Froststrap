using Froststrap;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings;

public partial class MainWindow : Window
{
    private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;

    public static ObservableCollection<NavigationViewItem> MainNavigationItems { get; } = new ObservableCollection<NavigationViewItem>();
    public static ObservableCollection<NavigationViewItem> FooterNavigationItems { get; } = new ObservableCollection<NavigationViewItem>();
    public ObservableCollection<NavigationViewItem> NavigationItemsView { get; } = new ObservableCollection<NavigationViewItem>();

    public static List<string> DefaultNavigationOrder { get; private set; } = new();
    public static List<string> DefaultFooterOrder { get; private set; } = new();
    
    public MainWindow()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.RequestSaveNoticeEvent += (_, _) => SettingsSavedTip.IsOpen = true;
        viewModel.RequestCloseWindowEvent += (_, _) => Close();

        DataContext = viewModel;
        InitializeComponent();
        
        App.Logger.WriteLine("MainWindow", "Initializing settings window");
        
        if (showAlreadyRunningWarning)
            ShowAlreadyRunningTooltip();
        
        gbs.Opacity =  viewModel.GBSEnabled ? 1 : 0.5;
        gbs.IsEnabled = viewModel.GBSEnabled; 
        
        LoadState();
        
        string? lastPageName = App.State.Prop.LastPage;
        Type? lastPage = lastPageName is null ? null : Type.GetType(lastPageName);

        App.RemoteData.Subscribe((object? sender, EventArgs e) => {
            RemoteDataBase Data = App.RemoteData.Prop;

            AlertBar.Visibility = Data.AlertEnabled ? Visibility.Visible : Visibility.Collapsed;
            AlertBar.Message = Data.AlertContent;
            AlertBar.Severity = Data.AlertSeverity;
        });
    }
}