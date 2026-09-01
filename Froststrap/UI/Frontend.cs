using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Froststrap.UI.Elements.Bootstrapper;
using Froststrap.UI.Elements.Dialogs;
using Froststrap.UI.Utility;

namespace Froststrap.UI
{
    internal static class Frontend
    {
        public static async Task<MessageBoxResult> ShowMessageBox(string message, MessageBoxImage icon = MessageBoxImage.Information, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            App.Logger.Info(message);

            if (App.LaunchSettings.QuietFlag.Active)
                return defaultResult;

            return await ShowFluentMessageBox(message, icon, buttons);
        }

        //Were supposed to show this when watcher fails to launch but we lowkey dont anymore idk why
        public static async Task ShowPlayerErrorDialog(bool crash = false)
        {
            if (App.LaunchSettings.QuietFlag.Active)
                return;

            string topLine = crash ? Strings.Dialog_PlayerError_Crash : Strings.Dialog_PlayerError_FailedLaunch;

            string info = string.Format(CultureInfo.InvariantCulture,
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
            Directory.CreateDirectory(Paths.CustomThemes);

            try
            {
                if (App.Settings.Prop.SelectedCustomTheme == null)
                    throw new InvalidOperationException("No custom theme selected");

                var dialog = new CustomDialog();
                dialog.ApplyCustomTheme(App.Settings.Prop.SelectedCustomTheme);
                return dialog;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception", ex);

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
                BootstrapperStyle.ModernDialog => new ModernDialog(),
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
    }
}