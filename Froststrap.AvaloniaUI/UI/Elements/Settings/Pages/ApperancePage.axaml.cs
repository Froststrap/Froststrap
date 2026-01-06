using System.Drawing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages;

public partial class ApperancePage : UserControl
{
    private readonly MainWindow _mainWindow;
    
    public ApperancePage()
    {
        DataContext = new AppearanceViewModel(this);
        InitializeComponent();
        App.FrostRPC?.SetPage("Appearance");
        
        // More deep issue that needs to be fixed regarding switching pages
        // TODO: Add proper page switching system
        _mainWindow = (MainWindow)Application.Current.MainWindow;
        ListBoxNavigationItems.ItemsSource = _mainWindow.NavigationItemsView;
        
        if (ListBoxNavigationItems.Items.Count > 0)
            ListBoxNavigationItems.SelectedIndex = 0;
        
        UpdateNavigationLockUI();
        ListBoxNavigationItems.SelectionChanged += ListBoxNavigationItems_SelectionChanged;
    }
    
    private bool _isWindowsBackdropInitialized = false;
    
    private void WindowsBackdropChangeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isWindowsBackdropInitialized)
        {
            _isWindowsBackdropInitialized = true;
            return;
        }

        if (e.AddedItems.Count == 0)
            return;

        var result = Frontend.ShowMessageBox(
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

            var startInfo = new ProcessStartInfo(Environment.ProcessPath!)
            {
                Arguments = "-menu"
            };

            Process.Start(startInfo);
            (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow.Close();
        }
    }
    
    public void CustomThemeSelection(object sender, SelectionChangedEventArgs e)
    {
        AppearanceViewModel viewModel = (AppearanceViewModel)DataContext;

        viewModel.SelectedCustomTheme = (string)((ListBox)sender).SelectedItem;
        viewModel.SelectedCustomThemeName = viewModel.SelectedCustomTheme;

        viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomTheme));
        viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomThemeName));
    }
    
    private void UpdateGradientTheme()
    {
        if (DataContext is AppearanceViewModel vm)
        {
            App.Settings.Prop.CustomGradientStops = vm.GradientStops.ToList();

            if (this.VisualRoot is MainWindow mainWindow)
            {
                mainWindow.ApplyTheme(); // wdym you cant find it????
            }
        }
    }
    
    private void OnAddGradientStop_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AppearanceViewModel vm) return;

        vm.GradientStops.Add(new GradientStops { Offset = 0.5, Color = "#000000" });
        UpdateGradientTheme();
    }
    
    private void OnRemoveGradientStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GradientStops stop } ||
            DataContext is not AppearanceViewModel vm) return;

        vm.GradientStops.Remove(stop);
        UpdateGradientTheme();
    }

    private async void OnChangeGradientColor_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Froststrap.Models.GradientStops stop } ||
            DataContext is not AppearanceViewModel vm)
            return;

        var colorPicker = new Avalonia.Controls.ColorPicker
        {
            Color = Avalonia.Media.Color.Parse(stop.Color),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };

        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };

        var panel = new StackPanel
        {
            Children = { colorPicker, okButton },
            Margin = new Avalonia.Thickness(10)
        };

        var pickerWindow = new Window
        {
            Title = "Select Color",
            Width = 300,
            Height = 400,
            Content = panel,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var tcs = new System.Threading.Tasks.TaskCompletionSource<Avalonia.Media.Color?>();

        okButton.Click += (_, _) =>
        {
            tcs.TrySetResult(colorPicker.Color);
            pickerWindow.Close();
        };

        var rootWindow = this.VisualRoot as Window;
        if (rootWindow is null) return;

        _ = pickerWindow.ShowDialog(rootWindow);
        var selectedColor = await tcs.Task;

        if (selectedColor is Avalonia.Media.Color c)
        {
            stop.Color = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            UpdateGradientTheme();
        }
    }
    
    private void OnSliderReleased(object sender, PointerReleasedEventArgs e)
    {
        UpdateGradientTheme();
    }
 }