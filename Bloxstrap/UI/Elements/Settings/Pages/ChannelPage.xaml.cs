using Bloxstrap.UI.Elements.ContextMenu;
using Bloxstrap.UI.Elements.Dialogs;
using Bloxstrap.UI.ViewModels.Settings;
using Microsoft.Win32;
using System.Windows;
using Wpf.Ui.Hardware;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for ChannelPage.xaml
    /// </summary>
    public partial class ChannelPage
    {
        public ChannelPage()
        {
            DataContext = new ChannelViewModel();
            InitializeComponent();
            App.FrostRPC?.SetPage("Settings");
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            var confirm = Frontend.ShowMessageBox(
                "Are you sure you want to reset all settings to their default values?",
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (confirm != MessageBoxResult.Yes)
                return;

            App.Settings.Prop = new Models.Persistable.Settings();
            App.Settings.Save();

            Frontend.ShowMessageBox("Settings have been reset. Restarting the app...", MessageBoxImage.Information);

            System.Windows.Forms.Application.Restart();
            Application.Current.Shutdown();
        }

        private void ExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = "FroststrapSettings.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string source = Path.Combine(Paths.Base, "Settings.json");
                if (!File.Exists(source))
                {
                    Frontend.ShowMessageBox("No settings file found to export.", MessageBoxImage.Warning);
                    return;
                }

                File.Copy(source, dialog.FileName, overwrite: true);
                Frontend.ShowMessageBox($"Settings exported successfully to:\n{dialog.FileName}", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to export settings: {ex.Message}", MessageBoxImage.Error);
            }
        }

        private void ImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string target = Path.Combine(Paths.Base, "Settings.json");

                File.Copy(dialog.FileName, target, overwrite: true);

                Frontend.ShowMessageBox("Settings imported successfully. Restarting the app...", MessageBoxImage.Information);
                System.Windows.Forms.Application.Restart();
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to import settings: {ex.Message}", MessageBoxImage.Error);
            }
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            LaunchHandler.LaunchUninstaller();
        }

        private void OpenChannelListDialog_Click(object sender, RoutedEventArgs e)
        {
            App.FrostRPC?.SetDialog("Channel List");

            var dialog = new ChannelListsDialog();
            dialog.Owner = Window.GetWindow(this);

            dialog.ShowDialog();

            App.FrostRPC?.ClearDialog();
        }
    }
}