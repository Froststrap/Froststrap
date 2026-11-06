// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Cli;
using NLog;

namespace Froststrap
{
    internal class LaunchSettings
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public LaunchFlag SettingsFlag { get; } = new();
        public LaunchFlag BackgroundUpdaterFlag { get; } = new();
        public LaunchFlag NoLaunchFlag { get; } = new();
        public LaunchFlag TestModeFlag { get; } = new();
        public LaunchFlag UpgradeFlag { get; } = new();
        public LaunchFlag PlayerFlag { get; } = new();
        public LaunchFlag StudioFlag { get; } = new();
        public LaunchFlag VersionFlag { get; } = new();
        public LaunchFlag ChannelFlag { get; } = new();
        public LaunchFlag ForceFlag { get; } = new();
        public LaunchFlag GameShortcutFlag { get; } = new();
        public LaunchFlag ConsoleFlag { get; } = new();
        public LaunchFlag NoGpuFlag { get; } = new();

#if DEBUG
        public static bool BypassUpdateCheck => true;
#else
        public bool BypassUpdateCheck => BackgroundUpdaterFlag.Active;
#endif

        public bool Silent { get; set; }

        public LaunchMode RobloxLaunchMode { get; set; } = LaunchMode.None;

        public string RobloxLaunchArgs { get; set; } = "";

        public string[] Args { get; private set; }
        public List<string> UnknownArgs { get; } = [];

        private static readonly HashSet<string> StudioFileExtensions = new(
            [".rbxl", ".rbxlx", ".rbxm", ".rbxmx"],
            StringComparer.OrdinalIgnoreCase);

        private static bool IsRobloxStudioFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                var ext = Path.GetExtension(path);
                return StudioFileExtensions.Contains(ext);
            }
            catch { return false; }
        }

        private (LaunchMode Mode, string Args)? _resolvedRoblox;

        public LaunchSettings(FroststrapCliCommand cli, string[] args)
        {
            Args = args;

            SettingsFlag.Active = cli.Settings;
            BackgroundUpdaterFlag.Active = cli.BackgroundUpdater;
            NoLaunchFlag.Active = cli.NoLaunch;
            TestModeFlag.Active = cli.TestMode;
            UpgradeFlag.Active = cli.Upgrade;
            ForceFlag.Active = cli.Force;
            ConsoleFlag.Active = cli.ShowConsole;
            NoGpuFlag.Active = cli.NoGpu;

            SetFlag(VersionFlag, cli.RobloxVersion);
            SetFlag(ChannelFlag, cli.Channel);
            SetFlag(GameShortcutFlag, cli.GameShortcut);

            PlayerFlag.Active = cli.Player;
            StudioFlag.Active = cli.Studio;

            ClassifyTargets(cli.Targets);

            if (VersionFlag.Active)
                RobloxLaunchMode = LaunchMode.Unknown;

            if (RobloxLaunchMode is LaunchMode.None or LaunchMode.Unknown)
            {
                if (PlayerFlag.Active)
                    RobloxLaunchMode = LaunchMode.Player;
                else if (StudioFlag.Active)
                    RobloxLaunchMode = LaunchMode.Studio;
            }

            if (GameShortcutFlag.Active && !string.IsNullOrEmpty(GameShortcutFlag.Data))
                ParseGameShortcut(GameShortcutFlag.Data);

            if (RobloxLaunchMode == LaunchMode.None)
                TryResolveRobloxUri();
        }

        private static void SetFlag(LaunchFlag flag, string? data)
        {
            if (data is null)
                return;

            flag.Active = true;
            flag.Data = data;
        }

        private void ClassifyTargets(string[] targets)
        {
            int startIdx = 0;

            if (targets.Length > 0)
            {
                string arg = targets[0];

                if (arg.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase)
                    || arg.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info("Got Roblox player argument");
                    RobloxLaunchMode = LaunchMode.Player;
                    RobloxLaunchArgs = arg;
                    startIdx = 1;
                }
                else if (arg.StartsWith("roblox-studio-auth:", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info("Got Roblox Studio Auth argument");
                    RobloxLaunchMode = LaunchMode.StudioAuth;
                    RobloxLaunchArgs = arg;
                    startIdx = 1;
                }
                else if (arg.StartsWith("roblox-studio:", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info("Got Roblox Studio argument");
                    RobloxLaunchMode = LaunchMode.Studio;
                    RobloxLaunchArgs = arg;
                    startIdx = 1;
                }
                else if (arg.StartsWith("version-", StringComparison.Ordinal))
                {
                    Logger.Info("Got version argument");
                    VersionFlag.Active = true;
                    VersionFlag.Data = arg;
                    startIdx = 1;
                }
                else if (IsRobloxStudioFile(arg))
                {
                    Logger.Info("Got Roblox Studio file argument");
                    RobloxLaunchMode = LaunchMode.Studio;
                    RobloxLaunchArgs = $"-task EditFile -localPlaceFile \"{arg}\"";
                    startIdx = 1;
                }
            }

            for (int i = startIdx; i < targets.Length; i++)
            {
                Logger.Error($"Invalid argument: {targets[i]}");
                UnknownArgs.Add(targets[i]);
            }
        }

        public bool TryResolveRobloxUri(IEnumerable<string>? args = null)
        {
            if (args is null && _resolvedRoblox.HasValue)
            {
                RobloxLaunchMode = _resolvedRoblox.Value.Mode;
                RobloxLaunchArgs = _resolvedRoblox.Value.Args;
                return true;
            }

            foreach (string arg in args ?? Args)
            {
                if (arg.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase)
                    || arg.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info("Found Roblox player URI outside first argument");
                    RobloxLaunchMode = LaunchMode.Player;
                    RobloxLaunchArgs = arg;
                    _resolvedRoblox = (RobloxLaunchMode, RobloxLaunchArgs);
                    return true;
                }
                else if (arg.StartsWith("roblox-studio-auth:", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info("Found Roblox Studio Auth URI outside first argument");
                    RobloxLaunchMode = LaunchMode.StudioAuth;
                    RobloxLaunchArgs = arg;
                    _resolvedRoblox = (RobloxLaunchMode, RobloxLaunchArgs);
                    return true;
                }
                else if (arg.StartsWith("roblox-studio:", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info("Found Roblox Studio URI outside first argument");
                    RobloxLaunchMode = LaunchMode.Studio;
                    RobloxLaunchArgs = arg;
                    _resolvedRoblox = (RobloxLaunchMode, RobloxLaunchArgs);
                    return true;
                }
            }

            return false;
        }

        private void ParseGameShortcut(string data)
        {
            var parts = data.Split(';');

            if (parts.Length < 1)
            {
                Logger.Error("Insufficient data for game shortcut");
                return;
            }

            string placeId = parts[0];
            string jobId = parts.Length > 1 ? parts[1] : "";
            string accessCode = parts.Length > 2 ? parts[2] : "";

            string deeplink = $"roblox://experiences/start?placeId={placeId}";

            if (!string.IsNullOrEmpty(accessCode))
                deeplink += "&accessCode=" + accessCode;
            else if (!string.IsNullOrEmpty(jobId))
                deeplink += "&gameInstanceId=" + jobId;

            Logger.Info($"Generated shortcut deeplink: {deeplink}");

            RobloxLaunchMode = LaunchMode.Player;
            RobloxLaunchArgs = deeplink;
        }
    }
}
