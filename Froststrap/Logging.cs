// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Layouts;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace Froststrap
{
    internal static class Logging
    {
        private const string FileLayout =
            """${date:format=HH\:mm\:ss} ${pad:padding=-5:inner=${level:uppercase=true}} ${froststrap-callsite} - ${froststrap-message} ${froststrap-exception}""";

        private const string ConsoleLayout =
            """${froststrap-date} ${froststrap-level} ${froststrap-message} ${froststrap-exception}""";

        public static bool Initialized => LogManager.Configuration != null;

        public static void Configure(string logRoot)
        {
            AnsiConsole.Initialize();

            LogManager.Setup().SetupExtensions(extensions =>
            {
                extensions.RegisterLayoutRenderer<FroststrapCallsiteRenderer>("froststrap-callsite");
                extensions.RegisterLayoutRenderer<ConsoleDateRenderer>("froststrap-date");
                extensions.RegisterLayoutRenderer<ConsoleLevelRenderer>("froststrap-level");
                extensions.RegisterLayoutRenderer<SanitizedMessageRenderer>("froststrap-message");
                extensions.RegisterLayoutRenderer<SanitizedExceptionRenderer>("froststrap-exception");
            });

            var config = new LoggingConfiguration();

            FileTarget? fileTarget = new("latestfile_inner")
            {
                FileName = Path.Combine(logRoot, "latest.log"),
                ArchiveOldFileOnStartup = true,
                ArchiveFileName = Path.Combine(logRoot, "${date:format=yyyy-MM-dd_HH-mm-ss}.log"),
                MaxArchiveFiles = 29,
                KeepFileOpen = true,
                DeleteOldFileOnStartup = false,
                Layout = FileLayout
            };

            AsyncTargetWrapper? asyncFile = null;
            ColoredConsoleTarget? console = null;

            try
            {
                asyncFile = new AsyncTargetWrapper("latestfile", fileTarget)
                {
                    QueueLimit = 10000,
                    BatchSize = 200,
                    OverflowAction = AsyncTargetWrapperOverflowAction.Discard,
                };
                fileTarget = null;

                console = new ColoredConsoleTarget("console")
                {
                    Layout = ConsoleLayout,
                    UseDefaultRowHighlightingRules = false,
                    DetectOutputRedirected = true,
                    EnableAnsiOutput = AnsiConsole.Enabled
                };

                console.WordHighlightingRules.Add(new FirstTokenHighlightingRule(
                    ConsoleOutputColor.Cyan,
                    ConsoleOutputColor.NoChange));

                AddLevelHighlight(console, "Trace", ConsoleOutputColor.Gray);
                AddLevelHighlight(console, "Debug", ConsoleOutputColor.Blue);
                AddLevelHighlight(console, "Info", ConsoleOutputColor.DarkMagenta);
                AddLevelHighlight(console, "Warn", ConsoleOutputColor.Yellow);
                AddLevelHighlight(console, "Error", ConsoleOutputColor.Red);
                AddLevelHighlight(console, "Fatal", ConsoleOutputColor.Yellow, ConsoleOutputColor.Red);

                config.AddRule(LogLevel.Debug, LogLevel.Fatal, asyncFile);
                asyncFile = null;

                config.AddRule(LogLevel.Debug, LogLevel.Fatal, console);
                console = null;

                LogManager.Configuration = config;
            }
            finally
            {
                fileTarget?.Dispose();
                asyncFile?.Dispose();
                console?.Dispose();
            }
        }

        private const int StdOutputHandle = -11;
        private const uint EnableVirtualTerminalProcessing = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern nint GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int GetConsoleMode(nint hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int SetConsoleMode(nint hConsoleHandle, uint dwMode);

        internal static bool EnableAnsiOnWindows()
        {
            nint handle = GetStdHandle(StdOutputHandle);
            if (handle == 0 || handle == -1)
                return false;

            if (GetConsoleMode(handle, out uint mode) == 0)
                return false;

            return SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing) != 0;
        }

        private static void AddLevelHighlight(
            ColoredConsoleTarget target,
            string level,
            ConsoleOutputColor foreground,
            ConsoleOutputColor background = ConsoleOutputColor.NoChange)
        {
            target.WordHighlightingRules.Add(new FirstWordHighlightingRule(
                level.ToUpperInvariant(),
                foreground,
                background)
            {
                Condition = $"level == LogLevel.{level}",
            });
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

    internal static class AnsiConsole
    {
        public static bool Enabled { get; private set; }

        public static void Initialize()
        {
            if (Console.IsOutputRedirected || IsNoColor())
            {
                Enabled = false;
                return;
            }

            Enabled = !OperatingSystem.IsWindows() || Logging.EnableAnsiOnWindows();
        }

        private static bool IsNoColor()
        {
            string? value = Environment.GetEnvironmentVariable("NO_COLOR")?.Trim();

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase);
        }
    }

    [LayoutRenderer("froststrap-date")]
    internal sealed class ConsoleDateRenderer : LayoutRenderer
    {
        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            AppendBold(builder, logEvent.TimeStamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        }

        internal static void AppendBold(StringBuilder builder, string text)
        {
            if (!AnsiConsole.Enabled)
            {
                builder.Append(text);
                return;
            }

            builder.Append("\x1b[1m");
            builder.Append(text);
            builder.Append("\x1b[22m");
        }
    }

    [LayoutRenderer("froststrap-level")]
    internal sealed class ConsoleLevelRenderer : LayoutRenderer
    {
        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            string level = logEvent.Level.Name.ToUpperInvariant();
            int padding = Math.Max(0, 5 - level.Length);

            ConsoleDateRenderer.AppendBold(builder, level);
            builder.Append(' ', padding);
        }
    }

    internal sealed class FirstTokenHighlightingRule : ConsoleWordHighlightingRule
    {
        public FirstTokenHighlightingRule(
            ConsoleOutputColor foreground,
            ConsoleOutputColor background)
            : base(string.Empty, foreground, background)
        {
        }

        protected override IEnumerable<KeyValuePair<int, int>>? GetWordsForHighlighting(string haystack)
        {
            int end = 0;
            while (end < haystack.Length && !char.IsWhiteSpace(haystack[end]))
                end++;

            return end > 0
                ? new[] { new KeyValuePair<int, int>(0, end) }
                : null;
        }
    }

    internal sealed class FirstWordHighlightingRule : ConsoleWordHighlightingRule
    {
        public FirstWordHighlightingRule(
            string text,
            ConsoleOutputColor foreground,
            ConsoleOutputColor background)
            : base(text, foreground, background)
        {
        }

        protected override IEnumerable<KeyValuePair<int, int>>? GetWordsForHighlighting(string haystack)
        {
            int index = haystack.IndexOf(Text, StringComparison.Ordinal);

            return index >= 0
                ? new[] { new KeyValuePair<int, int>(index, Text.Length) }
                : null;
        }
    }

    [LayoutRenderer("froststrap-message")]
    internal sealed class SanitizedMessageRenderer : LayoutRenderer
    {
        private static readonly Layout MessageLayout = "${message}";

        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(LogSanitizer.Sanitize(MessageLayout.Render(logEvent)));
        }
    }

    [LayoutRenderer("froststrap-exception")]
    internal sealed class SanitizedExceptionRenderer : LayoutRenderer
    {
        private static readonly Layout ExceptionLayout = "${exception:format=tostring}";

        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(LogSanitizer.Sanitize(ExceptionLayout.Render(logEvent)));
        }
    }

    internal static class LogSanitizer
    {
        private static readonly IReadOnlyList<(Regex Pattern, string Replacement)> Rules = CreateRules();

        public static string Sanitize(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            foreach (var (pattern, replacement) in Rules)
                text = pattern.Replace(text, replacement);

            return text;
        }

        private static List<(Regex Pattern, string Replacement)> CreateRules()
        {
            var rules = new List<(Regex, string)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            AddPathRule(Paths.UserProfile, "~");
            AddPathRule(Environment.GetEnvironmentVariable("HOME"), "~");
            AddPathRule(Environment.GetEnvironmentVariable("USERPROFILE"), "~");

            return rules;

            void AddPathRule(string? path, string replacement)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                path = Path.TrimEndingDirectorySeparator(path);

                if (path.Length < 2
                    || string.Equals(Path.GetPathRoot(path), path, StringComparison.OrdinalIgnoreCase))
                    return;

                foreach (string variant in PathVariants(path))
                {
                    if (!seen.Add(variant))
                        continue;

                    string escaped = Regex.Escape(variant);
                    var pattern = new Regex(
                        $@"(?<![\w.-]){escaped}(?=$|[\\/""'\s:;,)\]])",
                        RegexOptions.Compiled | RegexOptions.CultureInvariant);

                    rules.Add((pattern, replacement));
                }
            }
        }

        private static IEnumerable<string> PathVariants(string path)
        {
            yield return path;
            yield return path.Replace('\\', '/');
            yield return path.Replace('/', '\\');
        }
    }
}
