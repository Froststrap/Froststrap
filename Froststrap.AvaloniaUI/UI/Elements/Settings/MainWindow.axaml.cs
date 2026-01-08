using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using Froststrap.UI.ViewModels.Settings;
using Froststrap.UI.Elements.Settings.Pages;

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
        
        
    }
}