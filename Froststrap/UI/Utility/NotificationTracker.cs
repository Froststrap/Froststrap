using Avalonia.Labs.Notifications;

namespace Froststrap.UI.Utility
{
    public static class NotificationTracker
    {
        private static readonly List<(INativeNotification Notification, DateTime ShowTime, TimeSpan Duration)> _active = [];
        private static readonly Lock _lock = new();

        public static void Track(INativeNotification notification, TimeSpan duration)
        {
            lock (_lock)
            {
                _active.Add((notification, DateTime.UtcNow, duration));
            }
            _ = Task.Delay(duration).ContinueWith(_ => Remove(notification));
        }

        private static void Remove(INativeNotification notification)
        {
            lock (_lock)
            {
                _active.RemoveAll(item => item.Notification == notification);
            }
        }

        public static void Cleanup()
        {
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                _active.RemoveAll(item => now - item.ShowTime >= item.Duration);
            }
        }
    }
}