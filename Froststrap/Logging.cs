using NLog;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace Froststrap
{
    internal static class Logging
    {
        public static bool Initialized => LogManager.Configuration != null;

        public static string? FileLocation
        {
            get
            {
                var target = FindFileTarget();
                if (target == null) return null;

                try
                {
                    return target.FileName.Render(LogEventInfo.CreateNullEvent());
                }
                catch
                {
                    return null;
                }
            }
        }

        public static string AsDocument
        {
            get
            {
                var path = FileLocation;
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return string.Empty;

                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
                catch (Exception ex)
                {
                    return $"[Failed to read log file: {ex.Message}]";
                }
            }
        }

        private static FileTarget? FindFileTarget()
        {
            var config = LogManager.Configuration;
            if (config == null) return null;

            return FindFileTarget(config.AllTargets);
        }

        private static FileTarget? FindFileTarget(IEnumerable<Target> targets)
        {
            foreach (var target in targets)
            {
                if (target is FileTarget fileTarget)
                    return fileTarget;

                if (target is WrapperTargetBase wrapper && wrapper.WrappedTarget != null)
                {
                    var found = FindFileTarget(new[] { wrapper.WrappedTarget });
                    if (found != null) return found;
                }
            }

            return null;
        }
    }
}
