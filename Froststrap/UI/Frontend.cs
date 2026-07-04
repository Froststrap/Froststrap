using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Labs.Notifications;
using Avalonia.Threading;
using Froststrap.UI.Elements.Bootstrapper;
using Froststrap.UI.Elements.Dialogs;
using Froststrap.UI.Utility;

namespace Froststrap.UI
{
    public static class Frontend
    {
        public static async Task<MessageBoxResult> ShowMessageBox(string message, MessageBoxImage icon = MessageBoxImage.Information, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxResult defaultResult = MessageBoxResult.None)
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

            string topLine = crash ? Strings.Dialog_PlayerError_Crash : Strings.Dialog_PlayerError_FailedLaunch;

            string info = string.Format(
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

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new ExceptionDialog(exception);

                Window? owner = null;
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
                }

                if (owner != null && owner.IsVisible)
                {
                    await dialog.ShowDialog(owner);
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    dialog.Closed += (s, e) => tcs.TrySetResult(true);
                    dialog.Show();
                    await tcs.Task;
                }
            });
        }

        public static async Task ShowConnectivityDialog(string title, string description, MessageBoxImage image, Exception exception)
        {
            if (App.LaunchSettings.QuietFlag.Active)
                return;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new ConnectivityDialog(title, description, image, exception);

                Window? owner = null;
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
                }

                if (owner != null && owner.IsVisible)
                {
                    await dialog.ShowDialog(owner);
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    dialog.Closed += (s, e) => tcs.TrySetResult(true);
                    dialog.Show();
                    await tcs.Task;
                }
            });
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
            return style switch
            {
                BootstrapperStyle.ClassicFluentDialog => new ClassicFluentDialog(),
                BootstrapperStyle.ByfronDialog => new ByfronDialog(),
                BootstrapperStyle.TwentyFiveDialog => new TwentyFiveDialog(),
                BootstrapperStyle.FluentDialog => new FluentDialog(false),
                BootstrapperStyle.FluentAeroDialog => new FluentDialog(true),
                BootstrapperStyle.CustomDialog => await GetCustomBootstrapper(),
                _ => new FluentDialog(false)
            };
        }

        private static async Task<MessageBoxResult> ShowFluentMessageBox(string message, MessageBoxImage icon, MessageBoxButton buttons)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var messagebox = new FluentMessageBox(message, icon, buttons);

                Window? owner = null;
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
                }

                if (owner != null)
                {
                    await messagebox.ShowDialog(owner);
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    messagebox.Closed += (s, e) => tcs.TrySetResult(true);
                    messagebox.Show();
                    await tcs.Task;
                }

                return messagebox.Result;
            });
        }

        public static void ShowBalloonTip(string title, string message, NotificationType category = NotificationType.Information, int timeoutSeconds = 5)
        {
            var manager = NativeNotificationManager.Current;
            if (manager == null || OperatingSystem.IsMacOS()) return;

            string categoryString = category switch
            {
                NotificationType.Success => "success",
                NotificationType.Warning => "warning",
                NotificationType.Error => "error",
                _ => "info"
            };

            var notification = manager.CreateNotification(categoryString);
            if (notification == null) return;

            notification.Title = title;
            notification.Message = message;
            notification.Expiration = TimeSpan.FromSeconds(timeoutSeconds);

            NotificationTracker.Track(notification, TimeSpan.FromSeconds(timeoutSeconds));

            Dispatcher.UIThread.Post(() =>
            {
                notification.Show();
            }, DispatcherPriority.ApplicationIdle);
        }
    }
}