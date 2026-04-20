using System;
using System.IO;
using System.Runtime.InteropServices;

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
        public static string PresetModifications { get; private set; } = "";
        public static string Roblox { get; private set; } = "";
        public static string CustomThemes { get; private set; } = "";
        public static string RobloxLogs { get; private set; } = "";
        public static string RobloxCache { get; private set; } = "";
        public static string CustomCursors { get; private set; } = "";
        public static string Application { get; private set; } = "";

        public static string CustomFont => Path.Combine(PresetModifications, "content", "fonts", "CustomFont.ttf");

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
                ConfigRoot = Path.Combine(UserProfile, ".config", App.ProjectName);
                DataRoot = Path.Combine(UserProfile, ".config", App.ProjectName);

                // TODO: give actal path
                Roblox = Path.Combine("dev", "null");
            }

            SavedFlagProfiles = Path.Combine(ConfigRoot, "SavedFlagProfiles");
            CustomCursors = Path.Combine(ConfigRoot, "CustomCursorsSets");
            CustomThemes = Path.Combine(ConfigRoot, "CustomThemes");

            Downloads = Path.Combine(DataRoot, "Downloads");
            Logs = Path.Combine(DataRoot, "Logs");
            Integrations = Path.Combine(DataRoot, "Integrations");
            Versions = Path.Combine(DataRoot, "Versions");
            Modifications = Path.Combine(DataRoot, "ModificationProfiles");
            PresetModifications = Path.Combine(DataRoot, "Modifications");
            Cache = Path.Combine(DataRoot, "Cache");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                RobloxLogs = Path.Combine(UserProfile, "Library", "Logs", "Roblox");
                RobloxCache = Path.Combine(UserProfile, "Library", "Caches", "com.roblox.RobloxPlayer");
            }
            else
            {
                RobloxLogs = Path.Combine(Roblox, "logs");
                RobloxCache = Path.Combine(Path.GetTempPath(), "Roblox");
            }

            string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{App.ProjectName}.exe" : App.ProjectName;
            Application = Path.Combine(DataRoot, exeName);

            Directory.CreateDirectory(ConfigRoot);
            Directory.CreateDirectory(DataRoot);
        }
    }
}
