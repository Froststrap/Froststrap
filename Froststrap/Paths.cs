using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;

namespace Froststrap
{
    static class Paths
    {
        public static string Temp => Path.Combine(Path.GetTempPath(), App.ProjectName);
        public static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        public static string System => Environment.GetFolderPath(Environment.SpecialFolder.System);
        public static string Process => Environment.ProcessPath!;

        public static string TempUpdates => Path.Combine(Temp, "Updates");
        public static string TempLogs => Path.Combine(Temp, "Logs");

        public static string ConfigRoot { get; private set; } = "";
        public static string DataRoot { get; private set; } = "";

        public static string Downloads { get; private set; } = "";
        public static string Cache { get; private set; } = "";
        public static string SavedFlagProfiles { get; private set; } = "";
        public static string Logs { get; private set; } = "";
        public static string Integrations { get; private set; } = "";
        public static string Versions { get; private set; } = "";
        public static string Modifications { get; private set; } = "";
        public static string Roblox { get; private set; } = "";
        public static string CustomThemes { get; private set; } = "";
        public static string RobloxLogs { get; private set; } = "";
        public static string RobloxCache { get; private set; } = "";
        public static string CustomCursors { get; private set; } = "";
        public static string Application { get; private set; } = "";

        public static string SoberAssetOverlay { get; private set; } = "";
        public static string SoberConfig { get; private set; } = "";
        public static string WineRoot { get; private set; } = "";

        public static string CustomFont => Path.Combine(Modifications, "content", "fonts", "CustomFont.ttf");

        public static string Base => DataRoot;
        public static bool Initialized => !String.IsNullOrEmpty(DataRoot);

        public static void Initialize(string baseDirectory)
        {
            if (OperatingSystem.IsWindows())
            {
                ConfigRoot = baseDirectory;
                DataRoot = baseDirectory;
                Roblox = Path.Combine(LocalAppData, "Roblox");
            }
            else if (OperatingSystem.IsMacOS())
            {
                string libraryPath = Path.Combine(UserProfile, "Library");
                ConfigRoot = Path.Combine(libraryPath, "Application Support", App.ProjectName);
                DataRoot = Path.Combine(libraryPath, "Application Support", App.ProjectName);

                Roblox = Path.Combine(libraryPath, "Application Support", "Roblox");
            }
            else if (OperatingSystem.IsLinux())
            {
                // Respect XDG Base Directory Specification when present
                string xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(UserProfile, ".config");
                string xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(UserProfile, ".local", "share");
                string xdgCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME") ?? Path.Combine(UserProfile, ".cache");
                string xdgState = Environment.GetEnvironmentVariable("XDG_STATE_HOME") ?? Path.Combine(UserProfile, ".local", "state");
                string xdgBin = Environment.GetEnvironmentVariable("XDG_BIN_HOME") ?? Path.Combine(UserProfile, ".local", "bin");

                ConfigRoot = Path.Combine(xdgConfig, App.ProjectName);
                DataRoot = Path.Combine(xdgData, App.ProjectName);

                // Sober data lives under Versions/Sober (symlink target managed by Installer)
                Roblox = Path.Combine(DataRoot, "Versions", "Sober");

                SoberAssetOverlay = Path.Combine(Roblox, "data", "sober", "asset_overlay");

                SoberConfig = Path.Combine(Roblox, "config", "sober");
                
                WineRoot = Path.Combine(DataRoot, "Wine");

                // Set cache and logs to XDG locations
                Cache = Path.Combine(xdgCache, App.ProjectName);
                Logs = Path.Combine(xdgState, App.ProjectName);

                Application = Path.Combine(xdgBin, App.ProjectName);
            }

            SavedFlagProfiles = Path.Combine(ConfigRoot, "SavedFlagProfiles");
            CustomCursors = Path.Combine(ConfigRoot, "CustomCursorsSets");
            CustomThemes = Path.Combine(ConfigRoot, "CustomThemes");

            Downloads = Path.Combine(DataRoot, "Downloads");
            Integrations = Path.Combine(DataRoot, "Integrations");
            Versions = Path.Combine(DataRoot, "Versions");
            Modifications = Path.Combine(ConfigRoot, "Modifications");

            // Ensure cache/logs have sensible defaults when not set by XDG above
            if (String.IsNullOrEmpty(Cache))
                Cache = Path.Combine(DataRoot, "Cache");

            if (String.IsNullOrEmpty(Logs))
                Logs = Path.Combine(DataRoot, "Logs");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                RobloxLogs = Path.Combine(UserProfile, "Library", "Logs", "Roblox");
                RobloxCache = Path.Combine(UserProfile, "Library", "Caches", "com.roblox.RobloxPlayer");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                RobloxLogs = Path.Combine(Roblox, "data", "sober", "sober_logs");
                RobloxCache = Path.Combine(Roblox, "cache");
            }
            else
            {
                RobloxLogs = Path.Combine(Roblox, "logs");
                RobloxCache = Path.Combine(Path.GetTempPath(), "Roblox");
            }

            string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{App.ProjectName}.exe" : App.ProjectName;

            if (!OperatingSystem.IsLinux())
                Application = Path.Combine(DataRoot, exeName);

            if (OperatingSystem.IsLinux())
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Application) ?? Application);
                Directory.CreateDirectory(WineRoot);
            }

                Directory.CreateDirectory(ConfigRoot);
            Directory.CreateDirectory(DataRoot);

            // Also ensure common per-user dirs exist
            Directory.CreateDirectory(Path.GetDirectoryName(SavedFlagProfiles) ?? SavedFlagProfiles);
            Directory.CreateDirectory(CustomCursors);
            Directory.CreateDirectory(CustomThemes);
            Directory.CreateDirectory(Modifications);
            Directory.CreateDirectory(Downloads);
            Directory.CreateDirectory(Integrations);
            Directory.CreateDirectory(Versions);
            Directory.CreateDirectory(Cache);
            Directory.CreateDirectory(Logs);

            // Perform one-time migration from legacy ~/.config/<AppName> layout to XDG layout
            if (OperatingSystem.IsLinux())
            {
                try
                {
                    // Compatibility migration for an earlier Linux path regression:
                    // move DataRoot/Roblox -> DataRoot/Versions/Sober if needed.
                    string regressedRobloxPath = Path.Combine(DataRoot, "Roblox");
                    if (Directory.Exists(regressedRobloxPath) && !Directory.Exists(Roblox))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Roblox)!);
                        Directory.Move(regressedRobloxPath, Roblox);
                    }

                    static void TryMove(string src, string dst)
                    {
                        try
                        {
                            if (File.Exists(src))
                            {
                                if (!File.Exists(dst))
                                    File.Move(src, dst);
                            }
                            else if (Directory.Exists(src))
                            {
                                if (!Directory.Exists(dst))
                                {
                                    Directory.Move(src, dst);
                                }
                                else
                                {
                                    // merge contents
                                    foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                                    {
                                        var relative = Path.GetRelativePath(src, dir);
                                        Directory.CreateDirectory(Path.Combine(dst, relative));
                                    }

                                    foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                                    {
                                        var relative = Path.GetRelativePath(src, file);
                                        var targetFile = Path.Combine(dst, relative);
                                        Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? Path.GetDirectoryName(dst)!);
                                        if (!File.Exists(targetFile)) File.Move(file, targetFile);
                                    }

                                    // Attempt to delete empty source tree
                                    try { Directory.Delete(src, true); } catch { }
                                }
                            }
                        }
                        catch { }
                    }

                    // Compatibility migration for earlier releases where mods/presets were under DataRoot.
                    TryMove(Path.Combine(DataRoot, "Modifications"), Modifications);

                    string legacyBase = Path.Combine(UserProfile, ".config", App.ProjectName);
                    string marker = Path.Combine(ConfigRoot, ".migrated_to_xdg");

                    if (Directory.Exists(legacyBase) && !File.Exists(marker) && !String.Equals(Path.GetFullPath(legacyBase), Path.GetFullPath(ConfigRoot), StringComparison.OrdinalIgnoreCase))
                    {

                        // Move user-editable config from legacy to XDG config
                        TryMove(Path.Combine(legacyBase, "Modifications"), Modifications);
                        TryMove(Path.Combine(legacyBase, "SavedFlagProfiles"), SavedFlagProfiles);
                        TryMove(Path.Combine(legacyBase, "CustomCursorsSets"), CustomCursors);
                        TryMove(Path.Combine(legacyBase, "CustomThemes"), CustomThemes);

                        // Move data folders from legacy to XDG data
                        TryMove(Path.Combine(legacyBase, "PlayerState.json"), Path.Combine(DataRoot, "PlayerState.json"));
                        TryMove(Path.Combine(legacyBase, "StudioState.json"), Path.Combine(DataRoot, "StudioState.json"));
                        TryMove(Path.Combine(legacyBase, "Downloads"), Downloads);
                        TryMove(Path.Combine(legacyBase, "Versions"), Versions);
                        TryMove(Path.Combine(legacyBase, "Cache"), Cache);
                        TryMove(Path.Combine(legacyBase, "Logs"), Logs);

                        // Mark migration completed
                        try { File.WriteAllText(marker, DateTime.UtcNow.ToString("o")); } catch { }
                    }
                }
                catch { }
            }
        }
    }
}
