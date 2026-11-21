// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.AppData;
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

internal sealed class Bootstrapper : IPackageReporter, IDisposable
{
    private const int ProgressBarMaximum = 10000;
    private const double TaskbarProgressMaximum = 1.0;
    private const string SoberFlatpakId = Clients.Runtimes.SoberClient.FlatpakId;
    public const string BackgroundUpdaterLockName = "BackgroundUpdater";

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

    private static bool AutomaticallyUpdateSober => App.Clients.Player.Layout.Shape == ExecutableShape.ExternalManaged && App.Settings.Prop.AutomaticallyUpdateSober;
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

    private bool _isInstalling;
    private bool _packageExtractionSuccess = true;

    private bool _matchmakingInProgress;
    private volatile bool _skipMatchmaking;
    private CancellationTokenSource? _matchmakingCts;

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

    public string LockName { get; set; } = "Bootstrapper";

    public bool QuitIfLockExists { get; set; }

    public WatcherData? PendingWatcherData { get; private set; }

    #endregion

    #region Core

    public Bootstrapper(LaunchMode launchMode)
    {
        _launchMode = launchMode;

        SetupAppData();

        Deployment.Channel = IsStudioLaunch ? App.Settings.Prop.StudioChannel : App.Settings.Prop.PlayerChannel;

        App.Logger.Info($"Using {(IsStudioLaunch ? "Studio" : "Player")} channel: {Deployment.Channel}");

        _joinData = GameJoin.GetJoinDataByLaunchCommand(_launchCommandLine);
    }

    private void SetupAppData() => AppData = IsStudioLaunch ? App.StudioData : App.PlayerData;

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

        if (AutomaticallyUpdateSober && _launchMode == LaunchMode.Player)
            await UpdateSoberFlatpakAsync();

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
        _appLock = new InterProcessLock(LockName, TimeSpan.Zero);

        if (!_appLock.IsAcquired)
        {
            lockWasAlreadyHeld = true;

            if (QuitIfLockExists)
            {
                App.Logger.Info($"{LockName} instance exists, exiting!");
                return;
            }

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
                    bool backgroundUpdaterLockOpen;
                    using (var checkLock = new InterProcessLock(BackgroundUpdaterLockName, TimeSpan.Zero))
                    {
                        backgroundUpdaterLockOpen = !checkLock.IsAcquired;
                    }

                    if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
                        backgroundUpdaterLockOpen = false; // we want to actually update lol

                    App.Logger.Debug($"Background updater running: {backgroundUpdaterLockOpen}");

                    if (backgroundUpdaterLockOpen && MustUpgrade)
                    {
                        // I am Forced Upgrade, killer of Background Updates
                        Utility.Processes.KillBackgroundUpdater();
                        backgroundUpdaterLockOpen = false;
                    }

                    if (!backgroundUpdaterLockOpen)
                    {
                        if (BackgroundUpdatePolicy.IsEligible(IsStudioLaunch, MustUpgrade, _latestVersion, AppData))
                            StartBackgroundUpdater();
                        else
                            await UpgradeRoblox();
                    }
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
            if (!_packageExtractionSuccess)
                Backend.NativeNotify.SendMessage(Strings.Bootstrapper_ExtractionFailed_Title, Strings.Bootstrapper_ExtractionFailed_Message);
            else if (!allModificationsApplied)
                Backend.NativeNotify.SendMessage(Strings.Bootstrapper_ModificationsFailed_Title, Strings.Bootstrapper_ModificationsFailed_Message);

            if (TargetClient.RuntimeEnvironment is not null)
                await LaunchStudioViaWineAsync();
            else if (TargetClient.Layout.Shape != ExecutableShape.ExternalManaged)
                await StartRoblox();
            else
            {
                if (!await EnsureSoberInstalledAsync())
                    return;
                await LaunchSober([]);
            }

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

    private async Task PrepareJoinAsync()
    {
        // reapplied on every launch because roblox can reset the file on its own.
        AppStorageManager.Apply();

        if (_joinData.JoinType == GameJoinType.Unknown)
            App.Logger.Warn("Unable to get join data");

        App.Logger.Info($"Join Type: {_joinData.JoinType}");
        App.Logger.Info($"Join Origin: {_joinData.JoinOrigin ?? "null"}");
        App.Logger.Info($"Place ID: {_joinData.PlaceId?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
        App.Logger.Info($"Job ID: {_joinData.JobId ?? "null"}");
        App.Logger.Info($"Access Code: {_joinData.AccessCode ?? "null"}");

        if (_launchCommandLine.StartsWith("roblox://", StringComparison.Ordinal))
        {
            App.Logger.Info("Joining through roblox:// URI, skipping Better Matchmaking");
            return;
        }

        bool isFollowUser = false;

        // _joinData.JoinType == GameJoinType.RequestFollowUser just doesnt work at all
        // idk why they dont use it when the user is following a friend, but ok
        if (App.Settings.Prop.EnableBetterMatchmaking &&
            (_joinData.JoinOrigin == "friendServerListJoin" || _joinData.JoinOrigin == "placesListInHomePage"))
        {
            App.Logger.Debug("User is trying to join a friend, showing dialog");

            var result = await Frontend.ShowMessageBox(
                String.Format(CultureInfo.InvariantCulture, Strings.Menu_Bootstrapper_Experimental_BetterMatchmaking_FollowUser),
                MessageBoxImage.Question,
                MessageBoxButton.YesNo
            );

            isFollowUser = result == MessageBoxResult.Yes;
        }

        string? serverid = null;
        bool matchmakingCancelled = false;

        _matchmakingInProgress = true;
        _skipMatchmaking = false;
        _matchmakingCts = new CancellationTokenSource();

        Dialog?.CancelButtonText = Strings.Bootstrapper_CancelButton_Skip;

        try
        {
            if (App.Settings.Prop.EnableBetterMatchmaking &&
                _joinData.JoinType == GameJoinType.RequestGame &&
                _joinData.PlaceId != null &&
                !isFollowUser)
            {
                if (_skipMatchmaking)
                {
                    App.Logger.Info("Matchmaking was skipped due to user cancellation.");
                    matchmakingCancelled = true;
                }
                else
                {
                    serverid = await Integrations.BetterMatchmaking.FindServerAsync((long)_joinData.PlaceId, SetStatus, _matchmakingCts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            App.Logger.Info("Better Matchmaking was skipped, joining original server.");
            matchmakingCancelled = true;
        }
        catch (HttpRequestException ex)
        {
            _ = Frontend.ShowConnectivityDialog(
                String.Format(CultureInfo.InvariantCulture, Strings.Dialog_Connectivity_UnableToConnect, "rovalra.com"),
                Strings.Dialog_Connectivity_MatchmakingFailed,
                MessageBoxImage.Warning,
                ex
            );
        }
        finally
        {
            Dialog?.CancelButtonText = Strings.Common_Cancel;
            _matchmakingInProgress = false;
            _matchmakingCts?.Dispose();
            _matchmakingCts = null;
        }

        if (!matchmakingCancelled && !string.IsNullOrEmpty(serverid) && _joinData.PlaceId is not null)
        {
            string placeLauncherUrl = UrlBuilder.BuildPlacelauncherUrl((long)_joinData.PlaceId, serverid);
            _launchCommandLine = _launchCommandLine.Replace(_joinData.PlaceLauncherUrl, HttpUtility.UrlEncode(placeLauncherUrl), StringComparison.Ordinal);
            _joinData.JobId = serverid;
        }
    }

    private async Task StartRoblox()
    {
        App.Logger.Info("Starting Roblox");
        if (_launchMode == LaunchMode.Player)
        {
            await PrepareJoinAsync();

            if (!Deployment.IsDefaultRobloxDomain && string.IsNullOrEmpty(_launchCommandLine))
                _launchCommandLine = "roblox://navigation/home";
        }

        SetStatus(Strings.Bootstrapper_Status_Starting);

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

        var request = new LaunchRequest
        {
            Mode = _launchMode,
            CommandLine = _launchCommandLine,
            RunAsAdmin = _launchMode == LaunchMode.Player && ShouldRunAsAdmin()
        };

        if (_launchMode == LaunchMode.StudioAuth && !request.RunAsAdmin)
        {
            await TargetClient.LaunchAsync(request);
            return;
        }

        var autoclosePids = new List<int>();

        // the code you're gonna read ahead is horrible. sorry for the hack, but it works ¯\_(ツ)_/¯
        // check if prelaunch is checked
        foreach (var integration in App.Settings.Prop.CustomIntegrations)
        {
            if (integration?.PreLaunch == true)
                await LaunchIntegrationAsync(integration, autoclosePids);
        }

        LaunchResult launch = await TargetClient.LaunchAsync(request);

        if (!launch.Started)
            return;

        _appPid = launch.Pid;

        App.Logger.Debug($"Started Roblox (PID {_appPid}). Launching Watcher...");

        if (!IsStudioLaunch)
        {
            // launch custom integrations now if normal roblox
            foreach (var integration in App.Settings.Prop.CustomIntegrations)
            {
                if (integration == null || integration.PreLaunch || integration.SpecifyGame)
                    continue;

                await LaunchIntegrationAsync(integration, autoclosePids);
            }
        }

        await PrepareWatcherIfNeededAsync(autoclosePids);

        // allow for window to show, since the log is created pretty far beforehand
        await Task.Delay(1000);
    }

    private async Task LaunchSober(List<int> autoclosePids)
    {
        await PrepareJoinAsync();

        string? soberUri = GameJoin.BuildSoberUri(_joinData);
        if (soberUri is not null)
        {
            App.Logger.Info($"Translating launch command for Sober: {soberUri}");
            _launchCommandLine = soberUri;
        }

        SetStatus(Strings.Bootstrapper_Status_StartingSober);

        try
        {
            LaunchResult launch = await TargetClient.LaunchAsync(
                new LaunchRequest { Mode = _launchMode, CommandLine = _launchCommandLine },
                _cancelTokenSource.Token);

            _appPid = launch.Pid;
            App.Logger.Debug("Launching Watcher...");
            await PrepareWatcherIfNeededAsync(autoclosePids);

            try
            {
                if (launch.WaitUntilReadyAsync is not null)
                    await launch.WaitUntilReadyAsync(_cancelTokenSource.Token);
            }
            finally
            {
                Dialog?.CloseBootstrapper();
            }

            App.Logger.Info("Sober is running, handing off to the watcher");
        }
        catch (OperationCanceledException)
        {
            App.Logger.Info("Sober launch cancelled by user.");
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to launch Sober via flatpak! {ex}");
            string detailsPart = string.IsNullOrWhiteSpace(ex.Message) ? "" : $"\n\n{ex.Message}";
            await Frontend.ShowMessageBox(
                string.Format(CultureInfo.InvariantCulture, Strings.Sober_LaunchFailed, SoberFlatpakId, detailsPart),
                MessageBoxImage.Error
            );
            App.Terminate(ErrorCode.ERROR_CANCELLED);
        }
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

    private static async Task LaunchIntegrationAsync(CustomIntegration integration, List<int> autoclosePids)
    {
        App.Logger.Info($"Launching custom integration '{integration.Name}' ({integration.Location} {integration.LaunchArgs} - autoclose is {integration.AutoClose})");

        int pid = 0;

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = integration.Location,
                Arguments = integration.LaunchArgs.Replace("\r\n", " ", StringComparison.Ordinal),
                WorkingDirectory = Path.GetDirectoryName(integration.Location),
                UseShellExecute = true
            })!;

            pid = process.Id;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to launch integration '{integration.Name}'! {ex.Message}");
        }

        if (integration.AutoClose && pid != 0)
            autoclosePids.Add(pid);

        if (integration.Delay != 0)
            await Task.Delay(integration.Delay);
    }

    private bool ShouldRunAsAdmin() => TargetClient.Registration.RequestsAdmin(AppData.ExecutablePath);

    public bool Cancel()
    {
        if (_matchmakingInProgress)
        {
            App.Logger.Info("Skipping Better MatchMaking.");
            _skipMatchmaking = true;
            _matchmakingCts?.Cancel();
            SetStatus(Strings.Bootstrapper_Status_SkippingMatchmaking);
            return true;
        }

        if (_cancelTokenSource.IsCancellationRequested)
            return false;

        App.Logger.Info("Cancelling launch...");
        _cancelTokenSource.Cancel();

        Dialog?.CancelEnabled = false;

        if (_isInstalling)
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
        {
            try
            {
                using var process = Process.GetProcessById(_appPid);
                process.Kill();
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Failed to kill process {_appPid}: {ex.Message}");
            }
        }

        if (App.Clients.Player.Layout.Shape == ExecutableShape.ExternalManaged)
        {
            try
            {
                foreach (var clientProcess in App.Clients.Player.Processes.FindAllPlayer())
                {
                    try
                    {
                        App.Logger.Info($"Killing {clientProcess.ProcessName} process (PID {clientProcess.Id})");
                        clientProcess.Kill(true);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"Failed to kill {clientProcess.ProcessName} process (PID {clientProcess.Id}) {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to enumerate {App.Clients.Player.DisplayName} processes. {ex}");
            }
        }

        _appLock?.Dispose();

        Dialog?.CloseBootstrapper();
        App.SoftTerminate(ErrorCode.ERROR_CANCELLED);
        return false;
    }
    #endregion

    #region Roblox Install

    private void MigrateCompatibilityFlags()
    {
        string oldClientLocation = Path.Combine(Paths.Versions, AppData.DistributionState.VersionGuid, AppData.ExecutableName);
        string newClientLocation = Path.Combine(_latestVersionDirectory, AppData.ExecutableName);

        // move old compatibility flags for the old location
        TargetClient.Registration.MigrateCompatibilityFlags(oldClientLocation, newClientLocation);
    }

    private static void KillRobloxPlayers()
    {
        var processesToKill = new List<Process>();
        processesToKill.AddRange(App.Clients.Player.Processes.FindAllPlayer());
        processesToKill.AddRange(App.Clients.Player.Processes.FindCrashHandlers());

        foreach (Process process in processesToKill)
        {
            try
            {
                process.Kill();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"Failed to close process {process.Id}");
            }
        }
    }

    private static FlatpakSource SoberSource => new(SoberFlatpakId);

    private async Task<bool> EnsureSoberInstalledAsync()
    {
        SetStatus(Strings.Bootstrapper_Status_CheckingFlatpak);

        try
        {
            await FlatpakSource.EnsureFlatpakAvailableAsync();
        }
        catch (TimeoutException ex)
        {
            App.Logger.Error(ex, "Timed out while checking Flatpak installation.");
            await Frontend.ShowMessageBox(
                "Timed out while checking Flatpak installation. Please make sure Flatpak is working and try again.",
                MessageBoxImage.Error
            );
            App.Terminate(ErrorCode.ERROR_CANCELLED);
            return false;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Flatpak not found.");
            await Frontend.ShowMessageBox(
                "Flatpak is required on Linux.\n\nPlease install Flatpak first, then launch Froststrap again.",
                MessageBoxImage.Error
            );
            App.Terminate(ErrorCode.ERROR_CANCELLED);
            return false;
        }

        if (await SoberSource.IsInstalledAsync())
        {
            App.Logger.Info("Sober is already installed.");
            return true;
        }

        App.Logger.Info("Installing Sober...");

        if (Dialog is not null)
        {
            Dialog.ProgressIndeterminate = true;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
        }

        SetStatus(Strings.Bootstrapper_Status_InstallingSober);

        InstallResult result = await SoberSource.InstallAsync(
            TargetClient.Install,
            new InlineProgress<InstallProgress>(step => SetStatus(step.Message)));

        if (!result.Success)
        {
            if (result.Error is null)
            {
                await Frontend.ShowMessageBox(
                    "Failed to start Sober installation via Flatpak.",
                    MessageBoxImage.Error
                );
            }
            else
            {
                string detailsPart = string.IsNullOrWhiteSpace(result.Error) ? "" : $"\n\n{result.Error}";
                string message = string.Format(CultureInfo.InvariantCulture, Strings.Sober_FlatpakInstallFailed, SoberFlatpakId, detailsPart);
                await Frontend.ShowMessageBox(message, MessageBoxImage.Error);
            }

            App.Terminate(ErrorCode.ERROR_CANCELLED);
            return false;
        }

        App.Logger.Info("Sober installation complete.");
        SetStatus(Strings.Bootstrapper_Status_StartingSober);
        return true;
    }

    private async Task UpdateSoberFlatpakAsync()
    {
        SetStatus(Strings.Bootstrapper_Status_UpdatingSober);

        if (Dialog is not null)
        {
            Dialog.ProgressIndeterminate = false;
            Dialog.ProgressMaximum = 100;
            Dialog.ProgressValue = 0;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;
            Dialog.TaskbarProgressValue = 0.0;
        }

        try
        {
            await SoberSource.UpdateAsync(new InlineProgress<InstallProgress>(OnSoberUpdateProgress), _cancelTokenSource.Token);
        }
        finally
        {
            if (Dialog is not null)
            {
                Dialog.ProgressIndeterminate = true;
                Dialog.ProgressValue = 0;
                Dialog.TaskbarProgressValue = 0.0;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.None;
            }
        }
    }

    private void OnSoberUpdateProgress(InstallProgress progress)
    {
        if (progress.Phase == InstallPhase.Finalizing)
        {
            if (Dialog is not null)
            {
                Dialog.ProgressValue = 100;
                Dialog.TaskbarProgressValue = 1.0;
                SetStatus(Strings.Bootstrapper_Status_SoberUpdateComplete);
            }

            return;
        }

        if (progress.Step <= 0)
        {
            SetStatus(progress.Message);
            return;
        }

        if (Dialog is null)
            return;

        Dialog.ProgressValue = (int)progress.Current;
        Dialog.TaskbarProgressValue = progress.Current / 100.0;

        SetStatus(progress.StepPercent >= 0
            ? string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_UpdatingSoberProgress, progress.Step, progress.StepCount, progress.StepPercent)
            : string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_UpdatingSoberBasic, progress.Step, progress.StepCount));
    }

    private async Task LaunchStudioViaWineAsync()
    {
        if (TargetClient.RuntimeEnvironment is { } runtimeEnvironment)
            await runtimeEnvironment.PrepareAsync(_latestVersionDirectory, CurrentModPaths.ClientSettingsRoot, this, _cancelTokenSource.Token);

        SetStatus(Strings.Bootstrapper_Status_Starting);

        var autoclosePids = new List<int>();
        foreach (var integration in App.Settings.Prop.CustomIntegrations)
            if (integration?.PreLaunch == true)
                await LaunchIntegrationAsync(integration, autoclosePids);

        LaunchResult launch = await TargetClient.LaunchAsync(new LaunchRequest
        {
            Mode = _launchMode,
            CommandLine = _launchCommandLine,
            WorkingDirectory = _latestVersionDirectory
        });

        if (!launch.Started)
        {
            await Frontend.ShowMessageBox("Could not start Roblox Studio. Please check your Wine installation.", MessageBoxImage.Error);
            App.Terminate(ErrorCode.ERROR_CANCELLED);
            return;
        }

        _appPid = launch.Pid;

        foreach (var integration in App.Settings.Prop.CustomIntegrations)
            if (integration != null && !integration.PreLaunch && !integration.SpecifyGame)
                await LaunchIntegrationAsync(integration, autoclosePids);

        await PrepareWatcherIfNeededAsync(autoclosePids);

        await Task.Delay(1000);
    }

    public async Task DownloadFileWithProgressAsync(string url, string destination)
    {
        using var response = await App.HttpClient.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1;
        if (totalBytes <= 0)
            throw new InvalidOperationException("Unable to determine file size for progress reporting.");

        _totalDownloadedBytes = 0;
        _progressIncrement = (double)ProgressBarMaximum / totalBytes;
        _taskbarProgressIncrement = TaskbarProgressMaximum / totalBytes;

        if (Dialog != null)
        {
            Dialog.ProgressIndeterminate = false;
            Dialog.ProgressMaximum = ProgressBarMaximum;
            Dialog.ProgressValue = 0;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;
            Dialog.TaskbarProgressValue = 0.0;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalRead += bytesRead;
            Interlocked.Add(ref _totalDownloadedBytes, bytesRead);
            UpdateProgressBar(false);
        }

        if (totalRead != totalBytes)
            throw new IOException($"Downloaded {totalRead} bytes but expected {totalBytes}");
    }

    private static void StartBackgroundUpdater()
    {
        using var checkLock = new InterProcessLock(BackgroundUpdaterLockName, TimeSpan.Zero);
        if (!checkLock.IsAcquired)
        {
            App.Logger.Info("Background updater already running");
            return;
        }
        checkLock.Dispose();

        App.Logger.Info("Starting background updater");
        Process.Start(Paths.Process, "--background-updater");
    }

    private Task<bool> ApplyModifications() => new ModEngine(
        new ModContext
        {
            ModPaths = CurrentModPaths,
            VersionDirectory = _latestVersionDirectory,
            VersionGuid = _latestVersionGuid,
            IsStudio = IsStudioLaunch,
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

        BeginDownloadProgress(restoreBytes);

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

    private void InitializeModFolders() => ModEngine.InitializeModFolders(_latestVersionDirectory, CurrentModPaths, TargetClient.DisplayName);

    private void BeginDownloadProgress(long totalBytes)
    {
        Interlocked.Exchange(ref _totalDownloadedBytes, 0);
        Interlocked.Exchange(ref _totalPackagedBytes, Math.Max(0, totalBytes));
        RecalculateProgressIncrements();

        if (Dialog is null)
            return;

        Dialog.ProgressIndeterminate = false;
        Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;
        Dialog.ProgressMaximum = ProgressBarMaximum;
        Dialog.ProgressValue = 0;
        Dialog.TaskbarProgressValue = 0.0;
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
            _matchmakingCts?.Dispose();
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

        VersionsCleaner.Run();

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

    public IBootstrapperDialog? Dialog { get; set; }

    private double _progressIncrement;
    private double _taskbarProgressIncrement;
    private double _taskbarProgressMaximum;
    private long _totalDownloadedBytes;
    private long _totalPackagedBytes;

    void IInstallReporter.SetIndeterminate(bool includeTaskbar)
    {
        Dialog?.ProgressIndeterminate = true;

        if (includeTaskbar)
            Dialog?.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
    }

    Task IInstallReporter.DownloadAsync(string url, string destination) => DownloadFileWithProgressAsync(url, destination);

    void IPackageReporter.AddDownloaded(long bytes, bool updateProgressBar)
    {
        Interlocked.Add(ref _totalDownloadedBytes, bytes);

        if (updateProgressBar)
            UpdateProgressBar();
    }

    void IPackageReporter.AddTotal(long bytes) => AddProgressTotal(bytes);

    void IPackageReporter.MarkExtractionFailed() => _packageExtractionSuccess = false;

    async Task IPackageReporter.OnChecksumFailedAsync(Exception exception)
    {
        await Frontend.ShowConnectivityDialog(
            Strings.Dialog_Connectivity_UnableToDownload,
            String.Format(CultureInfo.InvariantCulture, Strings.Dialog_Connectivity_UnableToDownloadReason, "[https://github.com/bloxstraplabs/bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox](https://github.com/bloxstraplabs/bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox)"),
            MessageBoxImage.Error,
            exception
        );

        App.Terminate(ErrorCode.ERROR_CANCELLED);
    }

    public void SetStatus(string message)
    {
        message = message.Replace("{product}", AppData.ProductName, StringComparison.Ordinal);
        Dialog?.Message = message;
    }

    private void UpdateProgressBar(bool updateStatus = true)
    {
        if (Dialog is null) return;

        long current = Math.Max(0, Interlocked.Read(ref _totalDownloadedBytes));
        long total = Interlocked.Read(ref _totalPackagedBytes);

        if (updateStatus && total > 0)
        {
            SetStatus(string.Format(CultureInfo.InvariantCulture,
                Strings.Bootstrapper_Status_DownloadingPackages,
                FormatBytes(Math.Min(current, total)), FormatBytes(total)));
        }

        if (double.IsFinite(_progressIncrement))
        {
            int progressValue = (int)Math.Floor(_progressIncrement * current);
            progressValue = Math.Clamp(progressValue, 0, ProgressBarMaximum);
            Dialog.ProgressValue = progressValue;
        }

        if (double.IsFinite(_taskbarProgressIncrement))
        {
            double taskbarProgressValue = _taskbarProgressIncrement * current;
            taskbarProgressValue = Math.Clamp(taskbarProgressValue, 0, _taskbarProgressMaximum);
            Dialog.TaskbarProgressValue = taskbarProgressValue;
        }
    }

    private void RecalculateProgressIncrements()
    {
        long total = Interlocked.Read(ref _totalPackagedBytes);

        _taskbarProgressMaximum = TaskbarProgressMaximum;

        if (total <= 0)
        {
            _progressIncrement = 0;
            _taskbarProgressIncrement = 0;
            return;
        }

        _progressIncrement = (double)ProgressBarMaximum / total;
        _taskbarProgressIncrement = _taskbarProgressMaximum / total;
    }

    private void AddProgressTotal(long bytes)
    {
        if (bytes <= 0) return;

        Interlocked.Add(ref _totalPackagedBytes, bytes);
        RecalculateProgressIncrements();
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
