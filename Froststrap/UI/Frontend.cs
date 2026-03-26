using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Froststrap.UI.Elements.Bootstrapper;
using Froststrap.UI.Elements.Dialogs;

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
            switch (style)
            {
                case BootstrapperStyle.ClassicFluentDialog:
                    return new ClassicFluentDialog();

                case BootstrapperStyle.ByfronDialog:
                    return new ByfronDialog();

                case BootstrapperStyle.TwentyFiveDialog:
                    return new TwentyFiveDialog();

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

        public static void ShowBalloonTip(string title, string message, NotificationType type = NotificationType.Information, int timeoutSeconds = 5, Action? onClick = null)
        {
            string imagePath = type switch
            {
                NotificationType.Warning => "avares://Froststrap/Froststrap/Resources/MessageBox/FullQuality/Warning.png",
                NotificationType.Error => "avares://Froststrap/Froststrap/Resources/MessageBox/FullQuality/Error.png",
                _ => "avares://Froststrap/Froststrap/Resources/MessageBox/FullQuality/Information.png"
            };

            Dispatcher.UIThread.Post(() =>
            {
                var notification = new NotificationDialog(title, message, imagePath, timeoutSeconds * 1000);
                notification.Show();
            });
        }
    }
}