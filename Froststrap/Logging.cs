// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Text;
using NLog;
using NLog.Conditions;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace Froststrap
{
    internal static class Logging
    {
        private const string FileLayout =
            """${date:format=HH\:mm\:ss} ${pad:padding=-5:inner=${level:uppercase=true}} ${froststrap-callsite} - ${message} ${exception:format=tostring}""";

        private const string ConsoleLayout =
            """${date:format=HH\:mm\:ss} ${pad:padding=-5:inner=${level:uppercase=true}} ${froststrap-callsite} - ${message} ${exception:format=tostring}""";

        public static bool Initialized => LogManager.Configuration != null;

        public static void Configure(string logRoot)
        {
            LogManager.Setup().SetupExtensions(
                extensions => extensions.RegisterLayoutRenderer<FroststrapCallsiteRenderer>("froststrap-callsite"));

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

    [LayoutRenderer("froststrap-callsite")]
    internal sealed class FroststrapCallsiteRenderer : LayoutRenderer, IUsesStackTrace
    {
        public StackTraceUsage StackTraceUsage => StackTraceUsage.WithCallSite | StackTraceUsage.WithCallSiteClassName;

        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            if (logEvent.Properties.TryGetValue("RustCallsite", out var rust)
                && rust is string native
                && !string.IsNullOrWhiteSpace(native))
            {
                builder.Append(native);
                return;
            }

            if (logEvent.CallerClassName is { Length: > 0 } callerClassName
                && logEvent.CallerMemberName is { Length: > 0 } callerMemberName)
            {
                builder.Append(Shorten(callerClassName));
                builder.Append('.');
                builder.Append(CleanMethod(callerMemberName, null));
                return;
            }

            if (logEvent.StackTrace is { } trace)
            {
                foreach (StackFrame frame in trace.GetFrames())
                {
                    if (!TryResolve(frame, out string typeName, out string methodName))
                        continue;

                    if (typeName == "NLog" || typeName.StartsWith("NLog.", StringComparison.Ordinal))
                        continue;

                    builder.Append(Shorten(typeName));
                    builder.Append('.');
                    builder.Append(CleanMethod(methodName, typeName));
                    return;
                }
            }

            builder.Append(logEvent.LoggerName);
        }

        private static bool TryResolve(StackFrame frame, out string typeName, out string methodName)
        {
            typeName = string.Empty;
            methodName = string.Empty;

            DiagnosticMethodInfo? info = DiagnosticMethodInfo.Create(frame);
            if (info is not null
                && !string.IsNullOrEmpty(info.DeclaringTypeName)
                && !string.IsNullOrEmpty(info.Name))
            {
                typeName = info.DeclaringTypeName;
                methodName = info.Name;
                return true;
            }

            string text = frame.ToString();
            int paren = text.IndexOf('(', StringComparison.Ordinal);
            if (paren >= 0)
                text = text[..paren];

            int lastDot = text.LastIndexOf('.');
            if (lastDot <= 0)
                return false;

            typeName = text[..lastDot];
            methodName = text[(lastDot + 1)..];
            return true;
        }

        private static string Shorten(string full)
        {
            int plus = full.IndexOf('+', StringComparison.Ordinal);
            if (plus >= 0)
                full = full[..plus];

            int dot = full.LastIndexOf('.');
            if (dot >= 0)
                full = full[(dot + 1)..];

            int tick = full.IndexOf('`', StringComparison.Ordinal);
            if (tick >= 0)
                full = full[..tick];

            return full.Length > 0 ? full : "?";
        }

        private static string CleanMethod(string methodName, string? typeName)
        {
            if (methodName == "MoveNext" && typeName is not null)
            {
                int open = typeName.LastIndexOf('<');
                int close = open >= 0 ? typeName.IndexOf('>', open + 1) : -1;
                if (close > open)
                    methodName = typeName[(open + 1)..close];
            }

            if (methodName.Length > 0 && methodName[0] == '<')
            {
                int close = methodName.IndexOf('>', StringComparison.Ordinal);
                if (close > 0)
                    methodName = methodName[1..close];
            }

            int bar = methodName.IndexOf('|', StringComparison.Ordinal);
            if (bar >= 0)
                methodName = methodName[..bar];

            int dollar = methodName.IndexOf('$', StringComparison.Ordinal);
            if (dollar >= 0)
                methodName = methodName[..dollar];

            return methodName.Length > 0 ? methodName : "?";
        }
    }
}
