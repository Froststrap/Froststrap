using Bloxstrap.UI.ViewModels.Settings;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class AppearancePage
    {
        public AppearancePage()
        {
            DataContext = new AppearanceViewModel(this);
            InitializeComponent();
            App.FrostRPC?.SetPage("Appearance");
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
                if (Window.GetWindow(this) is MainWindow mainWindow &&
                    mainWindow.DataContext is MainWindowViewModel mainWindowViewModel)
                {
                    mainWindowViewModel.SaveSettings();
                }

                var startInfo = new ProcessStartInfo(Environment.ProcessPath!)
                {
                    Arguments = "-menu"
                };

                Process.Start(startInfo);
                Application.Current.Shutdown();
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
                ((MainWindow)Window.GetWindow(this)!).ApplyTheme();
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

        private void OnChangeGradientColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: GradientStops stop } ||
                DataContext is not AppearanceViewModel vm) return;

            var dialog = new System.Windows.Forms.ColorDialog();
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var color = dialog.Color;
            stop.Color = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            UpdateGradientTheme();
        }

        private void OnSliderReleased(object sender, MouseButtonEventArgs e)
        {
            UpdateGradientTheme();
        }

        private void OnGradientColorHexChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox { DataContext: GradientStops stop } ||
                DataContext is not AppearanceViewModel vm) return;

            if (IsValidHexColor(stop.Color))
                UpdateGradientTheme();
        }

        private void OnResetGradient_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AppearanceViewModel vm) return;

            vm.ResetGradientStops();
            vm.GradientAngle = 0;
            UpdateGradientTheme();
        }

        private void OnExportGradient_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AppearanceViewModel vm) return;

            var saveDialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|Text Files (*.txt)|*.txt",
                DefaultExt = ".json",
                FileName = "Froststrap Gradient Background"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var gradientData = new
                    {
                        GradientStops = vm.GradientStops.Select(gs => new { gs.Offset, gs.Color }).ToList(),
                        GradientAngle = vm.GradientAngle,
                        Version = App.Version
                    };

                    string json = JsonSerializer.Serialize(gradientData, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    File.WriteAllText(saveDialog.FileName, json);

                    Frontend.ShowMessageBox(
                        "Gradient exported successfully!",
                        MessageBoxImage.Information,
                        MessageBoxButton.OK
                    );
                }
                catch (Exception ex)
                {
                    Frontend.ShowMessageBox(
                        $"Failed to export gradient: {ex.Message}",
                        MessageBoxImage.Error,
                        MessageBoxButton.OK
                    );
                }
            }
        }

        private void OnImportGradient_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AppearanceViewModel vm) return;

            var openDialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|Text Files (*.txt)|*.txt",
                Multiselect = false
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(openDialog.FileName);
                    using var document = JsonDocument.Parse(json);
                    var root = document.RootElement;

                    if (!root.TryGetProperty("GradientStops", out var stopsElement) ||
                        stopsElement.GetArrayLength() == 0)
                    {
                        throw new InvalidDataException("Invalid gradient file format.");
                    }

                    var gradientStops = new List<GradientStops>();
                    foreach (var stopElement in stopsElement.EnumerateArray())
                    {
                        if (stopElement.TryGetProperty("Offset", out var offsetElement) &&
                            stopElement.TryGetProperty("Color", out var colorElement) &&
                            offsetElement.ValueKind == JsonValueKind.Number &&
                            colorElement.ValueKind == JsonValueKind.String)
                        {
                            var offset = offsetElement.GetDouble();
                            var color = colorElement.GetString()!;

                            if (offset < 0 || offset > 1 || !IsValidHexColor(color))
                            {
                                throw new InvalidDataException("Invalid gradient stop data.");
                            }

                            gradientStops.Add(new GradientStops { Offset = offset, Color = color });
                        }
                        else
                        {
                            throw new InvalidDataException("Invalid gradient stop format.");
                        }
                    }

                    double gradientAngle = vm.GradientAngle;
                    if (root.TryGetProperty("GradientAngle", out var angleElement) &&
                        angleElement.ValueKind == JsonValueKind.Number)
                    {
                        var angle = angleElement.GetDouble();
                        if (angle >= 0 && angle <= 360)
                        {
                            gradientAngle = angle;
                        }
                    }

                    vm.GradientStops.Clear();
                    foreach (var stop in gradientStops)
                    {
                        vm.GradientStops.Add(stop);
                    }

                    vm.GradientAngle = gradientAngle;
                    App.Settings.Prop.CustomGradientStops = vm.GradientStops.ToList();
                    App.Settings.Prop.GradientAngle = gradientAngle;

                    UpdateGradientTheme();

                    Frontend.ShowMessageBox(
                        "Gradient imported successfully!",
                        MessageBoxImage.Information,
                        MessageBoxButton.OK
                    );
                }
                catch (Exception ex)
                {
                    Frontend.ShowMessageBox(
                        $"Failed to import gradient: {ex.Message}",
                        MessageBoxImage.Error,
                        MessageBoxButton.OK
                    );
                }
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

        private static bool IsValidHexColor(string color) => !string.IsNullOrWhiteSpace(color) && color.StartsWith("#") && color.Length >= 7;
    }
}