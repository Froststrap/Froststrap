using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Froststrap.UI.Elements.Settings;

namespace Froststrap.Utility;

public static class NotificationService
{
    public static void Show(
        string title,
        string message,
        NotificationType type = NotificationType.Information,
        bool showDesktop = false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            MainWindow.NotificationManager?.Show(
                new Notification(title, message, type, TimeSpan.FromSeconds(4)));

            if (showDesktop)
            {
                var trayIcons = TrayIcon.GetIcons(Application.Current!);
                var activeTray = trayIcons?.FirstOrDefault();

                var severity = type switch
                {
                    NotificationType.Success => FAInfoBarSeverity.Success,
                    NotificationType.Warning => FAInfoBarSeverity.Warning,
                    NotificationType.Error => FAInfoBarSeverity.Error,
                    _ => FAInfoBarSeverity.Informational
                };

                MainWindow.ShowGlobalNotification(title, message, severity, 4000);
            }
        });
    }
}
