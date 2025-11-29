using Bloxstrap.UI.ViewModels.Settings;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class AppearancePage
    {
        private readonly MainWindow _mainWindow;

        public AppearancePage()
        {
            DataContext = new AppearanceViewModel(this);
            InitializeComponent();
            App.FrostRPC?.SetPage("Appearance");

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

        private void OnGradientSliderReleased(object sender, MouseButtonEventArgs e)
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
            UpdateGradientTheme();
        }

        private static bool IsValidHexColor(string color) => !string.IsNullOrWhiteSpace(color) && color.StartsWith("#") && color.Length >= 7;

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (ListBoxNavigationItems.SelectedItem is not Wpf.Ui.Controls.NavigationItem selectedItem)
                return;

            int result = _mainWindow.MoveNavigationItem(selectedItem, -1);
            if (result != -1)
            {
                ListBoxNavigationItems.SelectedItem = selectedItem;
                ListBoxNavigationItems.ScrollIntoView(selectedItem);
            }

            UpdateMoveButtons();
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (ListBoxNavigationItems.SelectedItem is not Wpf.Ui.Controls.NavigationItem selectedItem)
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

            int count = ListBoxNavigationItems.Items.Count;
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
            _mainWindow.ResetNavigationToDefault();

            ListBoxNavigationItems.ItemsSource = null;
            ListBoxNavigationItems.ItemsSource = _mainWindow.NavigationItemsView;

            if (ListBoxNavigationItems.Items.Count > 0)
            {
                ListBoxNavigationItems.SelectedIndex = 0;
                ListBoxNavigationItems.ScrollIntoView(ListBoxNavigationItems.SelectedItem);
            }

            UpdateMoveButtons();
        }

        private void ListBoxNavigationItems_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateMoveButtons();
        }
    }
}