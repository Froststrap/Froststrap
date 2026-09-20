// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Froststrap.Utility
{
    [SupportedOSPlatform("macos")]
    internal static partial class MacChannelStore
    {
        private const string DefaultsTool = "/usr/bin/defaults";
        private const string PlistTool = "/usr/bin/plutil";

        [GeneratedRegex("^[A-Za-z0-9_-]*$")]
        private static partial Regex ChannelValueRegex();

        public static void Apply(string registryName, string robloxDomain, string? channel)
        {
            if (!OperatingSystem.IsMacOS())
                return;

            const string LOG_IDENT = "MacChannelStore::Apply";

            try
            {
                string domain = ResolveDomain(registryName);
                string defaultKey = "www." + robloxDomain;

                var before = ReadStringValues(domain);
                App.Logger.Info($"{LOG_IDENT}: '{domain}' before: {Describe(before)}");

                var keys = before
                    .Where(kv => ChannelValueRegex().IsMatch(kv.Value))
                    .Select(kv => kv.Key)
                    .ToList();

                if (keys.Count == 0)
                    keys.Add(defaultKey);

                foreach (string key in keys)
                {
                    before.TryGetValue(key, out string? current);

                    if (channel is null)
                    {
                        if (current is null)
                            continue;

                        var delete = Run(DefaultsTool, ["delete", domain, key]);
                        App.Logger.Info($"{LOG_IDENT}: cleared '{key}' (was '{current}'), exit {delete.ExitCode}");
                    }
                    else
                    {
                        if (string.Equals(current, channel, StringComparison.Ordinal))
                            continue;

                        var write = Run(DefaultsTool, ["write", domain, key, "-string", channel]);
                        App.Logger.Info($"{LOG_IDENT}: set '{key}' to '{channel}' (was '{current ?? "<unset>"}'), exit {write.ExitCode}");

                        if (write.ExitCode != 0)
                            App.Logger.Warn($"{LOG_IDENT}: defaults write failed: {write.StdErr.Trim()}");
                    }
                }

                App.Logger.Info($"{LOG_IDENT}: '{domain}' after: {Describe(ReadStringValues(domain))}");
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"{LOG_IDENT}: failed to pin the channel: {ex.Message}");
            }
        }

        private static string ResolveDomain(string registryName)
        {
            string fallback = $"com.roblox.{registryName}Channel";

            try
            {
                string prefs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Preferences");

                if (!Directory.Exists(prefs))
                    return fallback;

                var robloxFiles = Directory.EnumerateFiles(prefs, "*.plist")
                    .Select(file => Path.GetFileNameWithoutExtension(file))
                    .Where(name => name is not null && name.Contains("roblox", StringComparison.OrdinalIgnoreCase))
                    .Cast<string>()
                    .ToList();

                App.Logger.Info($"MacChannelStore::ResolveDomain: Roblox preference files: [{string.Join(", ", robloxFiles)}]");

                string? existing = robloxFiles.FirstOrDefault(name => string.Equals(name, fallback, StringComparison.OrdinalIgnoreCase));
                return existing ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static Dictionary<string, string> ReadStringValues(string domain)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            var export = Run(DefaultsTool, ["export", domain, "-"]);
            if (export.ExitCode != 0 || string.IsNullOrWhiteSpace(export.StdOut))
                return result;

            var json = Run(PlistTool, ["-convert", "json", "-o", "-", "-"], export.StdOut);
            if (json.ExitCode != 0 || string.IsNullOrWhiteSpace(json.StdOut))
                return result;

            using var doc = JsonDocument.Parse(json.StdOut);

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                    result[property.Name] = property.Value.GetString() ?? "";
            }

            return result;
        }

        private static string Describe(Dictionary<string, string> values) =>
            values.Count == 0 ? "<no string values>" : string.Join(", ", values.Select(kv => $"{kv.Key}='{kv.Value}'"));

        private static (int ExitCode, string StdOut, string StdErr) Run(string fileName, string[] arguments, string? stdin = null, int timeoutMs = 5000)
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = stdin is not null,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo)!;

            if (stdin is not null)
            {
                process.StandardInput.Write(stdin);
                process.StandardInput.Close();
            }

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(true); } catch { }
                return (-1, "", "timed out");
            }

            return (process.ExitCode, stdOutTask.GetAwaiter().GetResult(), stdErrTask.GetAwaiter().GetResult());
        }
    }
}