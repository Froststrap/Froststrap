using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages;

public partial class AppearancePage : UserControl
{
    public AppearancePage()
    {
        InitializeComponent();

        App.FrostRPC?.SetPage("Appearance");
    }

    private bool _isWindowsBackdropInitialized = false;

    private async void WindowsBackdropChangeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isWindowsBackdropInitialized)
        {
            _isWindowsBackdropInitialized = true;
            return;
        }

        if (e.AddedItems.Count == 0)
            return;

        var result = await Frontend.ShowMessageBox(
            "You need to restart the app for the changes to apply. Do you want to restart now?",
            MessageBoxImage.Information,
            MessageBoxButton.YesNo
        );

        if (result == MessageBoxResult.Yes)
        {
            if (this.VisualRoot is MainWindow mainWindow &&
                mainWindow.DataContext is MainWindowViewModel mainWindowViewModel)
            {
                mainWindowViewModel.SaveSettings();
            }

            Process.Start(Paths.Process, "-menu");
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
    }
}
