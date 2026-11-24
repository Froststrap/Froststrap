// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.AppData;
using Froststrap.Bootstrapping;
using Froststrap.Clients;
using Froststrap.Clients.Contracts;
using Froststrap.Clients.Launchers;
using Froststrap.Clients.Model;
using Froststrap.Clients.Sources;
using Froststrap.Mods;
using Froststrap.RobloxInterfaces;
using System.IO.Compression;
using System.ComponentModel;
using System.Data;
using System.Net.Http.Json;
using System.Web;
using Microsoft.Win32;

namespace Froststrap;

internal sealed class Bootstrapper : IDisposable
{
    private const string LockName = "Bootstrapper";

    #region Properties

    private readonly CancellationTokenSource _cancelTokenSource = new();

    private IAppData AppData = default!;
    private Dictionary<string, string> PackageDirectoryMap = null!;
    private LaunchMode _launchMode;

    private string _launchCommandLine = App.LaunchSettings.RobloxLaunchArgs;
    private Version? _latestVersion;
    private string _latestVersionGuid = null!;
    private string _latestVersionDirectory = null!;
    private PackageManifest _versionPackageManifest = null!;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _sizedFromResponse = new();
    private readonly GameJoinData _joinData = null!;
    private readonly JoinPreparation _join;

    private string CurrentDirectory => _latestVersionDirectory ?? AppData.Directory;

    private bool MustUpgrade => UpgradeReason != InstallReason.None;

    private InstallReason UpgradeReason
    {
        get
        {
            if (App.LaunchSettings.ForceFlag.Active || App.State.Prop.ForceReinstall)
                return InstallReason.Forced;

            if (!TargetClient.Capabilities.RequiresDeploymentApi)
                return InstallReason.None;

            if (string.IsNullOrEmpty(AppData.DistributionState.VersionGuid))
                return InstallReason.FirstInstall;

            bool executableExists = AppData.IsBundle
                ? Directory.Exists(AppData.ExecutablePath)
                : File.Exists(AppData.ExecutablePath);

            if (!executableExists)
                return InstallReason.Missing;

            if (TargetClient.Layout.RequiredFiles.Any(file => !File.Exists(Path.Combine(CurrentDirectory, file))))
                return InstallReason.Repair;

            return InstallReason.None;
        }
    }

    private DeploymentInstaller? _installer;


    private bool _noConnection;

    private InterProcessLock? _appLock;
    private int _appPid;
    private bool _disposed;

    public bool IsStudioLaunch => _launchMode != LaunchMode.Player;

    private IRobloxClient TargetClient => IsStudioLaunch ? App.Clients.Studio : App.Clients.Player;

    private DeploymentCdnSource CdnSource => new(TargetClient.Layout);

    private ModPaths CurrentModPaths
    {
        get
        {
            var (contentRoot, clientSettingsRoot) = TargetClient.Layout.ModRoots(_latestVersionDirectory);

            return new ModPaths(
                _latestVersionDirectory,
                contentRoot,
                clientSettingsRoot,
                TargetClient.Capabilities.ModSupport,
                TargetClient.Capabilities.RestoreStrategy);
        }
    }

    public IBootstrapperDialog? Dialog { get; set; }

    private readonly BootstrapperProgress _progress;


    public WatcherData? PendingWatcherData { get; private set; }

    #endregion

    #region Core

    public Bootstrapper(LaunchMode launchMode)
    {
        _launchMode = launchMode;
        _progress = new BootstrapperProgress(() => Dialog, () => AppData.ProductName);

        SetupAppData();

        Deployment.Channel = IsStudioLaunch ? App.Settings.Prop.StudioChannel : App.Settings.Prop.PlayerChannel;

        App.Logger.Info($"Using {(IsStudioLaunch ? "Studio" : "Player")} channel: {Deployment.Channel}");

        _joinData = GameJoin.GetJoinDataByLaunchCommand(_launchCommandLine);
        _join = new JoinPreparation(_joinData, () => Dialog, _progress);
    }

    public void SetStatus(string message) => _progress.SetStatus(message);

    private void SetupAppData() => AppData = IsStudioLaunch ? App.Clients.StudioDeployment : App.Clients.PlayerDeployment;

    private async Task HandleConnectionError(Exception ex)
    {
        _noConnection = true;

        if (App.Settings.Prop.StaticDirectory || !string.IsNullOrEmpty(AppData.DistributionState.VersionGuid))
            _latestVersionDirectory = AppData.Directory;

        App.Logger.Warn($"Connectivity check failed: {ex}");

        string message = Strings.Dialog_Connectivity_BadConnection;

        if (ex is AggregateException)
            ex = ex.InnerException!;

        // https://gist.github.com/pizzaboxer/4b58303589ee5b14cc64397460a8f386
        if (ex is HttpRequestException && ex.InnerException is null)
            message = String.Format(CultureInfo.InvariantCulture, Strings.Dialog_Connectivity_RobloxDown, "[status.roblox.com](https://status.roblox.com)");

        if (MustUpgrade)
            message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeNeeded}\n\n{Strings.Dialog_Connectivity_TryAgainLater}";
        else
            message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeSkip}";

        await Frontend.ShowConnectivityDialog(
            String.Format(CultureInfo.InvariantCulture, Strings.Dialog_Connectivity_UnableToConnect, "Roblox"),
            message,
            MustUpgrade ? MessageBoxImage.Error : MessageBoxImage.Warning,
            ex);

        if (MustUpgrade)
            App.Terminate(ErrorCode.ERROR_CANCELLED);
    }

    public async Task Run()
    {
        App.Logger.Info("Running bootstrapper");
        Dialog?.CancelEnabled = true;

        if (_launchMode == LaunchMode.Player)
            await TargetClient.UpdateAsync(_progress, _cancelTokenSource.Token);

        SetStatus(Strings.Bootstrapper_Status_Connecting);

        if (!TargetClient.Capabilities.RequiresDeploymentApi)
        {
            _noConnection = true;
            _latestVersionDirectory = TargetClient.Install.ContentRoot;
            App.Logger.Info($"{TargetClient.DisplayName} does not use the deployment API, skipping the connectivity check.");
        }
        else
        {
            var connectionResult = await Deployment.InitializeConnectivity();
            App.Logger.Info("Connectivity check finished");

            if (connectionResult is not null)
                await HandleConnectionError(connectionResult);
        }

        // ensure only one instance of the bootstrapper is running at the time
        // so that we don't have stuff like two updates happening simultaneously
        bool lockWasAlreadyHeld = false;

        if (!BackgroundUpdate.IsCurrentProcess)
        {
            _appLock = new InterProcessLock(LockName, TimeSpan.Zero);

            if (!_appLock.IsAcquired)
            {
                lockWasAlreadyHeld = true;

                App.Logger.Warn($"{LockName} instance exists, waiting...");
                SetStatus(Strings.Bootstrapper_Status_WaitingOtherInstances);

                while (!_cancelTokenSource.Token.IsCancellationRequested)
                {
                    _appLock.Dispose();
                    _appLock = new InterProcessLock(LockName, TimeSpan.Zero);
                    if (_appLock.IsAcquired)
                        break;

                    await Task.Delay(500, _cancelTokenSource.Token);
                }

                if (_cancelTokenSource.Token.IsCancellationRequested)
                    return;
            }

            App.Logger.Info("Lock acquired.");
        }

        try
        {
            if (lockWasAlreadyHeld)
            {
                App.Settings.Load();
                App.State.Load();
                AppData.DistributionStateManager.Load();
            }

            if (!_noConnection)
            {
                try
                {
                    await GetLatestVersionInfo();
                }
                catch (Exception ex)
                {
                    await HandleConnectionError(ex);
                }
            }

            VersionsCleaner.Run(); // cleanup after background updater

            bool allModificationsApplied = true;

            if (!_noConnection)
            {
                if (App.RemoteData.LoadedState == GenericTriState.Unknown) // we dont want it to flicker
                    SetStatus(Strings.Bootstrapper_Status_WaitingForData);

                await SetupPackageDictionaries(); // mods also require it

                if (AppData.DistributionState.VersionGuid != _latestVersionGuid || MustUpgrade)
                {
                    if (MustUpgrade)
                        await BackgroundUpdate.StopAsync();

                    if (!BackgroundUpdate.TryDefer(IsStudioLaunch, MustUpgrade, _latestVersion, AppData))
                        await UpgradeRoblox();
                }

                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                // we require deployment details for applying modifications for a worst case scenario,
                // where we'd need to restore files from a package that isn't present on disk and needs to be redownloaded
                allModificationsApplied = await ApplyModifications();
            }
            else if (!TargetClient.Capabilities.RequiresDeploymentApi)
            {
                if (MustUpgrade)
                {
                    App.Logger.Debug($"{TargetClient.DisplayName}: Force reinstall enabled.");

                    if (TargetClient.ResetInstalledPackages())
                        App.State.Prop.ForceReinstall = false;
                }

                PackageDirectoryMap ??= [];
                if (!_cancelTokenSource.IsCancellationRequested)
                    allModificationsApplied = await ApplyModifications();
            }

            // check registry entries for every launch, just in case the stock bootstrapper changes it back
            TargetClient.Registration.Register(_latestVersionDirectory);

            if (IsStudioLaunch)
            {
                App.Logger.Debug("Studio launch detected, syncing RPC plugin...");
                StudioPluginManager.Sync();
            }

            if (App.LaunchSettings.NoLaunchFlag.Active || _cancelTokenSource.IsCancellationRequested)
                return;

            // show tips
            if (!_progress.ExtractionSucceeded)
                Backend.NativeNotify.SendMessage(Strings.Bootstrapper_ExtractionFailed_Title, Strings.Bootstrapper_ExtractionFailed_Message);
            else if (!allModificationsApplied)
                Backend.NativeNotify.SendMessage(Strings.Bootstrapper_ModificationsFailed_Title, Strings.Bootstrapper_ModificationsFailed_Message);

            await LaunchClientAsync();

            Dialog?.CloseBootstrapper();
        }
        finally
        {
            _appLock?.Dispose();
        }
    }

    private void SyncLaunchChannel()
    {
        string launchChannel = Deployment.IsDefaultChannel ? Deployment.DefaultChannel : Deployment.Channel;

        if (!string.IsNullOrEmpty(_launchCommandLine))
        {
            string synced = Regex.Replace(
                _launchCommandLine,
                "channel:[a-zA-Z0-9-_]+",
                $"channel:{launchChannel}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!string.Equals(synced, _launchCommandLine, StringComparison.Ordinal))
            {
                App.Logger.Info($"Launch arguments carried a different channel than the one installed, using '{launchChannel}'");
                _launchCommandLine = synced;
            }
        }

        TargetClient.Channel.Write(Deployment.RobloxDomain, Deployment.IsDefaultChannel ? null : Deployment.Channel);
    }

    private async Task LaunchClientAsync()
    {
        InstallResult installed = await TargetClient.EnsureInstalledAsync(_progress);

        if (!installed.Success)
        {
            await Frontend.ShowMessageBox(installed.Error ?? string.Empty, MessageBoxImage.Error);
            App.Terminate(ErrorCode.ERROR_CANCELLED);
            return;
        }

        if (TargetClient.Capabilities.RequiresDeploymentApi)
        {
            string expectedName = AppData.ExecutableName;
            string expectedPath = Path.Combine((string)AppData.Directory, expectedName);

            if (!Directory.Exists(expectedPath) && !File.Exists(expectedPath))
            {
                App.Logger.Warn($"{expectedName} not found at {expectedPath}, triggering upgrade...");
                await UpgradeRoblox();
            }

            if (!Directory.Exists(expectedPath) && !File.Exists(expectedPath))
                throw new FileNotFoundException($"Roblox application not found at expected path after upgrade: {expectedPath}");

            App.Logger.Info($"Resolved Roblox path: {expectedPath}");
        }

        if (TargetClient.RuntimeEnvironment is { } runtimeEnvironment)
            await runtimeEnvironment.PrepareAsync(_latestVersionDirectory, CurrentModPaths.ClientSettingsRoot, _progress, _cancelTokenSource.Token);

        App.Logger.Info($"Starting {TargetClient.DisplayName}");
        if (_launchMode == LaunchMode.Player)
        {
            _launchCommandLine = await _join.PrepareAsync(_launchCommandLine);

            if (!Deployment.IsDefaultRobloxDomain && string.IsNullOrEmpty(_launchCommandLine))
                _launchCommandLine = "roblox://navigation/home";
        }

        SetStatus(TargetClient.StartingStatus);

        var request = new LaunchRequest
        {
            Mode = _launchMode,
            CommandLine = _launchCommandLine,
            JoinData = _joinData,
            WorkingDirectory = _latestVersionDirectory,
            RunAsAdmin = _launchMode == LaunchMode.Player && ShouldRunAsAdmin()
        };

        if (_launchMode == LaunchMode.StudioAuth && !request.RunAsAdmin)
        {
            await TryLaunchAsync(request);
            return;
        }

        var autoclosePids = new List<int>();
        bool runIntegrations = TargetClient.Capabilities.SupportsCustomIntegrations;

        if (runIntegrations)
            await CustomIntegrationLauncher.LaunchPreLaunchAsync(autoclosePids);

        if (await TryLaunchAsync(request) is not { } launch)
            return;

        _appPid = launch.Pid;

        App.Logger.Debug($"Started {TargetClient.DisplayName} (PID {_appPid}). Launching Watcher...");

        if (runIntegrations && _launchMode == LaunchMode.Player)
            await CustomIntegrationLauncher.LaunchPostLaunchAsync(autoclosePids);

        await PrepareWatcherIfNeededAsync(autoclosePids);

        if (launch.WaitUntilReadyAsync is null)
        {
            // allow for window to show, since the log is created pretty far beforehand
            await Task.Delay(1000);
            return;
        }

        try
        {
            await launch.WaitUntilReadyAsync(_cancelTokenSource.Token);
            App.Logger.Info($"{TargetClient.DisplayName} is running, handing off to the watcher");
        }
        catch (OperationCanceledException)
        {
            App.Logger.Info($"{TargetClient.DisplayName} launch cancelled by user.");
        }
        finally
        {
            Dialog?.CloseBootstrapper();
        }
    }

    private async Task<LaunchResult?> TryLaunchAsync(LaunchRequest request)
    {
        string details = "";

        try
        {
            LaunchResult launch = await TargetClient.LaunchAsync(request, _cancelTokenSource.Token);

            if (launch.Started)
                return launch;

            App.Logger.Error($"Failed to start {TargetClient.DisplayName}");
        }
        catch (OperationCanceledException)
        {
            App.Logger.Info($"{TargetClient.DisplayName} launch cancelled by user.");
            return null;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to launch {TargetClient.DisplayName}! {ex}");
            details = string.IsNullOrWhiteSpace(ex.Message) ? "" : $"\n\n{ex.Message}";
        }

        await Frontend.ShowMessageBox(TargetClient.DescribeLaunchFailure(details), MessageBoxImage.Error);
        App.Terminate(ErrorCode.ERROR_CANCELLED);
        return null;
    }

    private async Task PrepareWatcherIfNeededAsync(List<int> autoclosePids, string? logFileName = null)
    {
        if (!(App.Settings.Prop.EnableActivityTracking
            || App.LaunchSettings.TestModeFlag.Active
            || autoclosePids.Count > 0))
            return;

        if (PendingWatcherData is not null)
        {
            App.Logger.Error("A watcher was already prepared for this launch, ignoring the duplicate");
            return;
        }

        try
        {
            _ = Process.GetProcessById(_appPid);
        }
        catch
        {
            return;
        }

        if (string.IsNullOrEmpty(logFileName))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancelTokenSource.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            try
            {
                logFileName = (await TargetClient.Logs.FindLatestAsync(DateTime.Now.AddSeconds(-5), timeout.Token))?.FullName;
            }
            catch (OperationCanceledException) when (!_cancelTokenSource.IsCancellationRequested)
            {
                App.Logger.Warn("No log file appeared within 30 seconds, the watcher will keep looking");
            }
        }

        PendingWatcherData = new WatcherData
        {
            ProcessId = _appPid,
            LogFile = logFileName,
            AutoclosePids = autoclosePids,
            LaunchMode = _launchMode,
            AccessCode = _joinData.AccessCode
        };

        App.Logger.Info($"Watcher prepared for pid {_appPid} ({_launchMode})");
    }

    private bool ShouldRunAsAdmin() => TargetClient.Registration.RequestsAdmin(AppData.ExecutablePath);

    public bool Cancel()
    {
        if (_join.TrySkipMatchmaking())
            return true;

        if (_cancelTokenSource.IsCancellationRequested)
            return false;

        App.Logger.Info("Cancelling launch...");
        _cancelTokenSource.Cancel();

        Dialog?.CancelEnabled = false;

        if (_installer?.IsInstalling == true)
        {
            try
            {
                TargetClient.Registration.ClearLocation();

                if (Directory.Exists(_latestVersionDirectory))
                    Directory.Delete(_latestVersionDirectory, true);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Could not fully clean up installation! {ex}");
            }
        }
        else if (_appPid != 0)
            TargetClient.Kill(_appPid);

        _appLock?.Dispose();

        Dialog?.CloseBootstrapper();
        App.Terminate(ErrorCode.ERROR_CANCELLED);
        return false;
    }
    #endregion

    #region Roblox Install

    private Task<bool> ApplyModifications() => new ModEngine(
        new ModContext
        {
            ModPaths = CurrentModPaths,
            VersionDirectory = _latestVersionDirectory,
            VersionGuid = _latestVersionGuid,
            Target = TargetClient.Target,
            AppData = AppData,
            PackageDirectoryMap = PackageDirectoryMap
        },
        SetStatus,
        RestoreFromPackagesAsync,
        _cancelTokenSource.Token).ApplyAsync();

    private async Task RestoreFromPackagesAsync(Dictionary<string, List<string>> fileRestoreMap)
    {
        long restoreBytes = fileRestoreMap.Keys
            .Select(name => _versionPackageManifest.Find(x => x.Name == name))
            .Where(p => p is not null)
            .Sum(p => (long)p!.PackedSize);

        _progress.BeginDownloadProgress(restoreBytes);

        foreach (var entry in fileRestoreMap)
        {
            var package = _versionPackageManifest.Find(x => x.Name == entry.Key);
            if (package is not null)
            {
                await DownloadPackage(package);

                SetStatus(Strings.Bootstrapper_Status_ApplyingModifications);
                await ExtractPackage(package, entry.Value);
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _cancelTokenSource?.Dispose();
            _join.Dispose();
            _appLock?.Dispose();
        }

        _disposed = true;
    }
    #endregion

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
        _installer = new DeploymentInstaller(
            new InstallContext
            {
                Client = TargetClient,
                AppData = AppData,
                VersionGuid = _latestVersionGuid,
                VersionDirectory = _latestVersionDirectory,
                Manifest = _versionPackageManifest,
                PackageDirectoryMap = PackageDirectoryMap,
                ModPaths = CurrentModPaths
            },
            PackageStore,
            _progress,
            () => Dialog,
            _cancelTokenSource.Token);

        await _installer.InstallAsync();
    }

    private DeploymentPackages PackageStore => new(
        TargetClient,
        AppData.DistributionStateManager,
        _progress,
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
        VersionResolution resolution = await VersionResolver.ResolveAsync(TargetClient.Target, AppData, _launchCommandLine, _latestVersionGuid, _latestVersion);

        _launchCommandLine = resolution.LaunchCommandLine;
        _latestVersionGuid = resolution.VersionGuid;
        _latestVersion = resolution.Version;

        if (resolution.Stop)
            return;

        SyncLaunchChannel();

        if (App.Settings.Prop.StaticDirectory)
            _latestVersionDirectory = AppData.StaticDirectory;
        else
            _latestVersionDirectory = Path.Combine(Paths.Versions, _latestVersionGuid);

        if (!TargetClient.Capabilities.RequiresDeploymentApi)
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
