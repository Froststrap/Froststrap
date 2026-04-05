using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Froststrap
{
    internal class Installer
    {
        /// <summary>
        /// Should this version automatically open the release notes page?
        /// Recommended for major updates only.
        /// </summary>
        private const bool OpenReleaseNotes = false;

        /// <summary>
        /// Kills any running Roblox processes and cleans up app data files.
        /// Registry entries, shortcuts, and the executable itself are managed by NSIS.
        /// </summary>
        public static async Task DoUninstall(bool keepData)
        {
            const string LOG_IDENT = "Installer::DoUninstall";

            var processes = new List<Process>();

            if (!string.IsNullOrEmpty(App.PlayerState.Prop.VersionGuid))
                processes.AddRange(Process.GetProcessesByName(App.RobloxPlayerAppName));

            if (App.IsStudioInstalled)
                processes.AddRange(Process.GetProcessesByName(App.RobloxStudioAppName));

            // prompt to shut down Roblox if it is currently running
            if (processes.Any())
            {
                var result = await Frontend.ShowMessageBox(
                    Strings.Bootstrapper_Uninstall_RobloxRunning,
                    MessageBoxImage.Information,
                    MessageBoxButton.OKCancel,
                    MessageBoxResult.OK
                );

                if (result != MessageBoxResult.OK)
                {
                    App.Terminate(ErrorCode.ERROR_CANCELLED);
                    return;
                }

                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.Close();
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to close process: {ex}");
                    }
                }
            }

            if (OperatingSystem.IsWindows())
                RestoreRobloxRegistryHandlers();

            // When invoked by NSIS (-nsis flag), stop here.
            if (App.LaunchSettings.NsisFlag.Active)
                return;

            var cleanupSequence = new List<Action>
            {
                () => Directory.Delete(Paths.Versions, true),
                () => Directory.Delete(Paths.Downloads, true),
                () => File.Delete(App.State.FileLocation),
                () =>
                {
                    // only delete the Roblox subfolder if it lives inside the Froststrap base
                    // directory, to avoid accidentally deleting a standalone Roblox install
                    if (Paths.Roblox == Path.Combine(Paths.Base, "Roblox"))
                        Directory.Delete(Paths.Roblox, true);
                }
            };

            if (!keepData)
            {
                cleanupSequence.AddRange(new List<Action>
                {
                    () => Directory.Delete(Paths.Modifications, true),
                    () => Directory.Delete(Paths.CustomCursors, true),
                    () => File.Delete(App.Settings.FileLocation),
                    () => File.Delete(App.State.FileLocation),
                });
            }

            foreach (var step in cleanupSequence)
            {
                try
                {
                    step();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Encountered exception during cleanup step #{cleanupSequence.IndexOf(step)}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private static void RestoreRobloxRegistryHandlers()
        {
            using var playerKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\roblox-player");
            var playerFolder = playerKey?.GetValue("InstallLocation");

            if (playerKey is null || playerFolder is not string playerFolderStr)
            {
                WindowsRegistry.Unregister("roblox");
                WindowsRegistry.Unregister("roblox-player");
            }
            else
            {
                string playerPath = Path.Combine(playerFolderStr, App.RobloxPlayerAppName);
                WindowsRegistry.RegisterPlayer(playerPath, "%1");
            }

            using var studioKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\roblox-studio");
            var studioFolder = studioKey?.GetValue("InstallLocation");

            if (studioKey is null || studioFolder is not string studioFolderStr)
            {
                WindowsRegistry.Unregister("roblox-studio");
                WindowsRegistry.Unregister("roblox-studio-auth");
                WindowsRegistry.Unregister("Roblox.Place");
                WindowsRegistry.Unregister(".rbxl");
                WindowsRegistry.Unregister(".rbxlx");
            }
            else
            {
                string studioPath = Path.Combine(studioFolderStr, App.RobloxStudioAppName);
                WindowsRegistry.RegisterStudioProtocol(studioPath, "%1");
                WindowsRegistry.RegisterStudioFileClass(studioPath, "-ide \"%1\"");
            }
        }

        public static async Task RunMigrations()
        {
            const string LOG_IDENT = "Installer::RunMigrations";

            string currentVer = App.Version;
            string? existingVer = App.State.Prop.LastMigratedVersion;

            // First run after switching to NSIS updater: treat installs that have never
            // recorded a migration version as coming from the oldest known release so that
            // all migration blocks are evaluated.
            if (existingVer is null)
            {
                App.Logger.WriteLine(LOG_IDENT, "No LastMigratedVersion recorded — treating as fresh migration run");
                existingVer = "0.0.0";
            }

            if (Utilities.CompareVersions(existingVer, currentVer) != VersionComparison.LessThan)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Migrations up to date (last={existingVer}, current={currentVer})");
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Running migrations: {existingVer} -> {currentVer}");

            if (Utilities.CompareVersions(existingVer, "1.2.5.0") == VersionComparison.LessThan)
            {
                App.Settings.Prop.ShowServerUptime = false;
            }

            if (Utilities.CompareVersions(existingVer, "1.4.0.0") == VersionComparison.LessThan)
            {
                JsonManager<RobloxState> legacyRobloxState = new();

                if (legacyRobloxState.IsSaved)
                {
                    if (legacyRobloxState.Load(false))
                    {
                        App.PlayerState.Prop.VersionGuid = legacyRobloxState.Prop.Player.VersionGuid;
                        App.PlayerState.Prop.PackageHashes = legacyRobloxState.Prop.Player.PackageHashes;
                        App.PlayerState.Prop.Size = legacyRobloxState.Prop.Player.Size;
                        App.PlayerState.Prop.ModManifest = legacyRobloxState.Prop.ModManifest;

                        App.StudioState.Prop.VersionGuid = legacyRobloxState.Prop.Studio.VersionGuid;
                        App.StudioState.Prop.PackageHashes = legacyRobloxState.Prop.Studio.PackageHashes;
                        App.StudioState.Prop.Size = legacyRobloxState.Prop.Studio.Size;
                    }

                    legacyRobloxState.Delete();
                }

                if (App.Settings.Prop.Theme == Theme.Custom)
                    App.Settings.Prop.Theme = Theme.Default;

                TryDelete(Path.Combine(Paths.Cache, "GameHistory.json"));
            }

            if (Utilities.CompareVersions(existingVer, "1.4.1.0") == VersionComparison.LessThan)
            {
                if (App.Settings.Prop.MultiInstanceLaunching)
                    App.Settings.Prop.MultiInstanceLaunching = false;

                TryDelete(Path.Combine(Paths.Cache, "GameHistory.json"));
            }

            if (Utilities.CompareVersions(existingVer, "1.4.2") == VersionComparison.LessThan)
            {
                string clientSettingsPath = Path.Combine(Paths.Modifications, "ClientSettings");
                string migrationPath = Path.Combine(Paths.Modifications, "Migration from 1.4.1.0");
                string genCacheDir = Path.Combine(Path.GetTempPath(), "Froststrap", "mod-generator");
                string pluginCacheDir = Path.Combine(Paths.Roblox, "Plugins", "FroststrapStudioRPC.rbxmx");
                string targetSettingsPath = Path.Combine(Paths.Base, "ClientSettings");

                if (Directory.Exists(clientSettingsPath))
                {
                    if (Directory.Exists(targetSettingsPath))
                        Directory.Delete(targetSettingsPath, true);
                    Directory.Move(clientSettingsPath, targetSettingsPath);
                }

                Directory.CreateDirectory(migrationPath);

                foreach (FileSystemInfo info in new DirectoryInfo(Paths.Modifications).GetFileSystemInfos())
                {
                    if (info.FullName == migrationPath)
                        continue;

                    string destPath = Path.Combine(migrationPath, info.Name);

                    try
                    {
                        if (info.Attributes.HasFlag(FileAttributes.Directory))
                        {
                            if (Directory.Exists(destPath)) Directory.Delete(destPath, true);
                            Directory.Move(info.FullName, destPath);
                        }
                        else
                        {
                            if (File.Exists(destPath)) File.Delete(destPath);
                            File.Move(info.FullName, destPath);
                        }
                    }
                    catch (IOException ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Could not migrate {info.Name}: {ex.Message}");
                    }
                }

                if (Directory.Exists(genCacheDir))
                {
                    Directory.Delete(genCacheDir, true);
                    App.Logger.WriteLine(LOG_IDENT, "Deleted mod-generator cache for migration.");
                }

                if (Directory.Exists(pluginCacheDir))
                {
                    Directory.Delete(pluginCacheDir, true);
                    App.Logger.WriteLine(LOG_IDENT, "Deleted studio plugin for migration.");
                }

                TryDelete(Path.Combine(Paths.Cache, "channelCache.json"));
                TryDelete(Path.Combine(Paths.Cache, "channelCacheMeta.json"));
                TryDelete(Path.Combine(Paths.Cache, "datacenters_cache.json"));
            }

            if (Utilities.CompareVersions(existingVer, "1.5.1.0") == VersionComparison.LessThan)
            {
                App.Settings.Prop.BootstrapperStyle = BootstrapperStyle.FluentAeroDialog;
                App.Settings.Prop.SelectedBackdrop = WindowsBackdrops.None;

                string legacyRoot = Path.Combine(Paths.Modifications, "Preset Modifications");

                if (Directory.Exists(legacyRoot))
                {
                    foreach (var sourceFolderPath in Directory.GetDirectories(legacyRoot))
                    {
                        string folderName = Path.GetFileName(sourceFolderPath);
                        string targetFolderRoot = Path.Combine(Paths.PresetModifications, folderName);

                        foreach (string file in Directory.GetFiles(sourceFolderPath, "*.*", SearchOption.AllDirectories))
                        {
                            string relativePath = Path.GetRelativePath(sourceFolderPath, file);
                            string destPath = Path.Combine(targetFolderRoot, relativePath);
                            string? destDir = Path.GetDirectoryName(destPath);

                            if (!string.IsNullOrEmpty(destDir))
                                Directory.CreateDirectory(destDir);

                            if (File.Exists(destPath))
                                File.Delete(destPath);

                            File.Move(file, destPath);
                        }

                        Directory.Delete(sourceFolderPath, true);
                    }

                    if (Directory.GetFileSystemEntries(legacyRoot).Length == 0)
                        Directory.Delete(legacyRoot, false);
                }
            }

            // Save everything and stamp the version so this batch doesn't rerun
            App.Settings.Save();
            App.FastFlags.Save();
            App.State.Prop.LastMigratedVersion = currentVer;
            App.State.Save();

            if (App.PlayerState.Loaded) App.PlayerState.Save();
            if (App.StudioState.Loaded) App.StudioState.Save();

            App.Logger.WriteLine(LOG_IDENT, $"Migrations complete — LastMigratedVersion set to {currentVer}");

            if (OpenReleaseNotes)
                // very interesting this clearly never got finished.
                // Possibly replace this by just opening the release page of the version where all the release notes should be
                // Or make our website have rleease notes for every version, but that is low reward for a considerable amount of effort
                Utilities.ShellExecute($"https://github.com/{App.ProjectRepository}/wiki/Release-notes-for-Froststrap-v{currentVer}");
        }

 
        [SupportedOSPlatform("windows")]
        public static void UpdateUninstallRegistryVersion()
        {
            using var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey);
            uninstallKey.SetValueSafe("DisplayVersion", App.Version);
            uninstallKey.SetValueSafe("Publisher", App.ProjectOwner);
            uninstallKey.SetValueSafe("HelpLink", App.ProjectHelpLink);
            uninstallKey.SetValueSafe("URLInfoAbout", App.ProjectSupportLink);
            uninstallKey.SetValueSafe("URLUpdateInfo", App.ProjectDownloadLink);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort */ }
        }
    }
}