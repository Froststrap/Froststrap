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

namespace Froststrap;

internal partial class Bootstrapper : IDisposable
{
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
            else if (OperatingSystem.IsLinux())
            {
                if (MustUpgrade)
                {
                    App.Logger.Debug("Linux: Force reinstall enabled.");

                    string clientPackagePath = Path.Combine(Paths.Versions, "Sober", "data", "sober", "packages", "x86_64", "com.roblox.client");

                    try
                    {
                        if (Directory.Exists(clientPackagePath))
                        {
                            DirectoryInfo di = new(clientPackagePath);

                            foreach (FileInfo file in di.GetFiles())
                                file.Delete();

                            foreach (DirectoryInfo dir in di.GetDirectories())
                                dir.Delete(true);

                            App.State.Prop.ForceReinstall = false;

                            App.Logger.Debug($"Successfully cleared contents of {clientPackagePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"Failed to purge packages: {ex.Message}");
                    }
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
        processesToKill.AddRange(Process.GetProcessesByName("RobloxCrashHandler"));

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
}
