using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using Froststrap.UI.Elements.Bootstrapper;
using Froststrap.UI.Elements.Dialogs;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

namespace Froststrap.UI
{
    public static class Frontend
    {
        private static WindowNotificationManager? _notificationManager;

        public static async Task<MessageBoxResult> ShowMessageBox(
                string message,
                MessageBoxImage icon = MessageBoxImage.None,
                MessageBoxButton buttons = MessageBoxButton.OK,
                MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            App.Logger.WriteLine("Frontend::ShowMessageBox", message);

            if (App.LaunchSettings.QuietFlag.Active)
                return defaultResult;

            return await ShowFluentMessageBox(message, icon, buttons);
        }

        public static async Task ShowPlayerErrorDialog(bool crash = false)
        {
            if (App.LaunchSettings.QuietFlag.Active)
                return;

            string topLine = Strings.Dialog_PlayerError_FailedLaunch;

            if (crash)
                topLine = Strings.Dialog_PlayerError_Crash;

            string info = String.Format(
                Strings.Dialog_PlayerError_HelpInformation,
                $"https://github.com/{App.ProjectRepository}/wiki/Roblox-crashes-or-does-not-launch",
                $"https://github.com/{App.ProjectRepository}/wiki/Switching-between-Roblox-and-Bloxstrap"
            );

            await ShowMessageBox($"{topLine}\n\n{info}", MessageBoxImage.Error);
        }

        public static async Task ShowExceptionDialog(Exception exception)
        {
            if (App.LaunchSettings.QuietFlag.Active)
                return;

            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (mainWindow != null)
            {
                var dialog = new ExceptionDialog(exception);
                await dialog.ShowDialog(mainWindow);
            }
        }

        public static async Task ShowConnectivityDialog(string title, string description, MessageBoxImage image, Exception exception)
        {
            if (App.LaunchSettings.QuietFlag.Active)
                return;

            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (mainWindow != null)
            {
                var dialog = new ConnectivityDialog(title, description, image, exception);
                await dialog.ShowDialog(mainWindow);
            }
        }

        private static async Task<IBootstrapperDialog> GetCustomBootstrapper()
        {
            const string LOG_IDENT = "Frontend::GetCustomBootstrapper";

            Directory.CreateDirectory(Paths.CustomThemes);

            try
            {
                if (App.Settings.Prop.SelectedCustomTheme == null)
                    throw new Exception("No custom theme selected");

                var dialog = new CustomDialog();
                dialog.ApplyCustomTheme(App.Settings.Prop.SelectedCustomTheme);
                return dialog;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);

                if (!App.LaunchSettings.QuietFlag.Active)
                    await ShowMessageBox($"Failed to setup custom bootstrapper: {ex.Message}.\nDefaulting to Fluent.", MessageBoxImage.Error);

                return await GetBootstrapperDialog(BootstrapperStyle.FluentDialog);
            }
        }

        public static async Task<IBootstrapperDialog> GetBootstrapperDialog(BootstrapperStyle style)
        {
            switch (style)
            {
                //case BootstrapperStyle.VistaDialog:
                //return new VistaDialog();

                //case BootstrapperStyle.LegacyDialog2008:
                //return new LegacyDialog2008();

                //case BootstrapperStyle.LegacyDialog2011:
                //return new LegacyDialog2011();

                //case BootstrapperStyle.ProgressDialog:
                //return new ProgressDialog();

                case BootstrapperStyle.ClassicFluentDialog:
                    return new ClassicFluentDialog();

                case BootstrapperStyle.ByfronDialog:
                    return new ByfronDialog();

                case BootstrapperStyle.FluentDialog:
                    return new FluentDialog(false);

                case BootstrapperStyle.FluentAeroDialog:
                    return new FluentDialog(true);

                case BootstrapperStyle.CustomDialog:
                    return await GetCustomBootstrapper();

                default:
                    return new FluentDialog(false);
            }
        }

        private static async Task<MessageBoxResult> ShowFluentMessageBox(
                string message,
                MessageBoxImage icon,
                MessageBoxButton buttons)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                {
                    ContentMessage = message,
                    ContentTitle = "Notification",
                    ButtonDefinitions = (ButtonEnum)buttons,
                    Icon = (Icon)icon,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                });

                var result = await box.ShowAsync();
                return (MessageBoxResult)result;
            });
        }
        public static void ShowBalloonTip(string title, string message, NotificationType type = NotificationType.Information, int timeoutSeconds = 5)
        {
            if (_notificationManager == null)
            {
                var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                if (mainWindow != null)
                {
                    _notificationManager = new WindowNotificationManager(mainWindow)
                    {
                        Position = NotificationPosition.BottomRight,
                        MaxItems = 3
                    };
                }
            }

            _notificationManager?.Show(new Notification(
                title,
                message,
                type,
                TimeSpan.FromSeconds(timeoutSeconds)));
        }
    }
}