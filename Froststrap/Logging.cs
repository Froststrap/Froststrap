// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using NLog;
using NLog.Conditions;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace Froststrap
{
    internal static class Logging
    {
        private const string FileLayout =
            """${date:format=HH\:mm\:ss} ${pad:padding=-5:inner=${level:uppercase=true}} ${event-properties:RustCallsite:whenEmpty=${callsite:includeNamespace=false}} - ${message} ${exception:format=tostring}""";

        private const string ConsoleLayout =
            """${date:format=HH\:mm\:ss} ${pad:padding=-5:inner=${level:uppercase=true}} - ${message} ${exception:format=tostring}""";

        public static bool Initialized => LogManager.Configuration != null;

        public static void Configure(string logRoot)
        {
            var config = new LoggingConfiguration();

            var fileTarget = new FileTarget("latestfile_inner")
            {
                FileName = Path.Combine(logRoot, "latest.log"),
                ArchiveOldFileOnStartup = true,
                ArchiveFileName = Path.Combine(logRoot, "${date:format=yyyy-MM-dd_HH-mm-ss}.log"),
                MaxArchiveFiles = 29,
                KeepFileOpen = true,
                DeleteOldFileOnStartup = false,
                Layout = FileLayout
            };

            var asyncFile = new AsyncTargetWrapper("latestfile", fileTarget)
            {
                QueueLimit = 10000,
                BatchSize = 200,
                OverflowAction = AsyncTargetWrapperOverflowAction.Discard,
            };

            var console = new ColoredConsoleTarget("console")
            {
                Layout = ConsoleLayout,
                UseDefaultRowHighlightingRules = false
            };

            AddHighlight(console, "Trace", ConsoleOutputColor.Gray);
            AddHighlight(console, "Debug", ConsoleOutputColor.Blue);
            AddHighlight(console, "Info", ConsoleOutputColor.Green);
            AddHighlight(console, "Warn", ConsoleOutputColor.Yellow);
            AddHighlight(console, "Error", ConsoleOutputColor.Red);
            AddHighlight(console, "Fatal", ConsoleOutputColor.Yellow, ConsoleOutputColor.Red);

            config.AddRule(LogLevel.Debug, LogLevel.Fatal, asyncFile);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, console);

            LogManager.Configuration = config;
        }

        private static void AddHighlight(
            ColoredConsoleTarget target,
            string level,
            ConsoleOutputColor foreground,
            ConsoleOutputColor background = ConsoleOutputColor.NoChange)
        {
            ConditionExpression condition = $"level == LogLevel.{level}";

            target.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                condition,
                foreground,
                background));
        }

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
