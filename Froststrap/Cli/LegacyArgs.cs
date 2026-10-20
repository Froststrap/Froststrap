// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Cli;

internal static class LegacyArgs
{
    private static readonly Dictionary<string, string> LegacyNames = new(StringComparer.Ordinal)
    {
        ["settings"] = "--settings",
        ["backgroundupdater"] = "--background-updater",
        ["nolaunch"] = "--no-launch",
        ["testmode"] = "--test-mode",
        ["upgrade"] = "--upgrade",
        ["player"] = "--player",
        ["studio"] = "--studio",
        ["robloxversion"] = "--roblox-version",
        ["channel"] = "--channel",
        ["force"] = "--force",
        ["gameshortcut"] = "--game-shortcut",
        ["console"] = "--console",
        ["nogpu"] = "--no-gpu",
    };

    public static string[] Normalize(string[] args)
    {
        int start = 0;

        if (args.Length >= 1 && ShouldSkipHostArgument(args[0]))
            start = 1;

        var result = new List<string>(args.Length - start);

        for (int i = start; i < args.Length; i++)
        {
            string arg = args[i];

            if (!IsFlagToken(arg))
            {
                result.Add(arg);
                continue;
            }

            string key = ToLowerAscii(arg.StartsWith("--", StringComparison.Ordinal) ? arg[2..] : arg[1..]);

            if (key.Length == 0)
            {
                result.Add(arg);
            }
            else if (key.Length == 1)
            {
                result.Add("-" + key);
            }
            else if (key is "version" or "robloxversion" or "roblox-version")
            {
                // "--version <hash>" used to pin the Roblox client version, while a bare
                // "--version" now prints the Froststrap version
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    result.Add("--roblox-version");
                    result.Add(args[i + 1]);
                    i++;
                }
                else
                {
                    result.Add(key == "version" ? "--version" : "--roblox-version");
                }
            }
            else
            {
                result.Add(LegacyNames.GetValueOrDefault(key) ?? "--" + key);
            }
        }

        return [.. result];
    }

    public static bool RequestsHelpOrVersion(string[] args) =>
        args.Any(static arg => arg is "--help" or "-h" or "-?" or "/h" or "/?" or "--version");

    private static string ToLowerAscii(string value) =>
        string.Create(value.Length, value, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                span[i] = c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;
            }
        });

    private static bool IsFlagToken(string arg)
    {
        if (arg.Length < 2 || arg[0] != '-')
            return false;

        for (int i = 1; i < arg.Length; i++)
        {
            char c = arg[i];

            if (!char.IsLetterOrDigit(c) && c != '-' && c != '?')
                return false;
        }

        return true;
    }

    private static bool ShouldSkipHostArgument(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return false;

        if (arg.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return true;

        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath) &&
            string.Equals(arg, processPath, StringComparison.OrdinalIgnoreCase))
            return true;

        string? firstCommandLineArg = Environment.GetCommandLineArgs().FirstOrDefault();
        if (!string.IsNullOrEmpty(firstCommandLineArg) &&
            string.Equals(arg, firstCommandLineArg, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
