using System.Runtime.Versioning;
using Microsoft.Win32;
using Froststrap.Utility;

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

            if (App.IsPlayerInstalled)
                processes.AddRange(Process.GetProcessesByName(App.RobloxPlayerAppName));

            if (App.IsStudioInstalled)
                processes.AddRange(Process.GetProcessesByName(App.RobloxStudioAppName));

            // prompt to shut down Roblox if it is currently running
            if (processes.Count != 0)
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
                cleanupSequence.AddRange(
                [
                    () => Directory.Delete(Paths.Modifications, true),
                    () => Directory.Delete(Paths.CustomCursors, true),
                    () => File.Delete(App.Settings.FileLocation),
                    () => File.Delete(App.State.FileLocation),
                ]);
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

        public static async Task HandleUpgrade()
        {
            const string LOG_IDENT = "Installer::HandleUpgrade";

            if (!File.Exists(Paths.Application) || Paths.Process == Paths.Application)
                return;

            bool isAutoUpgrade = App.LaunchSettings.UpgradeFlag.Active
                || Paths.Process.StartsWith(Path.Combine(Paths.Base, "Updates"))
                || Paths.Process.StartsWith(Path.Combine(Paths.Temp, "Updates"))
                || Paths.Process.StartsWith(Paths.TempUpdates);

            var existingVer = GetVersionInfo(Paths.Application);
            var currentVer = GetVersionInfo(Paths.Process);

            if (MD5Hash.FromFile(Paths.Process) == MD5Hash.FromFile(Paths.Application))
                return;

            if (currentVer is not null && existingVer is not null)
            {
                var comparison = Utilities.CompareVersions(currentVer, existingVer);

                if (comparison == VersionComparison.LessThan)
                {
                    var result = await Frontend.ShowMessageBox(
                        Strings.InstallChecker_VersionLessThanInstalled,
                        MessageBoxImage.Question,
                        MessageBoxButton.YesNo
                    );

                    if (result != MessageBoxResult.Yes)
                        return;
                }
            }

            if (!isAutoUpgrade)
            {
                var result = await Frontend.ShowMessageBox(
                    Strings.InstallChecker_VersionDifferentThanInstalled,
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo
                );

                if (result != MessageBoxResult.Yes)
                    return;
            }

            App.Logger.WriteLine(LOG_IDENT, "Starting upgrade process...");

            bool copySuccess = await CopyExecutableWithRetry();
            if (!copySuccess)
                return;

            await UpdateVersionInfo();

            await RunMigrations(existingVer);

            App.Settings.Save();
            App.FastFlags.Save();
            App.State.Save();
            App.PlayerState.Save();
            App.StudioState.Save();

            if (isAutoUpgrade && OpenReleaseNotes)
            {
                Utilities.ShellExecute($"https://github.com/{App.ProjectRepository}/releases/tag/{currentVer ?? App.Version}");
            }
            else if (!isAutoUpgrade)
            {
                await Frontend.ShowMessageBox(
                    string.Format(Strings.InstallChecker_Updated, currentVer ?? App.Version),
                    MessageBoxImage.Information
                );
            }

            App.Logger.WriteLine(LOG_IDENT, "Upgrade completed successfully");
        }

        private static string? GetVersionInfo(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                var versionInfo = FileVersionInfo.GetVersionInfo(filePath);

                if (!string.IsNullOrEmpty(versionInfo.ProductVersion))
                    return versionInfo.ProductVersion;

                if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                    return versionInfo.FileVersion;

                if (OperatingSystem.IsMacOS())
                {
                    string infoPlist = Path.Combine(Path.GetDirectoryName(filePath) ?? "", "..", "Info.plist");
                    if (File.Exists(infoPlist))
                    {
                        var plist = new System.Xml.XmlDocument();
                        plist.Load(infoPlist);
                        var node = plist.SelectSingleNode("//key[text()='CFBundleShortVersionString']/following-sibling::string");
                        if (node != null)
                            return node.InnerText;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<bool> CopyExecutableWithRetry()
        {
            const string LOG_IDENT = "Installer::CopyExecutableWithRetry";

            try
            {
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    if (File.Exists(Paths.Application))
                    {
                        var fileInfo = new FileInfo(Paths.Application) { IsReadOnly = false };
                        if (OperatingSystem.IsLinux())
                        {
                            var psi = new ProcessStartInfo("chmod", $"+w \"{Paths.Application}\"")
                            {
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            using var process = Process.Start(psi);
                            await process!.WaitForExitAsync();
                        }
                    }
                }

                for (int i = 1; i <= 10; i++)
                {
                    try
                    {
                        File.Copy(Paths.Process, Paths.Application, true);

                        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                        {
                            var psi = new ProcessStartInfo("chmod", $"+x \"{Paths.Application}\"")
                            {
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            using var process = Process.Start(psi);
                            await process!.WaitForExitAsync();
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        if (i == 10)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Failed to copy after 10 attempts: {ex.Message}");
                            App.Logger.WriteException(LOG_IDENT, ex);
                            return false;
                        }

                        await Task.Delay(500);
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to copy executable: {ex.Message}");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        private static async Task UpdateVersionInfo()
        {
            const string LOG_IDENT = "Installer::UpdateVersionInfo";

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey);
                    uninstallKey.SetValueSafe("DisplayVersion", App.Version);
                    uninstallKey.SetValueSafe("Publisher", App.ProjectOwner);
                    uninstallKey.SetValueSafe("HelpLink", App.ProjectHelpLink);
                    uninstallKey.SetValueSafe("URLInfoAbout", App.ProjectSupportLink);
                    uninstallKey.SetValueSafe("URLUpdateInfo", App.ProjectDownloadLink);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    string appPath = Paths.Application;
                    string infoPlist = Path.Combine(Path.GetDirectoryName(appPath) ?? "", "..", "Info.plist");

                    if (File.Exists(infoPlist))
                    {
                        var plist = new System.Xml.XmlDocument();
                        plist.Load(infoPlist);

                        var versionNode = plist.SelectSingleNode("//key[text()='CFBundleShortVersionString']/following-sibling::string");
                        if (versionNode != null)
                        {
                            versionNode.InnerText = App.Version;
                            plist.Save(infoPlist);
                        }
                    }
                }
                else if (OperatingSystem.IsLinux())
                {
                    string versionFile = Path.Combine(Paths.Base, ".version");
                    await File.WriteAllTextAsync(versionFile, App.Version);

                    string desktopFile = Path.Combine(Paths.UserProfile, ".local", "share", "applications",
                        $"{App.ProjectName.ToLower()}.desktop");
                    if (File.Exists(desktopFile))
                    {
                        var content = await File.ReadAllTextAsync(desktopFile);
                    }
                }

                App.Logger.WriteLine(LOG_IDENT, $"Version info updated to {App.Version}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to update version info: {ex.Message}");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public static async Task RunMigrations(string? previousVersion = null)
        {
            const string LOG_IDENT = "Installer::RunMigrations";

            if (OperatingSystem.IsLinux())
                SetupSoberSymlink();

            string currentVer = App.Version;
            string? existingVer = previousVersion ?? App.State.Prop.LastMigratedVersion;

            if (existingVer is null && !App.Settings.IsSaved)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Fresh install detected — stamping LastMigratedVersion as {currentVer}");
                App.State.Prop.LastMigratedVersion = currentVer;
                App.State.Save();
                return;
            }

            if (existingVer is null)
            {
                var legacyStateCheck = new JsonManager<RobloxState>();
                if (!legacyStateCheck.IsSaved)
                {
                    App.Logger.WriteLine(LOG_IDENT, "No LastMigratedVersion but no legacy data found — treating as already migrated");
                    App.State.Prop.LastMigratedVersion = currentVer;
                    App.State.Save();
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT, "Legacy RobloxState data found — treating as pre-migration install");
                existingVer = "0.0.0";
            }

            if (Utilities.CompareVersions(existingVer, currentVer) != VersionComparison.LessThan)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Migrations up to date (last={existingVer}, current={currentVer})");
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Running migrations: {existingVer} -> {currentVer}");

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
            if (Utilities.CompareVersions(existingVer, "1.4.2") == VersionComparison.LessThan)
            {
                string genCacheDir = Path.Combine(Path.GetTempPath(), "Froststrap", "mod-generator");
                string pluginCacheDir = Path.Combine(Paths.Roblox, "Plugins", "FroststrapStudioRPC.rbxmx");

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

            if (Utilities.CompareVersions(existingVer, "1.5.1") == VersionComparison.LessThan)
            {
                App.Settings.Prop.BootstrapperStyle = BootstrapperStyle.FluentAeroDialog;
                App.Settings.Prop.SelectedBackdrop = WindowsBackdrops.None;
            }

            App.State.Prop.LastMigratedVersion = currentVer;
            App.State.Save();

            if (App.PlayerState.Loaded) App.PlayerState.Save();
            if (App.StudioState.Loaded) App.StudioState.Save();

            App.Logger.WriteLine(LOG_IDENT, $"Migrations complete — LastMigratedVersion set to {currentVer}");
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

        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        private static void SetupSoberSymlink()
        {
            const string LOG_IDENT = "Installer::SetupSoberSymlink";

            string flatpakId = "org.vinegarhq.Sober";
            string flatpakDataPath = Path.Combine(Paths.UserProfile, ".var", "app", flatpakId);
            string soberTarget = Path.Combine(Paths.Versions, "Sober");

            if (IsSymlinkPointingAt(flatpakDataPath, soberTarget))
            {
                App.Logger.WriteLine(LOG_IDENT, "Sober symlink already in place, skipping.");
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Setting up Sober symlink: {flatpakDataPath} -> {soberTarget}");

            Directory.CreateDirectory(soberTarget);

            if (Directory.Exists(flatpakDataPath) && !IsSymlink(flatpakDataPath))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Copying existing Sober data from {flatpakDataPath} to {soberTarget}");

                var cp = new ProcessStartInfo("cp", $"-a \"{flatpakDataPath}/.\" \"{soberTarget}/\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(cp))
                    proc?.WaitForExit();

                App.Logger.WriteLine(LOG_IDENT, $"Removing original Sober data directory at {flatpakDataPath}");

                // rm -rf handles locked subdirs that Directory.Delete can't remove.
                var rm = new ProcessStartInfo("rm", $"-rf \"{flatpakDataPath}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(rm))
                    proc?.WaitForExit();
            }
            else if (IsSymlink(flatpakDataPath))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Removing stale symlink at {flatpakDataPath}");
                Directory.Delete(flatpakDataPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(flatpakDataPath)!);

            Directory.CreateSymbolicLink(flatpakDataPath, soberTarget);
            App.Logger.WriteLine(LOG_IDENT, $"Created symlink: {flatpakDataPath} -> {soberTarget}");
        }

        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        private static bool IsSymlink(string path)
        {
            if (!Path.Exists(path))
                return false;

            try
            {
                var attributes = File.GetAttributes(path);
                return attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch { return false; }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        private static bool IsSymlinkPointingAt(string path, string expectedTarget)
        {
          if (!IsSymlink(path)) 
                return false;

          try
          {
               string? actual = Directory.ResolveLinkTarget(path, returnFinalTarget: false)?.FullName;
              return actual == expectedTarget;
           }
           catch { return false; }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort */ }
        }
    }
}
