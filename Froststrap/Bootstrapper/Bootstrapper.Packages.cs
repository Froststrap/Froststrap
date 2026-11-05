// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Win32;

namespace Froststrap;

internal partial class Bootstrapper : IDisposable
{
    private async Task SetupPackageDictionaries()
    {
        if (OperatingSystem.IsMacOS())
        {
            PackageDirectoryMap = new Dictionary<string, string>
            {
                { "RobloxPlayer.zip", "" },
                { "RobloxStudioApp.zip", "" }
            };
            return;
        }

        if (OperatingSystem.IsLinux() && !IsStudioLaunch)
        {
            PackageDirectoryMap = [];
            return;
        }

        await App.RemoteData.WaitUntilDataFetched();

        var localData = App.RemoteData.Prop.PackageMaps[IsStudioLaunch ? "studio" : "player"];
        var commonData = App.RemoteData.Prop.PackageMaps.CommonPackageMap;

        PackageDirectoryMap = new(commonData);

        foreach (var package in localData)
            PackageDirectoryMap[package.Key] = package.Value;

        if (OperatingSystem.IsWindows())
            return;

        foreach (var key in PackageDirectoryMap.Keys.ToList())
        {
            if (PackageDirectoryMap[key] != null)
                PackageDirectoryMap[key] = PackageDirectoryMap[key].Replace('\\', '/');
        }
    }

    private async Task UpgradeRoblox()
    {
        if (!App.Settings.Prop.UpdateRoblox)
        {
            SetStatus(Strings.Bootstrapper_Status_CancelUpgrade);
            App.Logger.Info("Upgrading disabled, cancelling the upgrade.");

            if (!Directory.Exists(_latestVersionDirectory))
            {
                _ = Frontend.ShowMessageBox(Strings.Bootstrapper_Dialog_NoUpgradeWithoutClient, MessageBoxImage.Warning, MessageBoxButton.OK);
            }
            else
            {
                await Task.Delay(2000);
                return;
            }
        }

        SetStatus(string.IsNullOrEmpty(AppData.DistributionState.VersionGuid)
            ? Strings.Bootstrapper_Status_Installing
            : Strings.Bootstrapper_Status_Upgrading);

        Directory.CreateDirectory(Paths.Base);
        Directory.CreateDirectory(Paths.Downloads);
        Directory.CreateDirectory(Paths.Versions);

        _isInstalling = true;

        // make sure nothing is running before continuing upgrade
        if (!App.LaunchSettings.BackgroundUpdaterFlag.Active && !IsStudioLaunch)
            KillRobloxPlayers();

        // get a fully clean install
        if (!App.LaunchSettings.BackgroundUpdaterFlag.Active && Directory.Exists(_latestVersionDirectory))
        {
            try
            {
                Directory.Delete(_latestVersionDirectory, true);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to delete the latest version directory");
            }
        }

        Directory.CreateDirectory(_latestVersionDirectory);

        if (OperatingSystem.IsMacOS())
        {
            string backupDir = GetResourcesBackupPath(_latestVersionGuid);
            if (!Directory.Exists(backupDir))
                return;
            try
            {
                Directory.Delete(backupDir, true);
                App.Logger.Info($"Deleted existing mod backup for {_latestVersionGuid}");
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Failed to delete mod backup: {ex.Message}");
            }
        }

        var cachedPackageHashes = Directory.GetFiles(Paths.Downloads).Select(x => Path.GetFileName(x)).ToList();

        // package manifest states packed size and uncompressed size in exact bytes
        long totalSizeRequired = 0;

        // packed size only matters if we don't already have the package cached on disk
        var installPackages = _versionPackageManifest.Where(p => p.Name != "RobloxPlayerInstaller.exe").ToList();

        totalSizeRequired += installPackages.Where(x => !cachedPackageHashes.Contains(x.Signature)).Sum(x => (long)x.PackedSize);
        totalSizeRequired += installPackages.Sum(x => (long)x.Size);

        long freeDiskSpace = Filesystem.GetFreeDiskSpace(Paths.Base);

        App.Logger.Info($"Disk space check for '{Paths.Base}': {freeDiskSpace} bytes free, {totalSizeRequired} bytes required");

        if (freeDiskSpace < 0)
            App.Logger.Warn("Could not determine free disk space, skipping the disk space check.");
        else if (freeDiskSpace < totalSizeRequired)
        {
            await Frontend.ShowMessageBox(Strings.Bootstrapper_NotEnoughSpace, MessageBoxImage.Error);
            App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
            return;
        }

        if (Dialog is not null)
        {
            Dialog.ProgressIndeterminate = false;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;

            Dialog.ProgressMaximum = ProgressBarMaximum;

            // compute total bytes to download
            _totalPackagedBytes = _versionPackageManifest.Where(p => p.Name != "RobloxPlayerInstaller.exe").Sum(package => package.PackedSize);
            _progressIncrement = (double)ProgressBarMaximum / _totalPackagedBytes;

            _taskbarProgressMaximum = TaskbarProgressMaximum;

            _taskbarProgressIncrement = _taskbarProgressMaximum / (double)_totalPackagedBytes;
        }

        var packageTasks = new List<Task>();

        // from largest to smallest, this is so larger packages (which need more time) get queued first
        var packages = _versionPackageManifest.Where(p => p.Name != "RobloxPlayerInstaller.exe").OrderByDescending(p => p.PackedSize);

        using var downloadSemaphore = new SemaphoreSlim(App.Settings.Prop.MaxThreadDownload > 0 ? App.Settings.Prop.MaxThreadDownload : 1);
        foreach (var package in packages)
        {
            await downloadSemaphore.WaitAsync(_cancelTokenSource.Token);


            var task = Task.Run(async () =>
            {
                try
                {
                    await DownloadPackage(package);

                    // we'll extract the runtime installer later if we need to
                    if (package.Name != "WebView2RuntimeInstaller.zip")
                        await ExtractPackage(package);
                }
                finally
                {
                    downloadSemaphore.Release();
                }
            }, _cancelTokenSource.Token);

            packageTasks.Add(task);
        }
        await Task.WhenAll(packageTasks);

        if (_cancelTokenSource.IsCancellationRequested) return;

        if (Dialog is not null)
        {
            Dialog.ProgressIndeterminate = true;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
            SetStatus(Strings.Bootstrapper_Status_Configuring);
        }

        if (OperatingSystem.IsWindows() && App.State.Prop.PromptWebView2Install)
        {
            using var hklmKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
            using var hkcuKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");

            // reset prompt state if the user has it installed
            if (hklmKey is not null || hkcuKey is not null)
                App.State.Prop.PromptWebView2Install = true;
            else
            {
                var result = await Frontend.ShowMessageBox(Strings.Bootstrapper_WebView2NotFound, MessageBoxImage.Warning, MessageBoxButton.YesNo, MessageBoxResult.Yes);

                if (result != MessageBoxResult.Yes)
                    App.State.Prop.PromptWebView2Install = false;
                else
                {
                    App.Logger.Info("Installing WebView2 runtime...");

                    var package = _versionPackageManifest.Find(x => x.Name == "WebView2RuntimeInstaller.zip");

                    if (package is null)
                    {
                        App.Logger.Info("Aborted runtime install because package does not exist, has WebView2 been added in this Roblox version yet?");
                        return;
                    }

                    string baseDirectory = Path.Combine(_latestVersionDirectory, PackageDirectoryMap[package.Name]);

                    await ExtractPackage(package);

                    SetStatus(Strings.Bootstrapper_Status_InstallingWebView2);

                    var startInfo = new ProcessStartInfo()
                    {
                        WorkingDirectory = baseDirectory,
                        FileName = Path.Combine(baseDirectory, "MicrosoftEdgeWebview2Setup.exe"),
                        Arguments = "/silent /install"
                    };

                    await Process.Start(startInfo)!.WaitForExitAsync();

                    App.Logger.Info("Finished installing runtime");

                    Directory.Delete(baseDirectory, true);
                }
            }
        }

        if (OperatingSystem.IsMacOS())
        {
            string[] appNames = ["RobloxPlayer.app", "RobloxStudio.app"];
            foreach (string appName in appNames)
            {
                string appPath = Path.Combine(_latestVersionDirectory, appName);
                if (!Directory.Exists(appPath)) continue;

                string macOSDir = Path.Combine(appPath, "Contents", "MacOS");
                if (Directory.Exists(macOSDir)) foreach (string file in Directory.GetFiles(macOSDir))
                {
                    var fileInfo = new FileInfo(file);
                    fileInfo.UnixFileMode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                }
                using var process = Process.Start("xattr", $"-dr com.apple.quarantine \"{appPath}\"");
                await process!.WaitForExitAsync();
            }
        }

        // finishing and cleanup
        MigrateCompatibilityFlags();

        AppData.DistributionState.VersionGuid = _latestVersionGuid;

        AppData.DistributionState.PackageHashes.Clear();

        foreach (var package in _versionPackageManifest)
            AppData.DistributionState.PackageHashes.Add(package.Name, package.Signature);

        CleanupVersionsFolder();

        var allPackageHashes = new List<string>();

        allPackageHashes.AddRange(App.PlayerState.Prop.PackageHashes.Values);
        allPackageHashes.AddRange(App.StudioState.Prop.PackageHashes.Values);

        if (!App.Settings.Prop.DebugDisableVersionPackageCleanup) foreach (string hash in cachedPackageHashes)
        {
            if (allPackageHashes.Contains(hash))
                return;

            App.Logger.Info($"Deleting unused package {hash}");

            try
            {
                File.Delete(Path.Combine(Paths.Downloads, hash));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"Failed to delete {hash}!");
            }
        }

        App.State.Prop.ForceReinstall = false;
        App.State.Save();
        AppData.DistributionStateManager.Save();

        if (OperatingSystem.IsWindows())
            WindowsRegistry.UpdateEstimatedSize();

        if (!IsStudioLaunch) InitializeModFolders();
        _isInstalling = false;
    }
}
