// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Win32;
using System.IO.Compression;
using Froststrap.Clients.Model;
using Froststrap.Clients.Sources;
using Froststrap.RobloxInterfaces;

namespace Froststrap;

internal partial class Bootstrapper : IDisposable
{
    private async Task SetupPackageDictionaries()
    {
        if (!TargetClient.Capabilities.RequiresDeploymentApi)
        {
            PackageDirectoryMap = [];
            return;
        }

        PackageDirectoryMap = await CdnSource.GetPackageMapAsync();
    }

    private async Task UpgradeRoblox()
    {
        if (!App.Settings.Prop.UpdateRoblox)
        {
            SetStatus(Strings.Bootstrapper_Status_CancelUpgrade);
            App.Logger.Info("Upgrading disabled, cancelling the upgrade.");

            if (!Directory.Exists(_latestVersionDirectory))
                _ = Frontend.ShowMessageBox(Strings.Bootstrapper_Dialog_NoUpgradeWithoutClient, MessageBoxImage.Warning, MessageBoxButton.OK);
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

        if (TargetClient.Capabilities.RestoreStrategy == RestoreStrategy.VersionBackup)
        {
            string backupDir = Mods.ModEngine.GetResourcesBackupPath(_latestVersionGuid);
            if (Directory.Exists(backupDir))
            {
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

        if (TargetClient.Layout.Shape == ExecutableShape.MacBundle)
        {
            string[] appNames = [App.PlayerData.ExecutableName, App.StudioData.ExecutableName];
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
        AppData.DistributionStateManager.Save();

        if (OperatingSystem.IsWindows())
            WindowsRegistry.UpdateEstimatedSize();

        if (!IsStudioLaunch) InitializeModFolders();
        _isInstalling = false;
    }

    private DeploymentPackages PackageStore => new(
        TargetClient,
        IsStudioLaunch ? App.StudioState : App.PlayerState,
        this,
        _sizedFromResponse,
        _latestVersionGuid,
        _latestVersionDirectory,
        PackageDirectoryMap,
        _cancelTokenSource.Token);

    private Task DownloadPackage(Package package, bool updateProgress = true) => PackageStore.DownloadAsync(package, updateProgress);

    private Task<bool> ExtractPackage(Package package, List<string>? files = null) => PackageStore.ExtractAsync(package, files);

    /// <summary>
    /// Will throw whatever HttpClient can throw
    /// </summary>
    /// <returns></returns>
    private async Task GetLatestVersionInfo()
    {

        // before we do anything, we need to query our channel
        // if it's set in the launch uri, we need to use it and set the registry key for it
        // else, check if the registry key for it exists, and use it

        var match = Regex.Match(
            App.LaunchSettings.RobloxLaunchArgs,
            "channel:([a-zA-Z0-9-_]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        void EnrollChannel(string channel = "production")
        {
            Deployment.Channel = channel;
            if (IsStudioLaunch)
                App.Settings.Prop.StudioChannel = channel;
            else
                App.Settings.Prop.PlayerChannel = channel;
            App.Settings.Save();
        }

        void RevertChannel()
        {
            Deployment.Channel = Deployment.DefaultChannel;
            Deployment.ChannelToken = string.Empty;
            if (IsStudioLaunch)
                App.Settings.Prop.StudioChannel = Deployment.DefaultChannel;
            else
                App.Settings.Prop.PlayerChannel = Deployment.DefaultChannel;
            App.Settings.Save();
        }

#pragma warning disable CA1308
        string enrolledChannel = match.Groups.Count == 2
            ? match.Groups[1].Value.ToLowerInvariant()
            : Deployment.DefaultChannel;
#pragma warning restore CA1308

        bool behindProductionCheck = App.Settings.Prop.ChannelChangeMode == ChannelChangeMode.Prompt;

        // Private channels
        string? tokenChannel = null;

        if (App.Cookies.Loaded)
        {
            UserChannel? userChannel = await Deployment.GetUserChannel(AppData.BinaryType);

            if (
                userChannel?.Token is not null &&
                userChannel.AssignmentType != 1 // might need a change in future
            )
            {
                // prevent roblox from thinking its different channel
                // have to do to prevent issues with channel fflags
                if (!string.IsNullOrEmpty(enrolledChannel))
                    _launchCommandLine = _launchCommandLine.Replace(
                        $"channel:{enrolledChannel}",
                        $"channel:{userChannel.Channel}",
                        StringComparison.OrdinalIgnoreCase);

                Deployment.ChannelToken = userChannel.Token;
                tokenChannel = userChannel.Channel;
                enrolledChannel = userChannel.Channel;
            }
        }

        bool channelFlag = App.LaunchSettings.ChannelFlag.Active && !string.IsNullOrEmpty(App.LaunchSettings.ChannelFlag.Data);

        if (!channelFlag)
        {
            switch (App.Settings.Prop.ChannelChangeMode)
            {
                case ChannelChangeMode.Automatic:
                    App.Logger.Info("Enrolling into channel");
                    EnrollChannel(enrolledChannel);
                    break;

                case ChannelChangeMode.Prompt:
                    App.Logger.Debug("Prompting channel enrollment");

                    if (!match.Success || match.Groups.Count != 2 || string.Equals(match.Groups[1].Value, Deployment.Channel, StringComparison.OrdinalIgnoreCase))
                    {
                        App.Logger.Warn("Channel is either equal or incorrectly formatted");
                        break;
                    }

                    string displayChannel = !String.IsNullOrEmpty(match.Groups[1].Value)
                        ? match.Groups[1].Value
                        : Deployment.DefaultChannel;

                    var promptResult = await Frontend.ShowMessageBox(
                        String.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Dialog_PromptChannelChange, displayChannel, Deployment.Channel),
                        MessageBoxImage.Question,
                        MessageBoxButton.YesNo
                    );

                    if (promptResult == MessageBoxResult.Yes)
                        EnrollChannel(enrolledChannel);
                    break;

                case ChannelChangeMode.Ignore:
                    App.Logger.Debug("Ignoring channel enrollment");
                    break;
            }
        }
        else
        {
            string channelFlagData = App.LaunchSettings.ChannelFlag.Data!;
            if (String.IsNullOrEmpty(channelFlagData))
                return;

            App.Logger.Debug($"Forcing channel {channelFlagData}");
            EnrollChannel(channelFlagData);
        }

        if (!string.IsNullOrEmpty(Deployment.ChannelToken)
            && !string.Equals(Deployment.Channel, tokenChannel, StringComparison.OrdinalIgnoreCase))
        {
            App.Logger.Info($"Dropping channel token for '{tokenChannel}' because the active channel is '{Deployment.Channel}'");
            Deployment.ChannelToken = string.Empty;
        }

        bool overrideUsed = false;

        if (IsStudioLaunch && App.Settings.Prop.StudioVersionOverrideEnabled && !string.IsNullOrEmpty(App.Settings.Prop.StudioVersionOverrideHash))
        {
            _latestVersionGuid = App.Settings.Prop.StudioVersionOverrideHash.Trim();
            App.Logger.Debug($"Studio version override active: pinned to {_latestVersionGuid}");
            overrideUsed = true;
        }
        else if (!IsStudioLaunch && App.Settings.Prop.PlayerVersionOverrideEnabled && !string.IsNullOrEmpty(App.Settings.Prop.PlayerVersionOverrideHash))
        {
            string overrideHash = App.Settings.Prop.PlayerVersionOverrideHash.Trim();
            var (valid, error) = await UI.ViewModels.Settings.ChannelViewModel.ValidateHashCore(overrideHash, true, AppData.BinaryType);

            if (valid)
            {
                _latestVersionGuid = overrideHash;
                App.Logger.Debug($"Player version override active: pinned to {_latestVersionGuid}");
                overrideUsed = true;
            }
            else
            {
                App.Logger.Warn($"Player version override invalid: {error}. Falling back to channel.");

                await Frontend.ShowMessageBox(
                    string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_InvalidOverride, overrideHash, error),
                    MessageBoxImage.Warning,
                    MessageBoxButton.OK
                );

                App.Settings.Prop.PlayerVersionOverrideEnabled = false;
                App.Settings.Prop.PlayerVersionOverrideHash = string.Empty;
                App.Settings.Save();
            }
        }

        if (!overrideUsed && (!App.LaunchSettings.VersionFlag.Active || string.IsNullOrEmpty(App.LaunchSettings.VersionFlag.Data)))
        {
            ClientVersion clientVersion;

            try
            {
                clientVersion = await Deployment.GetInfo(Deployment.Channel, behindProductionCheck, false, AppData.BinaryType);
            }
            catch (InvalidChannelException ex)
            {
                // If channel does not exist
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    App.Logger.Warn($"Reverting enrolled channel to {Deployment.DefaultChannel} because a WindowsPlayer build does not exist for {Deployment.Channel}");
                }
                // If channel is not available to the user (private/internal release channel)
                else if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    App.Logger.Warn($"Reverting enrolled channel to {Deployment.DefaultChannel} because {Deployment.Channel} is restricted for public use.");

                    // Only prompt if user has channel switching mode set to something other than Automatic.
                    if (App.Settings.Prop.ChannelChangeMode != ChannelChangeMode.Automatic)
                    {
                        await Frontend.ShowMessageBox(
                            String.Format(CultureInfo.InvariantCulture,
                                Strings.Bootstrapper_Dialog_UnauthorizedChannel,
                                Deployment.Channel,
                                Deployment.DefaultChannel
                            ),
                            MessageBoxImage.Information
                        );
                    }
                }
                else
                {
                    throw;
                }

                RevertChannel();
                clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel, behindProductionCheck, false, AppData.BinaryType);
            }

            if (clientVersion.IsBehindDefaultChannel && App.Settings.Prop.ChannelChangeMode == ChannelChangeMode.Prompt)
            {
                MessageBoxResult action = await Frontend.ShowMessageBox(
                        String.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Dialog_ChannelOutOfDate, Deployment.Channel, Deployment.DefaultChannel),
                        MessageBoxImage.Warning,
                        MessageBoxButton.YesNo
                    );

                if (action == MessageBoxResult.Yes)
                {
                    App.Logger.Debug($"Changed Roblox channel from {Deployment.Channel} to {Deployment.DefaultChannel}");

                    RevertChannel();
                    clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel, behindProductionCheck: false, binaryTypeOverride: AppData.BinaryType);
                }
            }

            _latestVersionGuid = clientVersion.VersionGuid;
            _latestVersion = Utility.Versioning.ParseVersionSafe(clientVersion.Version);
        }
        else if (!overrideUsed)
        {
            string? versionData = App.LaunchSettings.VersionFlag.Data;
            if (string.IsNullOrEmpty(versionData))
            {
                App.Logger.Error("VersionFlag.Data was unexpectedly null or empty. Falling back to default channel.");
                var fallbackInfo = await Deployment.GetInfo(Deployment.DefaultChannel, false, false, AppData.BinaryType);
                _latestVersionGuid = fallbackInfo.VersionGuid;
                _latestVersion = Utility.Versioning.ParseVersionSafe(fallbackInfo.Version);
            }
            else
            {
                App.Logger.Debug($"Version set to {versionData} from arguments");
                _latestVersionGuid = versionData;
            }
        }

        SyncLaunchChannel();

        if (App.Settings.Prop.StaticDirectory)
            _latestVersionDirectory = AppData.StaticDirectory;
        else
            _latestVersionDirectory = Path.Combine(Paths.Versions, _latestVersionGuid);

        // Mods are applied directly into Sober's asset_overlay directory instead of a versioned folder.
        if (TargetClient.Layout.Shape == ExecutableShape.ExternalManaged)
            _latestVersionDirectory = TargetClient.Install.ContentRoot;

        _versionPackageManifest = await CdnSource.GetPackageManifestAsync(_latestVersionGuid);

        // this can happen if version is set through arguments
        if (_launchMode == LaunchMode.Unknown)
        {
            App.Logger.Info("Identifying launch mode from package manifest");

            bool isPlayer = _versionPackageManifest.Exists(x => x.Name == "RobloxApp.zip" || x.Name == "RobloxPlayer.zip");
            App.Logger.Info($"isPlayer={isPlayer}");

            _launchMode = isPlayer ? LaunchMode.Player : LaunchMode.Studio;
            SetupAppData(); // we need to set it up again
        }
    }
}
