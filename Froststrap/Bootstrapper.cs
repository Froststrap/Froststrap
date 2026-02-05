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
using Froststrap.RobloxInterfaces;
using Froststrap.UI.Elements.Bootstrapper.Base;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using MsBox.Avalonia.Enums;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Data;
using System.Reflection;

namespace Froststrap
{
    public class Bootstrapper
    {
        #region Properties
        private const int ProgressBarMaximum = 10000;

        private const double TaskbarProgressMaximumWpf = 1; // this can not be changed. keep it at 1.
        private const int TaskbarProgressMaximumWinForms = AvaloniaDialogBase.TaskbarProgressMaximum;

        private const string AppSettings =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
            "<Settings>\r\n" +
            "	<ContentFolder>content</ContentFolder>\r\n" +
            "	<BaseUrl>http://www.roblox.com</BaseUrl>\r\n" +
            "</Settings>\r\n";

        private readonly FastZipEvents _fastZipEvents = new();
        private readonly CancellationTokenSource _cancelTokenSource = new();

        private IAppData AppData = default!;
        private Dictionary<string, string> PackageDirectoryMap = null!;
        private LaunchMode _launchMode;

        private string _launchCommandLine = App.LaunchSettings.RobloxLaunchArgs;
        private Version? _latestVersion = null;
        private string _latestVersionGuid = null!;
        private string _latestVersionDirectory = null!;
        private PackageManifest _versionPackageManifest = null!;
        public static bool _staticDirectory => App.Settings.Prop.StaticDirectory;

        private bool _isInstalling = false;
        private double _progressIncrement;
        private double _taskbarProgressIncrement;
        private double _taskbarProgressMaximum;
        private long _totalDownloadedBytes = 0;
        private bool _packageExtractionSuccess = true;

        private bool _mustUpgrade => App.LaunchSettings.ForceFlag.Active || App.State.Prop.ForceReinstall || String.IsNullOrEmpty(AppData.DistributionState.VersionGuid) || !File.Exists(AppData.ExecutablePath);

        private bool _noConnection = false;

        private AsyncMutex? _mutex;

        private static Mutex? _multiInstanceMutex1;
        private static Mutex? _multiInstanceMutex2;

        private int _appPid = 0;

        public IBootstrapperDialog? Dialog = null;

        public bool IsStudioLaunch => _launchMode != LaunchMode.Player;

        public string MutexName { get; set; } = "Froststrap-Bootstrapper";
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
                // only give a pass to font files (no idea whats wrong with them)
                if (!e.Name.EndsWith(".ttf"))
                    throw e.Exception;

                App.Logger.WriteLine("FastZipEvents::OnFileFailure", $"Failed to extract {e.Name}");
                _packageExtractionSuccess = false;
            };
            _fastZipEvents.DirectoryFailure += (_, e) => throw e.Exception;
            _fastZipEvents.ProcessFile += (_, e) => e.ContinueRunning = !_cancelTokenSource.IsCancellationRequested;
            SetupAppData();
        }

        private void SetupAppData()
        {
            AppData = IsStudioLaunch ? new RobloxStudioData() : new RobloxPlayerData();
            Deployment.BinaryType = AppData.BinaryType;
        }

        // we will use this later on since we have to wait for remote data
        private async Task SetupPackageDictionaries()
        {
            await App.RemoteData.WaitUntilDataFetched();

            var localData = App.RemoteData.Prop.PackageMaps[IsStudioLaunch ? "studio" : "player"];
            var commonData = App.RemoteData.Prop.PackageMaps.CommonPackageMap;

            PackageDirectoryMap = new(commonData);

            foreach (var package in localData)
                PackageDirectoryMap[package.Key] = package.Value;
        }

        private void SetStatus(string message)
        {
            message = message.Replace("{product}", AppData.ProductName);

            if (Dialog is not null)
                Dialog.Message = message;
        }

        private void UpdateProgressBar()
        {
            if (Dialog is null)
                return;

            // UI progress
            int progressValue = (int)Math.Floor(_progressIncrement * _totalDownloadedBytes);

            // bugcheck: if we're restoring a file from a package, it'll incorrectly increment the progress beyond 100
            // too lazy to fix properly so lol
            progressValue = Math.Clamp(progressValue, 0, ProgressBarMaximum);

            Dialog.ProgressValue = progressValue;

            // taskbar progress
            double taskbarProgressValue = _taskbarProgressIncrement * _totalDownloadedBytes;
            taskbarProgressValue = Math.Clamp(taskbarProgressValue, 0, _taskbarProgressMaximum);

            Dialog.TaskbarProgressValue = taskbarProgressValue;
        }

        private void HandleConnectionError(Exception exception)
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

            if (_mustUpgrade)
                message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeNeeded}\n\n{Strings.Dialog_Connectivity_TryAgainLater}";
            else
                message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeSkip}";

            Frontend.ShowConnectivityDialog(
                String.Format(Strings.Dialog_Connectivity_UnableToConnect, "Roblox"),
                message,
                _mustUpgrade ? MessageBoxImage.Error : MessageBoxImage.Warning,
                exception);

            if (_mustUpgrade)
                App.Terminate(ErrorCode.ERROR_CANCELLED);
        }

        public async Task Run()
        {
            const string LOG_IDENT = "Bootstrapper::Run";

            App.Logger.WriteLine(LOG_IDENT, "Running bootstrapper");

            // this is now always enabled as of v2.8.0
            if (Dialog is not null)
                Dialog.CancelEnabled = true;

            SetStatus(Strings.Bootstrapper_Status_Connecting);

            var connectionResult = await Deployment.InitializeConnectivity();

            App.Logger.WriteLine(LOG_IDENT, "Connectivity check finished");

            if (connectionResult is not null)
                HandleConnectionError(connectionResult);

#if (!DEBUG || DEBUG_UPDATER) && !QA_BUILD
            if (App.Settings.Prop.CheckForUpdates && !App.LaunchSettings.UpgradeFlag.Active)
            {
                bool updatePresent = await CheckForUpdates();
                
                if (updatePresent)
                    return;
            }
#endif

            // ensure only one instance of the bootstrapper is running at the time
            // so that we don't have stuff like two updates happening simultaneously

            bool mutexExists = Utilities.DoesMutexExist(MutexName);

            if (mutexExists)
            {
                if (!QuitIfMutexExists)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{MutexName} mutex exists, waiting...");
                    SetStatus(Strings.Bootstrapper_Status_WaitingOtherInstances);
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{MutexName} mutex exists, exiting!");
                    return;
                }
            }

            // wait for mutex to be released if it's not yet
            await using var mutex = new AsyncMutex(false, MutexName);
            await mutex.AcquireAsync(_cancelTokenSource.Token);

            _mutex = mutex;

            // reload our configs since they've likely changed by now
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
                    HandleConnectionError(ex);
                }
            }

            CleanupVersionsFolder(); // cleanup after background updater

            bool allModificationsApplied = true;

            if (!_noConnection)
            {
                if (App.RemoteData.LoadedState == GenericTriState.Unknown) // we dont want it to flicker
                    SetStatus(Strings.Bootstrapper_Status_WaitingForData);

                await SetupPackageDictionaries(); // mods also require it

                // we are checking if eurotrucks2 exists in client directory
                if (
                    File.Exists(Path.Combine(AppData.Directory, App.RobloxAnselAppName))
                    )
                {
                    Frontend.ShowMessageBox(
                        Strings.Bootstrapper_Dialog_AnselDisabled,
                        MessageBoxImage.Warning
                    );
                    await UpgradeRoblox();
                }

                if (AppData.DistributionState.VersionGuid != _latestVersionGuid || _mustUpgrade)
                {
                    bool backgroundUpdaterMutexOpen = Utilities.DoesMutexExist("Froststrap-BackgroundUpdater");
                    if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
                        backgroundUpdaterMutexOpen = false; // we want to actually update lol

                    App.Logger.WriteLine(LOG_IDENT, $"Background updater running: {backgroundUpdaterMutexOpen}");

                    if (backgroundUpdaterMutexOpen && _mustUpgrade)
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

            // check registry entries for every launch, just in case the stock bootstrapper changes it back

            if (IsStudioLaunch)
                WindowsRegistry.RegisterStudio();
            else
                WindowsRegistry.RegisterPlayer();

            WindowsRegistry.RegisterClientLocation(IsStudioLaunch, _latestVersionDirectory); // if it for some reason doesnt exist

            if (_launchMode != LaunchMode.Player)
                await mutex.ReleaseAsync();

            if (_launchMode == LaunchMode.Player)
            {
                // await because some peoples pc are so ass that roblox opens before this finishes causing an error due to the event
                if (App.Settings.Prop.MultiInstanceLaunching)
                    await LaunchMultiInstanceWatcher();
            }

            if (!App.LaunchSettings.NoLaunchFlag.Active && !_cancelTokenSource.IsCancellationRequested)
            {
                if (!App.LaunchSettings.QuietFlag.Active)
                {
                    // show some balloon tips
                    if (!_packageExtractionSuccess)
                        Frontend.ShowBalloonTip(Strings.Bootstrapper_ExtractionFailed_Title, Strings.Bootstrapper_ExtractionFailed_Message, ToolTipIcon.Warning);
                    else if (!allModificationsApplied)
                        Frontend.ShowBalloonTip(Strings.Bootstrapper_ModificationsFailed_Title, Strings.Bootstrapper_ModificationsFailed_Message, ToolTipIcon.Warning);
                }

                StartRoblox();
            }

            _ =  HandlePostLaunchOperations(_launchMode);

            await mutex.ReleaseAsync();

            Dialog?.CloseBootstrapper();
        }

        private async Task HandlePostLaunchOperations(LaunchMode launchMode)
        {
            const string LOG_IDENT = "Bootstrapper::PostLaunch";

            try
            {
                // roblox studio automatically sets its icon everytime you do smth
                if (launchMode == LaunchMode.Player)
                {
                    if (App.Settings.Prop.SelectedRobloxIcon != RobloxIcon.Default)
                    {
                        var robloxProcess = Process.GetProcessById(_appPid);

                        if (!robloxProcess.HasExited)
                        {
                            await SetProcessWindowIcon(robloxProcess, App.Settings.Prop.SelectedRobloxIcon);
                        }
                    }
                }
                else
                {
                    await Task.Delay(20000);
                }

                if (launchMode == LaunchMode.Player)
                {
                    if (App.Settings.Prop.AutoCloseCrashHandler)
                    {
                        try
                        {
                            var crashHandlerProcesses = Process.GetProcessesByName("RobloxCrashHandler");

                            foreach (var proc in crashHandlerProcesses)
                            {
                                try
                                {
                                    proc.Kill();
                                }
                                catch (Exception ex)
                                {
                                    App.Logger.WriteLine(LOG_IDENT, $"Failed to kill RobloxCrashHandler process (PID {proc.Id}): {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Error killing RobloxCrashHandler: {ex.Message}");
                        }
                    }
                }

                if (App.Settings.Prop.SelectedProcessPriority != ProcessPriorityOption.Normal)
                {
                    try
                    {
                        ProcessPriorityClass priorityClass = App.Settings.Prop.SelectedProcessPriority switch
                        {
                            ProcessPriorityOption.Low => ProcessPriorityClass.Idle,
                            ProcessPriorityOption.BelowNormal => ProcessPriorityClass.BelowNormal,
                            ProcessPriorityOption.Normal => ProcessPriorityClass.Normal,
                            ProcessPriorityOption.AboveNormal => ProcessPriorityClass.AboveNormal,
                            ProcessPriorityOption.High => ProcessPriorityClass.High,
                            ProcessPriorityOption.RealTime => ProcessPriorityClass.RealTime,
                            _ => ProcessPriorityClass.Normal
                        };

                        var robloxProcess = Process.GetProcessById(_appPid);
                        robloxProcess.PriorityClass = priorityClass;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Process priority setting failed: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Post-launch operations failed: {ex}");
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

            using var key = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\ROBLOX Corporation\\Environments\\{AppData.RegistryName}\\Channel");

            var match = Regex.Match(
                App.LaunchSettings.RobloxLaunchArgs,
                "channel:([a-zA-Z0-9-_]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            bool ChannelFlag = App.LaunchSettings.ChannelFlag.Active && !string.IsNullOrEmpty(App.LaunchSettings.ChannelFlag.Data);

            // CHANNEL CHANGE MODE

            void EnrollChannel(string Channel = "production") => Deployment.Channel = Channel;
            void RevertChannel() => Deployment.Channel = Deployment.DefaultChannel;

            string EnrolledChannel = match.Groups.Count == 2 ? match.Groups[1].Value.ToLowerInvariant() : Deployment.DefaultChannel;
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
                    if (!string.IsNullOrEmpty(EnrolledChannel))
                        _launchCommandLine = _launchCommandLine.Replace(
                            $"channel:{EnrolledChannel}",
                            $"channel:{userChannel.Channel}",
                            StringComparison.OrdinalIgnoreCase);

                    Deployment.ChannelToken = userChannel.Token;
                    EnrolledChannel = userChannel.Channel;
                }
            }

            if (!ChannelFlag)
            {
                switch (App.Settings.Prop.ChannelChangeMode)
                {
                    case ChannelChangeMode.Automatic:
                        App.Logger.WriteLine(LOG_IDENT, "Enrolling into channel");

                        EnrollChannel(EnrolledChannel);
                        break;
                    case ChannelChangeMode.Prompt:
                        App.Logger.WriteLine(LOG_IDENT, "Prompting channel enrollment");

                        if
                        (
                        !match.Success ||
                        match.Groups.Count != 2 ||
                        match.Groups[1].Value.ToLowerInvariant() == Deployment.Channel
                        )
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Channel is either equal or incorrectly formatted");
                            break;
                        }

                        string DisplayChannel = !String.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : Deployment.DefaultChannel;

                        var Result = Frontend.ShowMessageBox(
                        String.Format(Strings.Bootstrapper_Bootstrapper_Dialog_PromptChannelChange,
                        DisplayChannel, App.Settings.Prop.Channel),
                        MessageBoxImage.Question,
                        MessageBoxButton.YesNo
                        );

                        if (Result == MessageBoxResult.Yes)
                            EnrollChannel(EnrolledChannel);
                        break;
                    case ChannelChangeMode.Ignore:
                        App.Logger.WriteLine(LOG_IDENT, "Ignoring channel enrollment");
                        break;
                }
            }
            else
            {
                string ChannelFlagData = App.LaunchSettings.ChannelFlag.Data!;

                if (!String.IsNullOrEmpty(ChannelFlagData))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Forcing channel {ChannelFlagData}");
                    EnrollChannel(ChannelFlagData);
                }
            }

            if (!App.LaunchSettings.VersionFlag.Active || string.IsNullOrEmpty(App.LaunchSettings.VersionFlag.Data))
            {
                ClientVersion clientVersion;

                try
                {
                    clientVersion = await Deployment.GetInfo(Deployment.Channel, behindProductionCheck);
                }
                catch (InvalidChannelException ex)
                {
                    // copied from v2.5.4
                    // we are keeping similar logic just updated for newer apis

                    // If channel does not exist
                    if (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Reverting enrolled channel to {Deployment.DefaultChannel} because a WindowsPlayer build does not exist for {App.Settings.Prop.Channel}");
                    }
                    // If channel is not available to the user (private/internal release channel)
                    else if (ex.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Reverting enrolled channel to {Deployment.DefaultChannel} because {App.Settings.Prop.Channel} is restricted for public use.");

                        // Only prompt if user has channel switching mode set to something other than Automatic.
                        if (App.Settings.Prop.ChannelChangeMode != ChannelChangeMode.Automatic)
                        {
                            Frontend.ShowMessageBox(
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
                    clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel, behindProductionCheck);
                }

                if (clientVersion.IsBehindDefaultChannel && App.Settings.Prop.ChannelChangeMode == ChannelChangeMode.Prompt)
                {
                    MessageBoxResult action = Frontend.ShowMessageBox(
                            String.Format(Strings.Bootstrapper_Dialog_ChannelOutOfDate, Deployment.Channel, Deployment.DefaultChannel),
                            MessageBoxImage.Warning,
                            MessageBoxButton.YesNo
                        );

                    if (action == MessageBoxResult.Yes)
                    {
                        App.Logger.WriteLine("Bootstrapper::CheckLatestVersion", $"Changed Roblox channel from {App.Settings.Prop.Channel} to {Deployment.DefaultChannel}");

                        RevertChannel();
                        clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel);
                    }
                }

                key.SetValueSafe("www.roblox.com", Deployment.IsDefaultChannel ? "" : Deployment.Channel);

                _latestVersionGuid = clientVersion.VersionGuid;
                _latestVersion = Utilities.ParseVersionSafe(clientVersion.Version);
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Version set to {App.LaunchSettings.VersionFlag.Data} from arguments");
                _latestVersionGuid = App.LaunchSettings.VersionFlag.Data;
                // we can't determine the version
            }

            if (_staticDirectory)
                _latestVersionDirectory = AppData.StaticDirectory;
            else
                _latestVersionDirectory = Path.Combine(Paths.Versions, _latestVersionGuid);

            string pkgManifestUrl = Deployment.GetLocation($"/{_latestVersionGuid}-rbxPkgManifest.txt");
            var pkgManifestData = await App.HttpClient.GetStringAsync(pkgManifestUrl);

            _versionPackageManifest = new(pkgManifestData);

            // this can happen if version is set through arguments
            if (_launchMode == LaunchMode.Unknown)
            {
                App.Logger.WriteLine(LOG_IDENT, "Identifying launch mode from package manifest");

                bool isPlayer = _versionPackageManifest.Exists(x => x.Name == "RobloxApp.zip");
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

            if (_mustUpgrade)
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
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Not eligible: Major version diff is {diff}");
                return false;
            }
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

            using EventWaitHandle initEventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "Froststrap-MultiInstanceWatcherInitialisationFinished");
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
                int count = Process.GetProcesses().Count(x => x.ProcessName is "RobloxPlayerBeta");
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

#if WINDOWS
        private const int WM_SETICON = 0x80;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
#endif

        private async Task<bool> SetProcessWindowIcon(Process process, RobloxIcon icon)
        {
            const string LOG_IDENT = "Bootstrapper::SetProcessWindowIcon";

            // Only run on Windows
            if (!OperatingSystem.IsWindows() || icon == RobloxIcon.Default)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Icon setting skipped (Platform: {RuntimeInformation.OSDescription}, Icon: {icon})");
                return false;
            }

#if WINDOWS
            using var iconHandle = LoadIconResource(icon);

            if (iconHandle == null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Icon resource '{icon}' not found.");
                return false;
            }

            var startTime = DateTime.Now;
            bool iconSet = false;

            while ((DateTime.Now - startTime).TotalSeconds < 20 && !process.HasExited)
            {
                try
                {
                    var hwnd = process.MainWindowHandle;
                    if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd))
                    {
                        await Task.Delay(25);
                        continue;
                    }

                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, iconHandle.Handle);
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, iconHandle.Handle);

                    iconSet = true;
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to set icon: {ex}");
                    await Task.Delay(25);
                }
            }

            App.Logger.WriteLine(LOG_IDENT, $"Icon setting {(iconSet ? "succeeded" : "failed or timed out")}.");
            return iconSet;
#else
            App.Logger.WriteLine(LOG_IDENT, $"Icon setting not supported on {RuntimeInformation.OSDescription}");
            return false;
#endif
        }

#if WINDOWS
        private Icon? LoadIconResource(RobloxIcon icon)
        {
            if (icon == RobloxIcon.Default)
                return null;

            var resourceName = $"Froststrap.Resources.{icon}.ico";
            var assembly = Assembly.GetExecutingAssembly();

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            return stream != null ? new Icon(stream) : null;
        }
#endif

        private async void StartRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::StartRoblox";

            SetStatus(Strings.Bootstrapper_Status_Starting);

            string[] Names = { App.RobloxPlayerAppName, App.RobloxAnselAppName, App.RobloxStudioAppName };
            string ResolvedName = null!;

            foreach (string Name in Names)
            {
                string Directory = Path.Combine((string)AppData.Directory, Name);
                if (File.Exists(Directory))
                {
                    ResolvedName = Name;
                }
            }

            if (String.IsNullOrEmpty(ResolvedName))
            {
                await UpgradeRoblox();
            }

            var startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(AppData.Directory, ResolvedName),
                Arguments = _launchCommandLine,
                WorkingDirectory = AppData.Directory
            };

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

            string? logFileName = null;

            string rbxDir = Paths.Roblox;
            if (!Directory.Exists(rbxDir))
                Directory.CreateDirectory(rbxDir);

            string rbxLogDir = Path.Combine(rbxDir, "logs");
            if (!Directory.Exists(rbxLogDir))
                Directory.CreateDirectory(rbxLogDir);

            using var logWatcher = new FileSystemWatcher()
            {
                Path = rbxLogDir,
                Filter = "*.log",
                EnableRaisingEvents = true
            };

            var logCreatedEvent = new AutoResetEvent(false);

            logWatcher.Created += (_, e) =>
            {
                logWatcher.EnableRaisingEvents = false;
                logFileName = e.FullPath;
                logCreatedEvent.Set();
            };

            try
            {
                using var process = Process.Start(startInfo)!;

                // Continue with the rest of your code like _appPid assignment, icon setting, etc.
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

            App.Logger.WriteLine(LOG_IDENT, $"Started Roblox (PID {_appPid}), waiting for log file");

            // should i increase timeout ? since i think watcher dosent launh sometimes cause it cannot find the log file in time.
            logCreatedEvent.WaitOne(TimeSpan.FromSeconds(30));

            if (String.IsNullOrEmpty(logFileName))
            {
                App.Logger.WriteLine(LOG_IDENT, "Unable to identify log file");
                // Frontend.ShowPlayerErrorDialog();
                return;
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Got log file as {logFileName}");
            }

            _mutex?.ReleaseAsync();

            var autoclosePids = new List<int>();

            if (!IsStudioLaunch)
            {
                // launch custom integrations now if normal roblox
                foreach (var integration in App.Settings.Prop.CustomIntegrations)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Launching custom integration '{integration.Name}' ({integration.Location} {integration.LaunchArgs} - autoclose is {integration.AutoClose})");

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
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to launch integration '{integration.Name}'!");
                        App.Logger.WriteLine(LOG_IDENT, ex.Message);
                    }

                    if (integration.AutoClose && pid != 0)
                        autoclosePids.Add(pid);
                }
            }

            if (App.Settings.Prop.EnableActivityTracking || App.LaunchSettings.TestModeFlag.Active || autoclosePids.Any())
            {
                using var ipl = new InterProcessLock("Watcher", TimeSpan.FromSeconds(5));

                var watcherData = new WatcherData
                {
                    ProcessId = _appPid,
                    LogFile = logFileName,
                    AutoclosePids = autoclosePids,
                    LaunchMode = _launchMode
                };

                string watcherDataArg = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(watcherData)));

                string args = $"-watcher \"{watcherDataArg}\"";

                if (App.LaunchSettings.TestModeFlag.Active)
                    args += " -testmode";

                if (ipl.IsAcquired || true)
                    Process.Start(Paths.Process, args);
            }

            // allow for window to show, since the log is created pretty far beforehand
            Thread.Sleep(1000);
        }

        private bool ShouldRunAsAdmin()
        {
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

            if (Dialog is not null)
                Dialog.CancelEnabled = false;

            if (_isInstalling)
            {
                try
                {
                    // clean up registry keys
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
            else if (_appPid != 0)
            {
                try
                {
                    using var process = Process.GetProcessById(_appPid);
                    process.Kill();
                }
                catch (Exception) { }
            }

            Dialog?.CloseBootstrapper();

            App.SoftTerminate(ErrorCode.ERROR_CANCELLED);
        }
        #endregion

        #region App Install
        private async Task<bool> CheckForUpdates()
        {
            const string LOG_IDENT = "Bootstrapper::CheckForUpdates";

            if (Process.GetProcessesByName(App.ProjectName).Length > 1)
            {
                App.Logger.WriteLine(LOG_IDENT, $"More than one {App.ProjectName} instance running, aborting update check.");
                return false;
            }

            SetStatus("Checking for Updates...");

#if !DEBUG_UPDATER
            var releaseInfo = await App.GetLatestRelease();
            if (releaseInfo is null)
                return false;

            string currentVer = App.Version;
            string releaseVer = releaseInfo.TagName;

            var versionComparison = Utilities.CompareVersions(currentVer, releaseVer);

            if (versionComparison == VersionComparison.LessThan)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Update available: {currentVer} -> {releaseVer}");

                var result = Frontend.ShowMessageBox(
                    $"A new version {releaseVer} is available. Would you like to update now?",
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
                App.Logger.WriteLine(LOG_IDENT, $"No update required. Current version: {currentVer}, Release version: {releaseVer}");
                return false;
            }

            string version = releaseVer;
#else
    string version = App.Version;
#endif

            SetStatus(Strings.Bootstrapper_Status_UpgradingFroststrap);

            try
            {
#if DEBUG_UPDATER
        string downloadLocation = Path.Combine(Paths.TempUpdates, "Froststrap.exe");
        Directory.CreateDirectory(Paths.TempUpdates);
        File.Copy(Paths.Process, downloadLocation, overwrite: true);
#else
                var asset = releaseInfo.Assets![0];
                string downloadLocation = Path.Combine(Paths.TempUpdates, asset.Name);
                Directory.CreateDirectory(Paths.TempUpdates);

                App.Logger.WriteLine(LOG_IDENT, $"Downloading {version}...");

                if (!File.Exists(downloadLocation))
                {
                    using var response = await App.HttpClient.GetAsync(asset.BrowserDownloadUrl);
                    response.EnsureSuccessStatusCode();

                    await using var fileStream = new FileStream(downloadLocation, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fileStream);
                }
#endif

                App.Logger.WriteLine(LOG_IDENT, $"Starting updater {version}...");

                var startInfo = new ProcessStartInfo(downloadLocation)
                {
                    UseShellExecute = true,
                };

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
                    var result = Frontend.ShowMessageBox(
                        string.Format(Strings.Bootstrapper_AutoUpdateFailed, version),
                    MessageBoxImage.Information,
                    MessageBoxButton.YesNo);

                    if (result == MessageBoxResult.Yes)
                    {
                        Utilities.ShellExecute(App.ProjectDownloadLink);
                    }
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the auto-updater");
                App.Logger.WriteException(LOG_IDENT, ex);

                var result = Frontend.ShowMessageBox(
                    string.Format(Strings.Bootstrapper_AutoUpdateFailed, version),
                    MessageBoxImage.Information,
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    Utilities.ShellExecute(App.ProjectDownloadLink);
                }
            }

            return false;
        }
        #endregion

        #region Roblox Install
        private static bool TryDeleteRobloxInDirectory(string dir)
        {
            // If neither of these exist in the directory, return true.
            // This was not implemented properly.
            string clientPath = Path.Combine(dir, App.RobloxPlayerAppName);
            if (!File.Exists(clientPath))
            {
                clientPath = Path.Combine(dir, App.RobloxStudioAppName);
                if (!File.Exists(clientPath))
                    return true;
            }

            try
            {
                File.Delete(clientPath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void CleanupVersionsFolder()
        {
            const string LOG_IDENT = "Bootstrapper::CleanupVersionsFolder";

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

                if (
                    !_staticDirectory && (dirName != App.PlayerState.Prop.VersionGuid && dirName != App.StudioState.Prop.VersionGuid) ||
                    _staticDirectory && (dirName != "WindowsPlayer" && dirName != "WindowsStudio64")
                    )
                {
                    // TODO: this is too expensive
                    //Filesystem.AssertReadOnlyDirectory(dir);

                    // check if it's still being used first
                    // we dont want to accidentally delete the files of a running roblox instance
                    if (!TryDeleteRobloxInDirectory(dir))
                        continue;
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {dir}");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                    catch (IOException ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {dir}");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }
            }
        }

        private void MigrateCompatibilityFlags()
        {
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
        private static void KillRobloxPlayers()
        {
            const string LOG_IDENT = "Bootstrapper::KillRobloxPlayers";

            List<Process> processes = new List<Process>();
            processes.AddRange(Process.GetProcessesByName("RobloxPlayerBeta"));
            processes.AddRange(Process.GetProcessesByName("eurotrucks2"));
            processes.AddRange(Process.GetProcessesByName("RobloxCrashHandler")); // roblox studio doesnt depend on crash handler being open, so this should be fine

            foreach (Process process in processes)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to close process {process.Id}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }


        private async Task UpgradeRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::UpgradeRoblox";

            bool CancelUpgrade = !App.Settings.Prop.UpdateRoblox;

            if (CancelUpgrade)
            {
                SetStatus(Strings.Bootstrapper_Status_CancelUpgrade);
                App.Logger.WriteLine(LOG_IDENT, "Upgrading disabled, cancelling the upgrade.");
                Thread.Sleep(2000);
            }

            if (CancelUpgrade && !Directory.Exists(_latestVersionDirectory))
            {
                Frontend.ShowMessageBox(Strings.Bootstrapper_Dialog_NoUpgradeWithoutClient, MessageBoxImage.Warning, MessageBoxButton.OK);
            }
            else if (CancelUpgrade)
            {
                return;
            }

            if (String.IsNullOrEmpty(AppData.DistributionState.VersionGuid))
                SetStatus(Strings.Bootstrapper_Status_Installing);
            else
                SetStatus(Strings.Bootstrapper_Status_Upgrading);

            Directory.CreateDirectory(Paths.Base);
            Directory.CreateDirectory(Paths.Downloads);
            Directory.CreateDirectory(Paths.Versions);

            _isInstalling = true;

            // make sure nothing is running before continuing upgrade
            if (!App.LaunchSettings.BackgroundUpdaterFlag.Active && !IsStudioLaunch) // TODO: wait for studio processes to close before updating to prevent data loss
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
                    App.Logger.WriteLine(LOG_IDENT, "Failed to delete the latest version directory");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            Directory.CreateDirectory(_latestVersionDirectory);

            var cachedPackageHashes = Directory.GetFiles(Paths.Downloads).Select(x => Path.GetFileName(x));

            // package manifest states packed size and uncompressed size in exact bytes
            int totalSizeRequired = 0;

            // packed size only matters if we don't already have the package cached on disk
            totalSizeRequired += _versionPackageManifest.Where(x => !cachedPackageHashes.Contains(x.Signature)).Sum(x => x.PackedSize);
            totalSizeRequired += _versionPackageManifest.Sum(x => x.Size);

            if (Filesystem.GetFreeDiskSpace(Paths.Base) < totalSizeRequired)
            {
                Frontend.ShowMessageBox(Strings.Bootstrapper_NotEnoughSpace, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
                return;
            }

            if (Dialog is not null)
            {
                Dialog.ProgressStyle = ProgressBarStyle.Continuous;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;

                Dialog.ProgressMaximum = ProgressBarMaximum;

                // compute total bytes to download
                int totalPackedSize = _versionPackageManifest.Sum(package => package.PackedSize);
                _progressIncrement = (double)ProgressBarMaximum / totalPackedSize;

                if (Dialog is AvaloniaDialogBase)
                    _taskbarProgressMaximum = (double)TaskbarProgressMaximumWinForms;
                else
                    _taskbarProgressMaximum = (double)TaskbarProgressMaximumWpf;

                _taskbarProgressIncrement = _taskbarProgressMaximum / (double)totalPackedSize;
            }

            var extractionTasks = new List<Task>();

            foreach (var package in _versionPackageManifest)
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                // download all the packages synchronously
                await DownloadPackage(package);

                // Extract this later if need be
                if (package.Name == "WebView2RuntimeInstaller.zip")
                    continue;

                // extract the package async immediately after download
                extractionTasks.Add(Task.Run(() => ExtractPackage(package), _cancelTokenSource.Token));
            }

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            if (Dialog is not null)
            {
                Dialog.ProgressStyle = ProgressBarStyle.Marquee;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
                SetStatus(Strings.Bootstrapper_Status_Configuring);
            }

            await Task.WhenAll(extractionTasks);

            App.Logger.WriteLine(LOG_IDENT, "Writing AppSettings.xml...");
            await File.WriteAllTextAsync(Path.Combine(_latestVersionDirectory, "AppSettings.xml"), AppSettings);

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            if (App.State.Prop.PromptWebView2Install)
            {
                using var hklmKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                using var hkcuKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");

                if (hklmKey is not null || hkcuKey is not null)
                {
                    // reset prompt state if the user has it installed
                    App.State.Prop.PromptWebView2Install = true;
                }
                else
                {
                    var result = Frontend.ShowMessageBox(Strings.Bootstrapper_WebView2NotFound, MessageBoxImage.Warning, MessageBoxButton.YesNo, MessageBoxResult.Yes);

                    if (result != MessageBoxResult.Yes)
                    {
                        App.State.Prop.PromptWebView2Install = false;
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Installing WebView2 runtime...");

                        var package = _versionPackageManifest.Find(x => x.Name == "WebView2RuntimeInstaller.zip");

                        if (package is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Aborted runtime install because package does not exist, has WebView2 been added in this Roblox version yet?");
                            return;
                        }

                        string baseDirectory = Path.Combine(_latestVersionDirectory, PackageDirectoryMap[package.Name]);

                        ExtractPackage(package);

                        SetStatus(Strings.Bootstrapper_Status_InstallingWebView2);

                        var startInfo = new ProcessStartInfo()
                        {
                            WorkingDirectory = baseDirectory,
                            FileName = Path.Combine(baseDirectory, "MicrosoftEdgeWebview2Setup.exe"),
                            Arguments = "/silent /install"
                        };

                        await Process.Start(startInfo)!.WaitForExitAsync();

                        App.Logger.WriteLine(LOG_IDENT, "Finished installing runtime");

                        Directory.Delete(baseDirectory, true);
                    }
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

            if (!App.Settings.Prop.DebugDisableVersionPackageCleanup)
            {
                foreach (string hash in cachedPackageHashes)
                {
                    if (!allPackageHashes.Contains(hash))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Deleting unused package {hash}");

                        try
                        {
                            File.Delete(Path.Combine(Paths.Downloads, hash));
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {hash}!");
                            App.Logger.WriteException(LOG_IDENT, ex);
                        }
                    }
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "Registering approximate program size...");

            int distributionSize = _versionPackageManifest.Sum(x => x.Size + x.PackedSize) / 1024;

            AppData.DistributionState.Size = distributionSize;

            int totalSize = App.PlayerState.Prop.Size + App.PlayerState.Prop.Size;

            using (var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey))
            {
                uninstallKey.SetValueSafe("EstimatedSize", totalSize);
            }

            WindowsRegistry.RegisterClientLocation(IsStudioLaunch, _latestVersionDirectory);

            App.Logger.WriteLine(LOG_IDENT, $"Registered as {totalSize} KB");

            App.State.Prop.ForceReinstall = false;

            App.State.Save();
            AppData.DistributionStateManager.Save();

            _isInstalling = false;
        }

        private static void StartBackgroundUpdater()
        {
            const string LOG_IDENT = "Bootstrapper::StartBackgroundUpdater";

            if (Utilities.DoesMutexExist("Froststrap-BackgroundUpdater"))
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

            // handle file mods
            App.Logger.WriteLine(LOG_IDENT, "Checking file mods...");

            // manifest has been moved to State.json
            File.Delete(Path.Combine(Paths.Base, "ModManifest.txt"));

            List<string> modFolderFiles = new();

            Directory.CreateDirectory(Paths.Modifications);

            // check custom font mod
            // instead of replacing the fonts themselves, we'll just alter the font family manifests

            string modFontFamiliesFolder = Path.Combine(Paths.Modifications, "content\\fonts\\families");

            var fontTask = Task.Run(() =>
            {
                if (File.Exists(Paths.CustomFont))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Begin font check");

                    Directory.CreateDirectory(modFontFamiliesFolder);

                    const string path = "rbxasset://fonts/CustomFont.ttf";

                    // lets make sure the content/fonts/families path exists in the version directory
                    string contentFolder = Path.Combine(_latestVersionDirectory, "content");
                    Directory.CreateDirectory(contentFolder);

                    string fontsFolder = Path.Combine(contentFolder, "fonts");
                    Directory.CreateDirectory(fontsFolder);

                    string familiesFolder = Path.Combine(fontsFolder, "families");
                    Directory.CreateDirectory(familiesFolder);

                    var jsonFiles = Directory.GetFiles(familiesFolder);

                    // Process font files in parallel (up to 4 at a time)
                    Parallel.ForEach(jsonFiles, new ParallelOptions { MaxDegreeOfParallelism = 4 }, jsonFilePath =>
                    {
                        string jsonFilename = Path.GetFileName(jsonFilePath);
                        string modFilepath = Path.Combine(modFontFamiliesFolder, jsonFilename);

                        if (File.Exists(modFilepath))
                            return;

                        App.Logger.WriteLine(LOG_IDENT, $"Setting font for {jsonFilename}");

                        var fontFamilyData = JsonSerializer.Deserialize<Models.FontFamily>(File.ReadAllText(jsonFilePath));

                        if (fontFamilyData is null)
                            return;

                        bool shouldWrite = false;

                        foreach (var fontFace in fontFamilyData.Faces)
                        {
                            if (fontFace.AssetId != path)
                            {
                                fontFace.AssetId = path;
                                shouldWrite = true;
                            }
                        }

                        if (shouldWrite)
                            File.WriteAllText(modFilepath, JsonSerializer.Serialize(fontFamilyData, new JsonSerializerOptions { WriteIndented = true }));
                    });

                    App.Logger.WriteLine(LOG_IDENT, "End font check");
                }
                else if (Directory.Exists(modFontFamiliesFolder))
                {
                    Directory.Delete(modFontFamiliesFolder, true);
                }
            });

            // Process regular file modifications with limited concurrency
            var fileTasks = new List<Task<bool>>();
            using var semaphore = new SemaphoreSlim(8); // Limit concurrent file operations

            foreach (string file in Directory.GetFiles(Paths.Modifications, "*.*", SearchOption.AllDirectories))
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return true;

                // get relative directory path
                string relativeFile = file.Substring(Paths.Modifications.Length + 1);

                // v1.7.0 - README has been moved to the preferences menu now
                if (relativeFile == "README.txt")
                {
                    File.Delete(file);
                    continue;
                }

                if (!App.Settings.Prop.UseFastFlagManager && String.Equals(relativeFile, "ClientSettings\\ClientAppSettings.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (relativeFile.EndsWith(".lock"))
                    continue;

                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(relativeFile);
                bool isDeleteOperation = fileNameWithoutExt.EndsWith("_Delete");

                if (isDeleteOperation)
                {
                    string directory = Path.GetDirectoryName(relativeFile) ?? "";
                    string originalFileNameWithoutDelete = fileNameWithoutExt.Substring(0, fileNameWithoutExt.Length - 7);
                    string originalExtension = Path.GetExtension(relativeFile);
                    string originalFileName = Path.Combine(directory, originalFileNameWithoutDelete + originalExtension);

                    string originalFileVersionPath = Path.Combine(_latestVersionDirectory, originalFileName);

                    modFolderFiles.Add(relativeFile);

                    fileTasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            if (File.Exists(originalFileVersionPath))
                            {
                                Filesystem.AssertReadOnly(originalFileVersionPath);
                                File.Delete(originalFileVersionPath);
                                App.Logger.WriteLine(LOG_IDENT, $"{originalFileName} has been deleted from the version folder");
                                return true;
                            }
                            else
                            {
                                App.Logger.WriteLine(LOG_IDENT, $"{originalFileName} not found in version folder, nothing to delete");
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Failed to delete file ({originalFileName})");
                            App.Logger.WriteException(LOG_IDENT, ex);
                            return false;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }
                else
                {
                    modFolderFiles.Add(relativeFile);

                    string fileModFolder = Path.Combine(Paths.Modifications, relativeFile);
                    string fileVersionFolder = Path.Combine(_latestVersionDirectory, relativeFile);

                    fileTasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            if (File.Exists(fileVersionFolder))
                            {
                                var hashTask = Task.Run(() => MD5Hash.FromFile(fileModFolder));
                                var existingHashTask = Task.Run(() => MD5Hash.FromFile(fileVersionFolder));

                                if (await hashTask == await existingHashTask)
                                {
                                    App.Logger.WriteLine(LOG_IDENT, $"{relativeFile} already exists in the version folder, and is a match");
                                    return true;
                                }
                            }

                            Directory.CreateDirectory(Path.GetDirectoryName(fileVersionFolder)!);

                            Filesystem.AssertReadOnly(fileVersionFolder);
                            try
                            {
                                File.Copy(fileModFolder, fileVersionFolder, true);
                                Filesystem.AssertReadOnly(fileVersionFolder);
                                App.Logger.WriteLine(LOG_IDENT, $"{relativeFile} has been copied to the version folder");
                                return true;
                            }
                            catch (Exception ex)
                            {
                                App.Logger.WriteLine(LOG_IDENT, $"Failed to apply modification ({relativeFile})");
                                App.Logger.WriteException(LOG_IDENT, ex);
                                return false;
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }
            }

            var fileResults = await Task.WhenAll(fileTasks);
            success = success && fileResults.All(r => r);

            await fontTask;

            // the manifest is primarily here to keep track of what files have been
            // deleted from the modifications folder, so that we know when to restore the original files from the downloaded packages
            // now check for files that have been deleted from the mod folder according to the manifest

            var fileRestoreMap = new Dictionary<string, List<string>>();

            foreach (string fileLocation in AppData.DistributionState.ModManifest)
            {
                if (modFolderFiles.Contains(fileLocation))
                    continue;

                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileLocation);
                bool isDeleteOperation = fileNameWithoutExt.EndsWith("_Delete");

                if (isDeleteOperation)
                {
                    string directory = Path.GetDirectoryName(fileLocation) ?? "";
                    string originalFileNameWithoutDelete = fileNameWithoutExt.Substring(0, fileNameWithoutExt.Length - 7);
                    string originalExtension = Path.GetExtension(fileLocation);
                    string originalFileName = Path.Combine(directory, originalFileNameWithoutDelete + originalExtension);

                    var packageMapEntry = PackageDirectoryMap.SingleOrDefault(x => !String.IsNullOrEmpty(x.Value) && originalFileName.StartsWith(x.Value));
                    string packageName = packageMapEntry.Key;

                    // package doesn't exist, likely mistakenly placed file
                    if (String.IsNullOrEmpty(packageName))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"{originalFileName} was removed but does not belong to a package");
                        continue;
                    }

                    string fileName = originalFileName.Substring(packageMapEntry.Value.Length);

                    if (!fileRestoreMap.ContainsKey(packageName))
                        fileRestoreMap[packageName] = new();

                    fileRestoreMap[packageName].Add(fileName);

                    App.Logger.WriteLine(LOG_IDENT, $"{originalFileName} was removed, restoring from {packageName}");
                }
                else
                {
                    var packageMapEntry = PackageDirectoryMap.SingleOrDefault(x => !String.IsNullOrEmpty(x.Value) && fileLocation.StartsWith(x.Value));
                    string packageName = packageMapEntry.Key;

                    // package doesn't exist, likely mistakenly placed file
                    if (String.IsNullOrEmpty(packageName))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"{fileLocation} was removed but does not belong to a package");

                        string versionFileLocation = Path.Combine(_latestVersionDirectory, fileLocation);

                        if (File.Exists(versionFileLocation))
                            File.Delete(versionFileLocation);

                        continue;
                    }

                    string fileName = fileLocation.Substring(packageMapEntry.Value.Length);

                    if (!fileRestoreMap.ContainsKey(packageName))
                        fileRestoreMap[packageName] = new();

                    fileRestoreMap[packageName].Add(fileName);

                    App.Logger.WriteLine(LOG_IDENT, $"{fileLocation} was removed, restoring from {packageName}");
                }
            }

            foreach (var entry in fileRestoreMap)
            {
                var package = _versionPackageManifest.Find(x => x.Name == entry.Key);

                if (package is not null)
                {
                    if (_cancelTokenSource.IsCancellationRequested)
                        return true;

                    await DownloadPackage(package);
                    ExtractPackage(package, entry.Value);
                }
            }

            // make sure we're not overwriting a new update
            // if we're the background update process, always overwrite
            if (App.LaunchSettings.BackgroundUpdaterFlag.Active || !AppData.DistributionStateManager.HasFileOnDiskChanged())
            {
                AppData.DistributionState.ModManifest = modFolderFiles;
                AppData.DistributionStateManager.Save();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"{AppData.DistributionStateManager.ClassName} disk mismatch, not saving ModManifest");
            }

            App.Logger.WriteLine(LOG_IDENT, $"Finished checking file mods");

            if (!success)
                App.Logger.WriteLine(LOG_IDENT, "Failed to apply all modifications");

            return success;
        }

        private async Task DownloadPackage(Package package)
        {
            string LOG_IDENT = $"Bootstrapper::DownloadPackage.{package.Name}";

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            Directory.CreateDirectory(Paths.Downloads);

            string packageUrl = Deployment.GetLocation($"/{_latestVersionGuid}-{package.Name}");
            string robloxPackageLocation = Path.Combine(Paths.Roblox, "Downloads", package.Signature);

            if (File.Exists(package.DownloadPath))
            {
                var file = new FileInfo(package.DownloadPath);

                string calculatedMD5 = MD5Hash.FromFile(package.DownloadPath);

                if (calculatedMD5 != package.Signature)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package is corrupted ({calculatedMD5} != {package.Signature})! Deleting and re-downloading...");
                    file.Delete();
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package is already downloaded, skipping...");

                    _totalDownloadedBytes += package.PackedSize;
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

                _totalDownloadedBytes += package.PackedSize;
                UpdateProgressBar();

                return;
            }

            if (File.Exists(package.DownloadPath))
                return;

            const int maxTries = 5;

            App.Logger.WriteLine(LOG_IDENT, "Downloading...");

            var buffer = new byte[4096];

            for (int i = 1; i <= maxTries; i++)
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

                        int bytesRead = await stream.ReadAsync(buffer, _cancelTokenSource.Token);

                        if (bytesRead == 0)
                            break;

                        totalBytesRead += bytesRead;

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cancelTokenSource.Token);

                        _totalDownloadedBytes += bytesRead;
                        SetStatus(
                            String.Format(App.Settings.Prop.DownloadingStringFormat,
                            package.Name,
                            totalBytesRead / 1048576,
                            package.Size / 1048576
                            ));
                        UpdateProgressBar();
                    }

                    string hash = MD5Hash.FromStream(fileStream);

                    if (hash != package.Signature)
                        throw new ChecksumFailedException($"Failed to verify download of {packageUrl}\n\nExpected hash: {package.Signature}\nGot hash: {hash}");

                    App.Logger.WriteLine(LOG_IDENT, $"Finished downloading! ({totalBytesRead} bytes total)");
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"An exception occurred after downloading {totalBytesRead} bytes. ({i}/{maxTries})");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    if (ex.GetType() == typeof(ChecksumFailedException))
                    {
                        Frontend.ShowConnectivityDialog(
                            Strings.Dialog_Connectivity_UnableToDownload,
                            String.Format(Strings.Dialog_Connectivity_UnableToDownloadReason, "[https://github.com/Froststraplabs/Froststrap/wiki/Froststrap-is-unable-to-download-Roblox](https://github.com/Froststraplabs/Froststrap/wiki/Froststrap-is-unable-to-download-Roblox)"),
                            MessageBoxImage.Error,
                            ex
                        );

                        App.Terminate(ErrorCode.ERROR_CANCELLED);
                    }
                    else if (i >= maxTries)
                        throw;

                    if (File.Exists(package.DownloadPath))
                        File.Delete(package.DownloadPath);

                    _totalDownloadedBytes -= totalBytesRead;
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

        private void ExtractPackage(Package package, List<string>? files = null)
        {
            const string LOG_IDENT = "Bootstrapper::ExtractPackage";

            string? packageDir = PackageDirectoryMap.GetValueOrDefault(package.Name);

            if (packageDir is null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"WARNING: {package.Name} was not found in the package map!");
                return;
            }

            string packageFolder = Path.Combine(_latestVersionDirectory, packageDir);
            string? fileFilter = null;

            // for sharpziplib, each file in the filter needs to be a regex
            if (files is not null)
            {
                var regexList = new List<string>();

                foreach (string file in files)
                    regexList.Add("^" + file.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + "$");

                fileFilter = String.Join(';', regexList);
            }

            App.Logger.WriteLine(LOG_IDENT, $"Extracting {package.Name}...");

            var fastZip = new FastZip(_fastZipEvents);
            fastZip.RestoreDateTimeOnExtract = false;
            fastZip.RestoreAttributesOnExtract = false;

            fastZip.ExtractZip(package.DownloadPath, packageFolder, fileFilter);

            App.Logger.WriteLine(LOG_IDENT, $"Finished extracting {package.Name}");
        }
        #endregion
    }
}