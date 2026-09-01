using System.Security.Cryptography;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Froststrap
{
    internal class Installer
    {
        /// <summary>
        /// Should this version automatically open the release notes page?
        /// Recommended for major updates only.
        /// </summary>
        private const bool OpenReleaseNotes = false;

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
            if (!File.Exists(Paths.Application) || Paths.Process == Paths.Application)
                return;

            bool isAutoUpgrade = App.LaunchSettings.UpgradeFlag.Active
                || Paths.Process.StartsWith(Path.Combine(Paths.Base, "Updates"), StringComparison.OrdinalIgnoreCase)
                || Paths.Process.StartsWith(Path.Combine(Paths.Temp, "Updates"), StringComparison.OrdinalIgnoreCase)
                || Paths.Process.StartsWith(Paths.TempUpdates, StringComparison.OrdinalIgnoreCase);

            var existingVer = GetVersionInfo(Paths.Application);
            var currentVer = GetVersionInfo(Paths.Process);

            if (SHA256Hash.FromFile(Paths.Process) == SHA256Hash.FromFile(Paths.Application))
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

            App.Logger.Info("Starting upgrade process...");

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
                    string.Format(CultureInfo.InvariantCulture, Strings.InstallChecker_Updated, currentVer ?? App.Version),
                    MessageBoxImage.Information
                );
            }

            App.Logger.Info("Upgrade completed successfully");
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
                            App.Logger.Error($"Failed to copy after 10 attempts: {ex}");
                            return false;
                        }

                        await Task.Delay(500);
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to copy executable: {ex}");
                return false;
            }
        }

        private static async Task UpdateVersionInfo()
        {
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
                       $"{App.ProjectName.ToUpperInvariant()}.desktop");

                    if (File.Exists(desktopFile))
                    {
                        var content = await File.ReadAllTextAsync(desktopFile);
                    }
                }

                App.Logger.Info($"Version info updated to {App.Version}");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to update version info: {ex}");
            }
        }

        public static async Task RunMigrations(string? previousVersion = null)
        {
            if (OperatingSystem.IsLinux())
                SetupSoberSymlink();

            string currentVer = App.Version;
            string? existingVer = previousVersion ?? App.State.Prop.LastMigratedVersion;

            if (existingVer is null && !App.Settings.IsSaved)
            {
                App.Logger.Info($"Fresh install detected — stamping LastMigratedVersion as {currentVer}");
                App.State.Prop.LastMigratedVersion = currentVer;
                App.State.Save();
                return;
            }

            if (existingVer is null)
            {
                var legacyStateCheck = new JsonManager<RobloxState>();
                if (!legacyStateCheck.IsSaved)
                {
                    App.Logger.Info("No LastMigratedVersion but no legacy data found — treating as already migrated");
                    App.State.Prop.LastMigratedVersion = currentVer;
                    App.State.Save();
                    return;
                }

                App.Logger.Info("Legacy RobloxState data found — treating as pre-migration install");
                existingVer = "0.0.0";
            }

            if (Utilities.CompareVersions(existingVer, currentVer) != VersionComparison.LessThan)
            {
                App.Logger.Info($"Migrations up to date (last={existingVer}, current={currentVer})");
                return;
            }

            App.Logger.Info($"Running migrations: {existingVer} -> {currentVer}");

            if (Utilities.CompareVersions(existingVer, "1.4.0.0") == VersionComparison.LessThan)
            {
                JsonManager<RobloxState> legacyRobloxState = new();

                if (legacyRobloxState.IsSaved)
                {
                    if (legacyRobloxState.Load(false))
                    {
                        App.PlayerState.Prop.VersionGuid = legacyRobloxState.Prop.Player.VersionGuid;
                        App.PlayerState.Prop.PackageHashes = legacyRobloxState.Prop.Player.PackageHashes;
                        App.PlayerState.Prop.ModManifest = legacyRobloxState.Prop.ModManifest;

                        App.StudioState.Prop.VersionGuid = legacyRobloxState.Prop.Studio.VersionGuid;
                        App.StudioState.Prop.PackageHashes = legacyRobloxState.Prop.Studio.PackageHashes;
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
                    App.Logger.Info("Deleted mod-generator cache for migration.");
                }

                if (Directory.Exists(pluginCacheDir))
                {
                    Directory.Delete(pluginCacheDir, true);
                    App.Logger.Info("Deleted studio plugin for migration.");
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

            App.Logger.Info($"Migrations complete — LastMigratedVersion set to {currentVer}");
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
            string flatpakId = "org.vinegarhq.Sober";
            string flatpakDataPath = Path.Combine(Paths.UserProfile, ".var", "app", flatpakId);
            string soberTarget = Path.Combine(Paths.Versions, "Sober");

            if (IsSymlinkPointingAt(flatpakDataPath, soberTarget))
            {
                App.Logger.Info("Sober symlink already in place, skipping.");
                return;
            }

            App.Logger.Info($"Setting up Sober symlink: {flatpakDataPath} -> {soberTarget}");

            Directory.CreateDirectory(soberTarget);

            if (Directory.Exists(flatpakDataPath) && !IsSymlink(flatpakDataPath))
            {
                App.Logger.Info($"Copying existing Sober data from {flatpakDataPath} to {soberTarget}");

                var cp = new ProcessStartInfo("cp", $"-a \"{flatpakDataPath}/.\" \"{soberTarget}/\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(cp))
                    proc?.WaitForExit();

                App.Logger.Info($"Removing original Sober data directory at {flatpakDataPath}");

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
                App.Logger.Info($"Removing stale symlink at {flatpakDataPath}");
                Directory.Delete(flatpakDataPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(flatpakDataPath)!);

            Directory.CreateSymbolicLink(flatpakDataPath, soberTarget);
            App.Logger.Info($"Created symlink: {flatpakDataPath} -> {soberTarget}");
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

        // TODO: Update all shortcuts to new directory
        public static async Task MoveInstallation(string newDir)
        {
            string oldDir = Paths.Base;
            if (string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase))
                return;

            Directory.CreateDirectory(newDir);
            string testFile = Path.Combine(newDir, ".writetest");
            try
            {
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                throw new IOException($"Cannot write to new directory: {ex.Message}", ex);
            }

            string[] itemsToMove = [
                "Settings.json",
                "State.json",
                "StudioState.json",
                "PlayerState.json",
                "Uninstall.exe",
                "Cache",
                "CustomCursorsSets",
                "CustomThemes",
                "Modifications"
            ];

            string exeName = Path.GetFileName(Paths.Process);

            foreach (string item in itemsToMove)
            {
                string source = Path.Combine(oldDir, item);
                string dest = Path.Combine(newDir, item);

                if (!File.Exists(source) && !Directory.Exists(source))
                {
                    App.Logger.Info($"Skipping missing item: {item}");
                    continue;
                }

                try
                {
                    if (Directory.Exists(source))
                    {
                        Directory.CreateDirectory(dest);
                        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                        {
                            string relDir = Path.GetRelativePath(source, dir);
                            Directory.CreateDirectory(Path.Combine(dest, relDir));
                        }
                        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                        {
                            string relFile = Path.GetRelativePath(source, file);
                            string targetFile = Path.Combine(dest, relFile);
                            File.Copy(file, targetFile, true);
                        }
                    }
                    else if (File.Exists(source))
                    {
                        File.Copy(source, dest, true);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex);
                    throw new IOException($"Failed to copy {item}: {ex.Message}", ex);
                }
            }

            string oldExePath = Paths.Process;
            string newExePath = Path.Combine(newDir, exeName);

            bool copied = false;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    using (var srcStream = new FileStream(oldExePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var dstStream = new FileStream(newExePath, FileMode.Create, FileAccess.Write))
                    {
                        await srcStream.CopyToAsync(dstStream);
                    }

                    var srcInfo = new FileInfo(oldExePath);
                    var dstInfo = new FileInfo(newExePath);
                    if (srcInfo.Length != dstInfo.Length)
                        throw new IOException("Size mismatch after copy.");

                    byte[] srcHash, dstHash;
                    using (var srcFs = new FileStream(oldExePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var dstFs = new FileStream(newExePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var buffer = new byte[1024 * 1024];
                        int read = await srcFs.ReadAsync(buffer);
                        srcHash = SHA256.HashData(buffer.AsSpan(0, read));
                        read = await dstFs.ReadAsync(buffer);
                        dstHash = SHA256.HashData(buffer.AsSpan(0, read));
                    }
                    if (!srcHash.SequenceEqual(dstHash))
                        throw new IOException("Hash mismatch after copy.");

                    copied = true;
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.Info($"Copy attempt {i + 1} failed: {ex.Message}");
                    if (i < 4) await Task.Delay(500);
                }
            }
            if (!copied)
                throw new IOException("Failed to copy executable after multiple retries.");

            UpdateRegistryForNewLocation(newDir);

            string batchName = "MoveHelper.bat";
            string batchPath = Path.Combine(newDir, batchName);

            string batchContent = $@"@echo off
timeout /t 3 /nobreak >nul
start """" ""{newExePath}"" -settings
timeout /t 3 /nobreak >nul
rmdir /s /q ""{oldDir}""
del ""%~f0""
";
            await File.WriteAllTextAsync(batchPath, batchContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Environment.Exit(0);
        }

        private static void UpdateRegistryForNewLocation(string newDir)
        {
            if (!OperatingSystem.IsWindows()) return;

            string exePath = Path.Combine(newDir, Path.GetFileName(Paths.Process));

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Froststrap");
            key.SetValue("InstallLocation", newDir);
            key.SetValue("AppPath", exePath);

            using var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey);
            uninstallKey.SetValue("InstallLocation", newDir);
            uninstallKey.SetValue("UninstallString", $"\"{Path.Combine(newDir, "Uninstall.exe")}\"");
            uninstallKey.SetValue("QuietUninstallString", $"\"{Path.Combine(newDir, "Uninstall.exe")}\" /S");
            uninstallKey.SetValue("DisplayIcon", $"{exePath},0");
            uninstallKey.SetValue("ModifyPath", $"\"{exePath}\" -settings");

            using var appPaths = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{Path.GetFileName(exePath)}");
            appPaths.SetValue("", exePath);
        }
    }
}
