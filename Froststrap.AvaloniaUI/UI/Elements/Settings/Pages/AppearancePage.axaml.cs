using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Froststrap.UI.ViewModels.Settings;
using FluentAvalonia.UI.Controls;

namespace Froststrap.UI.Elements.Settings.Pages;

public partial class AppearancePage : UserControl
{
    private MainWindow? _mainWindow;

    public AppearancePage()
    {
        DataContext = new AppearanceViewModel(this);
        InitializeComponent();

        App.FrostRPC?.SetPage("Appearance");

        this.AttachedToVisualTree += (s, e) =>
        {
            if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
            {
                _mainWindow = mainWindow;

                ListBoxNavigationItems.ItemsSource = _mainWindow.NavigationItemsView;

                if (ListBoxNavigationItems.ItemCount > 0)
                    ListBoxNavigationItems.SelectedIndex = 0;
            }

            UpdateNavigationLockUI();
        };

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
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
    }

    public void CustomThemeSelection(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is AppearanceViewModel viewModel)
        {
            var selectedItem = ((ListBox)sender).SelectedItem as string;
            if (selectedItem != null)
            {
                viewModel.SelectedCustomTheme = selectedItem;
                viewModel.SelectedCustomThemeName = viewModel.SelectedCustomTheme;

                viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomTheme));
                viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomThemeName));
            }
        }
    }

    private void UpdateGradientTheme()
    {
        if (DataContext is AppearanceViewModel vm)
        {
            App.Settings.Prop.CustomGradientStops = vm.GradientStops.ToList();

            if (this.VisualRoot is MainWindow mainWindow)
            {
                mainWindow.ApplyTheme();
            }
        }
    }

    private void OnAddGradientStop_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AppearanceViewModel vm) return;

        vm.GradientStops.Add(new Froststrap.Models.GradientStops { Offset = 0.5, Color = "#000000" });
        UpdateGradientTheme();
    }

    private void OnRemoveGradientStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Froststrap.Models.GradientStops stop } ||
            DataContext is not AppearanceViewModel vm) return;

        vm.GradientStops.Remove(stop);
        UpdateGradientTheme();
    }

    private async void OnChangeGradientColor_Click(object? sender, RoutedEventArgs e)
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

    private void OnGradientColorHexChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: Froststrap.Models.GradientStops stop } ||
            DataContext is not AppearanceViewModel vm)
            return;

        if (IsValidHexColor(stop.Color))
            UpdateGradientTheme();
    }

    private async void OnExportGradient_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppearanceViewModel vm)
            return;

        if (this.VisualRoot is not Window window)
            return;

        var file = await window.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export Gradient Background",
                SuggestedFileName = "Froststrap Gradient Background",
                DefaultExtension = "json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("JSON Files")
                    {
                        Patterns = new[] { "*.json" }
                    },
                    new FilePickerFileType("Text Files")
                    {
                        Patterns = new[] { "*.txt" }
                    }
                }
            });

        if (file == null)
            return;

        try
        {
            var gradientData = new
            {
                GradientStops = vm.GradientStops
                    .Select(gs => new { gs.Offset, gs.Color })
                    .ToList(),
                GradientAngle = vm.GradientAngle,
                Version = App.Version
            };

            var json = JsonSerializer.Serialize(
                gradientData,
                new JsonSerializerOptions { WriteIndented = true });

            var filePath = file.Path.LocalPath;
            if (!string.IsNullOrEmpty(filePath))
            {
                await File.WriteAllTextAsync(filePath, json);
            }
            else
            {
                throw new InvalidOperationException("Could not get local file path");
            }

            Frontend.ShowMessageBox(
                "Gradient exported successfully!",
                MessageBoxImage.Information,
                MessageBoxButton.OK);
        }
        catch (Exception ex)
        {
            Frontend.ShowMessageBox(
                $"Failed to export gradient: {ex.Message}",
                MessageBoxImage.Error,
                MessageBoxButton.OK);
        }
    }

    private async void OnImportGradient_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppearanceViewModel vm)
            return;

        if (this.VisualRoot is not Window window)
            return;

        var files = await window.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import Gradient Background",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("JSON Files")
                    {
                        Patterns = new[] { "*.json" }
                    },
                    new FilePickerFileType("Text Files")
                    {
                        Patterns = new[] { "*.txt" }
                    }
                }
            });

        var file = files.FirstOrDefault();
        if (file == null)
            return;

        try
        {
            var json = await File.ReadAllTextAsync(file.Path.LocalPath);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("GradientStops", out var stopsElement) ||
                stopsElement.ValueKind != JsonValueKind.Array ||
                stopsElement.GetArrayLength() == 0)
            {
                throw new InvalidDataException("Invalid gradient file format.");
            }

            var gradientStops = new List<Froststrap.Models.GradientStops>();

            foreach (var stopElement in stopsElement.EnumerateArray())
            {
                if (!stopElement.TryGetProperty("Offset", out var offsetElement) ||
                    !stopElement.TryGetProperty("Color", out var colorElement) ||
                    offsetElement.ValueKind != JsonValueKind.Number ||
                    colorElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("Invalid gradient stop format.");
                }

                var offset = offsetElement.GetDouble();
                var color = colorElement.GetString()!;

                if (offset < 0 || offset > 1 || !IsValidHexColor(color))
                {
                    throw new InvalidDataException("Invalid gradient stop data.");
                }

                gradientStops.Add(new Froststrap.Models.GradientStops()
                {
                    Offset = offset,
                    Color = color
                });
            }

            var gradientAngle = vm.GradientAngle;

            if (root.TryGetProperty("GradientAngle", out var angleElement) &&
                angleElement.ValueKind == JsonValueKind.Number)
            {
                var angle = angleElement.GetDouble();
                if (angle is >= 0 and <= 360)
                    gradientAngle = angle;
            }

            vm.GradientStops.Clear();
            foreach (var stop in gradientStops)
                vm.GradientStops.Add(stop);

            vm.GradientAngle = gradientAngle;

            App.Settings.Prop.CustomGradientStops = vm.GradientStops.ToList();
            App.Settings.Prop.GradientAngle = gradientAngle;

            UpdateGradientTheme();

            Frontend.ShowMessageBox(
                "Gradient imported successfully!",
                MessageBoxImage.Information,
                MessageBoxButton.OK);
        }
        catch (Exception ex)
        {
            Frontend.ShowMessageBox(
                $"Failed to import gradient: {ex.Message}",
                MessageBoxImage.Error,
                MessageBoxButton.OK);
        }
    }

    private void OnSelectBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AppearanceViewModel vm) return;
        vm.SelectBackgroundImage();
    }

    private void OnClearBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AppearanceViewModel vm) return;
        vm.ClearBackgroundImage();
    }

    private void OnResetGradient_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AppearanceViewModel vm) return;

        vm.ResetGradientStops();
        vm.GradientAngle = 0;
        UpdateGradientTheme();
    }

    private static bool IsValidHexColor(string color) => !string.IsNullOrWhiteSpace(color) && color.StartsWith("#") && color.Length >= 7;

    private void MoveUp_Click(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow == null || ListBoxNavigationItems.SelectedItem is not NavigationViewItem selectedItem)
            return;

        var result = _mainWindow.MoveNavigationItem(selectedItem, -1);
        if (result != -1)
        {
            ListBoxNavigationItems.SelectedItem = selectedItem;
            ListBoxNavigationItems.ScrollIntoView(selectedItem);
        }

        UpdateMoveButtons();
    }

    private void MoveDown_Click(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow == null || ListBoxNavigationItems.SelectedItem is not NavigationViewItem selectedItem)
            return;

        int result = _mainWindow.MoveNavigationItem(selectedItem, +1);
        if (result != -1)
        {
            ListBoxNavigationItems.SelectedItem = selectedItem;
            ListBoxNavigationItems.ScrollIntoView(selectedItem);
        }

        UpdateMoveButtons();
    }

    private void UpdateNavigationLockUI()
    {
        bool isLocked = App.Settings.Prop.IsNavigationOrderLocked;

        ResetToDefaultButton.IsEnabled = !isLocked;
        UpdateMoveButtons();

        ToggleLockOrder.IsChecked = isLocked;
    }

    private void UpdateMoveButtons()
    {
        MoveUpButton.IsEnabled = false;
        MoveDownButton.IsEnabled = false;

        int idx = ListBoxNavigationItems.SelectedIndex;
        if (idx < 0) return;

        if (App.Settings.Prop.IsNavigationOrderLocked)
            return;

        int count = ListBoxNavigationItems.ItemCount;
        MoveUpButton.IsEnabled = idx > 0;
        MoveDownButton.IsEnabled = idx < (count - 1);
    }

    private void LockToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        SetNavigationLock(true);
    }

    private void LockToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        SetNavigationLock(false);
    }

    private void SetNavigationLock(bool isLocked)
    {
        if (App.Settings.Prop.IsNavigationOrderLocked == isLocked)
            return;

        App.Settings.Prop.IsNavigationOrderLocked = isLocked;
        App.State.Save();

        UpdateNavigationLockUI();
    }

    private void ResetOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_mainWindow == null) return;

        _mainWindow.ResetNavigationToDefault();

        ListBoxNavigationItems.ItemsSource = null;
        ListBoxNavigationItems.ItemsSource = _mainWindow.NavigationItemsView;

        if (ListBoxNavigationItems.ItemCount > 0)
        {
            ListBoxNavigationItems.SelectedIndex = 0;
        }
    }

    private void ListBoxNavigationItems_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateMoveButtons();
    }
}