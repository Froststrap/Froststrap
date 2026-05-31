// To debug the automatic updater:
// - Uncomment the definition below
// - Publish the executable
// - Launch the executable (click no when it asks you to upgrade)
// - Launch Roblox (for testing web launches, run it from the command prompt)
// - To re-test the same executable, delete it from the installation folder

// Brother why does this file have both core AND UI logic in it
// TODO: Split this file into Core and UI parts

// #define DEBUG_UPDATER

#if DEBUG_UPDATER
#warning "Automatic updater debugging is enabled"
#endif

using Froststrap.AppData;
using Froststrap.Models.APIs;
using Froststrap.RobloxInterfaces;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.Web;

namespace Froststrap
{
    public class Bootstrapper
    {
        #region Constants

        private const int ProgressBarMaximum = 10000;
        private const double TaskbarProgressMaximum = 1.0;
        private const int DownloadBufferSize = 4096;
        private const int MaxDownloadAttempts = 5;
        private const string SoberFlatpakId = "org.vinegarhq.Sober";
        private const string BackgroundUpdaterMutexName = "Bloxstrap-BackgroundUpdater";
        private const string WinePrefixDir = "wineprefix";
        private const string KombuchaRepoOwner = "vinegarhq";
        private const string KombuchaRepoName = "kombucha";
        private const string DxvkVersion = "2.7.1";
        private const string DxvkSarekVersion = "1.11.0-async";
        private const string DxvkSarekUrl = "https://github.com/Fartopblu/DXVK-Sarek/releases/download/1.11.0-async/dxvk-sarek-1.11.0-async.tar.gz";
        private const string KombuchaWineDir = "wine";
        private const string KombuchaSymlink = "kombucha";

        private const string AppSettings =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
            "<Settings>\r\n" +
            "	<ContentFolder>content</ContentFolder>\r\n" +
            "	<BaseUrl>http://www.roblox.com</BaseUrl>\r\n" +
            "</Settings>\r\n";

        #endregion

        #region Properties

        private readonly FastZipEvents _fastZipEvents = new();
        private static readonly JsonSerializerOptions _indentedJsonOptions = new() { WriteIndented = true };
        private readonly CancellationTokenSource _cancelTokenSource = new();

        private IAppData AppData = default!;
        private Dictionary<string, string> PackageDirectoryMap = null!;
        private LaunchMode _launchMode;

        private string _launchCommandLine = App.LaunchSettings.RobloxLaunchArgs;
        private Version? _latestVersion = null;
        private string _latestVersionGuid = null!;
        private string _latestVersionDirectory = null!;
        private PackageManifest _versionPackageManifest = null!;
        private GameJoinData _joinData = null!;

        public static bool StaticDirectory => App.Settings.Prop.StaticDirectory;
        private static int MaxThreadDownload => App.Settings.Prop.MaxThreadDownload;
        private static bool AutomaticallyUpdateSober => OperatingSystem.IsLinux() && App.Settings.Prop.AutomaticallyUpdateSober;
        private bool MustUpgrade => App.LaunchSettings.ForceFlag.Active
            || App.State.Prop.ForceReinstall
            || (!OperatingSystem.IsLinux() && (String.IsNullOrEmpty(AppData.DistributionState.VersionGuid)
            || (OperatingSystem.IsMacOS() ? !Directory.Exists(AppData.ExecutablePath) : !File.Exists(AppData.ExecutablePath))))
            || (OperatingSystem.IsLinux() && IsStudioLaunch && !File.Exists(Path.Combine(_latestVersionDirectory, App.RobloxStudioAppName)));

        private bool _isInstalling = false;
        private double _progressIncrement;
        private double _taskbarProgressIncrement;
        private double _taskbarProgressMaximum;
        private long _totalDownloadedBytes = 0;
        private bool _packageExtractionSuccess = true;

        private SynchronizationContext? _uiContext;

        private bool _noConnection = false;

        private AsyncMutex? _mutex;
        private int _appPid = 0;

        public IBootstrapperDialog? Dialog = null;
        public bool IsStudioLaunch => _launchMode != LaunchMode.Player;
        public string MutexName { get; set; } = "Bloxstrap-Bootstrapper";

        private static Mutex? _multiInstanceMutex1;
        private static Mutex? _multiInstanceMutex2;
        public bool QuitIfMutexExists { get; set; } = false;

        #endregion

        #region Core

        public Bootstrapper(LaunchMode launchMode)
        {
            _launchMode = launchMode;

            // https://github.com/icsharpcode/SharpZipLib/blob/master/src/ICSharpCode.SharpZipLib/Zip/FastZip.cs/#L669-L680
            // exceptions don't get thrown if we define events without actually binding to the failure events. probably a bug. ¯\_(ツ)_/¯
            _fastZipEvents.FileFailure += (_, e) =>
            {
                App.Logger.WriteLine("FastZipEvents::OnFileFailure", $"Failed to extract {e.Name}: {e.Exception.Message}");
                _packageExtractionSuccess = false;
            };
            _fastZipEvents.DirectoryFailure += (_, e) => throw e.Exception;
            _fastZipEvents.ProcessFile += (_, e) => e.ContinueRunning = !_cancelTokenSource.IsCancellationRequested;

            SetupAppData();

            Deployment.Channel = IsStudioLaunch ? App.Settings.Prop.StudioChannel : App.Settings.Prop.PlayerChannel;

            App.Logger.WriteLine("Bootstrapper::Run", $"Using {(IsStudioLaunch ? "Studio" : "Player")} channel: {Deployment.Channel}");
        }

        private void SetupAppData()
        {
            AppData = IsStudioLaunch ? new RobloxStudioData() : new RobloxPlayerData();
        }

        private async Task SetupPackageDictionaries()
        {
            await App.RemoteData.WaitUntilDataFetched();

            var localData = App.RemoteData.Prop.PackageMaps[IsStudioLaunch ? "studio" : "player"];
            var commonData = App.RemoteData.Prop.PackageMaps.CommonPackageMap;

            PackageDirectoryMap = new(commonData);

            foreach (var package in localData)
                PackageDirectoryMap[package.Key] = package.Value;

            // Linux treats \\ weirdly, it leaves a \ in their name and dosent place in correct directory
            if (OperatingSystem.IsLinux())
            {
                foreach (var key in PackageDirectoryMap.Keys.ToList())
                {
                    if (PackageDirectoryMap[key] != null)
                        PackageDirectoryMap[key] = PackageDirectoryMap[key].Replace('\\', '/');
                }
            }
        }

        private void SetStatus(string message)
        {
            message = message.Replace("{product}", AppData.ProductName);
            _uiContext?.Post(_ => Dialog?.Message = message, null);
        }

        private void UpdateProgressBar()
        {
            long current = Interlocked.Read(ref _totalDownloadedBytes);
            _uiContext?.Post(_ =>
            {
                if (Dialog is null) return;
                int progressValue = (int)Math.Floor(_progressIncrement * current);
                progressValue = Math.Clamp(progressValue, 0, ProgressBarMaximum);
                Dialog.ProgressValue = progressValue;

                double taskbarProgressValue = _taskbarProgressIncrement * current;
                taskbarProgressValue = Math.Clamp(taskbarProgressValue, 0, _taskbarProgressMaximum);
                Dialog.TaskbarProgressValue = taskbarProgressValue;
            }, null);
        }

        private async Task HandleConnectionError(Exception exception)
        {
            const string LOG_IDENT = "Bootstrapper::HandleConnectionError";

            _noConnection = true;

            App.Logger.WriteLine(LOG_IDENT, "Connectivity check failed");
            App.Logger.WriteException(LOG_IDENT, exception);

            string message = Strings.Dialog_Connectivity_BadConnection;

            if (exception is AggregateException)
                exception = exception.InnerException!;

            // https://gist.github.com/pizzaboxer/4b58303589ee5b14cc64397460a8f386
            if (exception is HttpRequestException && exception.InnerException is null)
                message = String.Format(Strings.Dialog_Connectivity_RobloxDown, "[status.roblox.com](https://status.roblox.com)");

            if (MustUpgrade)
                message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeNeeded}\n\n{Strings.Dialog_Connectivity_TryAgainLater}";
            else
                message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeSkip}";

            await Frontend.ShowConnectivityDialog(
                String.Format(Strings.Dialog_Connectivity_UnableToConnect, "Roblox"),
                message,
                MustUpgrade ? MessageBoxImage.Error : MessageBoxImage.Warning,
                exception);

            if (MustUpgrade)
                App.Terminate(ErrorCode.ERROR_CANCELLED);
        }

        public async Task Run()
        {
            const string LOG_IDENT = "Bootstrapper::Run";

            App.Logger.WriteLine(LOG_IDENT, "Running bootstrapper");

            // this is now always enabled as of v2.8.0
            Dialog?.CancelEnabled = true;

            _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

            if (AutomaticallyUpdateSober && _launchMode == LaunchMode.Player)
                await UpdateSoberFlatpakAsync();

            SetStatus(Strings.Bootstrapper_Status_Connecting);

            // Skip the Roblox deployment API connectivity check entirely.
            if (OperatingSystem.IsLinux() && !IsStudioLaunch)
            {
                _noConnection = true;
                _latestVersionDirectory = Paths.SoberAssetOverlay;
                App.Logger.WriteLine(LOG_IDENT, "Linux (Player): skipping connectivity check — Sober manages Roblox.");
            }
            else
            {
                var connectionResult = await Deployment.InitializeConnectivity();
                App.Logger.WriteLine(LOG_IDENT, "Connectivity check finished");

                if (connectionResult is not null)
                    await HandleConnectionError(connectionResult);
            }

#if (!DEBUG || DEBUG_UPDATER) && !QA_BUILD
            if (!App.UpdateCheckCompleted && !App.LaunchSettings.BypassUpdateCheck && App.Settings.Prop.UpdateChecks != UpdateCheck.Disabled)
            {
                bool updatePresent = await CheckForUpdates();
                if (updatePresent)
                    return;
            }
#endif

            // ensure only one instance of the bootstrapper is running at the time
            // so that we don't have stuff like two updates happening simultaneously

            bool mutexExists;

            if (OperatingSystem.IsWindows())
            {
                mutexExists = Utilities.DoesMutexExist(MutexName);
            }
            else
            {
                mutexExists = Utilities.IsInstanceRunningFileLock(MutexName);
            }

            if (mutexExists)
            {
                if (!QuitIfMutexExists)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{MutexName} instance exists, waiting...");
                    SetStatus(Strings.Bootstrapper_Status_WaitingOtherInstances);

                    if (!OperatingSystem.IsWindows())
                    {
                        while (Utilities.IsInstanceRunningFileLock(MutexName) && !_cancelTokenSource.Token.IsCancellationRequested)
                        {
                            await Task.Delay(500, _cancelTokenSource.Token);
                        }
                    }
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{MutexName} instance exists, exiting!");
                    return;
                }
            }

            // wait for mutex to be released if it's not yet
            if (OperatingSystem.IsWindows())
            {
                var winMutex = new AsyncMutex(false, MutexName);
                await winMutex.AcquireAsync(_cancelTokenSource.Token);
                _mutex = winMutex;
            }

            if (mutexExists)
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

            CleanupVersionsFolder(); // cleanup after background updater

            bool allModificationsApplied = true;

            if (!_noConnection)
            {
                if (App.RemoteData.LoadedState == GenericTriState.Unknown) // we dont want it to flicker
                    SetStatus(Strings.Bootstrapper_Status_WaitingForData);

                await SetupPackageDictionaries(); // mods also require it

                if (AppData.DistributionState.VersionGuid != _latestVersionGuid || MustUpgrade)
                {
                    bool backgroundUpdaterMutexOpen = Utilities.DoesMutexExist(BackgroundUpdaterMutexName);

                    if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
                        backgroundUpdaterMutexOpen = false; // we want to actually update lol

                    App.Logger.WriteLine(LOG_IDENT, $"Background updater running: {backgroundUpdaterMutexOpen}");

                    if (backgroundUpdaterMutexOpen && MustUpgrade)
                    {
                        // I am Forced Upgrade, killer of Background Updates
                        Utilities.KillBackgroundUpdater();
                        backgroundUpdaterMutexOpen = false;
                    }

                    if (!backgroundUpdaterMutexOpen)
                    {
                        if (IsEligibleForBackgroundUpdate())
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
                    App.Logger.WriteLine(LOG_IDENT, "Linux: Force reinstall enabled.");

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

                            App.Logger.WriteLine(LOG_IDENT, $"Successfully cleared contents of {clientPackagePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to purge packages: {ex.Message}");
                    }
                }

                PackageDirectoryMap ??= [];
                if (!_cancelTokenSource.IsCancellationRequested)
                    allModificationsApplied = await ApplyModifications();
            }

            // check registry entries for every launch, just in case the stock bootstrapper changes it back

            if (OperatingSystem.IsWindows())
            {
                if (IsStudioLaunch)
                {
                    WindowsRegistry.RegisterStudio();
                    App.Logger.WriteLine(LOG_IDENT, "Studio launch detected, syncing RPC plugin...");
                    StudioPluginManager.Sync();
                }
                else
                {
                    WindowsRegistry.RegisterPlayer();
                }

                WindowsRegistry.RegisterClientLocation(IsStudioLaunch, _latestVersionDirectory); // if it for some reason doesnt exist
            }
            else
            {
                if (IsStudioLaunch)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Studio launch detected, syncing RPC plugin...");
                    StudioPluginManager.Sync();
                }
            }

            if (_launchMode != LaunchMode.Player)
            {
                if (OperatingSystem.IsWindows() && _mutex is not null) await _mutex.ReleaseAsync();
                else Utilities._lockFileStream?.Dispose();
            }

            if (_launchMode == LaunchMode.Player && App.Settings.Prop.MultiInstanceLaunching)
                await LaunchMultiInstanceWatcher();

            if (!App.LaunchSettings.NoLaunchFlag.Active && !_cancelTokenSource.IsCancellationRequested)
            {
                if (!App.LaunchSettings.QuietFlag.Active)
                {
                    // show some balloon tips
                    if (!_packageExtractionSuccess)
                        Frontend.ShowBalloonTip(Strings.Bootstrapper_ExtractionFailed_Title, Strings.Bootstrapper_ExtractionFailed_Message, Avalonia.Controls.Notifications.NotificationType.Warning);
                    else if (!allModificationsApplied)
                        Frontend.ShowBalloonTip(Strings.Bootstrapper_ModificationsFailed_Title, Strings.Bootstrapper_ModificationsFailed_Message, Avalonia.Controls.Notifications.NotificationType.Warning);
                }

                if (!OperatingSystem.IsLinux())
                {
                    await StartRoblox();
                }
                else if (IsStudioLaunch)
                {
                    if (!await EnsureWineAndDependenciesAsync())
                        await Frontend.ShowMessageBox(
    "Necessery wine files could not be auto installed.",
    MessageBoxImage.Error);
                    App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);

                    string studioExe = Path.Combine(_latestVersionDirectory, App.RobloxStudioAppName);
                    if (!File.Exists(studioExe))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "RobloxStudioBeta.exe missing, triggering upgrade...");
                        await UpgradeRoblox();
                    }

                    if (!File.Exists(studioExe))
                    {
                        await Frontend.ShowMessageBox(
                            "Roblox Studio installation failed. Please check your connection and try again.",
                            MessageBoxImage.Error);
                        App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
                    }

                    SetStatus("Starting Roblox Studio with Wine...");
                    await LaunchStudioViaWineAsync();
                }
                else
                {
                    if (!await EnsureSoberInstalledAsync())
                        return;
                    SetStatus("Starting Sober...");
                    await LaunchViaSober([]);
                }

                if (OperatingSystem.IsWindows() && _mutex is not null) await _mutex.ReleaseAsync();
                else Utilities._lockFileStream?.Dispose();

                Dialog?.CloseBootstrapper();
            }
        }

        /// <summary>
        /// Will throw whatever HttpClient can throw
        /// </summary>
        /// <returns></returns>
        private async Task GetLatestVersionInfo()
        {
            const string LOG_IDENT = "Bootstrapper::GetLatestVersionInfo";

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
                if (IsStudioLaunch)
                    App.Settings.Prop.StudioChannel = Deployment.DefaultChannel;
                else
                    App.Settings.Prop.PlayerChannel = Deployment.DefaultChannel;
                App.Settings.Save();
            }

            string enrolledChannel = match.Groups.Count == 2
                ? match.Groups[1].Value.ToLowerInvariant()
                : Deployment.DefaultChannel;

            bool behindProductionCheck = App.Settings.Prop.ChannelChangeMode == ChannelChangeMode.Prompt;

            // Private channels
            if (App.Cookies.Loaded)
            {
                UserChannel? userChannel = await Deployment.GetUserChannel(Deployment.BinaryType);

                if (
                    userChannel?.Token is not null &&
                    userChannel.AssignmentType != 1 // might need a change in the future
                    )
                {
                    // prevent roblox from thinking its a different channel
                    // we have to do it to prevent issues with channel fflags
                    if (!string.IsNullOrEmpty(enrolledChannel))
                        _launchCommandLine = _launchCommandLine.Replace(
                            $"channel:{enrolledChannel}",
                            $"channel:{userChannel.Channel}",
                            StringComparison.OrdinalIgnoreCase);

                    Deployment.ChannelToken = userChannel.Token;
                    enrolledChannel = userChannel.Channel;
                }
            }

            bool channelFlag = App.LaunchSettings.ChannelFlag.Active && !string.IsNullOrEmpty(App.LaunchSettings.ChannelFlag.Data);

            if (!channelFlag)
            {
                switch (App.Settings.Prop.ChannelChangeMode)
                {
                    case ChannelChangeMode.Automatic:
                        App.Logger.WriteLine(LOG_IDENT, "Enrolling into channel");
                        EnrollChannel(enrolledChannel);
                        break;

                    case ChannelChangeMode.Prompt:
                        App.Logger.WriteLine(LOG_IDENT, "Prompting channel enrollment");

                        if (!match.Success || match.Groups.Count != 2 || string.Equals(match.Groups[1].Value, Deployment.Channel, StringComparison.OrdinalIgnoreCase))
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Channel is either equal or incorrectly formatted");
                            break;
                        }

                        string displayChannel = !String.IsNullOrEmpty(match.Groups[1].Value)
                            ? match.Groups[1].Value
                            : Deployment.DefaultChannel;

                        var promptResult = await Frontend.ShowMessageBox(
                            String.Format(Strings.Bootstrapper_Bootstrapper_Dialog_PromptChannelChange, displayChannel, Deployment.Channel),
                            MessageBoxImage.Question,
                            MessageBoxButton.YesNo
                        );

                        if (promptResult == MessageBoxResult.Yes)
                            EnrollChannel(enrolledChannel);
                        break;

                    case ChannelChangeMode.Ignore:
                        App.Logger.WriteLine(LOG_IDENT, "Ignoring channel enrollment");
                        break;
                }
            }
            else
            {
                string channelFlagData = App.LaunchSettings.ChannelFlag.Data!;
                if (!String.IsNullOrEmpty(channelFlagData))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Forcing channel {channelFlagData}");
                    EnrollChannel(channelFlagData);
                }
            }

            if (IsStudioLaunch
                && App.Settings.Prop.StudioVersionOverrideEnabled
                && !string.IsNullOrEmpty(App.Settings.Prop.StudioVersionOverrideHash))
            {
                _latestVersionGuid = App.Settings.Prop.StudioVersionOverrideHash.Trim();
                App.Logger.WriteLine(LOG_IDENT, $"Studio version override active: pinned to {_latestVersionGuid}");
            }
            else if (!App.LaunchSettings.VersionFlag.Active || string.IsNullOrEmpty(App.LaunchSettings.VersionFlag.Data))
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
                        App.Logger.WriteLine(LOG_IDENT, $"Reverting enrolled channel to {Deployment.DefaultChannel} because a WindowsPlayer build does not exist for {Deployment.Channel}");
                    }
                    // If channel is not available to the user (private/internal release channel)
                    else if (ex.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Reverting enrolled channel to {Deployment.DefaultChannel} because {Deployment.Channel} is restricted for public use.");

                        // Only prompt if user has channel switching mode set to something other than Automatic.
                        if (App.Settings.Prop.ChannelChangeMode != ChannelChangeMode.Automatic)
                        {
                            await Frontend.ShowMessageBox(
                                String.Format(
                                    Strings.Boostrapper_Dialog_UnauthorizedChannel,
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
                            String.Format(Strings.Bootstrapper_Dialog_ChannelOutOfDate, Deployment.Channel, Deployment.DefaultChannel),
                            MessageBoxImage.Warning,
                            MessageBoxButton.YesNo
                        );

                    if (action == MessageBoxResult.Yes)
                    {
                        App.Logger.WriteLine("Bootstrapper::CheckLatestVersion", $"Changed Roblox channel from {Deployment.Channel} to {Deployment.DefaultChannel}");

                        RevertChannel();
                        clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel, behindProductionCheck: false, binaryTypeOverride: AppData.BinaryType);
                    }
                }

                if (OperatingSystem.IsWindows())
                {
                    using var key = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\ROBLOX Corporation\\Environments\\{AppData.RegistryName}\\Channel");
                    key.SetValueSafe("www." + Deployment.RobloxDomain, Deployment.IsDefaultChannel ? "" : Deployment.Channel);
                }

                _latestVersionGuid = clientVersion.VersionGuid;
                _latestVersion = Utilities.ParseVersionSafe(clientVersion.Version);
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Version set to {App.LaunchSettings.VersionFlag.Data} from arguments");
                _latestVersionGuid = App.LaunchSettings.VersionFlag.Data;
                // we can't determine the version
            }

            if (StaticDirectory)
                _latestVersionDirectory = AppData.StaticDirectory;
            else
                _latestVersionDirectory = Path.Combine(Paths.Versions, _latestVersionGuid);

            // Mods are applied directly into Sober's asset_overlay directory instead of a versioned folder.
            if (OperatingSystem.IsLinux() && !IsStudioLaunch)
                _latestVersionDirectory = Paths.SoberAssetOverlay;

            if (OperatingSystem.IsMacOS())
            {
                // Mac uses monolithic zip downloads instead of individual packages
                string zipName = IsStudioLaunch ? "RobloxStudioApp.zip" : "RobloxPlayer.zip";

                // Construct a fake package manifest response to trick the internal system
                string fakeManifest = $"v0\n{zipName}\n{_latestVersionGuid}\n0\n0";
                _versionPackageManifest = new(fakeManifest);
            }
            else
            {
                string pkgManifestUrl = Deployment.GetLocation($"/{_latestVersionGuid}-rbxPkgManifest.txt");
                var pkgManifestData = await App.HttpClient.GetStringAsync(pkgManifestUrl);
                _versionPackageManifest = new(pkgManifestData);
            }

            // this can happen if version is set through arguments
            if (_launchMode == LaunchMode.Unknown)
            {
                App.Logger.WriteLine(LOG_IDENT, "Identifying launch mode from package manifest");

                bool isPlayer = _versionPackageManifest.Exists(x => x.Name == "RobloxApp.zip" || x.Name == "RobloxPlayer.zip");
                App.Logger.WriteLine(LOG_IDENT, $"isPlayer: {isPlayer}");

                _launchMode = isPlayer ? LaunchMode.Player : LaunchMode.Studio;
                SetupAppData(); // we need to set it up again
            }
        }

        private bool IsEligibleForBackgroundUpdate()
        {
            const string LOG_IDENT = "Bootstrapper::IsEligibleForBackgroundUpdate";

            if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Is the background updater process");
                return false;
            }

            if (!App.Settings.Prop.BackgroundUpdatesEnabled)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Background updates disabled");
                return false;
            }

            if (IsStudioLaunch)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Studio launch");
                return false;
            }

            if (MustUpgrade)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Must upgrade is true");
                return false;
            }

            // at least 3GB of free space
            const long minimumFreeSpace = 3_000_000_000;
            long space = Filesystem.GetFreeDiskSpace(Paths.Base);
            if (space < minimumFreeSpace)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Not eligible: User has {space} free space, at least {minimumFreeSpace} is required");
                return false;
            }

            if (_latestVersion == default)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Latest version is undefined");
                return false;
            }

            Version? currentVersion = Utilities.GetRobloxVersion(AppData);
            if (currentVersion == default)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Current version is undefined");
                return false;
            }

            // always normally upgrade for downgrades
            if (currentVersion.Minor > _latestVersion.Minor)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Downgrade");
                return false;
            }

            // only background update if we're:
            // - one major update behind
            // - the same major update
            int diff = _latestVersion.Minor - currentVersion.Minor;
            if (diff == 0 || diff == 1)
            {
                App.Logger.WriteLine(LOG_IDENT, "Eligible");
                return true;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Not eligible: Major version diff is {diff}");
            return false;
        }

        private static async Task LaunchMultiInstanceWatcher()
        {
            const string LOG_IDENT = "Bootstrapper::LaunchMultiInstanceWatcher";

            try
            {
                if (Utilities.DoesMutexExist("ROBLOX_singletonMutex"))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Mutex ROBLOX_singletonMutex already exists, skipping creation");
                }
                else
                {
                    _multiInstanceMutex1 = new Mutex(true, "ROBLOX_singletonMutex");
                    App.Logger.WriteLine(LOG_IDENT, "Created multi-instance mutex: ROBLOX_singletonMutex");
                }

                if (Utilities.DoesMutexExist("ROBLOX_singletonEvent") || Utilities.DoesEventExist("ROBLOX_singletonEvent"))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Mutex ROBLOX_singletonEvent already exists");
                }
                else
                {
                    _multiInstanceMutex2 = new Mutex(true, "ROBLOX_singletonEvent");
                    App.Logger.WriteLine(LOG_IDENT, "Created multi-instance mutex: ROBLOX_singletonEvent");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to apply multi-instance setup: {ex.Message}");
            }

            if (App.Settings.Prop.Error773Fix)
            {
                try
                {
                    string cookiesPath = Path.Combine(Paths.Roblox, "LocalStorage", "RobloxCookies.dat");

                    if (File.Exists(cookiesPath))
                    {
                        FileAttributes attributes = File.GetAttributes(cookiesPath);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            attributes &= ~FileAttributes.ReadOnly;
                            File.SetAttributes(cookiesPath, attributes);
                        }

                        File.SetAttributes(cookiesPath, FileAttributes.ReadOnly);

                        App.Logger.WriteLine(LOG_IDENT, "Applied Error 773 fix");
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT, "773 fix not needed");
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to apply 773 fix: {ex.Message}");
                }
            }

            using EventWaitHandle initEventHandle = new(false, EventResetMode.AutoReset, "Bloxstrap-MultiInstanceWatcherInitialisationFinished");
            Process.Start(Paths.Process, "-multiinstancewatcher");

            await Task.Run(() => initEventHandle.WaitOne(TimeSpan.FromSeconds(2)));

            App.Logger.WriteLine(LOG_IDENT, "Multi-instance watcher initialization completed");
        }

        // Cleanup starts in watcher not here
        public void CleanupMultiInstanceResources()
        {
            const string LOG_IDENT = "Bootstrapper::CleanupMultiInstanceResources";

            try
            {
                string processName = OperatingSystem.IsMacOS() ? "RobloxPlayer" : "RobloxPlayerBeta";
                int count = Process.GetProcesses().Count(x => x.ProcessName == processName);
                count -= 1;

                if (count > 0)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Skipping cleanup - {count} Roblox process(es) still running");
                    return;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return;
            }

            bool launchingMutex = Utilities.DoesMutexExist(MutexName);

            if (launchingMutex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Skipping cleanup, currently launching roblox");
                return;
            }

            try
            {
                if (_multiInstanceMutex1 != null)
                {
                    _multiInstanceMutex1.Dispose();
                    _multiInstanceMutex1 = null;
                    App.Logger.WriteLine(LOG_IDENT, "Disposed ROBLOX_singletonMutex");
                }

                if (_multiInstanceMutex2 != null)
                {
                    _multiInstanceMutex2.Dispose();
                    _multiInstanceMutex2 = null;
                    App.Logger.WriteLine(LOG_IDENT, "Disposed ROBLOX_singletonEvent");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error disposing mutexes: {ex.Message}");
            }

            if (App.Settings.Prop.Error773Fix)
            {
                try
                {
                    string cookiesPath = Path.Combine(Paths.Roblox, "LocalStorage", "RobloxCookies.dat");

                    if (File.Exists(cookiesPath))
                    {
                        FileAttributes attributes = File.GetAttributes(cookiesPath);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            attributes &= ~FileAttributes.ReadOnly;
                            File.SetAttributes(cookiesPath, attributes);
                            App.Logger.WriteLine(LOG_IDENT, "Removed read-only attribute from RobloxCookies.dat");
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to remove read-only attribute: {ex.Message}");
                }
            }
        }

        private static double Deg2Rad(double deg) => deg * (Math.PI / 180.0);

        private static double GetDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;

            double dLat = Deg2Rad(lat2 - lat1);
            double dLon = Deg2Rad(lon2 - lon1);
            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private async Task<string> GetBetterMatchmakingServerID()
        {
            const string LOG_IDENT = "Bootstrapper::GetBetterMatchmakingServerID";

            var ipinfo = await Http.GetJson<IPInfoResponse>(new Uri("https://ipinfo.io/json"));
            if (string.IsNullOrEmpty(ipinfo.Loc))
                throw new HttpRequestException("Location data missing from ipinfo.io");

            string[] location = ipinfo.Loc.Split(',');
            double userLat = double.Parse(location[0], CultureInfo.InvariantCulture);
            double userLon = double.Parse(location[1], CultureInfo.InvariantCulture);

            List<RegionDistance> topRegions = await GetClosestRegionsWithDistanceAsync(userLat, userLon, 5);
            if (topRegions.Count == 0)
                throw new HttpRequestException("No regions found from datacenter list");

            App.Logger.WriteLine(LOG_IDENT, $"Top 5 regions: {string.Join(" -> ", topRegions.Select(r => r.Region))}");

            if (!string.IsNullOrEmpty(_joinData.JobId))
            {
                string? defaultRegion = await GetServerRegionAsync(_joinData.JobId, (long)_joinData.PlaceId!);
                if (defaultRegion != null)
                {
                    bool isInTop5 = topRegions.Any(r => r.Region.Equals(defaultRegion, StringComparison.OrdinalIgnoreCase));
                    App.Logger.WriteLine(LOG_IDENT, $"Default server region: {defaultRegion} – {(isInTop5 ? "in top 5, keeping it" : "not in top 5, will override")}");
                    if (isInTop5)
                        return _joinData.JobId;
                }
            }

            const int MAX_CANDIDATES = 100;
            var fetcher = new Integrations.RobloxServerFetcher();
            string? cookie = await fetcher.ResolveCookieAsync();
            if (string.IsNullOrEmpty(cookie))
                throw new HttpRequestException("Could not obtain a valid .ROBLOSECURITY cookie");

            SetStatus("Searching for nearby servers...");
            var fetchResult = await fetcher.FetchServerInstancesAsync((long)_joinData.PlaceId!, cursor: "", sortOrder: 2, optionalCookie: cookie);

            var candidates = fetchResult.Servers?
                .Where(s => !string.IsNullOrEmpty(s.Id))
                .Take(MAX_CANDIDATES)
                .ToList() ?? new List<ServerInstance>();

            if (candidates.Count == 0)
            {
                App.Logger.WriteLine(LOG_IDENT, "No servers found at all.");
                return "";
            }

            App.Logger.WriteLine(LOG_IDENT, $"Collected {candidates.Count} candidate servers (max {MAX_CANDIDATES}).");

            foreach (var regionInfo in topRegions)
            {
                var match = candidates.FirstOrDefault(s => s.Region != null && s.Region.Equals(regionInfo.Region, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Found server in {regionInfo.Region} (rank {Array.IndexOf(topRegions.Select(r => r.Region).ToArray(), regionInfo.Region) + 1})");
                    return match.Id;
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "No server found in any of the top 5 regions.");
            return "";
        }

        private async Task<string?> GetServerRegionAsync(string jobId, long placeId)
        {
            var fetcher = new Integrations.RobloxServerFetcher();
            string? cookie = await fetcher.ResolveCookieAsync();
            if (string.IsNullOrEmpty(cookie))
                return null;

            using var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://gamejoin.roblox.com/v1/join-game-instance");
            request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { placeId, isTeleport = false, gameId = jobId, gameJoinAttemptId = jobId }),
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            //forgot which one it was and too lazy to check honestly
            if (doc.RootElement.TryGetProperty("DataCenterId", out var dcElem) ||
                doc.RootElement.TryGetProperty("dataCenterId", out dcElem))
            {
                if (dcElem.TryGetInt32(out int dcId))
                {
                    var datacenters = await Http.GetJson<List<DatacenterEntry>>(new Uri("https://apis.rovalra.com/v1/datacenters/list"));
                    var entry = datacenters?.FirstOrDefault(dc => dc.DataCenterIds.Contains(dcId));
                    if (entry != null)
                        return $"{entry.Location.City}, {entry.Location.Country}".TrimStart(',').Trim();
                }
            }
            return null;
        }

        private async Task<List<RegionDistance>> GetClosestRegionsWithDistanceAsync(double userLat, double userLon, int topCount = 3)
        {
            var datacenters = await Http.GetJson<List<DatacenterEntry>>(new Uri("https://apis.rovalra.com/v1/datacenters/list"));
            if (datacenters == null || datacenters.Count == 0)
                return new List<RegionDistance>();

            var regionDistance = new Dictionary<string, double>();

            foreach (var dc in datacenters)
            {
                if (dc.Location == null || dc.Location.LatLong == null || dc.Location.LatLong.Length < 2)
                    continue;

                if (!double.TryParse(dc.Location.LatLong[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                    !double.TryParse(dc.Location.LatLong[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                    continue;

                double distance = GetDistance(userLat, userLon, lat, lon);
                string regionKey = $"{dc.Location.City}, {dc.Location.Country}".TrimStart(',').Trim();

                if (!regionDistance.ContainsKey(regionKey) || distance < regionDistance[regionKey])
                    regionDistance[regionKey] = distance;
            }

            var closest = regionDistance
                .OrderBy(kvp => kvp.Value)
                .Take(topCount)
                .Select(kvp => new RegionDistance { Region = kvp.Key, DistanceKm = kvp.Value })
                .ToList();

            return closest;
        }

        private class RegionDistance
        {
            public string Region { get; set; } = "";
            public double DistanceKm { get; set; }
        }

        private async Task StartRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::StartRoblox";

            if (_launchMode == LaunchMode.Player)
            {
                _joinData = GameJoin.GetJoinDataByLaunchCommand(_launchCommandLine);

                if (_joinData.JoinType == GameJoinType.Unknown)
                    App.Logger.WriteLine(LOG_IDENT, "Unable to get join data");

                App.Logger.WriteLine(LOG_IDENT, $"Join origin: {_joinData.JoinOrigin}");

                bool isFollowUser = false;

                // _joinData.JoinType == GameJoinType.RequestFollowUser just doesnt work at all
                // idk why they dont use it when the user is following a friend, but ok
                if (App.Settings.Prop.EnableBetterMatchmaking &&
                    (_joinData.JoinOrigin == "friendServerListJoin" || _joinData.JoinOrigin == "placesListInHomePage"))
                {
                    App.Logger.WriteLine(LOG_IDENT, "User is trying to join a friend — showing dialog");

                    var result = await Frontend.ShowMessageBox(
                        String.Format(Strings.Bootstrapper_Experimental_BetterMatchmaking_FollowUser),
                        MessageBoxImage.Question,
                        MessageBoxButton.YesNo
                    );

                    if (result == MessageBoxResult.Yes)
                        isFollowUser = true;
                }

                try
                {
                    if (App.Settings.Prop.EnableBetterMatchmaking && _joinData.JoinType != GameJoinType.RequestPrivateGame && _joinData.PlaceId != null && !isFollowUser)
                    {
                        string serverid = await GetBetterMatchmakingServerID();
                        string placeLauncherUrl = UrlBuilder.BuildPlacelauncherUrl((long)_joinData.PlaceId, serverid);

                        if (!string.IsNullOrEmpty(serverid))
                            _launchCommandLine = _launchCommandLine.Replace(_joinData.PlaceLauncherUrl, HttpUtility.UrlEncode(placeLauncherUrl));
                    }
                }
                catch (HttpRequestException ex)
                {
                    _ = Frontend.ShowConnectivityDialog(
                        String.Format(Strings.Dialog_Connectivity_UnableToConnect, "rovalra.com"),
                        Strings.Dialog_Connectivity_MatchmakingFailed,
                        MessageBoxImage.Warning,
                        ex
                        );
                }

                if (!Deployment.IsDefaultRobloxDomain && string.IsNullOrEmpty(_launchCommandLine))
                    _launchCommandLine = "roblox://navigation/home";
            }

            SetStatus(Strings.Bootstrapper_Status_Starting);

            string expectedName = IsStudioLaunch ? App.RobloxStudioAppName : App.RobloxPlayerAppName;
            string expectedPath = Path.Combine((string)AppData.Directory, expectedName);

            if (!Directory.Exists(expectedPath) && !File.Exists(expectedPath))
            {
                App.Logger.WriteLine(LOG_IDENT, $"{expectedName} not found at {expectedPath}, triggering upgrade...");
                await UpgradeRoblox();
            }

            if (!Directory.Exists(expectedPath) && !File.Exists(expectedPath))
                throw new FileNotFoundException($"Roblox application not found at expected path after upgrade: {expectedPath}");

            App.Logger.WriteLine(LOG_IDENT, $"Resolved Roblox path: {expectedPath}");

            var startInfo = new ProcessStartInfo()
            {
                FileName = OperatingSystem.IsMacOS() ? "open" : expectedPath,
                Arguments = OperatingSystem.IsMacOS() ? $"-n \"{expectedPath}\" --args {_launchCommandLine}" : _launchCommandLine,
                WorkingDirectory = AppData.Directory
            };

            if (OperatingSystem.IsWindows())
                WindowsRegistry.DisableFullscreenOptimizations(AppData.ExecutablePath);

            if (OperatingSystem.IsMacOS())
                startInfo.UseShellExecute = true;

            if (_launchMode == LaunchMode.Player && ShouldRunAsAdmin())
            {
                startInfo.Verb = "runas";
                startInfo.UseShellExecute = true;
            }
            else if (_launchMode == LaunchMode.StudioAuth)
            {
                Process.Start(startInfo);
                return;
            }

            var autoclosePids = new List<int>();

            // the code you're gonna read ahead is horrible. sorry for the hack, but it works ¯\_(ツ)_/¯
            // check if prelaunch is checked
            foreach (var integration in App.Settings.Prop.CustomIntegrations)
            {
                if (integration?.PreLaunch == true)
                    LaunchIntegration(integration, autoclosePids, LOG_IDENT);
            }

            // v2.2.0 - byfron will trip if we keep a process handle open for over a minute, so we're doing this now
            try
            {
                using var process = Process.Start(startInfo)!;
                _appPid = process.Id;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // 1223 = ERROR_CANCELLED, gets thrown if a UAC prompt is cancelled
                return;
            }
            catch (Exception)
            {
                // attempt a reinstall on next launch
                File.Delete(AppData.ExecutablePath);
                throw;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Started Roblox (PID {_appPid}). Launching Watcher...");
            _mutex?.ReleaseAsync();

            if (!IsStudioLaunch)
            {
                // launch custom integrations now if normal roblox
                foreach (var integration in App.Settings.Prop.CustomIntegrations)
                {
                    if (integration == null || integration.PreLaunch || integration.SpecifyGame)
                        continue;

                    LaunchIntegration(integration, autoclosePids, LOG_IDENT);
                }
            }

            await LaunchWatcherIfNeededAsync(autoclosePids);

            // allow for window to show, since the log is created pretty far beforehand
            await Task.Delay(1000);
        }

        private async Task LaunchViaSober(List<int> autoclosePids)
        {
            const string LOG_IDENT = "Bootstrapper::LaunchViaSober";

            if (App.Settings.Prop.ShowServerDetails)
                App.SoberSettings.Prop.ServerLocationIndicatorEnabled = false;

            if (App.Settings.Prop.UseDiscordRichPresence)
            {
                App.SoberSettings.Prop.DiscordRpcEnabled = false;
                App.SoberSettings.Prop.DiscordRpcShowJoinButton = false;
            }

            if (App.Settings.Prop.UseDisableAppPatch)
                App.SoberSettings.Prop.CloseOnLeave = false;

            App.Logger.WriteLine(LOG_IDENT, $"Launching Sober via flatpak with args: {_launchCommandLine}");

            var startInfo = new ProcessStartInfo
            {
                FileName = "flatpak",
                Arguments = $"run {SoberFlatpakId} {_launchCommandLine}",
                UseShellExecute = false,
            };

            try
            {
                // Record time before launch so we can detect the new latest.log
                var launchTime = DateTime.UtcNow;

                using var process = Process.Start(startInfo)!;
                _appPid = process.Id;
                App.Logger.WriteLine(LOG_IDENT, $"Sober launched with PID {_appPid}");
                App.Logger.WriteLine(LOG_IDENT, "Launching Watcher...");
                _mutex?.ReleaseAsync();
                await LaunchWatcherIfNeededAsync(autoclosePids);

                _ = Task.Run(async () =>
                {
                    string[] soberReadySignals = ["will_handle_app_startup", "will_handle_start_game"];
                    const int pollIntervalMs = 50;
                    const int timeoutMs = 30_000;
                    var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

                    try
                    {
                        string latestLog = Path.Combine(Paths.RobloxLogs, "latest.log");

                        while (DateTime.UtcNow < deadline)
                        {
                            if (File.Exists(latestLog) && File.GetLastWriteTimeUtc(latestLog) >= launchTime)
                                break;
                            await Task.Delay(pollIntervalMs);
                        }

                        if (!File.Exists(latestLog))
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"latest.log not found at {latestLog}, closing dialog.");
                            return;
                        }

                        App.Logger.WriteLine(LOG_IDENT, $"Tailing {latestLog} for ready signal...");

                        using var fs = new FileStream(latestLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        using var reader = new StreamReader(fs);

                        while (DateTime.UtcNow < deadline)
                        {
                            string? line = await reader.ReadLineAsync();
                            if (line is null)
                            {
                                await Task.Delay(pollIntervalMs);
                                continue;
                            }
                            if (soberReadySignals.Any(line.Contains))
                            {
                                App.Logger.WriteLine(LOG_IDENT, "Sober window ready — closing bootstrapper dialog.");
                                Dialog?.CloseBootstrapper();
                                return;
                            }
                        }

                        App.Logger.WriteLine(LOG_IDENT, "Timed out waiting for Sober ready signal — closing dialog.");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Log watcher error: {ex.Message} — closing dialog.");
                    }
                    finally
                    {
                        Dialog?.CloseBootstrapper();
                    }
                });

                await Task.Run(async () =>
                {
                    while (!_cancelTokenSource.IsCancellationRequested)
                    {
                        await Task.Delay(2500);
                        if (Process.GetProcessesByName("sober").Length == 0)
                            break;
                    }
                });

                App.Logger.WriteLine(LOG_IDENT, "Sober process exited");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to launch Sober via flatpak!");
                App.Logger.WriteException(LOG_IDENT, ex);
                await Frontend.ShowMessageBox(
                    $"Failed to launch Sober. Make sure Flatpak and {SoberFlatpakId} are installed.\n\n{ex.Message}",
                    MessageBoxImage.Error
                );
                App.Terminate(ErrorCode.ERROR_CANCELLED);
            }
        }

        private async Task LaunchStudioViaWineAsync()
        {
            const string LOG_IDENT = "Bootstrapper::LaunchStudioViaWine";

            string studioExe = Path.Combine(_latestVersionDirectory, App.RobloxStudioAppName);
            if (!File.Exists(studioExe))
            {
                App.Logger.WriteLine(LOG_IDENT, "Cannot find RobloxStudioBeta.exe");
                return;
            }

            var env = new Dictionary<string, string>();

            foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
                if (de.Key is string key && de.Value is string value)
                    env[key] = value;

            env["WINEPREFIX"] = App.Settings.Prop.WinePrefixPath ?? "";
            env["WINEARCH"] = "win64";
            env["WINEDEBUG"] = App.Settings.Prop.StudioDebug ? "warn+seh,fixme-all,err-kerberos,err-ntlm,err-combase" : "warn+seh";
            env["WINEDLLOVERRIDES"] = "dxdiagn,winemenubuilder.exe,mscoree,mshtml=";
            env["WINEESYNC"] = "1";
            env["WINEFSYNC"] = "1";
            env["GDK_DISABLE"] = "vulkan";
            env["XR_LOADER_DEBUG"] = "none";

            var renderer = App.Settings.Prop.StudioRenderer;
            bool useDxvk = (renderer == StudioRenderer.DXVK || renderer == StudioRenderer.DXVKSarek);
            if (useDxvk)
            {
                env["WINE_D3D_CONFIG"] = "renderer=vulkan";
                if (!App.Settings.Prop.StudioDebug)
                    env["DXVK_LOG_LEVEL"] = "warn";
                env["DXVK_LOG_PATH"] = "none";
                env["DXVK_STATE_CACHE_PATH"] = Paths.Cache;
            }
            else if (renderer == StudioRenderer.Vulkan)
            {
                env["WINE_D3D_CONFIG"] = "renderer=vulkan";
            }

            string angle = (useDxvk || renderer == StudioRenderer.Vulkan) ? "vulkan" : "gl";
            env["WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS"] = $"--disable-gpu-compositing --use-angle={angle}";

            string? forcedGpu = App.Settings.Prop.StudioForcedGpu;
            if (!string.IsNullOrEmpty(forcedGpu))
            {
                bool isNvidia = CheckForNvidiaGpu();
                env["DRI_PRIME"] = forcedGpu;
                if (isNvidia)
                {
                    env["__NV_PRIME_RENDER_OFFLOAD"] = "1";
                    env["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia";
                    env["__EGL_VENDOR_LIBRARY_FILENAMES"] = IsFlatpak()
                        ? "/usr/lib/x86_64-linux-gnu/GL/glvnd/egl_vendor.d/10_nvidia.json"
                        : "/usr/share/glvnd/egl_vendor.d/10_nvidia.json";
                }
                else
                {
                    env["__GLX_VENDOR_LIBRARY_NAME"] = "mesa";
                    env["__EGL_VENDOR_LIBRARY_FILENAMES"] = IsFlatpak()
                        ? "/usr/lib/x86_64-linux-gnu/GL/glvnd/egl_vendor.d/50_mesa.json"
                        : "/usr/share/glvnd/egl_vendor.d/50_mesa.json";
                    env["__NV_PRIME_RENDER_OFFLOAD"] = "0";
                }
                env["__VK_LAYER_NV_optimus"] = "";
                env["VK_LOADER_DRIVERS_SELECT"] = "";
            }

            if (useDxvk || renderer == StudioRenderer.Vulkan)
            {
                env["VK_LOADER_LAYERS_ENABLE"] = "VK_LAYER_VINEGAR_VinegarLayer";
                env["VK_LAYER_PATH"] = Path.Combine(Paths.WineRoot, "vulkan_layer");
            }

            foreach (var userEnv in App.Settings.Prop.StudioEnvironmentVariables)
                env[userEnv.Key] = userEnv.Value;

            if (App.Settings.Prop.StudioDebug)
            {
                App.Logger.WriteLine(LOG_IDENT, "Studio Debug enabled - Environment variables:");
                foreach (var kvp in env)
                    App.Logger.WriteLine(LOG_IDENT, $"  {kvp.Key}={kvp.Value}");
            }

            string? dll;
            string fastPath = Path.Combine(_latestVersionDirectory, "Plugins", "Qt5", "platforms", "qwindows.dll");
            if (File.Exists(fastPath))
                dll = fastPath;
            else
                dll = Directory.GetFiles(_latestVersionDirectory, "qwindows.dll", SearchOption.AllDirectories).FirstOrDefault();

            string? platformsDir = !string.IsNullOrEmpty(dll) ? Path.GetDirectoryName(Path.GetDirectoryName(dll)) : null;
            if (platformsDir != null && Directory.Exists(platformsDir))
            {
                env["QT_PLUGIN_PATH"] = platformsDir;
                App.Logger.WriteLine(LOG_IDENT, $"QT_PLUGIN_PATH set to {platformsDir}");
            }
            else
                App.Logger.WriteLine(LOG_IDENT, "Warning: Qt platform plugin not found – Studio may fail to start.");

            if (!string.IsNullOrEmpty(App.Settings.Prop.WinePrefixPath))
            {
                string systemUser = Environment.UserName ?? "froststrap";
                string winePluginDir = Path.Combine(App.Settings.Prop.WinePrefixPath, "drive_c", "users", systemUser, "AppData", "Local", "Roblox", "Plugins");
                StudioPluginManager.OverridePluginDirectory = winePluginDir;
                StudioPluginManager.Sync();
            }
            else
                App.Logger.WriteLine(LOG_IDENT, "Wine prefix path is null or empty; cannot set plugin override.");

            string? cookie = await GetRobloxSecurityCookieAsync();
            if (!string.IsNullOrEmpty(cookie))
                App.HttpClient.DefaultRequestHeaders.Add("Cookie", $".ROBLOSECURITY={cookie}");

            string baseArgs = $"\"{studioExe}\" {_launchCommandLine}";
            string finalArgs = baseArgs;

            string? virtualDesktop = App.Settings.Prop.StudioVirtualDesktop;
            if (!string.IsNullOrEmpty(virtualDesktop))
            {
                string uuid = Guid.NewGuid().ToString();
                finalArgs = $"explorer /desktop={uuid},{virtualDesktop} {baseArgs}";
            }

            string? customLauncher = App.Settings.Prop.StudioLauncher;
            if (!string.IsNullOrEmpty(customLauncher))
            {
                finalArgs = customLauncher.Replace("%command%", finalArgs);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = App.Settings.Prop.WineBinaryPath,
                Arguments = finalArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = _latestVersionDirectory
            };

            foreach (var kvp in env)
                startInfo.Environment[kvp.Key] = kvp.Value;

            try
            {
                var ulimit = await RunProcessAndReadOutputAsync("bash", "-c \"ulimit -n\"");
                if (int.TryParse(ulimit?.Trim(), out int fdLimit) && fdLimit < 524288)
                    App.Logger.WriteLine(LOG_IDENT, $"File descriptor limit ({fdLimit}) may be too low for esync/fsync.");
            }
            catch { }

            var autoclosePids = new List<int>();
            foreach (var integration in App.Settings.Prop.CustomIntegrations)
                if (integration?.PreLaunch == true)
                    LaunchIntegration(integration, autoclosePids, LOG_IDENT);

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to start Roblox Studio process - Process.Start returned null");
                    await Frontend.ShowMessageBox("Could not start Roblox Studio. Please check your Wine installation.", MessageBoxImage.Error);
                    App.Terminate(ErrorCode.ERROR_CANCELLED);
                    return;
                }
                _appPid = process.Id;
                App.Logger.WriteLine(LOG_IDENT, $"Roblox Studio started with Wine (PID {_appPid})");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to launch Studio: {ex.Message}");
                await Frontend.ShowMessageBox("Could not start Roblox Studio. Please check your Wine installation.", MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_CANCELLED);
                return;
            }

            _mutex?.ReleaseAsync();

            foreach (var integration in App.Settings.Prop.CustomIntegrations)
                if (integration != null && !integration.PreLaunch && !integration.SpecifyGame)
                    LaunchIntegration(integration, autoclosePids, LOG_IDENT);

            string wineLogDir = GetWineLogDirectory();
            Directory.CreateDirectory(wineLogDir);
            await LaunchWatcherIfNeededAsync(autoclosePids, logDirectory: wineLogDir);

            await Task.Delay(1000);
        }

        private async Task LaunchWatcherIfNeededAsync(List<int> autoclosePids, string? logFileName = null, string? logDirectory = null)
        {
            if (!(App.Settings.Prop.EnableActivityTracking
                || App.LaunchSettings.TestModeFlag.Active
                || autoclosePids.Count > 0))
                return;

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
                string rbxLogDir = logDirectory ?? Paths.RobloxLogs;

                for (int i = 0; i < 60; i++)
                {
                    if (Directory.Exists(rbxLogDir))
                    {
                        logFileName = Directory.GetFiles(rbxLogDir, "*.log")
                            .Select(f => new FileInfo(f))
                            .Where(f => f.CreationTimeUtc > DateTime.UtcNow.AddSeconds(-5))
                            .OrderByDescending(f => f.CreationTimeUtc)
                            .FirstOrDefault()?.FullName;
                    }

                    if (logFileName != null)
                        break;

                    await Task.Delay(500, _cancelTokenSource.Token);
                }
            }

            using var ipl = new InterProcessLock("WatcherLaunch", TimeSpan.FromSeconds(5));
            if (!ipl.IsAcquired)
                return;

            var watcherData = new WatcherData
            {
                ProcessId = _appPid,
                LogFile = logFileName,
                AutoclosePids = autoclosePids,
                RobloxDirectory = _latestVersionDirectory,
                LaunchMode = _launchMode
            };

            string watcherDataArg = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(watcherData)));

            string args = $"-watcher \"{watcherDataArg}\"";

            if (App.LaunchSettings.TestModeFlag.Active)
                args += " -testmode";

            Process.Start(Paths.Process, args);
        }

        private static void LaunchIntegration(CustomIntegration integration, List<int> autoclosePids, string logIdent)
        {
            App.Logger.WriteLine(logIdent, $"Launching custom integration '{integration.Name}' ({integration.Location} {integration.LaunchArgs} - autoclose is {integration.AutoClose})");

            int pid = 0;

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = integration.Location,
                    Arguments = integration.LaunchArgs.Replace("\r\n", " "),
                    WorkingDirectory = Path.GetDirectoryName(integration.Location),
                    UseShellExecute = true
                })!;

                pid = process.Id;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(logIdent, $"Failed to launch integration '{integration.Name}'!");
                App.Logger.WriteLine(logIdent, ex.Message);
            }

            if (integration.AutoClose && pid != 0)
                autoclosePids.Add(pid);

            if (integration.Delay != 0)
                Thread.Sleep(integration.Delay);

        }

        private bool ShouldRunAsAdmin()
        {
            if (!OperatingSystem.IsWindows())
                return false;

            foreach (var root in WindowsRegistry.Roots)
            {
                using var key = root.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers");

                if (key is null)
                    continue;

                string? flags = (string?)key.GetValue(AppData.ExecutablePath);

                if (flags is not null && flags.Contains("RUNASADMIN", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public void Cancel()
        {
            const string LOG_IDENT = "Bootstrapper::Cancel";

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            App.Logger.WriteLine(LOG_IDENT, "Cancelling launch...");
            _cancelTokenSource.Cancel();

            Dialog?.CancelEnabled = false;

            if (_isInstalling)
            {
                try
                {
                    // clean up registry keys
                    if (OperatingSystem.IsWindows())
                        WindowsRegistry.RegisterClientLocation(IsStudioLaunch, null);

                    // clean up install
                    if (Directory.Exists(_latestVersionDirectory))
                        Directory.Delete(_latestVersionDirectory, true);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Could not fully clean up installation!");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
            else
            {
                try
                {
                    using var process = Process.GetProcessById(_appPid);
                    process.Kill();
                }
                catch (Exception) { }
            }

            if (OperatingSystem.IsLinux())
            {
                try
                {
                    foreach (var soberProcess in Process.GetProcessesByName("sober"))
                    {
                        try
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Killing sober process (PID {soberProcess.Id})");
                            soberProcess.Kill(true);
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Failed to kill sober process (PID {soberProcess.Id})");
                            App.Logger.WriteException(LOG_IDENT, ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to enumerate sober processes.");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            Dialog?.CloseBootstrapper();
            App.SoftTerminate(ErrorCode.ERROR_CANCELLED);
        }

        #endregion

        #region App Install

        private async Task<bool> CheckForUpdates()
        {
            const string LOG_IDENT = "Bootstrapper::CheckForUpdates";

            if (App.Settings.Prop.UpdateChecks == UpdateCheck.Disabled)
            {
                App.Logger.WriteLine(LOG_IDENT, "Update checking is disabled in settings.");
                return false;
            }

            if (Process.GetProcessesByName(App.ProjectName).Length > 1)
            {
                App.Logger.WriteLine(LOG_IDENT, $"More than one {App.ProjectName} instance running, aborting update check.");
                return false;
            }

            SetStatus("Checking for Updates...");

            GithubRelease? releaseInfo = null;
            string version;

#if !DEBUG_UPDATER
            UpdateCheck preference = App.Settings.Prop.UpdateChecks;
            bool includePreRelease = preference == UpdateCheck.Both || preference == UpdateCheck.Test;

            releaseInfo = await App.GetLatestRelease(includePreRelease);

            if (releaseInfo is null)
                return false;

            string currentVer = App.GetUpdateCheckVersion();
            string releaseVer = releaseInfo.TagName;
            version = releaseVer;

            var versionComparison = Utilities.CompareVersions(currentVer, releaseVer);

            if (versionComparison == VersionComparison.LessThan)
            {
                string releaseType = releaseInfo.Prerelease ? "Pre-release" : "Stable";
                App.Logger.WriteLine(LOG_IDENT, $"{releaseType} update available: {currentVer} -> {releaseVer}");

                var result = await Frontend.ShowMessageBox(
                    $"A new {releaseType.ToLower()} version {releaseVer} is available. Would you like to update now?",
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo
                );

                if (result != MessageBoxResult.Yes)
                {
                    App.Logger.WriteLine(LOG_IDENT, "User declined the update.");
                    return false;
                }
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"No update required. Current: {currentVer}, Latest: {releaseVer}");
                return false;
            }
#else
    version = App.Version;
#endif

            SetStatus(Strings.Bootstrapper_Status_UpgradingFroststrap);

            try
            {
                string downloadLocation;

#if DEBUG_UPDATER
                downloadLocation = Path.Combine(Paths.TempUpdates, "Bloxstrap.exe");
                Directory.CreateDirectory(Paths.TempUpdates);
                File.Copy(Paths.Process, downloadLocation, overwrite: true);
#else
                if (App.IsMockReleaseEnabled)
                {
                    downloadLocation = Path.Combine(Paths.TempUpdates, Path.GetFileName(Paths.Process));
                    Directory.CreateDirectory(Paths.TempUpdates);
                    App.Logger.WriteLine(LOG_IDENT, $"Using local mock updater payload for {version}.");
                    File.Copy(Paths.Process, downloadLocation, overwrite: true);
                }
                else if (releaseInfo!.Assets is null || releaseInfo.Assets.Count == 0)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Release found but no assets were available for download.");
                    return false;
                }
                else
                {
                    var asset = releaseInfo.Assets[0];
                    downloadLocation = Path.Combine(Paths.TempUpdates, asset.Name);
                    Directory.CreateDirectory(Paths.TempUpdates);

                    App.Logger.WriteLine(LOG_IDENT, $"Downloading {version}...");

                    if (!File.Exists(downloadLocation))
                    {
                        using var response = await App.HttpClient.GetAsync(asset.BrowserDownloadUrl);
                        response.EnsureSuccessStatusCode();

                        await using var fileStream = new FileStream(downloadLocation, FileMode.Create, FileAccess.Write, FileShare.None);
                        await response.Content.CopyToAsync(fileStream);
                    }
                }
#endif

                App.Logger.WriteLine(LOG_IDENT, $"Starting updater {version}...");

                var startInfo = new ProcessStartInfo(downloadLocation) { UseShellExecute = true };
                startInfo.ArgumentList.Add("-upgrade");

                foreach (var arg in App.LaunchSettings.Args)
                    startInfo.ArgumentList.Add(arg);

                if (_launchMode == LaunchMode.Player && !startInfo.ArgumentList.Contains("-player"))
                    startInfo.ArgumentList.Add("-player");
                else if (_launchMode == LaunchMode.Studio && !startInfo.ArgumentList.Contains("-studio"))
                    startInfo.ArgumentList.Add("-studio");

                App.Settings.Save();

                using var updateLock = new InterProcessLock("AutoUpdater");

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    var result = await Frontend.ShowMessageBox(
                        string.Format(Strings.Bootstrapper_AutoUpdateFailed, version),
                        MessageBoxImage.Information,
                        MessageBoxButton.YesNo);

                    if (result == MessageBoxResult.Yes)
                        Utilities.ShellExecute(App.ProjectDownloadLink);

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the auto-updater");
                App.Logger.WriteException(LOG_IDENT, ex);

                var result = await Frontend.ShowMessageBox(
                    string.Format(Strings.Bootstrapper_AutoUpdateFailed, version),
                    MessageBoxImage.Information,
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                    Utilities.ShellExecute(App.ProjectDownloadLink);
            }

            return false;
        }

        #endregion

        #region Roblox Install

        private static bool TryDeleteRobloxInDirectory(string dir)
        {
            string[] executables = [App.RobloxPlayerAppName, App.RobloxStudioAppName];

            foreach (string exe in executables)
            {
                string path = Path.Combine(dir, exe);

                bool exists = OperatingSystem.IsMacOS() ? Directory.Exists(path) : File.Exists(path);
                if (!exists)
                    return true;

                try
                {
                    if (OperatingSystem.IsMacOS())
                        Directory.Delete(path, true);
                    else
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                        File.Delete(path);
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return true;
        }

        public static void CleanupVersionsFolder()
        {
            const string LOG_IDENT = "Bootstrapper::CleanupVersionsFolder";

            if (OperatingSystem.IsLinux())
            {
                bool isStudio = App.Bootstrapper?.IsStudioLaunch ?? false;
                if (!isStudio)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Skipping cleanup on Linux (Player) to protect Sober's data directory.");
                    return;
                }
            }

            if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Background updater tried to cleanup, stopping!");
                return;
            }

            if (!Directory.Exists(Paths.Versions))
            {
                App.Logger.WriteLine(LOG_IDENT, "Versions directory does not exist, skipping cleanup.");
                return;
            }

            foreach (string dir in Directory.GetDirectories(Paths.Versions))
            {
                string dirName = Path.GetFileName(dir);

                // to make static directory work on studio linux
                if (OperatingSystem.IsLinux() && dirName == "Sober")
                    continue;

                bool shouldDelete = StaticDirectory
                    ? dirName != "WindowsPlayer" && dirName != "WindowsStudio64" && dirName != "MacPlayer" && dirName != "MacStudio"
                    : dirName != App.PlayerState.Prop.VersionGuid && dirName != App.StudioState.Prop.VersionGuid;

                if (!shouldDelete)
                    continue;

                // check if it's still being used first
                // we dont want to accidentally delete the files of a running roblox instance
                if (!TryDeleteRobloxInDirectory(dir))
                    continue;

                if (OperatingSystem.IsWindows())
                    WindowsRegistry.EnableFullscreenOptimizations(Path.Join(dir, "RobloxPlayerBeta.exe"));

                try
                {
                    Directory.Delete(dir, true);
                }
                catch (UnauthorizedAccessException)
                {
                    try
                    {
                        Filesystem.AssertReadOnlyDirectory(dir);
                        Directory.Delete(dir, true);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {dir}");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }
                catch (IOException ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {dir}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }

        private void MigrateCompatibilityFlags()
        {
            if (!OperatingSystem.IsWindows())
                return;

            const string LOG_IDENT = "Bootstrapper::MigrateCompatibilityFlags";

            string oldClientLocation = Path.Combine(Paths.Versions, AppData.DistributionState.VersionGuid, AppData.ExecutableName);
            string newClientLocation = Path.Combine(_latestVersionDirectory, AppData.ExecutableName);

            // move old compatibility flags for the old location
            using RegistryKey appFlagsKey = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers");
            string? appFlags = appFlagsKey.GetValue(oldClientLocation) as string;

            if (appFlags is not null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Migrating app compatibility flags from {oldClientLocation} to {newClientLocation}...");
                appFlagsKey.SetValueSafe(newClientLocation, appFlags);
                appFlagsKey.DeleteValueSafe(oldClientLocation);
            }
        }

        private void KillRobloxPlayers()
        {
            const string LOG_IDENT = "Bootstrapper::KillRobloxPlayers";

            var processesToKill = new List<Process>();
            string playerProcessName = OperatingSystem.IsMacOS() ? "RobloxPlayer" : "RobloxPlayerBeta";
            processesToKill.AddRange(Process.GetProcessesByName(playerProcessName));
            processesToKill.AddRange(Process.GetProcessesByName("RobloxCrashHandler"));

            foreach (Process process in processesToKill)
            {
                try
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Terminating process {process.ProcessName} ({process.Id})");
                    process.Kill();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to close process {process.Id}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            string studioProcessName = OperatingSystem.IsMacOS() ? "RobloxStudio" : "RobloxStudioBeta";
            var studioProcesses = Process.GetProcessesByName(studioProcessName);

            if (studioProcesses.Length == 0)
                return;

            App.Logger.WriteLine(LOG_IDENT, "Waiting for Roblox Studio processes to exit...");
            SetStatus("Waiting for Roblox Studio...");

            while (Process.GetProcessesByName(studioProcessName).Length > 0)
            {
                Thread.Sleep(1000);

                if (_cancelTokenSource.IsCancellationRequested)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Studio wait cancelled by user.");
                    return;
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "All Roblox Studio processes closed.");
        }

        private async Task UpgradeRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::UpgradeRoblox";

            bool cancelUpgrade = !App.Settings.Prop.UpdateRoblox;

            if (cancelUpgrade)
            {
                SetStatus(Strings.Bootstrapper_Status_CancelUpgrade);
                App.Logger.WriteLine(LOG_IDENT, "Upgrading disabled, cancelling the upgrade.");

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

            if (!App.LaunchSettings.BackgroundUpdaterFlag.Active)
                KillRobloxPlayers();

            if (!App.LaunchSettings.BackgroundUpdaterFlag.Active && Directory.Exists(_latestVersionDirectory))
            {
                try { Directory.Delete(_latestVersionDirectory, true); }
                catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
            }

            Directory.CreateDirectory(_latestVersionDirectory);

            var packages = _versionPackageManifest
                    .OrderByDescending(p => p.PackedSize)
                    .ToList();

            var cachedPackageHashes = Directory.GetFiles(Paths.Downloads)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Select(name => name!)
                .ToHashSet();

            int totalSizeRequired = packages
                .Where(x => x.Signature != null && !cachedPackageHashes.Contains(x.Signature))
                .Sum(x => x.PackedSize) + packages.Sum(x => x.Size);

            if (Filesystem.GetFreeDiskSpace(Paths.Base) < totalSizeRequired)
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

                int totalPackedSize = packages.Sum(package => package.PackedSize);
                _progressIncrement = totalPackedSize > 0 ? (double)ProgressBarMaximum / totalPackedSize : 0;
                _taskbarProgressMaximum = TaskbarProgressMaximum;
                _taskbarProgressIncrement = totalPackedSize > 0 ? _taskbarProgressMaximum / (double)totalPackedSize : 0;
            }

            int totalPackages = packages.Count;
            int processedPackages = 0;
            int completedDownloads = 0;
            int totalDownloads = packages.Count(p => p.Name != "WebView2RuntimeInstaller.zip");
            var downloadsTcs = new TaskCompletionSource<bool>();
            if (totalDownloads == 0) downloadsTcs.TrySetResult(true);
            int maxConcurrency = MaxThreadDownload > 0 ? MaxThreadDownload : 1;
            using var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task>();
            var extractionSuccesses = new ConcurrentBag<bool>();

            foreach (var package in packages)
            {
                if (_cancelTokenSource.IsCancellationRequested) break;

                await semaphore.WaitAsync(_cancelTokenSource.Token);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (_cancelTokenSource.IsCancellationRequested) return;

                        int remaining = totalPackages - Interlocked.Increment(ref processedPackages);

                        bool packageExists = File.Exists(package.DownloadPath);
                        if (packageExists)
                        {
                            string calculatedMD5 = MD5Hash.FromFile(package.DownloadPath);
                            if (!OperatingSystem.IsMacOS() && calculatedMD5 != package.Signature)
                            {
                                App.Logger.WriteLine(LOG_IDENT, $"Package {package.Name} is corrupted ({calculatedMD5} != {package.Signature})! Deleting and re-downloading...");
                                File.Delete(package.DownloadPath);
                                packageExists = false;
                            }
                            else
                            {
                                App.Logger.WriteLine(LOG_IDENT, $"Package {package.Name} already exists in cache, skipping download...");
                                Interlocked.Add(ref _totalDownloadedBytes, package.PackedSize);
                                UpdateProgressBar();
                            }
                        }

                        if (!packageExists)
                        {
                            string downloadMsg = remaining > 0
                                ? $"{Strings.Bootstrapper_Status_Downloading} {package.Name} - {remaining} {Strings.Bootstrapper_Status_PackagesLeft}"
                                : $"{Strings.Bootstrapper_Status_Downloading} {package.Name}...";
                            SetStatus(downloadMsg);
                            await DownloadPackage(package);
                        }

                        if (Interlocked.Increment(ref completedDownloads) == totalDownloads)
                            downloadsTcs.TrySetResult(true);

                        if (package.Name != "WebView2RuntimeInstaller.zip")
                        {
                            string extractMsg = remaining > 0
                                ? $"{Strings.Bootstrapper_Status_Extracting} {package.Name} - {remaining} {Strings.Bootstrapper_Status_PackagesLeft}"
                                : $"{Strings.Bootstrapper_Status_Extracting} {package.Name}...";
                            SetStatus(extractMsg);
                            bool success = await ExtractPackage(package);
                            extractionSuccesses.Add(success);
                        }
                        else
                        {
                            extractionSuccesses.Add(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Error processing {package.Name}: {ex.Message}");
                        extractionSuccesses.Add(false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, _cancelTokenSource.Token));
            }

            await Task.WhenAll(tasks);
            _packageExtractionSuccess = extractionSuccesses.All(s => s);

            if (_cancelTokenSource.IsCancellationRequested) return;

            if (Dialog is not null)
            {
                Dialog.ProgressIndeterminate = true;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
                SetStatus(Strings.Bootstrapper_Status_Configuring);
            }

            if (OperatingSystem.IsWindows() && App.State.Prop.PromptWebView2Install)
            {
                using var hklmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                using var hkcuKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");

                if (hklmKey == null && hkcuKey == null)
                {
                    var result = await Frontend.ShowMessageBox(Strings.Bootstrapper_WebView2NotFound, MessageBoxImage.Warning, MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        var package = _versionPackageManifest.Find(x => x.Name == "WebView2RuntimeInstaller.zip");
                        if (package != null)
                        {
                            string baseDir = Path.Combine(_latestVersionDirectory, PackageDirectoryMap[package.Name]);
                            await ExtractPackage(package);
                            SetStatus(Strings.Bootstrapper_Status_InstallingWebView2);
                            await Process.Start(new ProcessStartInfo
                            {
                                FileName = Path.Combine(baseDir, "MicrosoftEdgeWebview2Setup.exe"),
                                Arguments = "/silent /install",
                                WorkingDirectory = baseDir
                            })!.WaitForExitAsync();
                            Directory.Delete(baseDir, true);
                        }
                    }
                    else
                    {
                        App.State.Prop.PromptWebView2Install = false;
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

                    string macOsDir = Path.Combine(appPath, "Contents", "MacOS");
                    if (Directory.Exists(macOsDir))
                    {
                        foreach (string file in Directory.GetFiles(macOsDir))
                        {
                            var fileInfo = new FileInfo(file);
                            fileInfo.UnixFileMode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                        }
                    }
                    Process.Start("xattr", $"-dr com.apple.quarantine \"{appPath}\"")?.WaitForExit();
                }
            }

            MigrateCompatibilityFlags();
            AppData.DistributionState.VersionGuid = _latestVersionGuid;
            AppData.DistributionState.PackageHashes.Clear();

            foreach (var package in _versionPackageManifest)
                AppData.DistributionState.PackageHashes.Add(package.Name, package.Signature);

            CleanupVersionsFolder();

            if (!App.Settings.Prop.DebugDisableVersionPackageCleanup)
            {
                var activeHashes = App.PlayerState.Prop.PackageHashes.Values
                    .Concat(App.StudioState.Prop.PackageHashes.Values)
                    .Where(h => h != null)
                    .ToHashSet();

                foreach (string hash in cachedPackageHashes)
                {
                    if (!activeHashes.Contains(hash))
                    {
                        try { File.Delete(Path.Combine(Paths.Downloads, hash)); }
                        catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
                    }
                }
            }

            int totalSize = App.PlayerState.Prop.Size + App.StudioState.Prop.Size;
            if (OperatingSystem.IsWindows())
            {
                using var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey);
                uninstallKey.SetValueSafe("EstimatedSize", totalSize);
                WindowsRegistry.RegisterClientLocation(IsStudioLaunch, _latestVersionDirectory);
            }

            App.State.Prop.ForceReinstall = false;
            App.State.Save();
            AppData.DistributionStateManager.Save();
            _isInstalling = false;
        }

        private static string? ParseFlatpakInstallStep(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            line = Regex.Replace(line, @"\x1B\[[0-9;?]*[ -/]*[@-~]", string.Empty).Replace('\r', ' ').Trim();

            int installingIndex = line.IndexOf("Installing", StringComparison.OrdinalIgnoreCase);
            if (installingIndex < 0)
                return null;

            return line[installingIndex..].Trim();
        }

        private async Task<bool> EnsureSoberInstalledAsync()
        {
            const string LOG_IDENT = "Bootstrapper::EnsureSoberInstalled";

            SetStatus("Checking Flatpak installation...");

            var flatpakCheck = new ProcessStartInfo
            {
                FileName = "flatpak",
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var checkProcess = Process.Start(flatpakCheck);
                _ = checkProcess ?? throw new InvalidOperationException("Failed to start flatpak process.");

                await checkProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));

                if (checkProcess.ExitCode != 0)
                    throw new InvalidOperationException("Flatpak returned a non-zero exit code.");
            }
            catch (TimeoutException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Timed out while checking Flatpak installation.");
                App.Logger.WriteException(LOG_IDENT, ex);
                await Frontend.ShowMessageBox(
                    "Timed out while checking Flatpak installation. Please make sure Flatpak is working and try again.",
                    MessageBoxImage.Error
                );
                App.Terminate(ErrorCode.ERROR_CANCELLED);
                return false;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Flatpak not found.");
                App.Logger.WriteException(LOG_IDENT, ex);
                await Frontend.ShowMessageBox(
                    "Flatpak is required on Linux.\n\nPlease install Flatpak first, then launch Froststrap again.",
                    MessageBoxImage.Error
                );
                App.Terminate(ErrorCode.ERROR_CANCELLED);
                return false;
            }

            var soberCheck = new ProcessStartInfo
            {
                FileName = "flatpak",
                Arguments = $"info {SoberFlatpakId}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var soberProcess = Process.Start(soberCheck);
                if (soberProcess is not null)
                {
                    await soberProcess.WaitForExitAsync();
                    if (soberProcess.ExitCode == 0)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Sober is already installed.");
                        SetStatus("Starting Sober...");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to check Sober installation status.");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            App.Logger.WriteLine(LOG_IDENT, "Installing Sober...");

            if (Dialog is not null)
            {
                Dialog.ProgressIndeterminate = true;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
            }

            SetStatus("Installing Sober...");

            var installStartInfo = new ProcessStartInfo
            {
                FileName = "flatpak",
                Arguments = $"install --assumeyes --noninteractive flathub {SoberFlatpakId}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var installProcess = Process.Start(installStartInfo);
            if (installProcess is null)
            {
                await Frontend.ShowMessageBox(
                    "Failed to start Sober installation via Flatpak.",
                    MessageBoxImage.Error
                );
                App.Terminate(ErrorCode.ERROR_CANCELLED);
                return false;
            }

            var errorLines = new List<string>();

            async Task ReadInstallStream(StreamReader reader)
            {
                while (true)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line is null)
                        break;

                    App.Logger.WriteLine(LOG_IDENT, $"[flatpak] {line}");

                    string? installStep = ParseFlatpakInstallStep(line);
                    if (!string.IsNullOrEmpty(installStep))
                        SetStatus(installStep);
                    else if (!string.IsNullOrWhiteSpace(line))
                        errorLines.Add(line.Trim());
                }
            }

            await Task.WhenAll(
                ReadInstallStream(installProcess.StandardOutput),
                ReadInstallStream(installProcess.StandardError),
                installProcess.WaitForExitAsync()
            );

            if (installProcess.ExitCode != 0)
            {
                string details = string.Join('\n', errorLines.TakeLast(8));
                await Frontend.ShowMessageBox(
                    $"Failed to install {SoberFlatpakId} via Flatpak.{(string.IsNullOrWhiteSpace(details) ? string.Empty : $"\n\n{details}")}",
                    MessageBoxImage.Error
                );
                App.Terminate(ErrorCode.ERROR_CANCELLED);
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, "Sober installation complete.");
            SetStatus("Starting Sober...");
            return true;
        }

        private async Task UpdateSoberFlatpakAsync()
        {
            const string LOG_IDENT = "Bootstrapper::UpdateSoberFlatpak";

            App.Logger.WriteLine(LOG_IDENT, $"Running 'flatpak update {SoberFlatpakId}'.");
            SetStatus("Updating Sober...");

            var updateStartInfo = new ProcessStartInfo
            {
                FileName = "flatpak",
                Arguments = $"update {SoberFlatpakId} --assumeyes",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process? updateProcess = null;
            try
            {
                using var process = Process.Start(updateStartInfo);
                updateProcess = process;
                if (updateProcess is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to start flatpak update process.");
                    return;
                }

                List<string> lines = [];
                object lineLock = new();

                async Task ReadUpdateStream(StreamReader reader)
                {
                    while (true)
                    {
                        string? line = await reader.ReadLineAsync();
                        if (line is null)
                            break;

                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            lock (lineLock)
                            {
                                lines.Add(line.Trim());
                            }

                            App.Logger.WriteLine(LOG_IDENT, $"[flatpak] {line}");
                        }
                    }
                }

                await Task.WhenAll(
                    ReadUpdateStream(updateProcess.StandardOutput),
                    ReadUpdateStream(updateProcess.StandardError),
                    updateProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3))
                );

                if (updateProcess.ExitCode != 0)
                {
                    string details = string.Join('\n', lines.TakeLast(8));
                    App.Logger.WriteLine(LOG_IDENT, $"flatpak update exited with code {updateProcess.ExitCode}. {details}");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "Sober update check finished successfully.");
                }
            }
            catch (TimeoutException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Timed out while updating Sober.");
                App.Logger.WriteException(LOG_IDENT, ex);

                if (updateProcess is not null && !updateProcess.HasExited)
                    updateProcess.Kill(true);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to update Sober.");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        #region Linux Studio Monstrosity (dont ask questions)

        private async Task EnsureWebView2ViaWineAsync()
        {
            const string LOG_IDENT = "Bootstrapper::EnsureWebView2ViaWine";

            if (!App.Settings.Prop.EnableWebView2)
            {
                string? installedVersion = await GetWebView2InstalledVersionAsync();
                if (!string.IsNullOrEmpty(installedVersion))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"WebView2 disabled, uninstalling version {installedVersion}");
                    await UninstallWebView2Async(installedVersion);
                    if (!string.IsNullOrEmpty(App.Settings.Prop.WinePrefixPath))
                    {
                        string markerPath = Path.Combine(App.Settings.Prop.WinePrefixPath, ".webview2_version");
                        if (File.Exists(markerPath))
                            File.Delete(markerPath);
                    }
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "WebView2 disabled and not installed, nothing to do.");
                }
                return;
            }

            if (string.IsNullOrEmpty(App.Settings.Prop.WinePrefixPath) || !Directory.Exists(App.Settings.Prop.WinePrefixPath))
            {
                App.Logger.WriteLine(LOG_IDENT, "Wine prefix not ready, skipping WebView2 check.");
                return;
            }

            string? currentInstalledVersion = await GetWebView2InstalledVersionAsync();

            if (currentInstalledVersion != null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"WebView2 already installed (version {currentInstalledVersion}), skipping.");
                return;
            }

            string? latestVersion = null;
            string? installerUrl = null;
            if (string.IsNullOrEmpty(installerUrl))
                return;

            string downloadPath = Path.Combine(Paths.Downloads, $"WebView2_{latestVersion}.exe");
            if (!File.Exists(downloadPath))
            {
                SetStatus("Downloading WebView2...");
                await DownloadFileWithProgressAsync(installerUrl, downloadPath);
            }

            if (Dialog != null)
            {
                Dialog.ProgressIndeterminate = true;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
            }

            try
            {
                string overrideKey = @"HKCU\Software\Wine\AppDefaults\msedgewebview2.exe";
                using var checkProc = Process.Start(new ProcessStartInfo(App.Settings.Prop.WineBinaryPath, $"reg query \"{overrideKey}\" /v Version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (checkProc != null)
                {
                    checkProc.StartInfo.Environment["WINEPREFIX"] = App.Settings.Prop.WinePrefixPath;
                    checkProc.StartInfo.Environment["WINEARCH"] = "win64";
                    await checkProc.WaitForExitAsync();
                    if (checkProc.ExitCode != 0)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Setting msedgewebview2.exe Windows version to win7");
                        using var setProc = Process.Start(new ProcessStartInfo(App.Settings.Prop.WineBinaryPath, $"reg add \"{overrideKey}\" /v Version /t REG_SZ /d win7 /f")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        if (setProc != null)
                        {
                            setProc.StartInfo.Environment["WINEPREFIX"] = App.Settings.Prop.WinePrefixPath;
                            setProc.StartInfo.Environment["WINEARCH"] = "win64";
                            await setProc.WaitForExitAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Could not set version override: {ex.Message}");
            }

            SetStatus("Installing WebView2...");
            App.Logger.WriteLine(LOG_IDENT, "Running WebView2 installer");

            string installerArgs = "--msedgewebview --do-not-launch-msedge --system-level";
            var psi = new ProcessStartInfo(App.Settings.Prop.WineBinaryPath, $"\"{downloadPath}\" {installerArgs}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Paths.Downloads
            };
            psi.Environment["WINEPREFIX"] = App.Settings.Prop.WinePrefixPath;
            psi.Environment["WINEARCH"] = "win64";

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to start WebView2 installer.");
                return;
            }

            _ = Task.Run(async () =>
            {
                try { while (await proc.StandardOutput.ReadLineAsync() is { } line) App.Logger.WriteLine(LOG_IDENT, $"[webview2-out] {line}"); } catch { }
            });
            _ = Task.Run(async () =>
            {
                try { while (await proc.StandardError.ReadLineAsync() is { } line) App.Logger.WriteLine(LOG_IDENT, $"[webview2-err] {line}"); } catch { }
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            await proc.WaitForExitAsync(cts.Token);

            if (proc.ExitCode == 0)
            {
                string markerPath = Path.Combine(App.Settings.Prop.WinePrefixPath, ".webview2_version");
                await File.WriteAllTextAsync(markerPath, latestVersion ?? "unknown");
                App.Logger.WriteLine(LOG_IDENT, "WebView2 installed successfully.");
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"WebView2 installer exited with code {proc.ExitCode}");
            }
        }

        private async Task UninstallWebView2Async(string version)
        {
            const string LOG_IDENT = "Bootstrapper::UninstallWebView2Async";
            if (string.IsNullOrEmpty(App.Settings.Prop.WinePrefixPath))
                return;

            string uninstallerPath = Path.Combine(App.Settings.Prop.WinePrefixPath, "drive_c", "Program Files (x86)", "Microsoft",
                "EdgeWebView", "Application", version, "Installer", "setup.exe");

            if (!File.Exists(uninstallerPath))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Uninstaller not found at {uninstallerPath}, skipping.");
                return;
            }

            SetStatus($"Uninstalling WebView2 {version}...");
            if (Dialog != null)
            {
                Dialog.ProgressIndeterminate = true;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
            }

            var psi = new ProcessStartInfo(App.Settings.Prop.WineBinaryPath, $"\"{uninstallerPath}\" --msedgewebview --uninstall --system-level --force-uninstall")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.Environment["WINEPREFIX"] = App.Settings.Prop.WinePrefixPath;
            psi.Environment["WINEARCH"] = "win64";

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to start uninstaller.");
                return;
            }

            _ = Task.Run(async () =>
            {
                try { while (await proc.StandardOutput.ReadLineAsync() is { } line) App.Logger.WriteLine(LOG_IDENT, $"[uninstall-out] {line}"); } catch { }
            });
            _ = Task.Run(async () =>
            {
                try { while (await proc.StandardError.ReadLineAsync() is { } line) App.Logger.WriteLine(LOG_IDENT, $"[uninstall-err] {line}"); } catch { }
            });

            await proc.WaitForExitAsync();

            if (proc.ExitCode == 0)
                App.Logger.WriteLine(LOG_IDENT, $"WebView2 {version} uninstalled successfully.");
            else
                App.Logger.WriteLine(LOG_IDENT, $"Uninstaller exited with code {proc.ExitCode}");
        }

        private static async Task<string?> GetWebView2InstalledVersionAsync()
        {
            if (string.IsNullOrEmpty(App.Settings.Prop.WineBinaryPath) || string.IsNullOrEmpty(App.Settings.Prop.WinePrefixPath))
                return null;

            const string versionPath = @"HKLM\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView";
            var psi = new ProcessStartInfo(App.Settings.Prop.WineBinaryPath, $"reg query \"{versionPath}\" /v DisplayVersion")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.Environment["WINEPREFIX"] = App.Settings.Prop.WinePrefixPath;
            psi.Environment["WINEARCH"] = "win64";

            using var proc = Process.Start(psi);
            if (proc == null) return null;
            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            var match = Regex.Match(output, @"DisplayVersion\s+REG_SZ\s+(\S+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        private async Task<bool> RunWineCmdAsync(string args, int timeoutMinutes = 5, bool captureOutput = false)
        {
            const string LOG_IDENT = "Bootstrapper::RunWineCmd";
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cancelTokenSource.Token, cts.Token);

            var psi = new ProcessStartInfo(App.Settings.Prop.WineBinaryPath, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = captureOutput,
                RedirectStandardError = !captureOutput,
                CreateNoWindow = true
            };
            psi.Environment["WINEPREFIX"] = App.Settings.Prop.WinePrefixPath ?? "";
            psi.Environment["WINEARCH"] = "win64";
            psi.Environment["WINEDLLOVERRIDES"] = "mscoree,mshtml=";

            App.Logger.WriteLine(LOG_IDENT, $"Starting: {App.Settings.Prop.WineBinaryPath} {args}");
            using var proc = Process.Start(psi);
            if (proc == null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to start process.");
                return false;
            }

            string output = "";
            if (captureOutput)
                output = await proc.StandardOutput.ReadToEndAsync();

            try
            {
                await proc.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Timeout/killed: {args}");
                proc.Kill(true);
                return false;
            }

            if (proc.ExitCode != 0)
                App.Logger.WriteLine(LOG_IDENT, $"Exit {proc.ExitCode}: {args}");
            return proc.ExitCode == 0;
        }

        private static async Task<string> RunProcessAndReadOutputAsync(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return string.Empty;
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }

        private static string GetWineLogDirectory()
        {
            if (string.IsNullOrEmpty(App.Settings.Prop.WinePrefixPath))
            {
                App.Logger.WriteLine("Bootstrapper::GetWineLogDirectory", "Wine prefix not set, using default.");
                App.Settings.Prop.WinePrefixPath = Path.Combine(Paths.Base, "WinePrefix");
                App.Settings.Save();
            }

            string systemUser = Environment.UserName;
            string logPath = Path.Combine(App.Settings.Prop.WinePrefixPath, "drive_c", "users", systemUser, "AppData", "Local", "Roblox", "logs");

            if (!Directory.Exists(logPath))
            {
                try
                {
                    Directory.CreateDirectory(logPath);
                    App.Logger.WriteLine("Bootstrapper::GetWineLogDirectory", $"Created log directory: {logPath}");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("Bootstrapper::GetWineLogDirectory", $"Failed to create log directory: {ex.Message}");
                }
            }

            return logPath;
        }

        private async Task<bool> EnsureWineAndDependenciesAsync()
        {
            const string LOG_IDENT = "Bootstrapper::EnsureWineAndDependencies";

            SetStatus("Checking dependencies");

            string wineInstallBase = Path.Combine(Paths.Base, KombuchaWineDir);
            string versionMarker = Path.Combine(wineInstallBase, ".version");
            bool kombuchaAvailable = File.Exists(Path.Combine(Paths.Base, KombuchaSymlink, "bin", "wine"));

            if (!kombuchaAvailable || (File.Exists(versionMarker) && await NeedsNewWineVersionAsync(versionMarker)))
            {
                if (Dialog != null)
                {
                    Dialog.ProgressIndeterminate = false;
                    Dialog.ProgressMaximum = ProgressBarMaximum;
                    Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;
                }

                if (!await DownloadAndExtractWineAsync())
                {
                    App.Logger.WriteLine(LOG_IDENT, "Kombucha download failed, falling back to system Wine.");
                    string? systemWine = await FindSystemWineAsync();
                    App.Settings.Prop.WineBinaryPath = systemWine ?? "wine";
                }
            }
            else
            {
                App.Settings.Prop.WineBinaryPath = Path.Combine(Paths.Base, KombuchaSymlink, "bin", "wine");
            }

            App.Settings.Save();

            var testPsi = new ProcessStartInfo(App.Settings.Prop.WineBinaryPath, "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var testProc = Process.Start(testPsi);
            if (testProc == null)
            {
                await Frontend.ShowMessageBox("Failed to start Wine process.", MessageBoxImage.Error);
                return false;
            }
            await testProc.WaitForExitAsync();
            if (testProc.ExitCode != 0)
            {
                await Frontend.ShowMessageBox("Wine is not working correctly.", MessageBoxImage.Error);
                return false;
            }

            App.Settings.Prop.WinePrefixPath = Path.Combine(Paths.Base, WinePrefixDir);
            Directory.CreateDirectory(App.Settings.Prop.WinePrefixPath);
            App.Settings.Save();

            Environment.SetEnvironmentVariable("WINEPREFIX", App.Settings.Prop.WinePrefixPath);
            Environment.SetEnvironmentVariable("WINEARCH", "win64");

            string stampFile = Path.Combine(App.Settings.Prop.WinePrefixPath, ".wineprefix_stamp");
            bool needInit = !File.Exists(stampFile) || await NeedsWineUpdateAsync();

            if (needInit)
            {
                SetStatus("Initializing Wine prefix...");
                if (Dialog != null)
                {
                    Dialog.ProgressIndeterminate = true;
                    Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
                }

                if (!await RunWineCmdAsync("wineboot -u", 15))
                {
                    await Frontend.ShowMessageBox("Failed to initialise Wine prefix.", MessageBoxImage.Error);
                    return false;
                }

                string wineInf = Path.Combine(Path.GetDirectoryName(App.Settings.Prop.WineBinaryPath) ?? "", "../share/wine/wine.inf");
                if (File.Exists(wineInf))
                {
                    long stamp = File.GetLastWriteTimeUtc(wineInf).ToFileTimeUtc();
                    await File.WriteAllTextAsync(stampFile, stamp.ToString());
                }
                else
                {
                    await File.WriteAllTextAsync(stampFile, "1");
                }

                await ApplyWineRegistryTweaksAsync();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Wine prefix is up to date.");
                await RunWineCmdAsync("wineserver -p6", 1);
            }

            await SetupVulkanLayerAsync();

            var renderer = App.Settings.Prop.StudioRenderer;
            bool useDxvk = (renderer == StudioRenderer.DXVK || renderer == StudioRenderer.DXVKSarek);
            if (useDxvk)
            {
                SetStatus("Setting up DXVK...");
                if (!await SetupDxvkAsync())
                    App.Logger.WriteLine(LOG_IDENT, "DXVK installation failed, continuing without it.");
            }
            else
            {
                await RestoreOriginalDllsAsync();
            }

            await EnsureWebView2ViaWineAsync();

            return true;
        }

        private async Task RestoreOriginalDllsAsync()
        {
            const string LOG_IDENT = "Bootstrapper::RestoreOriginalDlls";
            if (string.IsNullOrEmpty(App.Settings.Prop.WinePrefixPath) || string.IsNullOrEmpty(_latestVersionDirectory))
                return;

            string[] dlls = [ "d3d9.dll", "d3d10core.dll", "d3d11.dll", "dxgi.dll" ];
            string sourceDir = Path.Combine(App.Settings.Prop.WinePrefixPath, "drive_c", "windows", "system32");
            string targetDir = _latestVersionDirectory;

            foreach (string dll in dlls)
            {
                string src = Path.Combine(sourceDir, dll);
                string dst = Path.Combine(targetDir, dll);
                if (File.Exists(src))
                {
                    try
                    {
                        Filesystem.AssertReadOnly(dst);
                        File.Copy(src, dst, true);
                        App.Logger.WriteLine(LOG_IDENT, $"Restored {dll}");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to restore {dll}: {ex.Message}");
                    }
                }
                else if (File.Exists(dst))
                {
                    try
                    {
                        Filesystem.AssertReadOnly(dst);
                        File.Delete(dst);
                        App.Logger.WriteLine(LOG_IDENT, $"Removed orphaned {dll}");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {dll}: {ex.Message}");
                    }
                }
            }
            await Task.CompletedTask;
        }

        private static async Task<bool> NeedsWineUpdateAsync()
        {
            string stampFile = Path.Combine(App.Settings.Prop.WinePrefixPath, ".wineprefix_stamp");
            if (!File.Exists(stampFile)) return true;

            string content = await File.ReadAllTextAsync(stampFile);
            if (content == "disable")
                return false;

            if (!long.TryParse(content, out long lastUpdate))
                return true;

            string wineInf = Path.Combine(Path.GetDirectoryName(App.Settings.Prop.WineBinaryPath) ?? "", "../share/wine/wine.inf");
            if (!File.Exists(wineInf))
                return true;

            long currentStamp = File.GetLastWriteTimeUtc(wineInf).ToFileTimeUtc();
            return lastUpdate != currentStamp;
        }

        private static async Task<bool> NeedsNewWineVersionAsync(string versionMarkerFile)
        {
            if (!File.Exists(versionMarkerFile))
                return true;
            string currentTag = await File.ReadAllTextAsync(versionMarkerFile);
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Froststrap");
            var url = $"https://api.github.com/repos/{KombuchaRepoOwner}/{KombuchaRepoName}/releases/latest";
            var json = await client.GetStringAsync(url);
            var release = JsonSerializer.Deserialize<GithubRelease>(json);
            return release?.TagName != currentTag;
        }

        private async Task DownloadFileWithProgressAsync(string url, string destination)
        {
            using var response = await App.HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
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
                UpdateProgressBar();
            }

            if (totalRead != totalBytes)
                throw new IOException($"Downloaded {totalRead} bytes but expected {totalBytes}");
        }

        private static bool IsFlatpak() => File.Exists("/.flatpak-info");

        private static void CreateSymlink(string symlinkPath, string targetPath)
        {
            if (OperatingSystem.IsWindows())
                throw new NotSupportedException("Symlinks not supported on Windows in this context.");
            var psi = new ProcessStartInfo("ln", $"-sf \"{targetPath}\" \"{symlinkPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
        }

        private static bool IsValidWineBinaryPath(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                byte[] magic = new byte[4];
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                if (fs.Read(magic, 0, 4) < 4) return false;
                return magic[0] == 0x7F && magic[1] == 'E' && magic[2] == 'L' && magic[3] == 'F';
            }
            catch { return false; }
        }

        private static async Task<string?> FindSystemWineAsync()
        {
            string[] paths = [ "/usr/bin/wine", "/usr/local/bin/wine" ];
            foreach (var p in paths)
                if (File.Exists(p)) return p;
            try
            {
                string result = await RunProcessAndReadOutputAsync("which", "wine");
                return result?.Trim();
            }
            catch { return null; }
        }

        private async Task<bool> DownloadAndExtractWineAsync()
        {
            const string LOG_IDENT = "Bootstrapper::DownloadAndExtractWine";
            try
            {
                var release = await GetLatestKombuchaReleaseAsync();
                string tag = release.TagName ?? throw new Exception("No tag");
                string wineInstallBase = Path.Combine(Paths.Base, KombuchaWineDir);
                string versionedDir = Path.Combine(wineInstallBase, $"kombucha-{tag}");
                string symlinkPath = Path.Combine(Paths.Base, KombuchaSymlink);

                if (Directory.Exists(versionedDir))
                {
                    string existingWineBinary = Path.Combine(versionedDir, "bin", "wine");
                    if (IsValidWineBinaryPath(existingWineBinary))
                    {
                        CreateSymlink(symlinkPath, versionedDir);
                        App.Settings.Prop.WineBinaryPath = Path.Combine(symlinkPath, "bin", "wine");
                        App.Settings.Save();
                        return true;
                    }
                    else
                    {
                        Directory.Delete(versionedDir, true);
                    }
                }

                var asset = release.Assets?.FirstOrDefault(a => a.Name?.EndsWith(".tar.xz") == true);
                if (asset is null) throw new Exception("No .tar.xz asset found");

                string cacheDir = Path.Combine(Paths.Cache, "kombucha");
                Directory.CreateDirectory(cacheDir);
                string cachedArchive = Path.Combine(cacheDir, $"{tag}.tar.xz");

                if (!File.Exists(cachedArchive))
                {
                    SetStatus("Downloading Wine (Kombucha)...");
                    await DownloadFileWithProgressAsync(asset.BrowserDownloadUrl, cachedArchive);
                }

                SetStatus("Extracting Wine...");
                if (Dialog != null)
                {
                    Dialog.ProgressIndeterminate = true;
                    Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
                }

                if (Directory.Exists(versionedDir)) Directory.Delete(versionedDir, true);
                Directory.CreateDirectory(versionedDir);

                var extractPsi = new ProcessStartInfo("tar", $"-xJf \"{cachedArchive}\" -C \"{versionedDir}\" --strip-components=1")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var extractProc = Process.Start(extractPsi) ?? throw new Exception("tar process failed");
                await extractProc.WaitForExitAsync();
                if (extractProc.ExitCode != 0) throw new Exception("tar extraction failed");

                string newWineBinary = Path.Combine(versionedDir, "bin", "wine");
                if (!IsValidWineBinaryPath(newWineBinary))
                    throw new Exception("Extracted Wine binary is invalid");

                if (Directory.Exists(symlinkPath)) Directory.Delete(symlinkPath);
                CreateSymlink(symlinkPath, versionedDir);

                App.Settings.Prop.WineBinaryPath = Path.Combine(symlinkPath, "bin", "wine");
                App.Settings.Save();

                if (Dialog != null)
                {
                    Dialog.ProgressIndeterminate = false;
                    Dialog.ProgressValue = ProgressBarMaximum;
                    Dialog.TaskbarProgressValue = 1.0;
                }

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error: {ex.Message}");
                return false;
            }
        }

        private static async Task<GithubRelease> GetLatestKombuchaReleaseAsync()
        {
            string url = $"https://api.github.com/repos/{KombuchaRepoOwner}/{KombuchaRepoName}/releases/latest";
            string json = await App.HttpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<GithubRelease>(json) ?? throw new Exception("Failed to parse release");
        }

        private async Task ApplyWineRegistryTweaksAsync()
        {
            const string LOG_IDENT = "Bootstrapper::ApplyRegistryTweaks";
            if (string.IsNullOrEmpty(App.Settings.Prop.WinePrefixPath))
                return;

            string appliedMarker = Path.Combine(App.Settings.Prop.WinePrefixPath, ".shellfolders_applied");
            if (File.Exists(appliedMarker))
                return;

            try
            {
                string localAppData = $"Z:{Paths.Base.Replace('/', '\\')}\\appdata";
                string documents = $"Z:{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments).Replace('/', '\\')}";
                string pictures = $"Z:{Environment.GetFolderPath(Environment.SpecialFolder.MyPictures).Replace('/', '\\')}";

                Directory.CreateDirectory(Path.Combine(Paths.Base, "appdata"));

                string tempDir = Paths.Temp;
                Directory.CreateDirectory(tempDir);
                string regFile = Path.Combine(tempDir, "vinegar_folders.reg");

                var regContent = $@"
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders]
""Local AppData""=hex(2):{ToRegHex(localAppData)}
""Documents""=hex(2):{ToRegHex(documents)}
""My Pictures""=hex(2):{ToRegHex(pictures)}
";
                await File.WriteAllTextAsync(regFile, regContent);

                var psi = new ProcessStartInfo(App.Settings.Prop.WineBinaryPath, $"regedit /S \"{regFile}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.Environment["WINEPREFIX"] = App.Settings.Prop.WinePrefixPath;
                psi.Environment["WINEARCH"] = "win64";
                using var p = Process.Start(psi);
                if (p != null) await p.WaitForExitAsync();

                try { File.Delete(regFile); } catch { }

                await File.WriteAllTextAsync(appliedMarker, DateTime.UtcNow.ToString("o"));
                await RunWineCmdAsync("wineboot -r", 2);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to apply folder tweaks: {ex.Message}");
            }
        }

        private static string ToRegHex(string input)
        {
            var bytes = Encoding.Unicode.GetBytes(input + "\0");
            return Convert.ToHexString(bytes);
        }

        // From vinegar source code
        private async Task SetupVulkanLayerAsync()
        {
            const string LOG_IDENT = "Bootstrapper::SetupVulkanLayer";
            string layerDir = Path.Combine(Paths.WineRoot, "vulkan_layer");
            Directory.CreateDirectory(layerDir);

            string soPath = Path.Combine(layerDir, "libVkLayer_VINEGAR_VinegarLayer.so");
            string cppPath = Path.Combine(layerDir, "vinegar_layer.cpp");
            string headerPath = Path.Combine(layerDir, "vk_layer.h");
            string manifestPath = Path.Combine(layerDir, "VkLayer_VINEGAR_VinegarLayer.json");

            if (!File.Exists(cppPath) || !File.Exists(headerPath) || !File.Exists(manifestPath))
            {
                try
                {
                    byte[] cppData = await Resource.Get("vinegar_layer.cpp");
                    await File.WriteAllBytesAsync(cppPath, cppData);

                    byte[] headerData = await Resource.Get("vk_layer.h");
                    await File.WriteAllBytesAsync(headerPath, headerData);

                    byte[] manifestData = await Resource.Get("VkLayer_VINEGAR_VinegarLayer.json");
                    await File.WriteAllBytesAsync(manifestPath, manifestData);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to extract Vulkan layer resources: {ex.Message}");
                    return;
                }
            }

            string manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifestObj = JsonSerializer.Deserialize<Dictionary<string, object>>(manifestJson);
            if (manifestObj != null && manifestObj.TryGetValue("layer", out var layerObj) && layerObj != null)
            {
                string layerJson = JsonSerializer.Serialize(layerObj);
                var layer = JsonSerializer.Deserialize<Dictionary<string, object>>(layerJson);
                if (layer != null && layer.ContainsKey("library_path"))
                {
                    layer["library_path"] = "libVkLayer_VINEGAR_VinegarLayer.so";
                    manifestObj["layer"] = layer;
                    string updatedJson = JsonSerializer.Serialize(manifestObj, _indentedJsonOptions);
                    await File.WriteAllTextAsync(manifestPath, updatedJson);
                }
            }

            if (!File.Exists(soPath))
            {
                SetStatus("Compiling Vulkan layer...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "g++",
                    Arguments = $"-shared -fPIC -o \"{soPath}\" -I\"{layerDir}\" \"{cppPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                try
                {
                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        if (process.ExitCode != 0)
                            App.Logger.WriteLine(LOG_IDENT, $"Compilation failed with exit code {process.ExitCode}");
                        else
                            App.Logger.WriteLine(LOG_IDENT, "Vulkan layer compiled successfully.");
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Compilation error: {ex.Message}");
                }
            }
        }

        private static bool CheckForNvidiaGpu()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "lspci",
                    Arguments = "",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process == null) return false;
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SetupDxvkAsync()
        {
            const string LOG_IDENT = "Bootstrapper::SetupDxvk";

            var renderer = App.Settings.Prop.StudioRenderer;
            bool useSarek = (renderer == StudioRenderer.DXVKSarek);
            if (!useSarek && renderer != StudioRenderer.DXVK)
            {
                App.Logger.WriteLine(LOG_IDENT, "DXVK not required, ensuring original DLLs are restored.");
                await RestoreOriginalDllsAsync();
                return true;
            }

            string version = useSarek ? DxvkSarekVersion : DxvkVersion;
            string dllPath = Path.Combine(_latestVersionDirectory, "d3d11.dll");

            string installedVersion = GetInstalledDxvkVersion(dllPath);
            if (installedVersion == version)
            {
                App.Logger.WriteLine(LOG_IDENT, $"{version} already installed (DLL version check).");
                return true;
            }

            string cacheDir = Path.Combine(Paths.Cache, "dxvk");
            Directory.CreateDirectory(cacheDir);
            string archivePath = Path.Combine(cacheDir, useSarek ? $"dxvk-sarek-{version}.tar.gz" : $"dxvk-{version}.tar.gz");

            if (!File.Exists(archivePath))
            {
                SetStatus(useSarek ? "Downloading DXVK-Sarek..." : "Downloading DXVK...");
                string url = useSarek ? DxvkSarekUrl : $"https://github.com/doitsujin/dxvk/releases/download/v{version}/dxvk-{version}.tar.gz";
                await DownloadFileWithProgressAsync(url, archivePath);
            }

            await RestoreOriginalDllsAsync();

            SetStatus(useSarek ? "Extracting DXVK-Sarek..." : "Extracting DXVK...");
            if (Dialog != null)
            {
                Dialog.ProgressIndeterminate = true;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
            }

            bool success = await Task.Run(() =>
            {
                try
                {
                    using var fileStream = File.OpenRead(archivePath);
                    using var gzipStream = new GZipInputStream(fileStream);
                    using var tarStream = new TarInputStream(gzipStream, Encoding.UTF8);

                    TarEntry entry;
                    while ((entry = tarStream.GetNextEntry()) != null)
                    {
                        if (!entry.Name.Contains("/x64/") || !entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string fileName = Path.GetFileName(entry.Name);
                        string destPath = Path.Combine(_latestVersionDirectory, fileName);
                        using var output = File.Create(destPath);
                        tarStream.CopyEntryContents(output);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Extraction failed: {ex.Message}");
                    return false;
                }
            });

            if (!success)
                return false;

            App.Logger.WriteLine(LOG_IDENT, $"{version} installed successfully.");
            return true;
        }

        private static string GetInstalledDxvkVersion(string dllPath)
        {
            if (!File.Exists(dllPath))
                return "";
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(dllPath);
                if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                    return string.Join(".", versionInfo.FileVersion.Split('.')[0..3]);
            }
            catch { }
            return "";
        }

        #endregion

        public static  async Task<string?> GetRobloxSecurityCookieAsync()
        {
            const string LOG_IDENT = "Bootstrapper::GetRobloxSecurityCookie";

            if (string.IsNullOrEmpty(App.Settings.Prop.WinePrefixPath) || !Directory.Exists(App.Settings.Prop.WinePrefixPath))
            {
                App.Logger.WriteLine(LOG_IDENT, "Wine prefix not set or missing.");
                return null;
            }

            string userRegPath = Path.Combine(App.Settings.Prop.WinePrefixPath, "user.reg");
            if (!File.Exists(userRegPath))
            {
                App.Logger.WriteLine(LOG_IDENT, "user.reg not found.");
                return null;
            }

            string[] lines = await File.ReadAllLinesAsync(userRegPath);
            string? encryptionKeyHex = null;
            Dictionary<string, string> credEntries = [];

            string? currentKey = null;
            foreach (string line in lines)
            {
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    currentKey = line.Trim('[', ']');
                    continue;
                }

                if (currentKey == "Software\\Wine\\Credential Manager" && line.StartsWith('"'))
                {
                    var match = Regex.Match(line, "\"([^\"]+)\"=(hex\\(\\d+\\))?(.*)");
                    if (match.Success)
                    {
                        string name = match.Groups[1].Value;
                        string value = match.Groups[3].Value.Trim();
                        if (name == "EncryptionKey" && value.StartsWith("hex:"))
                            encryptionKeyHex = value[4..];
                        else if (name.StartsWith("Generic: https://www.roblox.com:RobloxStudioAuth") && value.StartsWith("hex:"))
                            credEntries[name] = value[4..];
                    }
                }
            }

            if (encryptionKeyHex == null)
            {
                App.Logger.WriteLine(LOG_IDENT, "EncryptionKey not found.");
                return null;
            }

            byte[] keyBytes = ParseRegBinaryHex(encryptionKeyHex);
            string? userIdEntry = credEntries.Keys.FirstOrDefault(k => k.Contains("userid"));
            if (userIdEntry == null)
            {
                App.Logger.WriteLine(LOG_IDENT, "No userId entry found.");
                return null;
            }

            byte[] encryptedUserId = ParseRegBinaryHex(credEntries[userIdEntry]);
            byte[] decryptedUserIdBytes = RC4Decrypt(keyBytes, encryptedUserId);
            string userId = Encoding.UTF8.GetString(decryptedUserIdBytes).TrimEnd('\0');
            App.Logger.WriteLine(LOG_IDENT, $"Found userId: {userId}");

            string cookieKey = $"Generic: https://www.roblox.com:RobloxStudioAuth.ROBLOSECURITY{userId}";
            if (!credEntries.TryGetValue(cookieKey, out string? encryptedCookieHex))
            {
                App.Logger.WriteLine(LOG_IDENT, $"No cookie entry for {cookieKey}");
                return null;
            }

            byte[] encryptedCookie = ParseRegBinaryHex(encryptedCookieHex!);
            byte[] decryptedCookie = RC4Decrypt(keyBytes, encryptedCookie);
            string cookie = Encoding.UTF8.GetString(decryptedCookie).TrimEnd('\0');
            App.Logger.WriteLine(LOG_IDENT, $"Successfully retrieved cookie (length {cookie.Length})");
            return cookie;
        }

        private static byte[] RC4Decrypt(byte[] key, byte[] data)
        {
            byte[] s = new byte[256];
            for (int i = 0; i < 256; i++) s[i] = (byte)i;
            int j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (j + s[i] + key[i % key.Length]) & 0xFF;
                (s[i], s[j]) = (s[j], s[i]);
            }
            byte[] output = new byte[data.Length];
            int i_idx = 0, j_idx = 0;
            for (int n = 0; n < data.Length; n++)
            {
                i_idx = (i_idx + 1) & 0xFF;
                j_idx = (j_idx + s[i_idx]) & 0xFF;
                (s[i_idx], s[j_idx]) = (s[j_idx], s[i_idx]);
                output[n] = (byte)(data[n] ^ s[(s[i_idx] + s[j_idx]) & 0xFF]);
            }
            return output;
        }

        private static byte[] ParseRegBinaryHex(string hexString)
        {
            hexString = hexString.Replace(",", "").Replace("\\", "").Trim();
            if (hexString.Length % 2 != 0) throw new FormatException("Invalid hex length");
            byte[] bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            return bytes;
        }

        private static void StartBackgroundUpdater()
        {
            const string LOG_IDENT = "Bootstrapper::StartBackgroundUpdater";

            if (Utilities.DoesMutexExist(BackgroundUpdaterMutexName))
            {
                App.Logger.WriteLine(LOG_IDENT, "Background updater already running");
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, "Starting background updater");
            Process.Start(Paths.Process, "-backgroundupdater");
        }

        private async Task<bool> ApplyModifications()
        {
            const string LOG_IDENT = "Bootstrapper::ApplyModifications";
            bool success = true;

            SetStatus(Strings.Bootstrapper_Status_ApplyingModifications);
            App.Logger.WriteLine(LOG_IDENT, "Checking file mods...");

            File.Delete(Path.Combine(Paths.Base, "ModManifest.txt"));

            var currentModManifest = new Dictionary<string, ModFileEntry>(StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(Paths.Modifications);

            string contentDirectory = OperatingSystem.IsMacOS()
                ? Path.Combine(_latestVersionDirectory, AppData.ExecutableName, "Contents", "Resources")
                : _latestVersionDirectory;

            App.Logger.WriteLine(LOG_IDENT, $"Total mods in state: {App.State.Prop.Mods.Count}");
            foreach (var m in App.State.Prop.Mods)
                App.Logger.WriteLine(LOG_IDENT, $"Mod: '{m.FolderName}' Target='{m.Target}' Priority={m.Priority} FolderExists={Directory.Exists(Path.Combine(Paths.Modifications, m.FolderName))}");

            var activeMods = App.State.Prop.Mods
                .Where(x => x.Target != "Disabled" && (
                    x.Target == "Both" ||
                    (IsStudioLaunch && x.Target == "Studio") ||
                    (!IsStudioLaunch && x.Target == "Player")))
                .OrderBy(x => x.Priority)
                .ToList();

            App.Logger.WriteLine(LOG_IDENT, $"Active mods after filter: {activeMods.Count}");

            string? activeFontFilename = null;
            string? modFontFamiliesFolder = null;

            if (File.Exists(Paths.CustomFont))
            {
                activeFontFilename = "CustomFont.ttf";
                modFontFamiliesFolder = Path.Combine(Paths.PresetModifications, "content", "fonts", "families");
            }
            else
            {
                string? customFontModName = activeMods.LastOrDefault(mod =>
                {
                    if (File.Exists(Path.Combine(Paths.Modifications, mod.FolderName, "content", "fonts", "CustomFont.ttf")))
                    {
                        activeFontFilename = "CustomFont.ttf";
                        return true;
                    }
                    if (File.Exists(Path.Combine(Paths.Modifications, mod.FolderName, "content", "fonts", "CustomFont.otf")))
                    {
                        activeFontFilename = "CustomFont.otf";
                        return true;
                    }
                    return false;
                })?.FolderName;

                if (customFontModName != null && activeFontFilename != null)
                    modFontFamiliesFolder = Path.Combine(Paths.Modifications, customFontModName, "content", "fonts", "families");
            }

            if (modFontFamiliesFolder != null && activeFontFilename != null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Executing font patcher for {activeFontFilename}...");
                Directory.CreateDirectory(modFontFamiliesFolder);

                string rbxAssetPath = $"rbxasset://fonts/{activeFontFilename}";
                string[] fontFamilyFiles =
                [
                    "AccanthisADFStd.json", "AmaticSC.json", "Arimo.json", "Balthazar.json",
                    "Bangers.json", "BuilderExtended.json", "BuilderMono.json", "BuilderSans.json",
                    "ComicNeueAngular.json", "Creepster.json", "DenkOne.json", "Fondamento.json",
                    "FredokaOne.json", "GrenzeGotisch.json", "Guru.json", "HighwayGothic.json",
                    "Inconsolata.json", "IndieFlower.json", "JosefinSans.json", "Jura.json",
                    "Kalam.json", "LegacyArial.json", "LegacyArimo.json", "LuckiestGuy.json",
                    "Merriweather.json", "Michroma.json", "Montserrat.json", "Nunito.json",
                    "Oswald.json", "PatrickHand.json", "PermanentMarker.json", "PressStart2P.json",
                    "Roboto.json", "RobotoCondensed.json", "RobotoMono.json", "RomanAntique.json",
                    "Sarpanch.json", "SourceSansPro.json", "SpecialElite.json", "TitilliumWeb.json",
                    "Ubuntu.json", "Zekton.json"
                ];

                await Task.Run(() =>
                {
                    Parallel.ForEach(fontFamilyFiles, new ParallelOptions { MaxDegreeOfParallelism = 4 }, jsonFilename =>
                    {
                        string modFilepath = Path.Combine(modFontFamiliesFolder, jsonFilename);

                        string familyName = Path.GetFileNameWithoutExtension(jsonFilename);
                        familyName = Regex.Replace(familyName, "(?<=[A-Z])([A-Z][a-z])", " $1");
                        familyName = Regex.Replace(familyName, "(?<=[a-z0-9])([A-Z])", " $1");

                        var fontFamilyData = new FontFamily
                        {
                            Name = familyName,
                            Faces =
                            [
                                new FontFace { Name = "Thin", Weight = 100, Style = "normal", AssetId = rbxAssetPath },
                                new FontFace { Name = "Light", Weight = 300, Style = "normal", AssetId = rbxAssetPath },
                                new FontFace { Name = "Regular", Weight = 400, Style = "normal", AssetId = rbxAssetPath },
                                new FontFace { Name = "Medium", Weight = 500, Style = "normal", AssetId = rbxAssetPath },
                                new FontFace { Name = "Semi Bold", Weight = 600, Style = "normal", AssetId = rbxAssetPath },
                                new FontFace { Name = "Bold", Weight = 700, Style = "normal", AssetId = rbxAssetPath },
                                new FontFace { Name = "Extra Bold", Weight = 800, Style = "normal", AssetId = rbxAssetPath }
                            ]
                        };

                        File.WriteAllText(modFilepath, JsonSerializer.Serialize(fontFamilyData, _indentedJsonOptions));
                    });
                });

                App.Logger.WriteLine(LOG_IDENT, "End font check");
            }

            var finalFilesToCopy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var filesToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in activeMods)
            {
                string modSource = Path.Combine(Paths.Modifications, mod.FolderName);

                if (Directory.Exists(modSource))
                    ProcessModDirectory(modSource, finalFilesToCopy, filesToDelete);
                else
                    App.Logger.WriteLine(LOG_IDENT, $"Skipping mod '{mod.FolderName}': directory not found");
            }

            if (Directory.Exists(Paths.PresetModifications))
            {
                App.Logger.WriteLine(LOG_IDENT, "Applying PresetModifications (Flat folder)...");
                ProcessModDirectory(Paths.PresetModifications, finalFilesToCopy, filesToDelete);
            }

            foreach (var relPath in filesToDelete)
            {
                string targetFile = Path.Combine(contentDirectory, relPath);
                if (File.Exists(targetFile))
                {
                    Filesystem.AssertReadOnly(targetFile);
                    File.Delete(targetFile);
                    App.Logger.WriteLine(LOG_IDENT, $"{relPath} deleted via _Delete flag");

                    string? parentDir = Path.GetDirectoryName(targetFile);
                    while (!string.IsNullOrEmpty(parentDir) &&
                           parentDir.TrimEnd(Path.DirectorySeparatorChar) != contentDirectory.TrimEnd(Path.DirectorySeparatorChar))
                    {
                        if (Directory.Exists(parentDir) && !Directory.EnumerateFileSystemEntries(parentDir).Any())
                        {
                            Directory.Delete(parentDir);
                            parentDir = Path.GetDirectoryName(parentDir);
                        }
                        else break;
                    }
                }

                lock (currentModManifest)
                    currentModManifest[relPath + "_Delete"] = new ModFileEntry { Size = 0, LastModified = DateTime.Now };
            }

            var fileTasks = new List<Task<bool>>();
            using var semaphore = new SemaphoreSlim(8);

            foreach (var entry in finalFilesToCopy)
            {
                if (_cancelTokenSource.IsCancellationRequested) return true;

                string relativeFile = entry.Key;
                string sourceFile = entry.Value;
                string fileVersionFolder = Path.Combine(contentDirectory, relativeFile);

                fileTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var sourceInfo = new FileInfo(sourceFile);
                        lock (currentModManifest)
                            currentModManifest[relativeFile] = new ModFileEntry { Size = sourceInfo.Length, LastModified = sourceInfo.LastWriteTime };

                        bool needsCopy = true;
                        if (File.Exists(fileVersionFolder))
                        {
                            var targetInfo = new FileInfo(fileVersionFolder);
                            if (targetInfo.Length == sourceInfo.Length && targetInfo.LastWriteTime == sourceInfo.LastWriteTime)
                            {
                                needsCopy = false;
                            }
                            else
                            {
                                string sourceHash = await Task.Run(() => MD5Hash.FromFile(sourceFile));
                                string targetHash = await Task.Run(() => MD5Hash.FromFile(fileVersionFolder));

                                if (sourceHash == targetHash)
                                {
                                    needsCopy = false;
                                    File.SetLastWriteTime(fileVersionFolder, sourceInfo.LastWriteTime);
                                }
                            }
                        }

                        if (needsCopy)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(fileVersionFolder)!);
                            Filesystem.AssertReadOnly(fileVersionFolder);
                            File.Copy(sourceFile, fileVersionFolder, true);
                            File.SetLastWriteTime(fileVersionFolder, sourceInfo.LastWriteTime);
                            Filesystem.AssertReadOnly(fileVersionFolder);
                            App.Logger.WriteLine(LOG_IDENT, $"{relativeFile} applied");
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to apply ({relativeFile}): {ex.Message}");
                        return false;
                    }
                    finally { semaphore.Release(); }
                }));
            }

            bool needsAppSettings = !OperatingSystem.IsLinux() || IsStudioLaunch;
            if (needsAppSettings && !File.Exists(Path.Combine(Paths.Modifications, "AppSettings.xml")))
                await File.WriteAllTextAsync(Path.Combine(_latestVersionDirectory, "AppSettings.xml"),
                    AppSettings.Replace("roblox.com", Deployment.RobloxDomain));

            var fileResults = await Task.WhenAll(fileTasks);
            success = success && fileResults.All(r => r);

            if (App.Settings.Prop.UseFastFlagManager && (!OperatingSystem.IsLinux() || IsStudioLaunch))
            {
                string source = Path.Combine(Paths.PresetModifications, "ClientSettings", "ClientAppSettings.json");
                if (File.Exists(source))
                {
                    string rel = Path.Combine("ClientSettings", "ClientAppSettings.json");
                    string dest = Path.Combine(contentDirectory, rel);
                    var info = new FileInfo(source);

                    lock (currentModManifest)
                        currentModManifest[rel] = new ModFileEntry { Size = info.Length, LastModified = info.LastWriteTime };

                    try
                    {
                        bool match = File.Exists(dest) && (await Task.Run(() => MD5Hash.FromFile(source)) == await Task.Run(() => MD5Hash.FromFile(dest)));
                        if (!match)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                            File.Copy(source, dest, true);
                            File.SetLastWriteTime(dest, info.LastWriteTime);
                            App.Logger.WriteLine(LOG_IDENT, "FastFlags Applied.");
                        }
                    }
                    catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
                }
            }

            if (App.Settings.Prop.EnableActivityTracking && App.Settings.Prop.UseWindowControl)
            {
                var idsPath = Path.Combine(_latestVersionDirectory, "content\\bloxstrap");

                Directory.CreateDirectory(idsPath);

                var directory = new DirectoryInfo(idsPath);

                foreach (FileInfo file in directory.GetFiles()) file.Delete();
                foreach (DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);

                using Image<Rgba32> enabledBitmap = new(1, 1);
                enabledBitmap[0, 0] = Color.White;

                enabledBitmap.Save(Path.Combine(idsPath, "enabled.png"));
            }


            var fileRestoreMap = new Dictionary<string, List<string>>();

            foreach (var fileLocation in AppData.DistributionState.ModManifest)
            {
                if (currentModManifest.ContainsKey(fileLocation))
                    continue;

                string targetFile = fileLocation;
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileLocation);

                if (fileNameWithoutExt.EndsWith("_Delete"))
                {
                    string directory = Path.GetDirectoryName(fileLocation) ?? "";
                    string originalName = fileNameWithoutExt[..^7];
                    targetFile = Path.Combine(directory, originalName + Path.GetExtension(fileLocation));
                }

                if (!PackageDirectoryMap.TryGetValue(targetFile.Split(Path.DirectorySeparatorChar)[0], out string? packageDir) || string.IsNullOrEmpty(packageDir))
                {
                    string versionFileLocation = Path.Combine(_latestVersionDirectory, targetFile);
                    if (File.Exists(versionFileLocation)) File.Delete(versionFileLocation);
                    continue;
                }

                string packageName = PackageDirectoryMap.FirstOrDefault(x => !string.IsNullOrEmpty(x.Value) && targetFile.StartsWith(x.Value, StringComparison.OrdinalIgnoreCase)).Key;
                if (string.IsNullOrEmpty(packageName))
                {
                    string versionFileLocation = Path.Combine(_latestVersionDirectory, targetFile);
                    if (File.Exists(versionFileLocation)) File.Delete(versionFileLocation);
                    continue;
                }

                if (!fileRestoreMap.ContainsKey(packageName))
                    fileRestoreMap[packageName] = [];

                string internalZipPath = targetFile[packageDir.Length..].TrimStart(Path.DirectorySeparatorChar);
                fileRestoreMap[packageName].Add(internalZipPath);
            }

            if (!OperatingSystem.IsLinux())
            {
                foreach (var entry in fileRestoreMap)
                {
                    var package = _versionPackageManifest.Find(x => x.Name == entry.Key);
                    if (package is not null)
                    {
                        await DownloadPackage(package);
                        await ExtractPackage(package, entry.Value);
                    }
                }
            }

            if (App.LaunchSettings.BackgroundUpdaterFlag.Active || !AppData.DistributionStateManager.HasFileOnDiskChanged())
            {
                AppData.DistributionState.ModManifest = [.. currentModManifest.Keys];
                AppData.DistributionStateManager.Save();
            }

            App.Logger.WriteLine(LOG_IDENT, "Finished checking file mods");
            return success;
        }

        private static void ProcessModDirectory(string sourcePath, Dictionary<string, string> copyMap, HashSet<string> deleteSet)
        {
            foreach (string file in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                string relativeFile = file[sourcePath.Length..].TrimStart(Path.DirectorySeparatorChar);

                if (relativeFile == "README.txt")
                {
                    File.Delete(file);
                    continue;
                }

                if (relativeFile.EndsWith("ClientSettings") || relativeFile.EndsWith(".lock"))
                    continue;

                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(relativeFile);

                if (fileNameWithoutExt.EndsWith("_Delete"))
                {
                    string originalRelName = Path.Combine(
                        Path.GetDirectoryName(relativeFile) ?? "",
                        string.Concat(fileNameWithoutExt.AsSpan(0, fileNameWithoutExt.Length - 7), Path.GetExtension(relativeFile))
                    );
                    deleteSet.Add(originalRelName);
                    copyMap.Remove(originalRelName);
                }
                else
                {
                    copyMap[relativeFile] = file;
                    deleteSet.Remove(relativeFile);
                }
            }
        }

        private static string GetMacArchPath()
        {
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
                == System.Runtime.InteropServices.Architecture.Arm64
                ? "/mac/arm64"
                : "/mac";
        }

        private async Task DownloadPackage(Package package)
        {
            string LOG_IDENT = $"Bootstrapper::DownloadPackage.{package.Name}";

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            Directory.CreateDirectory(Paths.Downloads);

            string packageUrl = OperatingSystem.IsMacOS()
                ? Deployment.GetLocation($"{GetMacArchPath()}/{_latestVersionGuid}-{package.Name}")
                : Deployment.GetLocation($"/{_latestVersionGuid}-{package.Name}");

            string robloxPackageLocation = Path.Combine(Paths.Roblox, "Downloads", package.Signature);

            if (File.Exists(package.DownloadPath))
            {
                string calculatedMD5 = MD5Hash.FromFile(package.DownloadPath);

                // Skip hash validation for macOS as the mock manifest lacks actual signature MD5s
                if (!OperatingSystem.IsMacOS() && calculatedMD5 != package.Signature)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package is corrupted ({calculatedMD5} != {package.Signature})! Deleting and re-downloading...");
                    File.Delete(package.DownloadPath);
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "Package is already downloaded, skipping...");
                    Interlocked.Add(ref _totalDownloadedBytes, package.PackedSize);
                    UpdateProgressBar();
                    return;
                }
            }
            else if (File.Exists(robloxPackageLocation))
            {
                // let's cheat! if the stock bootstrapper already previously downloaded the file,
                // then we can just copy the one from there
                App.Logger.WriteLine(LOG_IDENT, $"Found existing copy at '{robloxPackageLocation}'! Copying to Downloads folder...");
                File.Copy(robloxPackageLocation, package.DownloadPath);
                Interlocked.Add(ref _totalDownloadedBytes, package.PackedSize);
                UpdateProgressBar();
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, "Downloading...");

            var buffer = new byte[DownloadBufferSize];

            for (int i = 1; i <= MaxDownloadAttempts; i++)
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                int totalBytesRead = 0;

                try
                {
                    var response = await App.HttpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead, _cancelTokenSource.Token);
                    await using var stream = await response.Content.ReadAsStreamAsync(_cancelTokenSource.Token);
                    await using var fileStream = new FileStream(package.DownloadPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Delete);

                    while (true)
                    {
                        if (_cancelTokenSource.IsCancellationRequested)
                        {
                            stream.Close();
                            fileStream.Close();
                            return;
                        }

                        int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), _cancelTokenSource.Token);
                        if (bytesRead == 0)
                            break;

                        totalBytesRead += bytesRead;
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cancelTokenSource.Token);

                        Interlocked.Add(ref _totalDownloadedBytes, bytesRead);
                        UpdateProgressBar();
                    }

                    string hash = MD5Hash.FromStream(fileStream);
                    if (!OperatingSystem.IsMacOS() && hash != package.Signature)
                        throw new ChecksumFailedException($"Failed to verify download of {packageUrl}\n\nExpected hash: {package.Signature}\nGot hash: {hash}");

                    App.Logger.WriteLine(LOG_IDENT, $"Finished downloading! ({totalBytesRead} bytes total)");
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"An exception occurred after downloading {totalBytesRead} bytes. ({i}/{MaxDownloadAttempts})");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    if (ex.GetType() == typeof(ChecksumFailedException))
                    {
                        await Frontend.ShowConnectivityDialog(
                            Strings.Dialog_Connectivity_UnableToDownload,
                            String.Format(Strings.Dialog_Connectivity_UnableToDownloadReason, "[https://github.com/bloxstraplabs/bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox](https://github.com/bloxstraplabs/bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox)"),
                            MessageBoxImage.Error,
                            ex
                        );
                        App.Terminate(ErrorCode.ERROR_CANCELLED);
                    }
                    else if (i >= MaxDownloadAttempts)
                    {
                        throw;
                    }

                    if (File.Exists(package.DownloadPath))
                        File.Delete(package.DownloadPath);

                    Interlocked.Add(ref _totalDownloadedBytes, -totalBytesRead);
                    UpdateProgressBar();

                    // attempt download over HTTP
                    // this isn't actually that unsafe - signatures were fetched earlier over HTTPS
                    // so we've already established that our signatures are legit, and that there's very likely no MITM anyway
                    if (ex.GetType() == typeof(IOException) && !packageUrl.StartsWith("http://"))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Retrying download over HTTP...");
                        packageUrl = packageUrl.Replace("https://", "http://");
                    }
                }
            }
        }

        private async Task<bool> ExtractPackage(Package package, List<string>? files = null)
        {
            const string LOG_IDENT = "Bootstrapper::ExtractPackage";
            int attempts = 0;
            const int maxAttempts = 3;

            while (attempts < maxAttempts)
            {
                try
                {
                    attempts++;

                    string packageFolder = _latestVersionDirectory;
                    if (!OperatingSystem.IsMacOS())
                    {
                        string? packageDir = PackageDirectoryMap.GetValueOrDefault(package.Name);
                        if (packageDir is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"WARNING: {package.Name} was not found in the package map!");
                            return false;
                        }
                        packageFolder = Path.Combine(_latestVersionDirectory, packageDir);
                    }

                    string? fileFilter = null;
                    if (files is not null)
                    {
                        var regexList = new List<string>();
                        foreach (string file in files)
                            regexList.Add("^" + file.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + "$");
                        fileFilter = string.Join(';', regexList);
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Extracting {package.Name} (Attempt {attempts}/{maxAttempts})...");

                    // Use custom extraction that transforms backslashes to forward slashes
                    ExtractZipWithTransform(package.DownloadPath, packageFolder, fileFilter);

                    App.Logger.WriteLine(LOG_IDENT, $"Finished extracting {package.Name}");
                    return true;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Extraction failed on attempt {attempts}: {ex.Message}");

                    if (ex.Message.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Ignoring non-critical extraction failure for font file: {ex.Message}");
                        return true;
                    }

                    if (File.Exists(package.DownloadPath))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Deleting corrupted package for retry...");
                        File.Delete(package.DownloadPath);
                    }

                    if (attempts >= maxAttempts)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Max extraction attempts reached. Giving up.");
                        return false;
                    }

                    App.Logger.WriteLine(LOG_IDENT, "Retrying download...");
                    SetStatus($"Retrying {package.Name}...");
                    await Task.Delay(1000);
                    await DownloadPackage(package);
                }
            }

            return false;
        }

        // for some reason on linux, it treats file directory entries in the zip as files with backslashes in their names,
        // instead of actual directories, which causes issues with mods, this should fix it
        private static void ExtractZipWithTransform(string zipPath, string destDir, string? fileFilter = null)
        {
            using var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
            using var zf = new ZipFile(fs);
            foreach (ZipEntry entry in zf)
            {
                if (entry.IsDirectory)
                    continue;

                string transformedName = entry.Name.Replace('\\', '/');

                if (!string.IsNullOrEmpty(fileFilter))
                {
                    var patterns = fileFilter.Split(';');
                    bool matches = false;
                    foreach (var pattern in patterns)
                    {
                        if (Regex.IsMatch(transformedName, pattern))
                        {
                            matches = true;
                            break;
                        }
                    }
                    if (!matches)
                        continue;
                }

                string destPath = Path.Combine(destDir, transformedName);
                string? destDirectory = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDirectory))
                    Directory.CreateDirectory(destDirectory);

                using var zipStream = zf.GetInputStream(entry);
                using var outputStream = File.Create(destPath);
                zipStream.CopyTo(outputStream);
            }
        }
        #endregion
    }
}