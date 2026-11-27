// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.AppData;
using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;
using Froststrap.Clients.Sources;
using Froststrap.Mods;
using Froststrap.Models.Manifest;
using Microsoft.Win32;

namespace Froststrap.Bootstrapping;

internal sealed record InstallContext
{
    public required IRobloxClient Client { get; init; }

    public required IAppData AppData { get; init; }

    public required string VersionGuid { get; init; }

    public required string VersionDirectory { get; init; }

    public required PackageManifest Manifest { get; init; }

    public required IReadOnlyDictionary<string, string> PackageDirectoryMap { get; init; }

    public required ModPaths ModPaths { get; init; }
}

internal sealed class DeploymentInstaller(
    InstallContext context,
    DeploymentPackages packageStore,
    BootstrapperProgress reporter,
    Func<IBootstrapperDialog?> dialog,
    CancellationToken cancellationToken)
{
    public bool IsInstalling { get; private set; }

    private IBootstrapperDialog? Dialog => dialog();

    public async Task InstallAsync()
    {
        if (!App.Settings.Prop.UpdateRoblox)
        {
            reporter.SetStatus(Strings.Bootstrapper_Status_CancelUpgrade);
            App.Logger.Info("Upgrading disabled, cancelling the upgrade.");

            if (!Directory.Exists(context.VersionDirectory))
                _ = Frontend.ShowMessageBox(Strings.Bootstrapper_Dialog_NoUpgradeWithoutClient, MessageBoxImage.Warning, MessageBoxButton.OK);
            else
            {
                await Task.Delay(2000);
                return;
            }
        }

        reporter.SetStatus(string.IsNullOrEmpty(context.AppData.DistributionState.VersionGuid)
            ? Strings.Bootstrapper_Status_Installing
            : Strings.Bootstrapper_Status_Upgrading);

        Directory.CreateDirectory(Paths.Base);
        Directory.CreateDirectory(Paths.Downloads);
        Directory.CreateDirectory(Paths.Versions);

        IsInstalling = true;

        // make sure nothing is running before continuing upgrade
        if (!BackgroundUpdate.IsCurrentProcess && context.Client.Target == ClientTarget.Player)
            await CloseRunningClientAsync();

        // get a fully clean install
        if (!BackgroundUpdate.IsCurrentProcess && Directory.Exists(context.VersionDirectory))
        {
            try
            {
                Directory.Delete(context.VersionDirectory, true);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to delete the latest version directory");
            }
        }

        Directory.CreateDirectory(context.VersionDirectory);

        if (context.Client.Capabilities.RestoreStrategy == RestoreStrategy.VersionBackup)
        {
            string backupDir = Mods.ModEngine.GetResourcesBackupPath(context.VersionGuid);
            if (Directory.Exists(backupDir))
            {
                try
                {
                    Directory.Delete(backupDir, true);
                    App.Logger.Info($"Deleted existing mod backup for {context.VersionGuid}");
                }
                catch (Exception ex)
                {
                    App.Logger.Warn($"Failed to delete mod backup: {ex.Message}");
                }
            }
        }

        var cachedPackageHashes = Directory.GetFiles(Paths.Downloads).Select(x => Path.GetFileName(x)).ToList();

        // package manifest states packed size and uncompressed size in exact bytes
        long totalSizeRequired = 0;

        // packed size only matters if we don't already have the package cached on disk
        var installPackages = context.Manifest.Where(p => p.Name != "RobloxPlayerInstaller.exe").ToList();

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

        reporter.BeginInstallProgress(context.Manifest.Where(p => p.Name != "RobloxPlayerInstaller.exe").Sum(package => package.PackedSize));

        var packageTasks = new List<Task>();

        // from largest to smallest, this is so larger packages (which need more time) get queued first
        var packages = context.Manifest.Where(p => p.Name != "RobloxPlayerInstaller.exe").OrderByDescending(p => p.PackedSize);

        using var downloadSemaphore = new SemaphoreSlim(App.Settings.Prop.MaxThreadDownload > 0 ? App.Settings.Prop.MaxThreadDownload : 1);
        foreach (var package in packages)
        {
            await downloadSemaphore.WaitAsync(cancellationToken);


            var task = Task.Run(async () =>
            {
                try
                {
                    await packageStore.DownloadAsync(package);

                    // we'll extract the runtime installer later if we need to
                    if (package.Name != "WebView2RuntimeInstaller.zip")
                        await packageStore.ExtractAsync(package);
                }
                finally
                {
                    downloadSemaphore.Release();
                }
            }, cancellationToken);

            packageTasks.Add(task);
        }
        await Task.WhenAll(packageTasks);

        if (cancellationToken.IsCancellationRequested) return;

        if (Dialog is not null)
        {
            Dialog.ProgressIndeterminate = true;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
            reporter.SetStatus(Strings.Bootstrapper_Status_Configuring);
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

                    var package = context.Manifest.Find(x => x.Name == "WebView2RuntimeInstaller.zip");

                    if (package is null)
                    {
                        App.Logger.Info("Aborted runtime install because package does not exist, has WebView2 been added in this Roblox version yet?");
                        return;
                    }

                    string baseDirectory = Path.Combine(context.VersionDirectory, context.PackageDirectoryMap[package.Name]);

                    await packageStore.ExtractAsync(package);

                    reporter.SetStatus(Strings.Bootstrapper_Status_InstallingWebView2);

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

        try
        {
            await context.Client.FinalizeInstallAsync(context.VersionDirectory);
        }
        catch (ChecksumFailedException ex)
        {
            foreach (var package in context.Manifest)
            {
                if (File.Exists(package.DownloadPath))
                    File.Delete(package.DownloadPath);

                context.AppData.DistributionState.PackageHashes.Remove(package.Name);
            }

            context.AppData.DistributionStateManager.Save();

            await ((IPackageReporter)reporter).OnChecksumFailedAsync(ex);
            return;
        }

        // finishing and cleanup
        MigrateCompatibilityFlags();

        context.AppData.DistributionState.VersionGuid = context.VersionGuid;

        context.AppData.DistributionState.PackageHashes.Clear();

        foreach (var package in context.Manifest)
            context.AppData.DistributionState.PackageHashes.Add(package.Name, package.Signature);

        VersionsCleaner.Run();

        var allPackageHashes = App.Clients.AllStates.SelectMany(state => state.PackageHashes.Values).ToList();

        if (!App.Settings.Prop.DebugDisableVersionPackageCleanup) foreach (string hash in cachedPackageHashes)
        {
            if (allPackageHashes.Contains(hash))
                continue;

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
        context.AppData.DistributionStateManager.Save();

        if (OperatingSystem.IsWindows())
            WindowsRegistry.UpdateEstimatedSize();

        if (context.Client.Target == ClientTarget.Player) ModEngine.InitializeModFolders(context.VersionDirectory, context.ModPaths, context.Client.DisplayName);
        IsInstalling = false;
    }

    private void MigrateCompatibilityFlags()
    {
        string oldClientLocation = Path.Combine(Paths.Versions, context.AppData.DistributionState.VersionGuid, context.AppData.ExecutableName);
        string newClientLocation = Path.Combine(context.VersionDirectory, context.AppData.ExecutableName);

        // move old compatibility flags for the old location
        context.Client.Registration.MigrateCompatibilityFlags(oldClientLocation, newClientLocation);
    }

    private async Task CloseRunningClientAsync()
    {
        await context.Client.CloseAllAsync(new KillRequest { Force = true, WaitForExit = true });
        await context.Client.CloseCrashHandlersAsync();
    }
}
