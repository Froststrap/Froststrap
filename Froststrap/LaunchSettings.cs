namespace Froststrap
{
    internal class LaunchSettings
    {
        public LaunchFlag MenuFlag { get; } = new("preferences,menu,settings");
        public LaunchFlag WatcherFlag { get; } = new("watcher");
        public LaunchFlag BackgroundUpdaterFlag { get; } = new("backgroundupdater");
        public LaunchFlag OnboardingFlag { get; } = new("onboarding");
        public LaunchFlag QuietFlag { get; } = new("quiet");
        public LaunchFlag NoLaunchFlag { get; } = new("nolaunch");
        public LaunchFlag TestModeFlag { get; } = new("testmode");
        public LaunchFlag UpgradeFlag { get; } = new("upgrade");
        public LaunchFlag PlayerFlag { get; } = new("player");
        public LaunchFlag StudioFlag { get; } = new("studio");
        public LaunchFlag VersionFlag { get; } = new("version");
        public LaunchFlag ChannelFlag { get; } = new("channel");
        public LaunchFlag ForceFlag { get; } = new("force");
        public LaunchFlag BloxshadeFlag { get; } = new("bloxshade");
        public LaunchFlag GameShortcutFlag { get; } = new("gameshortcut");
        public LaunchFlag NsisFlag { get; } = new("nsis");

#if DEBUG
        public static bool BypassUpdateCheck => true;
#else
        public bool BypassUpdateCheck => WatcherFlag.Active || BackgroundUpdaterFlag.Active || NsisFlag.Active;
#endif

        public LaunchMode RobloxLaunchMode { get; set; } = LaunchMode.None;

        public string RobloxLaunchArgs { get; set; } = "";

        /// <summary>
        /// Original launch arguments
        /// </summary>
        public string[] Args { get; private set; }

        private static readonly string[] StudioFileExtensions = [".rbxl", ".rbxlx", ".rbxm", ".rbxmx"];

        private static bool IsRobloxStudioFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                var ext = Path.GetExtension(path);
                return StudioFileExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public LaunchSettings(string[] args)
        {
#if DEBUG
            App.Logger.Info($"Launched with arguments: {string.Join(' ', args)}");
#endif

            Args = args;
            string? entryAssemblyPath = AppContext.BaseDirectory;

            Dictionary<string, LaunchFlag> flagMap = [];

            // build flag map
            foreach (var prop in this.GetType().GetProperties())
            {
                if (prop.PropertyType != typeof(LaunchFlag))
                    continue;

                if (prop.GetValue(this) is not LaunchFlag flag)
                    continue;

                foreach (string identifier in flag.Identifiers.Split(','))
                    flagMap.Add(identifier, flag);
            }

            int startIdx = 0;

            // infer roblox launch uris
            if (Args.Length >= 1)
            {
                string arg = Args[0];

                if (ShouldSkipHostArgument(arg, entryAssemblyPath))
                {
                    startIdx = 1;
                }
                else if (arg.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase)
                    || arg.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.Info("Got Roblox player argument");
                    RobloxLaunchMode = LaunchMode.Player;
                    RobloxLaunchArgs = arg;
                    startIdx = 1;
                }
                else if (arg.StartsWith("roblox-studio-auth:", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.Info("Got Roblox Studio Auth argument");
                    RobloxLaunchMode = LaunchMode.StudioAuth;
                    RobloxLaunchArgs = arg;
                    startIdx = 1;
                }
                else if (arg.StartsWith("roblox-studio:", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.Info("Got Roblox Studio argument");
                    RobloxLaunchMode = LaunchMode.Studio;
                    RobloxLaunchArgs = arg;
                    startIdx = 1;
                }
                else if (arg.StartsWith("version-", StringComparison.Ordinal))
                {
                    App.Logger.Info("Got version argument");
                    VersionFlag.Active = true;
                    VersionFlag.Data = arg;
                    startIdx = 1;
                }
                else if (IsRobloxStudioFile(arg))
                {
                    App.Logger.Info("Got Roblox Studio file argument");
                    RobloxLaunchMode = LaunchMode.Studio;
                    RobloxLaunchArgs = $"-task EditFile -localPlaceFile \"{arg}\"";
                    startIdx = 1;
                }
            }

            // parse
            for (int i = startIdx; i < Args.Length; i++)
            {
                string arg = Args[i];

                if (!arg.StartsWith('-'))
                {
                    App.Logger.Error($"Invalid argument: {arg}");
                    continue;
                }

                string identifier = arg[1..];

                if (!flagMap.TryGetValue(identifier, out LaunchFlag? flag) || flag is null)
                {
                    App.Logger.Warn($"Unknown argument: {identifier}");
                    continue;
                }

                if (flag.Active)
                {
                    App.Logger.Error($"Tried to set {identifier} flag twice");
                    continue;
                }

                flag.Active = true;

                if (i < Args.Length - 1 && Args[i + 1] is string nextArg && !nextArg.StartsWith('-'))
                {
                    flag.Data = nextArg;
                    i++;
                    App.Logger.Info($"Identifier '{identifier}' is active with data");
                }
                else
                {
                    App.Logger.Info($"Identifier '{identifier}' is active");
                }
            }

            if (VersionFlag.Active)
                RobloxLaunchMode = LaunchMode.Unknown; // determine in bootstrapper

            if (PlayerFlag.Active)
                ParsePlayer(PlayerFlag.Data);
            else if (StudioFlag.Active)
                ParseStudio(StudioFlag.Data);

            if (GameShortcutFlag.Active && !string.IsNullOrEmpty(GameShortcutFlag.Data))
                ParseGameShortcut(GameShortcutFlag.Data);

            if (RobloxLaunchMode == LaunchMode.None)
                TryResolveRobloxUri();
        }

        public bool TryResolveRobloxUri(IEnumerable<string>? args = null)
        {
            foreach (string arg in args ?? Args)
            {
                if (arg.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase)
                    || arg.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.Info("Found Roblox player URI outside first argument");
                    RobloxLaunchMode = LaunchMode.Player;
                    RobloxLaunchArgs = arg;
                    return true;
                }
                else if (arg.StartsWith("roblox-studio-auth:", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.Info("Found Roblox Studio Auth URI outside first argument");
                    RobloxLaunchMode = LaunchMode.StudioAuth;
                    RobloxLaunchArgs = arg;
                    return true;
                }
                else if (arg.StartsWith("roblox-studio:", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.Info("Found Roblox Studio URI outside first argument");
                    RobloxLaunchMode = LaunchMode.Studio;
                    RobloxLaunchArgs = arg;
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldSkipHostArgument(string arg, string? entryAssemblyPath)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return false;

            if (!Path.IsPathRooted(arg))
                return false;

            if (string.Equals(arg, entryAssemblyPath, StringComparison.OrdinalIgnoreCase))
                return true;

            if (arg.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private void ParsePlayer(string? data)
        {
            RobloxLaunchMode = LaunchMode.Player;

            if (!string.IsNullOrEmpty(data))
            {
                App.Logger.Info("Got Roblox launch arguments");
                RobloxLaunchArgs = data;
            }
            else
            {
                App.Logger.Error("No Roblox launch arguments were provided");
            }
        }

        private void ParseStudio(string? data)
        {
            RobloxLaunchMode = LaunchMode.Studio;

            if (string.IsNullOrEmpty(data))
            {
                App.Logger.Error("No Roblox launch arguments were provided");
                return;
            }

            if (data.StartsWith("roblox-studio:", StringComparison.Ordinal))
            {
                App.Logger.Info("Got Roblox Studio launch arguments");
                RobloxLaunchArgs = data;
            }
            else if (data.StartsWith("roblox-studio-auth:", StringComparison.Ordinal))
            {
                App.Logger.Info("Got Roblox Studio Auth launch arguments");
                RobloxLaunchMode = LaunchMode.StudioAuth;
                RobloxLaunchArgs = data;
            }
            else
            {
                // likely a local path
                App.Logger.Info("Got Roblox Studio local place file");
                RobloxLaunchArgs = $"-task EditFile -localPlaceFile \"{data}\"";
            }
        }

        private void ParseGameShortcut(string data)
        {
            var parts = data.Split(';');

            if (parts.Length < 1)
            {
                App.Logger.Error("Insufficient data for game shortcut");
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

            App.Logger.Info($"Generated shortcut deeplink: {deeplink}");

            RobloxLaunchMode = LaunchMode.Player;
            RobloxLaunchArgs = deeplink;
        }
    }
}
