// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

// TODO:
// All the "Installer" logic to do with updating, has been moved in here.
// This file now is a rats net of code, so try to clean this up at some point.

using Microsoft.Win32;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

namespace Froststrap;

internal class Updater
{
    /// <summary>
    /// Should this version automatically open the release notes page?
    /// Recommended for major updates only.
    /// </summary>
    private const bool OpenReleaseNotes = false;

    public static Bootstrapper? Bootstrapper { get; set; } = null!;

    public static async Task<bool> CheckForUpdates()
    {
        if (Process.GetProcessesByName(App.ProjectName).Length > 1)
        {
            App.Logger.Info($"More than one {App.ProjectName} instance running, aborting update check");
            return false;
        }

        if (App.Settings.Prop.UpdateChecks == UpdateCheck.Disabled)
        {
            App.Logger.Info("Update checking is disabled in settings");
            return false;
        }

        Bootstrapper?.SetStatus(Strings.Bootstrapper_Status_CheckingUpdates);

        App.Logger.Info("Checking for updates...");

        try
        {
            bool includePreRelease = false;

#if DEBUG
            includePreRelease = true;
#endif

            if (App.Settings.Prop.UpdateChecks == UpdateCheck.Both || App.Settings.Prop.UpdateChecks == UpdateCheck.Test)
                includePreRelease = true;

            var releaseInfo = await App.GetLatestRelease(includePreRelease);

            if (releaseInfo is null)
            {
                App.Logger.Error("Failed to get release information - it returned null");
                return false;
            }

            string currentVer = App.Version;
            string releaseVer = releaseInfo.TagName;
            var versionComparison = Utilities.CompareVersions(currentVer, releaseVer);

            if (versionComparison == VersionComparison.Equal || versionComparison == VersionComparison.GreaterThan)
            {
                App.Logger.Info($"No updates found. Current: {currentVer}, Latest: {releaseVer}");
                return false;
            }

            App.Logger.Info($"Update available: {currentVer} -> {releaseVer}");

            if (OperatingSystem.IsLinux())
            {
                App.Logger.Debug("Update detected, prompting user to manually update");

                var results = await Frontend.ShowMessageBox(
                    string.Format(CultureInfo.InvariantCulture, Strings.Update_Linux_Available, releaseVer),
                    MessageBoxImage.Information,
                    MessageBoxButton.YesNo
                );

                if (results == MessageBoxResult.Yes)
                {
                    App.Logger.Debug("User chose to visit releases page");
                    Utilities.ShellExecute(App.ProjectDownloadLink);
                }
                else
                {
                    App.Logger.Debug("User declined the update, continuing launch");
                }

                return false;
            }

            var asset = FindPlatformAsset(releaseInfo.Assets);
            if (asset is null)
            {
                App.Logger.Warn("No suitable asset found for this platform");
                await Frontend.ShowMessageBox(
                    string.Format(CultureInfo.InvariantCulture, Strings.Update_NoPackageAvailable, GetPlatformName()),
                    MessageBoxImage.Warning
                );
                Utilities.ShellExecute(App.ProjectDownloadLink);
                return false;
            }

            App.Logger.Info($"Found matching asset: {asset.Name}");

            if (!App.LaunchSettings.QuietFlag.Active)
            {
                string releaseType = releaseInfo.Prerelease ? "pre-release" : "stable";
                string newlinePart = "\n\nWould you like to update now?";
                var result = await Frontend.ShowMessageBox(
                    string.Format(CultureInfo.InvariantCulture, Strings.Update_Available, releaseType, releaseVer, newlinePart),
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo
                );

                if (result != MessageBoxResult.Yes)
                {
                    App.Logger.Debug("User declined the update");
                    return false;
                }
            }

            Bootstrapper?.SetStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_DownloadingUpdate, releaseVer));

            string downloadPath = Path.Combine(Paths.TempUpdates, asset.Name);
            Directory.CreateDirectory(Paths.TempUpdates);

            App.Logger.Info($"Downloading update from {asset.BrowserDownloadUrl}");

            if (Bootstrapper is not null)
            {
                await Bootstrapper.DownloadFileWithProgressAsync(asset.BrowserDownloadUrl, downloadPath);
            }

            App.Logger.Info($"Download complete: {downloadPath}");

            if (Bootstrapper is not null)
            {
                Bootstrapper.Dialog?.ProgressIndeterminate = true;
                Bootstrapper.Dialog?.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
                Bootstrapper.SetStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_InstallingUpdate, releaseVer));
            }


            bool updateApplied = await ApplyUpdate(downloadPath);

            if (!updateApplied)
            {
                App.Logger.Info("Update application failed");
                await Frontend.ShowMessageBox(
                    string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_AutoUpdateFailed, releaseVer),
                    MessageBoxImage.Information
                );
                Utilities.ShellExecute(App.ProjectDownloadLink);
                return false;
            }

            App.Logger.Info("Update applied successfully");
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "An exception occurred during update check");

            if (!App.LaunchSettings.QuietFlag.Active)
            {
                await Frontend.ShowMessageBox(Strings.Bootstrapper_AutoUpdateFailed, MessageBoxImage.Information);
            }

            return false;
        }
    }

    private static GithubReleaseAsset? FindPlatformAsset(List<GithubReleaseAsset>? assets)
    {
        if (assets is null || assets.Count == 0)
            return null;

        var patterns = GetPlatformAssetPatterns();

        foreach (var pattern in patterns)
        {
            var asset = assets.FirstOrDefault(a =>
                a.Name?.EndsWith(pattern, StringComparison.OrdinalIgnoreCase) == true);
            if (asset is not null)
                return asset;
        }

        return null;
    }

    private static List<string> GetPlatformAssetPatterns()
    {
        if (OperatingSystem.IsWindows())
        {
            return ["Froststrap-windows.msi", "-windows.msi"];
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (RuntimeInformation.OSArchitecture == Architecture.X64)
            {
                return ["Froststrap-macos-x64.pkg", "-macos-x64.pkg"];
            }
            else
            {
                return ["Froststrap-macos-arm64.pkg", "-macos-arm64.pkg"];
            }
        }

        return [];
    }

    private static string GetPlatformName()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unknown";
    }

    private static async Task<bool> ApplyUpdate(string updatePath)
    {
        try
        {
            App.Settings.Save();
            App.State.Save();
            App.PlayerState.Save();
            App.StudioState.Save();

            App.Logger.Info($"Applying update: {updatePath}");

            if (OperatingSystem.IsWindows())
            {
                return await ApplyWindowsUpdate(updatePath);
            }
            else if (OperatingSystem.IsMacOS())
            {
                return await ApplyMacOSUpdate(updatePath);
            }

            App.Logger.Warn("Unsupported operating system for updates");
            return false;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Failed to apply update: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> ApplyWindowsUpdate(string updatePath)
    {

        App.Logger.Info($"Applying Windows update: {updatePath}");

        try
        {
            string scriptPath = Path.Combine(Paths.TempUpdates, "update_runner.bat");
            string processPath = Paths.Process;

            string scriptContent = $@"@echo off
echo Waiting for {App.ProjectName} to exit...
timeout /t 2 /nobreak >nul

echo Installing update...
""{updatePath}"" /S

if errorlevel 1 (
echo Update failed with error code %errorlevel%
pause
exit /b %errorlevel%
)

echo Update installed successfully!
echo Restarting {App.ProjectName}...

start "" "" ""{processPath}""
exit";

            await File.WriteAllTextAsync(scriptPath, scriptContent);

            var startInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
            App.Terminate();
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Failed to apply Windows update: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> ApplyMacOSUpdate(string updatePath)
    {
        App.Logger.Info($"Applying macOS update: {updatePath}");

        try
        {
            string scriptPath = Path.Combine(Paths.TempUpdates, "update_runner.sh");
            string appName = App.ProjectName;

            string scriptContent = $@"#!/bin/bash
set -e

echo ""Waiting for {appName} to exit...""
sleep 2

echo ""Installing update via package...""
sudo installer -pkg ""{updatePath}"" -target /

echo ""Starting {appName}...""
open /Applications/{appName}.app

exit";

            await File.WriteAllTextAsync(scriptPath, scriptContent);

            var chmodInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var chmod = Process.Start(chmodInfo))
                await chmod!.WaitForExitAsync();

            var startInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(startInfo);
            App.Terminate();
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Failed to apply macOS update: {ex.Message}");
            return false;
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

        if (FastHash.FromFile(Paths.Process) == FastHash.FromFile(Paths.Application))
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
                    if (OperatingSystem.IsLinux()) await Utilities.RunAsync("chmod", $"+w \"{Paths.Application}\"");
                }
            }

            for (int i = 1; i <= 10; i++)
            {
                try
                {
                    File.Copy(Paths.Process, Paths.Application, true);
                    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) await Utilities.RunAsync("chmod", $"+x \"{Paths.Application}\"");
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

    [SupportedOSPlatform("linux")]
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

    [SupportedOSPlatform("linux")]
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

    [SupportedOSPlatform("linux")]
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
