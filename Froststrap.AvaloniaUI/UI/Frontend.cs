using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using Froststrap.UI.Elements.Bootstrapper;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Dto;
using Froststrap.UI.Elements.Dialogs;

namespace Froststrap.UI
{
    public static class Frontend
    {
        public static MessageBoxResult ShowMessageBox(string message, MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            App.Logger.WriteLine("Frontend::ShowMessageBox", message);

            if (App.LaunchSettings.QuietFlag.Active)
                return defaultResult;

            return ShowFluentMessageBox(message, icon, buttons);
        }

        public static void ShowPlayerErrorDialog(bool crash = false)
        {
            if (App.LaunchSettings.QuietFlag.Active)
                return;

            string topLine = Strings.Dialog_PlayerError_FailedLaunch;

            if (crash)
                topLine = Strings.Dialog_PlayerError_Crash;

            string info = string.Format(
                Strings.Dialog_PlayerError_HelpInformation,
                $"https://github.com/{App.ProjectRepository}/wiki/Roblox-crashes-or-does-not-launch",
                $"https://github.com/{App.ProjectRepository}/wiki/Switching-between-Roblox-and-Froststrap"
            );

            ShowMessageBox($"{topLine}\n\n{info}", MessageBoxImage.Error);
        }

        public static void ShowExceptionDialog(Exception exception)
        {
            if (App.LaunchSettings.QuietFlag.Active)
                return;

            Dispatcher.UIThread.Invoke(() =>
            {
                var dialog = new ExceptionDialog(exception);
                dialog.ShowDialog(GetCurrentWindow());
            });
        }

        public static void ShowConnectivityDialog(string title, string description, MessageBoxImage image, Exception exception)
        {
            if (App.LaunchSettings.QuietFlag.Active)
                return;

            Dispatcher.UIThread.Invoke(() =>
            {
                var dialog = new ConnectivityDialog(title, description, image, exception);
                dialog.ShowDialog(GetCurrentWindow());
            });
        }

        private static IBootstrapperDialog GetCustomBootstrapper()
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
                    Frontend.ShowMessageBox($"Failed to setup custom bootstrapper: {ex.Message}.\nDefaulting to Fluent.", MessageBoxImage.Error);

                return GetBootstrapperDialog(BootstrapperStyle.FluentDialog);
            }
        }

        public static IBootstrapperDialog GetBootstrapperDialog(BootstrapperStyle style)
        {
            return style switch
            {
                BootstrapperStyle.VistaDialog => new VistaDialog(),
                BootstrapperStyle.LegacyDialog2008 => new LegacyDialog2008(),
                BootstrapperStyle.LegacyDialog2011 => new LegacyDialog2011(),
                BootstrapperStyle.ProgressDialog => new ProgressDialog(),
                BootstrapperStyle.ClassicFluentDialog => new ClassicFluentDialog(),
                BootstrapperStyle.ByfronDialog => new ByfronDialog(),
                BootstrapperStyle.FroststrapDialog => new FroststrapDialog(),
                BootstrapperStyle.FluentDialog => new FluentDialog(false),
                BootstrapperStyle.FluentAeroDialog => new FluentDialog(true),
                BootstrapperStyle.CustomDialog => GetCustomBootstrapper(),
                _ => new FluentDialog(false)
            };
        }

        private static MessageBoxResult ShowFluentMessageBox(string message, MessageBoxImage icon, MessageBoxButton buttons)
        {
            var iconEnum = icon switch
            {
                MessageBoxImage.Error => Icon.Error,
                MessageBoxImage.Warning => Icon.Warning,
                MessageBoxImage.Information => Icon.Info,
                MessageBoxImage.Question => Icon.Question,
                MessageBoxImage.None => Icon.None,
                _ => Icon.None
            };

            var buttonEnum = buttons switch
            {
                MessageBoxButton.OK => ButtonEnum.Ok,
                MessageBoxButton.OKCancel => ButtonEnum.OkCancel,
                MessageBoxButton.YesNo => ButtonEnum.YesNo,
                MessageBoxButton.YesNoCancel => ButtonEnum.YesNoCancel,
                _ => ButtonEnum.Ok
            };

            var messageBox = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ButtonDefinitions = buttonEnum,
                ContentTitle = App.ProjectName,
                ContentMessage = message,
                Icon = iconEnum,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInCenter = true,
                SizeToContent = SizeToContent.WidthAndHeight,
                MaxWidth = 600,
                MaxHeight = 400
            });

            var result = messageBox.ShowWindowDialogAsync(GetCurrentWindow()).GetAwaiter().GetResult();

            return result switch
            {
                ButtonResult.Ok => MessageBoxResult.OK,
                ButtonResult.Yes => MessageBoxResult.Yes,
                ButtonResult.No => MessageBoxResult.No,
                ButtonResult.Cancel => MessageBoxResult.Cancel,
                ButtonResult.Abort => MessageBoxResult.Cancel,
                _ => MessageBoxResult.None
            };
        }

        private static Window GetCurrentWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                foreach (var window in desktopLifetime.Windows)
                {
                    if (window.IsActive)
                        return window;
                }

                if (desktopLifetime.MainWindow is not null)
                    return desktopLifetime.MainWindow;

                throw new InvalidOperationException("No active or main window found");
            }

            throw new InvalidOperationException("No active window found");
        }

        public static void ShowBalloonTip(string title, string message, object? icon = null, int timeout = 5)
        {
            // TODO: Implement tray icon support for Avalonia

            Dispatcher.UIThread.Invoke(() =>
            {
                ShowNotificationWindow(title, message, timeout);
            });
        }

        private static void ShowNotificationWindow(string title, string message, int timeoutSeconds)
        {
            // Create a simple notification window
            var notification = new Window
            {
                Title = title,
                Width = 300,
                Height = 100,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false,
                CanResize = false,
                Background = new SolidColorBrush(Colors.DarkSlateGray),
                Foreground = new SolidColorBrush(Colors.White),
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true
            };

            var screen = notification.Screens?.Primary;
            if (screen != null)
            {
                notification.Position = new PixelPoint(
                    (int)(screen.WorkingArea.Right - 320),
                    (int)(screen.WorkingArea.Bottom - 120)
                );
            }

            var content = new StackPanel
            {
                Margin = new Thickness(10),
                Spacing = 5
            };

            content.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap
            });

            content.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            });

            notification.Content = content;
            notification.Show();

            Task.Delay(timeoutSeconds * 1000).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Invoke(() => notification.Close());
            });
        }
    }
}