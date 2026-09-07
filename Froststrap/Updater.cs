// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;

namespace Froststrap;

internal class Updater
{
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
}
