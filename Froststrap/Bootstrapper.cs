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
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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
        private const string BackgroundUpdaterMutexName = "Froststrap-BackgroundUpdater";
        private static readonly string[] DxvkDlls = ["d3d9.dll", "d3d10core.dll", "d3d11.dll", "dxgi.dll"];

        private const string WebView2MicrosoftRootPem = """
            -----BEGIN CERTIFICATE-----
            MIIF7TCCA9WgAwIBAgIQP4vItfyfspZDtWnWbELhRDANBgkqhkiG9w0BAQsFADCB
            iDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1Jl
            ZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEyMDAGA1UEAxMp
            TWljcm9zb2Z0IFJvb3QgQ2VydGlmaWNhdGUgQXV0aG9yaXR5IDIwMTEwHhcNMTEw
            MzIyMjIwNTI4WhcNMzYwMzIyMjIxMzA0WjCBiDELMAkGA1UEBhMCVVMxEzARBgNV
            BAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jv
            c29mdCBDb3Jwb3JhdGlvbjEyMDAGA1UEAxMpTWljcm9zb2Z0IFJvb3QgQ2VydGlm
            aWNhdGUgQXV0aG9yaXR5IDIwMTEwggIiMA0GCSqGSIb3DQEBAQUAA4ICDwAwggIK
            AoICAQCygEGqNThNE3IyaCJNuLLx/9VSvGzH9dJKjDbu0cJcfoyKrq8TKG/Ac+M6
            ztAlqFo6be+ouFmrEyNozQwph9FvgFyPRH9dkAFSWKxRxV8qh9zc2AodwQO5e7BW
            6KPeZGHCnvjzfLnsDbVU/ky2ZU+I8JxImQxCCwl8MVkXeQZ4KI2JOkwDJb5xalwL
            54RgpJki49KvhKSn+9GY7Qyp3pSJ4Q6g3MDOmT3qCFK7VnnkH4S6Hri0xElcTzFL
            h93dBWcmmYDgcRGjuKVB4qRTufcyKYMME782XgSzS0NHL2vikR7TmE/dQgfI6B0S
            /Jmpaz6SfsjWaTr8ZL22CZ3K/QwLopt3YEsDlKQwaRLWQi3BQUzK3Kr9j1uDRprZ
            /LHR47PJf0h6zSTwQY9cdNCssBAgBkm3xy0hyFfj0IbzA2j70M5xwYmZSmQBbP3s
            MJHPQTySx+W6hh1hhMdfgzlirrSSL0fzC/hV66AfWdC7dJse0Hbm8ukG1xDo+mTe
            acY1logC8Ea4PyeZb8txiSk190gWAjWP1Xl8TQLPX+uKg09FcYj5qQ1OcunCnAfP
            SRtOBA5jUYxe2ADBVSy2xuDCZU7JNDn1nLPEfuhhbhNfFcRf2X7tHc7uROzLLoax
            7Dj2cO2rXBPB2Q8Nx4CyVe0096yb5MPa50c8prWPMd/FS6/r8QIDAQABo1EwTzAL
            BgNVHQ8EBAMCAYYwDwYDVR0TAQH/BAUwAwEB/zAdBgNVHQ4EFgQUci06AjGQQ7kU
            BU7h6qfHMdEjiTQwEAYJKwYBBAGCNxUBBAMCAQAwDQYJKoZIhvcNAQELBQADggIB
            AH9yzw+3xRXbm8BJyiZb/p4T5tPw0tuXX/JLP02zrhmu7deXoKzvqTqjwkGw5biR
            nhOBJAPmCf0/V0A5ISRW0RAvS0CpNoZLtFNXmvvxfomPEf4YbFGq6O0JlbXlccmh
            6Yd1phV/yX43VF50k8XDZ8wNT2uoFwxtCJJ+i92Bqi1wIcM9BhS7vyRep4TXPw8h
            Ir1LAAbblxzYXtTFC1yHblCk6MM4pPvLLMWSZpuFXst6bJN8gClYW1e1QGm6CHmm
            ZGIVnYeWRbVmIyADixxzoNOieTPgUFmG2y/lAiXqcyqfABTINseSO+lOAOzYVgm5
            M0kS0lQLAausR7aRKX1MtHWAUgHoyoL2n8ysnI8X6i8msKtyrAv+nlEex0NVZ09R
            s1fWtuzuUrc66U7h14GIvE+OdbtLqPA1qibUZ2dJsnBMO5PcHd94kIZysjik0dyS
            TclY6ysSXNQ7roxrsIPlAT/4CTL2kzU0Iq/dNw13CYArzUgA8YyZGUcFAenRv9FO
            0OYoQzeZpApKCNmacXPSqs0xE2N2oTdvkjgefRI8ZjLny23h/FKJ3crWZgWalmG+
            oijHHKOnNlA8OqTfSm7mhzvO6/DggTedEzxSjr25HTTGHdUKaj2YKXCMiSrRq4IQ
            SB/c9O+lxbtVGjhjhE63bK2VVOxlIhBJF7jAHscPrFRH
            -----END CERTIFICATE-----
            """;

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

        private bool _matchmakingInProgress = false;
        private bool _skipMatchmaking = false;
        private CancellationTokenSource? _matchmakingCts;

        private SynchronizationContext? _uiContext;

        private bool _noConnection = false;

        private AsyncMutex? _mutex;
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
            if (!App.LaunchSettings.BypassUpdateCheck && !App.LaunchSettings.UpgradeFlag.Active && App.Settings.Prop.UpdateChecks != UpdateCheck.Disabled)
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
                    await LaunchStudioViaWineAsync();
                }
                else
                {
                    if (!await EnsureSoberInstalledAsync())
                        return;
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

            if (!string.IsNullOrEmpty(Deployment.ChannelToken))
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Private channel enrollment");
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

        private async Task<string> GetBetterMatchmakingServerID(CancellationToken cancellationToken = default)
        {
            const string LOG_IDENT = "Bootstrapper::GetBetterMatchmakingServerID";

            if (!string.IsNullOrEmpty(App.Settings.Prop.SelectedRegion) &&
                !App.Settings.Prop.SelectedRegion.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                App.Logger.WriteLine(LOG_IDENT, $"User selected specific region: {App.Settings.Prop.SelectedRegion}");

                var selectedRegionFetcher = new Integrations.RobloxServerFetcher();
                string? selectedRegionCookie = await selectedRegionFetcher.ResolveCookieAsync();
                if (string.IsNullOrEmpty(selectedRegionCookie))
                    throw new HttpRequestException("Could not obtain a valid .ROBLOSECURITY cookie");

                SetStatus(string.Format(Strings.Bootstrapper_Status_SearchingServers, App.Settings.Prop.SelectedRegion));

                var selectedRegionResult = await selectedRegionFetcher.FindBestServerInSelectedRegionAsync(
                    (long)_joinData.PlaceId!,
                    App.Settings.Prop.SelectedRegion,
                    App.Settings.Prop.JoinSmallerServer,
                    App.Settings.Prop.MaxServerCheck,
                    cookie: selectedRegionCookie,
                    cancellationToken: cancellationToken);

                if (selectedRegionResult.Found)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Found server in selected region {App.Settings.Prop.SelectedRegion}: {selectedRegionResult.ServerId} (players: {selectedRegionResult.Players})");
                    return selectedRegionResult.ServerId!;
                }

                App.Logger.WriteLine(LOG_IDENT, $"No servers found in selected region {App.Settings.Prop.SelectedRegion}. Falling back to Auto mode.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                App.Logger.WriteLine(LOG_IDENT, "Matchmaking was cancelled before auto mode could start.");
                return "";
            }

            cancellationToken.ThrowIfCancellationRequested();

            var autoFetcher = new Integrations.RobloxServerFetcher();

            if (cancellationToken.IsCancellationRequested)
                return "";

            SetStatus(string.Format(Strings.Bootstrapper_Status_FindingTopRegions, App.Settings.Prop.BestRegionAmounts));

            var topRegions = await autoFetcher.GetClosestRegionsForAutoModeAsync(App.Settings.Prop.BestRegionAmounts, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return "";

            if (topRegions.Count == 0)
                throw new HttpRequestException("No regions found from datacenter list");

            if (!string.IsNullOrEmpty(_joinData.JobId))
            {
                string? defaultRegion = await GetServerRegionAsync(_joinData.JobId, (long)_joinData.PlaceId!, cancellationToken);
                if (defaultRegion != null && topRegions.Count > 0 &&
                    defaultRegion.Equals(topRegions[0], StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Default server is already in the closest region. Keeping it.");
                    return _joinData.JobId;
                }
            }

            SetStatus(Strings.Bootstrapper_Status_SearchingNearbyServers);
            string? autoCookie = await autoFetcher.ResolveCookieAsync();
            if (string.IsNullOrEmpty(autoCookie))
                throw new HttpRequestException("Could not obtain a valid .ROBLOSECURITY cookie");

            var autoResult = await autoFetcher.FindBestServerInRegionAsync(
                (long)_joinData.PlaceId!,
                topRegions,
                App.Settings.Prop.JoinSmallerServer,
                App.Settings.Prop.MaxServerCheck,
                cookie: autoCookie,
                cancellationToken: cancellationToken);

            if (autoResult.Found)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Selected best server in {autoResult.Region} (rank {autoResult.Rank}, players: {autoResult.Players})");
                return autoResult.ServerId!;
            }

            App.Logger.WriteLine(LOG_IDENT, "No server found in any of the top regions.");
            return "";
        }

        private static async Task<string?> GetServerRegionAsync(string jobId, long placeId, CancellationToken cancellationToken = default)
        {
            var fetcher = new Integrations.RobloxServerFetcher();
            string? cookie = await fetcher.ResolveCookieAsync();
            if (string.IsNullOrEmpty(cookie))
                return null;

            var datacentersResult = await fetcher.GetDatacentersAsync(cancellationToken);
            if (datacentersResult == null)
                return null;

            var url = UrlBuilder.BuildApiUrl("gamejoin", "v1/join-game-instance");
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { placeId, isTeleport = false, gameId = jobId, gameJoinAttemptId = jobId }),
                Encoding.UTF8,
                "application/json"
            );

            var response = await App.HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("DataCenterId", out var dcElem) && dcElem.TryGetInt32(out int dcId))
            {
                var (_, dcMap) = datacentersResult.Value;
                if (dcMap.TryGetValue(dcId, out string? region))
                    return region;
            }
            return null;
        }

        private static async Task ApplyFastFlagsBasedOnPlaceId(long placeId, string contentDirectory)
        {
            const string LOG_IDENT = "Bootstrapper::ApplyFastFlagsBasedOnPlaceId";

            if (placeId <= 0)
            {
                App.Logger.WriteLine(LOG_IDENT, "Invalid place ID, skipping FastFlag application.");
                return;
            }

            if (!App.Settings.Prop.UseFastFlagManager)
            {
                App.Logger.WriteLine(LOG_IDENT, "FastFlag manager is disabled in settings.");
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Checking for FastFlag profile matching place ID: {placeId}");

            foreach (var kvp in App.Settings.Prop.ProfilePlaceIds)
            {
                string profileName = kvp.Key;
                List<string> placeIds = kvp.Value;

                if (placeIds.Contains(placeId.ToString()))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Found matching profile '{profileName}' for place ID {placeId}");

                    try
                    {
                        string profilesDirectory = Paths.SavedFlagProfiles;
                        string profilePath = Path.Combine(profilesDirectory, profileName);

                        if (!File.Exists(profilePath))
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Profile file '{profileName}' not found at {profilePath}");
                            return;
                        }

                        string profileJson = File.ReadAllText(profilePath);
                        var flags = JsonSerializer.Deserialize<Dictionary<string, object>>(profileJson);

                        if (flags == null || flags.Count == 0)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Profile '{profileName}' is empty or invalid");
                            return;
                        }

                        string clientSettingsDir = Path.Combine(contentDirectory, "ClientSettings");
                        Directory.CreateDirectory(clientSettingsDir);
                        string destPath = Path.Combine(clientSettingsDir, "ClientAppSettings.json");

                        Dictionary<string, object> existingSettings = [];
                        if (File.Exists(destPath))
                        {
                            try
                            {
                                string existingJson = File.ReadAllText(destPath);
                                existingSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson) ?? [];
                            }
                            catch (Exception ex)
                            {
                                App.Logger.WriteLine(LOG_IDENT, $"Failed to read existing ClientAppSettings.json: {ex.Message}");
                            }
                        }

                        foreach (var flag in flags)
                        {
                            existingSettings[flag.Key] = flag.Value;
                        }

                        string mergedJson = JsonSerializer.Serialize(existingSettings, _indentedJsonOptions);
                        await File.WriteAllTextAsync(destPath, mergedJson);

                        App.Logger.WriteLine(LOG_IDENT, $"Successfully applied FastFlag profile '{profileName}' for place ID {placeId} ({flags.Count} flags)");
                        App.Logger.WriteLine(LOG_IDENT, $"Updated versions folder: {destPath}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to apply FastFlag profile '{profileName}': {ex.Message}");
                    }
                }
            }

            App.Logger.WriteLine(LOG_IDENT, $"No FastFlag profile found for place ID {placeId}");
        }

        private async Task StartRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::StartRoblox";

            if (_launchMode == LaunchMode.Player)
            {
                _joinData = GameJoin.GetJoinDataByLaunchCommand(_launchCommandLine);

                if (_joinData.JoinType == GameJoinType.Unknown)
                    App.Logger.WriteLine(LOG_IDENT, "Unable to get join data");

                App.Logger.WriteLine(LOG_IDENT, $"Join Type: {_joinData.JoinType}");
                App.Logger.WriteLine(LOG_IDENT, $"Join Origin: {_joinData.JoinOrigin ?? "null"}");
                App.Logger.WriteLine(LOG_IDENT, $"Place ID: {_joinData.PlaceId?.ToString() ?? "null"}");
                App.Logger.WriteLine(LOG_IDENT, $"Job ID: {_joinData.JobId ?? "null"}");

                if (_joinData.PlaceId.HasValue && _joinData.PlaceId.Value > 0)
                {
                    string contentDirectory = OperatingSystem.IsMacOS()
                        ? Path.Combine(_latestVersionDirectory, AppData.ExecutableName, "Contents", "Resources")
                        : _latestVersionDirectory;
                    await ApplyFastFlagsBasedOnPlaceId(_joinData.PlaceId.Value, contentDirectory);
                }

                bool isRobloxUri = _launchCommandLine.StartsWith("roblox://", StringComparison.Ordinal);
                if (isRobloxUri)
                    App.Logger.WriteLine(LOG_IDENT, "Joining through roblox:// URI - skipping Better Matchmaking");
                else
                {
                    bool isFollowUser = false;

                    // _joinData.JoinType == GameJoinType.RequestFollowUser just doesnt work at all
                    // idk why they dont use it when the user is following a friend, but ok
                    if (App.Settings.Prop.EnableBetterMatchmaking &&
                        (_joinData.JoinOrigin == "friendServerListJoin" || _joinData.JoinOrigin == "placesListInHomePage"))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "User is trying to join a friend, showing dialog");

                        var result = await Frontend.ShowMessageBox(
                            String.Format(Strings.Menu_Bootstrapper_Experimental_BetterMatchmaking_FollowUser),
                            MessageBoxImage.Question,
                            MessageBoxButton.YesNo
                        );

                        if (result == MessageBoxResult.Yes)
                            isFollowUser = true;
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
                                App.Logger.WriteLine(LOG_IDENT, "Matchmaking was skipped due to user cancellation.");
                                matchmakingCancelled = true;
                            }
                            else
                            {
                                serverid = await GetBetterMatchmakingServerID(_matchmakingCts.Token);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Better Matchmaking was Skipped, joining original server.");
                        matchmakingCancelled = true;
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
                        _launchCommandLine = _launchCommandLine.Replace(_joinData.PlaceLauncherUrl, HttpUtility.UrlEncode(placeLauncherUrl));
                    }
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
                WorkingDirectory = AppData.Directory,
                UseShellExecute = OperatingSystem.IsMacOS()
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

                if (OperatingSystem.IsMacOS() && startInfo.FileName == "open")
                {
                    _appPid = await GetRobloxProcessIdAsync(expectedName, TimeSpan.FromSeconds(5));
                    if (_appPid == 0)
                    {
                        _appPid = process.Id;
                        App.Logger.WriteLine(LOG_IDENT, "Could not locate Roblox process, falling back to open PID.");
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Detected Roblox process PID: {_appPid}");
                    }
                }
                else
                {
                    _appPid = process.Id;
                }
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
                App.SoberSettings.SetPreset("ServerLocationIndicatorEnabled", "false");

            if (App.Settings.Prop.UseDiscordRichPresence)
            {
                App.SoberSettings.SetPreset("DiscordRpcEnabled", "false");
                App.SoberSettings.SetPreset("DiscordRpcShowJoinButton", "false");
            }

            if (App.Settings.Prop.UseDisableAppPatch)
                App.SoberSettings.SetPreset("CloseOnLeave", "false");

            App.SoberSettings.Save();

            _joinData = GameJoin.GetJoinDataByLaunchCommand(_launchCommandLine);

            if (_joinData.JoinType == GameJoinType.Unknown)
                App.Logger.WriteLine(LOG_IDENT, "Unable to get join data");

            App.Logger.WriteLine(LOG_IDENT, $"Join Type: {_joinData.JoinType}");
            App.Logger.WriteLine(LOG_IDENT, $"Join Origin: {_joinData.JoinOrigin ?? "null"}");
            App.Logger.WriteLine(LOG_IDENT, $"Place ID: {_joinData.PlaceId?.ToString() ?? "null"}");
            App.Logger.WriteLine(LOG_IDENT, $"Job ID: {_joinData.JobId ?? "null"}");

            if (_joinData.PlaceId.HasValue && _joinData.PlaceId.Value > 0)
            {
                string contentDirectory = OperatingSystem.IsMacOS()
                    ? Path.Combine(_latestVersionDirectory, AppData.ExecutableName, "Contents", "Resources")
                    : _latestVersionDirectory;
                await ApplyFastFlagsBasedOnPlaceId(_joinData.PlaceId.Value, contentDirectory);
            }

            bool isRobloxUri = _launchCommandLine.StartsWith("roblox://", StringComparison.Ordinal);
            if (isRobloxUri)
                App.Logger.WriteLine(LOG_IDENT, "Joining through roblox:// URI - skipping Better Matchmaking");
            else
            {
                bool isFollowUser = false;

                // _joinData.JoinType == GameJoinType.RequestFollowUser just doesnt work at all
                // idk why they dont use it when the user is following a friend, but ok
                if (App.Settings.Prop.EnableBetterMatchmaking &&
                    (_joinData.JoinOrigin == "friendServerListJoin" || _joinData.JoinOrigin == "placesListInHomePage"))
                {
                    App.Logger.WriteLine(LOG_IDENT, "User is trying to join a friend, showing dialog");

                    var result = await Frontend.ShowMessageBox(
                        String.Format(Strings.Menu_Bootstrapper_Experimental_BetterMatchmaking_FollowUser),
                        MessageBoxImage.Question,
                        MessageBoxButton.YesNo
                    );

                    if (result == MessageBoxResult.Yes)
                        isFollowUser = true;
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
                            App.Logger.WriteLine(LOG_IDENT, "Matchmaking was skipped due to user cancellation.");
                            matchmakingCancelled = true;
                        }
                        else
                        {
                            serverid = await GetBetterMatchmakingServerID(_matchmakingCts.Token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Better Matchmaking was Skipped, joining original server.");
                    matchmakingCancelled = true;
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
                    _launchCommandLine = _launchCommandLine.Replace(_joinData.PlaceLauncherUrl, HttpUtility.UrlEncode(placeLauncherUrl));
                }
            }

            SetStatus(Strings.Bootstrapper_Status_StartingSober);

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
                string detailsPart = string.IsNullOrWhiteSpace(ex.Message) ? "" : $"\n\n{ex.Message}";
                await Frontend.ShowMessageBox(
                    string.Format(Strings.Sober_LaunchFailed, SoberFlatpakId, detailsPart),
                    MessageBoxImage.Error
                );
                App.Terminate(ErrorCode.ERROR_CANCELLED);
            }
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

        private static async Task<int> GetRobloxProcessIdAsync(string expectedName, TimeSpan timeout)
        {
            string processName = expectedName.Replace(".app", "");
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                var processes = Process.GetProcessesByName(processName);
                var target = processes.OrderByDescending(p => p.StartTime).FirstOrDefault();
                if (target != null)
                {
                    return target.Id;
                }
                await Task.Delay(100);
            }
            return 0;
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

        public bool Cancel()
        {
            const string LOG_IDENT = "Bootstrapper::Cancel";

            if (_matchmakingInProgress)
            {
                App.Logger.WriteLine(LOG_IDENT, "Skipping Better MatchMaking.");
                _skipMatchmaking = true;
                _matchmakingCts?.Cancel();
                SetStatus(Strings.Bootstrapper_Status_SkippingMatchmaking);
                return true;
            }

            if (_cancelTokenSource.IsCancellationRequested)
                return false;

            App.Logger.WriteLine(LOG_IDENT, "Cancelling launch...");
            _cancelTokenSource.Cancel();

            Dialog?.CancelEnabled = false;

            if (_isInstalling)
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                        WindowsRegistry.RegisterClientLocation(IsStudioLaunch, null);

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
            return false;
        }
        #endregion

        #region App Install
        private async Task<bool> CheckForUpdates()
        {
            const string LOG_IDENT = "Bootstrapper::CheckForUpdates";

            if (Process.GetProcessesByName(App.ProjectName).Length > 1)
            {
                App.Logger.WriteLine(LOG_IDENT, $"More than one {App.ProjectName} instance running, aborting update check");
                return false;
            }

            if (App.Settings.Prop.UpdateChecks == UpdateCheck.Disabled)
            {
                App.Logger.WriteLine(LOG_IDENT, "Update checking is disabled in settings");
                return false;
            }

            SetStatus(Strings.Bootstrapper_Status_CheckingUpdates);

            App.Logger.WriteLine(LOG_IDENT, "Checking for updates...");

            try
            {
                bool includePreRelease = false;

#if QA_BUILD || DEBUG
                includePreRelease = true;
#endif

                if (App.Settings.Prop.UpdateChecks == UpdateCheck.Both || App.Settings.Prop.UpdateChecks == UpdateCheck.Test)
                    includePreRelease = true;

                var releaseInfo = await App.GetLatestRelease(includePreRelease);

                if (releaseInfo is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to get release information");
                    return false;
                }

                string currentVer = App.Version;
                string releaseVer = releaseInfo.TagName;
                var versionComparison = Utilities.CompareVersions(currentVer, releaseVer);

                if (versionComparison == VersionComparison.Equal || versionComparison == VersionComparison.GreaterThan)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"No updates found. Current: {currentVer}, Latest: {releaseVer}");
                    return false;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Update available: {currentVer} -> {releaseVer}");

                if (OperatingSystem.IsLinux())
                {
                    App.Logger.WriteLine(LOG_IDENT, "Update detected, prompting user to manually update");

                    var results = await Frontend.ShowMessageBox(
                        string.Format(Strings.Update_Linux_Available, releaseVer),
                        MessageBoxImage.Information,
                        MessageBoxButton.YesNo
                    );

                    if (results == MessageBoxResult.Yes)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "User chose to visit releases page");
                        Utilities.ShellExecute(App.ProjectDownloadLink);
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT, "User declined the update, continuing launch");
                    }

                    return false;
                }

                var asset = FindPlatformAsset(releaseInfo.Assets);
                if (asset is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "No suitable asset found for this platform");
                    await Frontend.ShowMessageBox(
                        string.Format(Strings.Update_NoPackageAvailable, GetPlatformName()),
                        MessageBoxImage.Warning
                    );
                    Utilities.ShellExecute(App.ProjectDownloadLink);
                    return false;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Found matching asset: {asset.Name}");

                if (!App.LaunchSettings.QuietFlag.Active)
                {
                    string releaseType = releaseInfo.Prerelease ? "pre-release" : "stable";
                    string newlinePart = "\n\nWould you like to update now?";
                    var result = await Frontend.ShowMessageBox(
                        string.Format(Strings.Update_Available, releaseType, releaseVer, newlinePart),
                        MessageBoxImage.Question,
                        MessageBoxButton.YesNo
                    );

                    if (result != MessageBoxResult.Yes)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "User declined the update");
                        return false;
                    }
                }

                SetStatus(string.Format(Strings.Bootstrapper_Status_DownloadingUpdate, releaseVer));

                string downloadPath = Path.Combine(Paths.TempUpdates, asset.Name);
                Directory.CreateDirectory(Paths.TempUpdates);

                App.Logger.WriteLine(LOG_IDENT, $"Downloading update from {asset.BrowserDownloadUrl}");

                await DownloadFileWithProgressAsync(asset.BrowserDownloadUrl, downloadPath);

                App.Logger.WriteLine(LOG_IDENT, $"Download complete: {downloadPath}");

                Dialog?.ProgressIndeterminate = true;
                Dialog?.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;

                SetStatus(string.Format(Strings.Bootstrapper_Status_InstallingUpdate, releaseVer));

                bool updateApplied = await ApplyUpdate(downloadPath);

                if (!updateApplied)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Update application failed");
                    await Frontend.ShowMessageBox(
                        string.Format(Strings.Bootstrapper_AutoUpdateFailed, releaseVer),
                        MessageBoxImage.Information
                    );
                    Utilities.ShellExecute(App.ProjectDownloadLink);
                    return false;
                }

                App.Logger.WriteLine(LOG_IDENT, "Update applied successfully");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "An exception occurred during update check");
                App.Logger.WriteException(LOG_IDENT, ex);

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
                try
                {
                    var fileInfo = new FileInfo(Paths.Process);
                    bool isSelfContained = fileInfo.Length > 80 * 1024 * 1024;

                    if (isSelfContained)
                        return ["Froststrap-SelfContained-Setup.exe", "-SelfContained-Setup.exe"];
                    else
                        return ["Froststrap-Setup.exe", "-Setup.exe"];
                }
                catch
                {
                    return ["Froststrap-Setup.exe", "-Setup.exe"];
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                return ["Froststrap-macOS.dmg", ".dmg"];
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
            const string LOG_IDENT = "Bootstrapper::ApplyUpdate";

            try
            {
                App.Settings.Save();
                App.State.Save();
                App.PlayerState.Save();
                App.StudioState.Save();

                App.Logger.WriteLine(LOG_IDENT, $"Applying update: {updatePath}");

                if (OperatingSystem.IsWindows())
                {
                    return await ApplyWindowsUpdate(updatePath);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    return await ApplyMacOSUpdate(updatePath);
                }

                App.Logger.WriteLine(LOG_IDENT, "Unsupported operating system for updates");
                return false;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to apply update: {ex.Message}");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        private static async Task<bool> ApplyWindowsUpdate(string updatePath)
        {
            const string LOG_IDENT = "Bootstrapper::ApplyWindowsUpdate";

            App.Logger.WriteLine(LOG_IDENT, $"Applying Windows update: {updatePath}");

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
                App.Logger.WriteLine(LOG_IDENT, $"Failed to apply Windows update: {ex.Message}");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        private static async Task<bool> ApplyMacOSUpdate(string updatePath)
        {
            const string LOG_IDENT = "Bootstrapper::ApplyMacOSUpdate";

            App.Logger.WriteLine(LOG_IDENT, $"Applying macOS update: {updatePath}");

            try
            {
                string scriptPath = Path.Combine(Paths.TempUpdates, "update_runner.sh");
                string appName = App.ProjectName;

                string scriptContent = $@"#!/bin/bash
set -e

echo ""Waiting for {appName} to exit...""
sleep 2

echo ""Mounting DMG...""
MOUNT_DIR=$(hdiutil attach ""{updatePath}"" -nobrowse -mountpoint /Volumes/{appName}Update | grep -o '/Volumes/.*')

if [ -z ""$MOUNT_DIR"" ]; then
    echo ""Failed to mount DMG""
    exit 1
fi

echo ""Copying app to /Applications...""
if [ -d ""/Applications/{appName}.app"" ]; then
    rm -rf ""/Applications/{appName}.app""
fi
cp -R ""$MOUNT_DIR/{appName}.app"" /Applications/

echo ""Unmounting DMG...""
hdiutil detach ""$MOUNT_DIR""

echo ""Removing old app data...""
rm -rf ""{Paths.Base}""

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
                App.Logger.WriteLine(LOG_IDENT, $"Failed to apply macOS update: {ex.Message}");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
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

        private static void KillRobloxPlayers()
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

            if (OperatingSystem.IsMacOS())
            {
                string backupDir = GetResourcesBackupPath(_latestVersionGuid);
                if (Directory.Exists(backupDir))
                {
                    try
                    {
                        Directory.Delete(backupDir, true);
                        App.Logger.WriteLine(LOG_IDENT, $"Deleted existing mod backup for {_latestVersionGuid}");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to delete mod backup: {ex.Message}");
                    }
                }
            }

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

            SetStatus(Strings.Bootstrapper_Status_CheckingFlatpak);

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

            SetStatus(Strings.Bootstrapper_Status_InstallingSober);

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
                string detailsPart = string.IsNullOrWhiteSpace(details) ? "" : $"\n\n{details}";
                string message = string.Format(Strings.Sober_FlatpakInstallFailed, SoberFlatpakId, detailsPart);
                await Frontend.ShowMessageBox(message, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_CANCELLED);
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, "Sober installation complete.");
            SetStatus(Strings.Bootstrapper_Status_StartingSober);
            return true;
        }

        private async Task UpdateSoberFlatpakAsync()
        {
            const string LOG_IDENT = "Bootstrapper::UpdateSoberFlatpak";

            App.Logger.WriteLine(LOG_IDENT, $"Running 'flatpak update {SoberFlatpakId}'.");
            SetStatus(Strings.Bootstrapper_Status_UpdatingSober);

            if (Dialog is not null)
            {
                Dialog.ProgressIndeterminate = false;
                Dialog.ProgressMaximum = 100;
                Dialog.ProgressValue = 0;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;
                Dialog.TaskbarProgressValue = 0.0;
            }

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

            var timeout = TimeSpan.FromMinutes(10);
            var cts = new CancellationTokenSource(timeout);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cts.Token,
                _cancelTokenSource.Token
            );

            try
            {
                using var process = Process.Start(updateStartInfo);
                updateProcess = process;
                if (updateProcess is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to start flatpak update process.");
                    return;
                }

                var progressRegex = new Regex(
                    @"Updating\s+(?<current>\d+)/(?<total>\d+)…",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase
                );
                var percentRegex = new Regex(
                    @"(?<percent>\d+)%",
                    RegexOptions.Compiled
                );

                int totalUpdates = 0;
                int currentUpdate = 0;

                var outputTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!linkedCts.Token.IsCancellationRequested)
                        {
                            string? line = await updateProcess.StandardOutput.ReadLineAsync();
                            if (line is null)
                                break;

                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                App.Logger.WriteLine(LOG_IDENT, $"[flatpak] {line}");

                                var progressMatch = progressRegex.Match(line);
                                int current = 0, total = 0;
                                if (progressMatch.Success)
                                {
                                    current = int.Parse(progressMatch.Groups["current"].Value);
                                    total = int.Parse(progressMatch.Groups["total"].Value);
                                }

                                var percentMatch = percentRegex.Match(line);
                                int percent = -1;
                                if (percentMatch.Success)
                                {
                                    percent = int.Parse(percentMatch.Groups["percent"].Value);
                                }

                                if (progressMatch.Success && percentMatch.Success)
                                {
                                    if (total != totalUpdates)
                                        totalUpdates = total;
                                    if (current != currentUpdate)
                                        currentUpdate = current;

                                    double segmentSize = 100.0 / total;
                                    double segmentProgress = (current - 1) * segmentSize + (percent / 100.0) * segmentSize;
                                    int overallPercent = (int)Math.Round(segmentProgress);
                                    overallPercent = Math.Clamp(overallPercent, 0, 100);

                                    _uiContext?.Post(_ =>
                                    {
                                        if (Dialog is not null)
                                        {
                                            Dialog.ProgressValue = overallPercent;
                                            Dialog.TaskbarProgressValue = overallPercent / 100.0;
                                            SetStatus(string.Format(Strings.Bootstrapper_Status_UpdatingSoberProgress, current, total, percent));
                                        }
                                    }, null);
                                }
                                else if (progressMatch.Success)
                                {
                                    totalUpdates = total;
                                    currentUpdate = current;
                                    double segmentStart = (current - 1) * (100.0 / total);
                                    int overallPercent = (int)Math.Round(segmentStart);
                                    overallPercent = Math.Clamp(overallPercent, 0, 100);
                                    _uiContext?.Post(_ =>
                                    {
                                        if (Dialog is not null)
                                        {
                                            Dialog.ProgressValue = overallPercent;
                                            Dialog.TaskbarProgressValue = overallPercent / 100.0;
                                            SetStatus(string.Format(Strings.Bootstrapper_Status_UpdatingSoberBasic, current, total));
                                        }
                                    }, null);
                                }
                                else
                                {
                                    string trimmed = line.Trim();
                                    if (!string.IsNullOrEmpty(trimmed) && trimmed.Length < 80)
                                    {
                                        _uiContext?.Post(_ =>
                                        {
                                            SetStatus(trimmed);
                                        }, null);
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Output reading cancelled.");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Error reading flatpak output: {ex.Message}");
                    }
                }, linkedCts.Token);

                var errorTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!linkedCts.Token.IsCancellationRequested)
                        {
                            string? line = await updateProcess.StandardError.ReadLineAsync();
                            if (line is null)
                                break;
                            if (!string.IsNullOrWhiteSpace(line))
                                App.Logger.WriteLine(LOG_IDENT, $"[flatpak-err] {line}");
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Error reading flatpak stderr: {ex.Message}");
                    }
                }, linkedCts.Token);

                await Task.WhenAny(
                    updateProcess.WaitForExitAsync(linkedCts.Token),
                    Task.Delay(Timeout.Infinite, linkedCts.Token)
                );

                if (!updateProcess.HasExited && linkedCts.IsCancellationRequested)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Update cancelled by user or timeout. Killing process.");
                    try { updateProcess.Kill(true); } catch { }
                }

                if (!updateProcess.HasExited)
                {
                    await updateProcess.WaitForExitAsync();
                }

                if (updateProcess.ExitCode != 0 && updateProcess.ExitCode != -1)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"flatpak update exited with code {updateProcess.ExitCode}.");
                }
                else if (updateProcess.ExitCode == 0)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Sober update finished successfully.");
                    _uiContext?.Post(_ =>
                    {
                        if (Dialog is not null)
                        {
                            Dialog.ProgressValue = 100;
                            Dialog.TaskbarProgressValue = 1.0;
                            SetStatus(Strings.Bootstrapper_Status_SoberUpdateComplete);
                        }
                    }, null);
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                App.Logger.WriteLine(LOG_IDENT, "Update timed out after 10 minutes.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to update Sober.");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
            finally
            {
                _uiContext?.Post(_ =>
                {
                    if (Dialog is not null)
                    {
                        Dialog.ProgressIndeterminate = true;
                        Dialog.ProgressValue = 0;
                        Dialog.TaskbarProgressValue = 0.0;
                        Dialog.TaskbarProgressState = TaskbarItemProgressState.None;
                    }
                }, null);
            }
        }

        private static async Task ExtractTarXzAsync(string archivePath, string outputDir, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo("tar", $"-xJf \"{archivePath}\" -C \"{outputDir}\"")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            using var process = Process.Start(psi);
            _ = process ?? throw new Exception("Failed to start tar");
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                var err = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new Exception($"tar extraction failed: {err}");
            }
        }

        private async Task SetupDxvkAndRendererAsync()
        {
            var renderer = App.Settings.Prop.StudioRenderer;
            string targetRenderer = renderer switch
            {
                StudioRenderer.D3D11 => "D3D11",
                StudioRenderer.D3D11FL10 => "D3D11FL10",
                StudioRenderer.DXVK => "D3D11",
                StudioRenderer.Vulkan => "Vulkan",
                StudioRenderer.OpenGL => "OpenGL",
                _ => "D3D11"
            };

            SetRendererFastFlags(targetRenderer);

            if (renderer == StudioRenderer.DXVK )
            {
                string url = "https://github.com/doitsujin/dxvk/releases/download/v2.7.1/dxvk-2.7.1.tar.gz";
                await InstallDxvkAsync(url);
            }
            else
            {
                CleanupDxvkDlls();
            }

            ApplyRendererFlagsToVersionDirectory();
        }

        private static void SetRendererFastFlags(string prefer)
        {
            string[] renderers = ["D3D11", "D3D11FL10", "Vulkan", "OpenGL"];
            foreach (var r in renderers)
            {
                bool isPreferred = r == prefer;
                App.FastFlags.SetValue($"FFlagDebugGraphicsPrefer{r}", isPreferred ? "True" : "False");
                App.FastFlags.SetValue($"FFlagDebugGraphicsDisable{r}", isPreferred ? "False" : "True");
            }
        }

        private async Task InstallDxvkAsync(string url)
        {
            string cacheFile = Path.Combine(Paths.Downloads, $"dxvk-2.7.1.tar.gz");
            Directory.CreateDirectory(Paths.Downloads);

            bool needsInstall = !DxvkDlls.Any(dll => File.Exists(Path.Combine(_latestVersionDirectory, dll)));

            if (needsInstall)
            {
                if (!File.Exists(cacheFile))
                {
                    SetStatus(Strings.Bootstrapper_Status_DownloadingDXVK);
                    await DownloadFileWithProgressAsync(url, cacheFile);
                    Dialog?.ProgressIndeterminate = true;
                }

                SetStatus(Strings.Bootstrapper_Status_ExtractingDXVK);
                await ExtractDxvkArchive(cacheFile, _latestVersionDirectory);
            }
        }

        private static async Task ExtractDxvkArchive(string archivePath, string outputDir)
        {
            await Task.Run(() =>
            {
                using var fileStream = File.OpenRead(archivePath);
                using var gzipStream = new GZipInputStream(fileStream);
                using var tarStream = new TarInputStream(gzipStream, System.Text.Encoding.UTF8);

                TarEntry entry;
                while ((entry = tarStream.GetNextEntry()) != null)
                {
                    if (entry.IsDirectory) continue;
                    string entryName = entry.Name.Replace('\\', '/');
                    if (entryName.Contains("/x64/") && entryName.EndsWith(".dll"))
                    {
                        string fileName = Path.GetFileName(entryName);
                        if (DxvkDlls.Contains(fileName))
                        {
                            string targetPath = Path.Combine(outputDir, fileName);
                            using var fs = File.Create(targetPath);
                            tarStream.CopyEntryContents(fs);
                        }
                    }
                }
            });
        }

        private void CleanupDxvkDlls()
        {
            foreach (var dll in DxvkDlls)
            {
                string path = Path.Combine(_latestVersionDirectory, dll);
                if (File.Exists(path))
                {
                    try { File.Delete(path); }
                    catch (Exception ex) { App.Logger.WriteLine("CleanupDxvkDlls", $"Failed to delete {dll}: {ex.Message}"); }
                }
            }
        }

        private void ApplyRendererFlagsToVersionDirectory()
        {
            if (string.IsNullOrEmpty(_latestVersionDirectory))
                return;

            string clientSettingsDir = Path.Combine(_latestVersionDirectory, "ClientSettings");
            Directory.CreateDirectory(clientSettingsDir);
            string destPath = Path.Combine(clientSettingsDir, "ClientAppSettings.json");

            Dictionary<string, object> existing = [];
            if (File.Exists(destPath))
            {
                try
                {
                    string existingJson = File.ReadAllText(destPath);
                    existing = JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson) ?? [];
                }
                catch { }
            }

            var rendererFlags = App.FastFlags.Prop
                .Where(kv => kv.Key.StartsWith("FFlagDebugGraphicsPrefer") || kv.Key.StartsWith("FFlagDebugGraphicsDisable"))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            foreach (var kv in rendererFlags)
                existing[kv.Key] = kv.Value;

            string mergedJson = JsonSerializer.Serialize(existing, _indentedJsonOptions);
            File.WriteAllText(destPath, mergedJson);
        }

        private async Task SetupWebView2Async(WineManager wineMgr)
        {
            const string LOG_IDENT = "Bootstrapper::SetupWebView2Async";

            if (!OperatingSystem.IsLinux() || !IsStudioLaunch)
                return;

            string? installedVersion = await wineMgr.QueryRegistryValueAsync(@"HKLM\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView", "DisplayVersion", _cancelTokenSource.Token);
            bool isInstalled = !string.IsNullOrEmpty(installedVersion);

            if (App.Settings.Prop.EnableWebView2 == isInstalled)
            {
                App.Logger.WriteLine(LOG_IDENT, App.Settings.Prop.EnableWebView2
                    ? "WebView2 already installed, skipping."
                    : "WebView2 already not installed, skipping.");
                return;
            }

            if (App.Settings.Prop.EnableWebView2)
            {
                App.Logger.WriteLine(LOG_IDENT, "Downloading WebView2 Runtime via Wine...");
                SetStatus(Strings.Bootstrapper_Status_DownloadingWebView2);

                try
                {
                    if (!await wineMgr.RegistryKeyExistsAsync(@"HKCU\Software\Wine\AppDefaults\msedgewebview2.exe", _cancelTokenSource.Token))
                        await wineMgr.AddRegistryValueAsync(@"HKCU\Software\Wine\AppDefaults\msedgewebview2.exe", "Version", "win7", cancellationToken: _cancelTokenSource.Token);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to set WebView2 AppDefaults override: {ex.Message}");
                }

                string? version = await GetWebView2LatestVersionAsync();
                if (version is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Could not resolve the latest WebView2 Runtime version, skipping install.");
                    return;
                }

                var download = await GetWebView2RuntimeDownloadAsync(version);
                if (download is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Could not resolve a WebView2 Runtime download, skipping install.");
                    return;
                }

                string tempFile = Path.Combine(Path.GetTempPath(), download.Value.FileId);
                try
                {
                    await DownloadFileWithProgressAsync(download.Value.Url, tempFile);
                    Dialog?.ProgressIndeterminate = true;

                    SetStatus(Strings.Bootstrapper_Status_InstallingWebView2);

                    int exitCode = await wineMgr.RunAsync(tempFile,
                        [ "--msedgewebview", "--do-not-launch-msedge", "--system-level" ],
                        cancellationToken: _cancelTokenSource.Token);

                    App.Logger.WriteLine(LOG_IDENT, exitCode == 0
                        ? $"WebView2 Runtime {version} installed successfully."
                        : $"WebView2 installer exited with code {exitCode}.");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to install WebView2: {ex.Message}");
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Uninstalling WebView2 Runtime via Wine...");
                SetStatus(Strings.Bootstrapper_Status_UninstallingWebView2);

                string uninstallerPath = Path.Combine(wineMgr.PrefixDir, "drive_c", "Program Files (x86)",
                    "Microsoft", "EdgeWebView", "Application", installedVersion!, "Installer", "setup.exe");

                if (!File.Exists(uninstallerPath))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"WebView2 uninstaller not found at {uninstallerPath}, skipping uninstall.");
                    return;
                }

                try
                {
                    int exitCode = await wineMgr.RunAsync(uninstallerPath,
                        [ "--msedgewebview", "--uninstall", "--system-level", "--force-uninstall" ],
                        cancellationToken: _cancelTokenSource.Token);

                    string? stillInstalled = await wineMgr.QueryRegistryValueAsync(@"HKLM\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView", "DisplayVersion", _cancelTokenSource.Token);
                    App.Logger.WriteLine(LOG_IDENT, string.IsNullOrEmpty(stillInstalled)
                        ? "WebView2 Runtime uninstalled successfully."
                        : $"WebView2 uninstaller exited with code {exitCode}, but WebView2 still appears installed.");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to uninstall WebView2: {ex.Message}");
                }
            }
        }

        private static readonly Lazy<HttpClient> _webView2HttpClient = new(CreateWebView2HttpClient);

        private static HttpClient CreateWebView2HttpClient()
        {
            var rootCert = X509Certificate2.CreateFromPem(WebView2MicrosoftRootPem);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, cert, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None)
                        return true;

                    if (cert is null || chain is null)
                        return false;

                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(rootCert);
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                    return chain.Build(cert);
                }
            };

            return new HttpClient(handler);
        }

        private async Task<string?> GetWebView2LatestVersionAsync()
        {
            const string LOG_IDENT = "Bootstrapper::GetWebView2LatestVersionAsync";
            try
            {
                string requestUrl = "https://msedge.api.cdp.microsoft.com/api/v1.1/contents/Browser/namespaces/Default/names/msedge-stable-win-x64/versions/latest?action=select";
                using var content = new StringContent(
                    "{\"targetingAttributes\":{\"Updater\":\"MicrosoftEdgeUpdate\"}}",
                    Encoding.UTF8, "application/json");

                using var response = await _webView2HttpClient.Value.PostAsync(requestUrl, content, _cancelTokenSource.Token);
                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Bad status: {(int)response.StatusCode} {response.StatusCode}");
                    return null;
                }

                var data = await response.Content.ReadFromJsonAsync<WebView2LatestResponse>(cancellationToken: _cancelTokenSource.Token);
                return data?.ContentId?.Version;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to query latest version: {ex.Message}");
                return null;
            }
        }

        private async Task<(string Url, string FileId)?> GetWebView2RuntimeDownloadAsync(string version)
        {
            const string LOG_IDENT = "Bootstrapper::GetWebView2RuntimeDownloadAsync";
            try
            {
                string requestUrl = $"https://msedge.api.cdp.microsoft.com/api/v1.1/contents/Browser/namespaces/Default/names/msedge-stable-win-x64/versions/{version}/files?action=GenerateDownloadInfo";
                using var response = await _webView2HttpClient.Value.PostAsync(requestUrl, null, _cancelTokenSource.Token);
                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Bad status: {(int)response.StatusCode} {response.StatusCode}");
                    return null;
                }

                var downloads = await response.Content.ReadFromJsonAsync<List<WebView2Download>>(cancellationToken: _cancelTokenSource.Token);
                if (downloads is null)
                    return null;

                foreach (var d in downloads)
                {
                    if (d.Url is null || d.FileId is null)
                        continue;

                    string trimmed = d.FileId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? d.FileId[..^4]
                        : d.FileId;

                    if (trimmed.Split('_').Length == 3)
                        return (d.Url, d.FileId);
                }

                App.Logger.WriteLine(LOG_IDENT, "No standalone Runtime entry found among CDP download results.");
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to query downloads: {ex.Message}");
                return null;
            }
        }

        private async Task<GithubRelease?> GetLatestKombuchaReleaseAsync()
        {
            const string url = $"https://api.github.com/repos/vinegarhq/kombucha/releases/latest";
            return await App.HttpClient.GetFromJsonAsync<GithubRelease>(url, _cancelTokenSource.Token);
        }

        private async Task LaunchStudioViaWineAsync()
        {
            const string LOG_IDENT = "Bootstrapper::LaunchStudioViaWineAsync";

            string baseWineDir = Path.Combine(Paths.Base, "Wine");
            string symlinkPath = Path.Combine(baseWineDir, "kombucha");
            string wineExe = Path.Combine(symlinkPath, "bin", "wine");

            if (!File.Exists(wineExe))
            {
                App.Logger.WriteLine(LOG_IDENT, "Wine not found – downloading Kombucha.");

                var release = await GetLatestKombuchaReleaseAsync();
                _ = release ?? throw new Exception("Could not fetch latest Kombucha release.");

                var asset = release.Assets?.FirstOrDefault(a => a.Name?.EndsWith(".tar.xz") == true);
                _ = asset ?? throw new Exception("No .tar.xz asset found in Kombucha release.");

                SetStatus(Strings.Bootstrapper_Status_DownloadingWine);

                string tempFile = Path.GetTempFileName();
                try
                {
                    await DownloadFileWithProgressAsync(asset.BrowserDownloadUrl, tempFile);
                    if (Dialog != null)
                    {
                        Dialog.ProgressIndeterminate = true;
                        Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
                    }

                    string versionTag = release.TagName.TrimStart('v');
                    string extractDir = Path.Combine(baseWineDir, "kombucha_versions", $"kombucha-{versionTag}");
                    if (Directory.Exists(extractDir))
                        Directory.Delete(extractDir, true);

                    Directory.CreateDirectory(Path.GetDirectoryName(extractDir)!);

                    SetStatus(Strings.Bootstrapper_Status_ExtractingWine);
                    await ExtractTarXzAsync(tempFile, Path.GetDirectoryName(extractDir)!, _cancelTokenSource.Token);

                    if (File.Exists(symlinkPath))
                        File.Delete(symlinkPath);
                    if (Directory.Exists(symlinkPath))
                        Directory.Delete(symlinkPath, true);

                    var psi = new ProcessStartInfo("ln", $"-s {extractDir} {symlinkPath}")
                    {
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                    };
                    using var proc = Process.Start(psi);
                    await proc!.WaitForExitAsync(_cancelTokenSource.Token);
                    if (proc.ExitCode != 0)
                    {
                        var err = await proc.StandardError.ReadToEndAsync(_cancelTokenSource.Token);
                        throw new Exception($"Symlink creation failed: {err}");
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Kombucha {versionTag} installed.");
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Wine already installed.");
            }

            var wineMgr = new WineManager(baseWineDir);

            SetStatus(Strings.Bootstrapper_Status_InitializingWinePrefix);
            await wineMgr.EnsurePrefixAsync(_cancelTokenSource.Token);

            await SetupDxvkAndRendererAsync();

            await SetupWebView2Async(wineMgr);

            SetStatus(Strings.Bootstrapper_Status_Starting);

            var env = new Dictionary<string, string>
            {
                { "WINEDLLOVERRIDES", "d3d9,d3d10core,d3d11,dxgi=n,b" }
            };

            if (App.Settings.Prop.StudioRenderer == StudioRenderer.DXVK)
            {
                env["DXVK_LOG_LEVEL"] = "warn";
                env["DXVK_STATE_CACHE_PATH"] = Paths.Cache;
            }

            if (App.Settings.Prop.StudioDebug)
            {
                env["WINEDEBUG"] = "warn+seh,fixme-all,err-kerberos,err-ntlm,err-combase";
            }

            foreach (var userEnv in App.Settings.Prop.StudioEnvironmentVariables)
                env[userEnv.Key] = userEnv.Value;

            string baseCommand = $"\"{Path.Combine(_latestVersionDirectory, App.RobloxStudioAppName)}\" {_launchCommandLine}";
            string finalCommand = baseCommand;

            string? virtualDesktop = App.Settings.Prop.StudioVirtualDesktop;
            if (!string.IsNullOrEmpty(virtualDesktop))
            {
                string uuid = Guid.NewGuid().ToString();
                finalCommand = $"explorer /desktop={uuid},{virtualDesktop} {baseCommand}";
            }

            string? customLauncher = App.Settings.Prop.StudioLauncher;
            if (!string.IsNullOrEmpty(customLauncher))
            {
                finalCommand = customLauncher.Replace("%command%", finalCommand);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = wineExe,
                Arguments = finalCommand,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = _latestVersionDirectory
            };

            startInfo.Environment["WINEPREFIX"] = wineMgr.PrefixDir;
            foreach (var kv in env)
                startInfo.Environment[kv.Key] = kv.Value;

            var autoclosePids = new List<int>();
            foreach (var integration in App.Settings.Prop.CustomIntegrations)
                if (integration?.PreLaunch == true)
                    LaunchIntegration(integration, autoclosePids, LOG_IDENT);

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to start Roblox Studio process.");
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

            string wineLogDir = Path.Combine(wineMgr.PrefixDir, "drive_c", "users", Environment.UserName, "AppData", "Local", "Roblox", "logs");
            Directory.CreateDirectory(wineLogDir);
            await LaunchWatcherIfNeededAsync(autoclosePids, logDirectory: wineLogDir);

            await Task.Delay(1000);
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

            Directory.CreateDirectory(Paths.Modifications);

            var allMods = App.State.Prop.Mods.ToList();
            var allModFolderNames = allMods.Select(m => m.FolderName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var activeMods = App.State.Prop.Mods
                .Where(m => m.Enabled && (
                    m.Target == ModTarget.Both ||
                    (IsStudioLaunch && m.Target == ModTarget.Studio) ||
                    (!IsStudioLaunch && m.Target == ModTarget.Player)))
                .OrderByDescending(x => x.Priority)
                .ToList();

            string contentDirectory = OperatingSystem.IsMacOS()
                ? Path.Combine(_latestVersionDirectory, AppData.ExecutableName, "Contents", "Resources")
                : _latestVersionDirectory;

            if (OperatingSystem.IsMacOS())
            {
                EnsureMacResourcesBackup(contentDirectory, _latestVersionGuid);
            }

            var currentModManifest = new Dictionary<string, ModFileEntry>(StringComparer.OrdinalIgnoreCase);

            string modFontFamiliesFolder = Path.Combine(Paths.Modifications, "content", "fonts", "families");

            string? customFontPath = null;
            string? customFontFilename = null;
            string? customFontModFolder = null;

            foreach (var mod in activeMods.OrderByDescending(m => m.Priority))
            {
                string modTtf = Path.Combine(Paths.Modifications, mod.FolderName, "content", "fonts", "CustomFont.ttf");
                string modOtf = Path.Combine(Paths.Modifications, mod.FolderName, "content", "fonts", "CustomFont.otf");

                if (File.Exists(modTtf))
                {
                    customFontPath = modTtf;
                    customFontFilename = "CustomFont.ttf";
                    customFontModFolder = mod.FolderName;
                    break;
                }
                else if (File.Exists(modOtf))
                {
                    customFontPath = modOtf;
                    customFontFilename = "CustomFont.otf";
                    customFontModFolder = mod.FolderName;
                    break;
                }
            }

            if (customFontPath == null && File.Exists(Paths.CustomFont))
            {
                customFontPath = Paths.CustomFont;
                customFontFilename = "CustomFont.ttf";
            }

            if (customFontPath != null && customFontFilename != null)
            {
                string fontFamiliesFolder;
                if (customFontModFolder != null)
                {
                    fontFamiliesFolder = Path.Combine(Paths.Modifications, customFontModFolder, "content", "fonts", "families");
                }
                else
                {
                    fontFamiliesFolder = Path.Combine(Paths.Modifications, "content", "fonts", "families");
                }

                App.Logger.WriteLine(LOG_IDENT, $"Begin font check using '{customFontFilename}' from '{customFontPath}' saving to '{fontFamiliesFolder}'");
                Directory.CreateDirectory(fontFamiliesFolder);

                string contentFolder = Path.Combine(_latestVersionDirectory, "content");
                Directory.CreateDirectory(contentFolder);
                string fontsFolder = Path.Combine(contentFolder, "fonts");
                Directory.CreateDirectory(fontsFolder);
                string familiesFolder = Path.Combine(fontsFolder, "families");
                Directory.CreateDirectory(familiesFolder);

                string rbxAssetPath = $"rbxasset://fonts/{customFontFilename}";

                foreach (string jsonFilePath in Directory.GetFiles(familiesFolder))
                {
                    string jsonFilename = Path.GetFileName(jsonFilePath);
                    string modFilepath = Path.Combine(fontFamiliesFolder, jsonFilename);
                    if (File.Exists(modFilepath))
                        continue;
                    var fontFamilyData = JsonSerializer.Deserialize<FontFamily>(File.ReadAllText(jsonFilePath));
                    if (fontFamilyData is null)
                        continue;
                    bool shouldWrite = false;
                    foreach (var fontFace in fontFamilyData.Faces)
                    {
                        if (fontFace.AssetId != rbxAssetPath)
                        {
                            fontFace.AssetId = rbxAssetPath;
                            shouldWrite = true;
                        }
                    }
                    if (shouldWrite)
                        File.WriteAllText(modFilepath, JsonSerializer.Serialize(fontFamilyData, _indentedJsonOptions));
                }
                App.Logger.WriteLine(LOG_IDENT, "End font check");
            }
            else
            {
                string flatFontFamiliesFolder = Path.Combine(Paths.Modifications, "content", "fonts", "families");
                if (Directory.Exists(flatFontFamiliesFolder))
                {
                    Directory.Delete(flatFontFamiliesFolder, true);
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "Writing AppSettings.xml...");
            if (!File.Exists(Path.Combine(Paths.Modifications, "AppSettings.xml"))
                && (!OperatingSystem.IsLinux() || IsStudioLaunch))
            {
                Directory.CreateDirectory(_latestVersionDirectory);
                await File.WriteAllTextAsync(
                    Path.Combine(_latestVersionDirectory, "AppSettings.xml"),
                    AppSettings.Replace("roblox.com", Deployment.RobloxDomain)
                );
            }

            var allModFiles = new Dictionary<string, (string SourcePath, int Priority, string ModName, FileInfo Info)>(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(Paths.Modifications))
            {
                App.Logger.WriteLine(LOG_IDENT, "Processing PresetModifications (Flat folder)...");

                foreach (string file in Directory.GetFiles(Paths.Modifications))
                {
                    string relativeFile = Path.GetFileName(file);
                    if (relativeFile == "README.txt" ||
                        relativeFile.EndsWith("info.json") ||
                        relativeFile.EndsWith(".lock") ||
                        relativeFile.StartsWith("ClientSettings\\"))
                        continue;

                    var info = new FileInfo(file);
                    allModFiles[relativeFile] = (file, int.MinValue, "BaseModification", info);
                }

                foreach (string dir in Directory.GetDirectories(Paths.Modifications))
                {
                    string dirName = Path.GetFileName(dir);
                    if (allModFolderNames.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                        continue;

                    foreach (string file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                    {
                        string relativeFile = Path.GetRelativePath(Paths.Modifications, file);
                        if (relativeFile == "README.txt" ||
                            relativeFile.EndsWith("info.json") ||
                            relativeFile.EndsWith(".lock") ||
                            relativeFile.StartsWith("ClientSettings\\"))
                            continue;

                        var info = new FileInfo(file);
                        allModFiles[relativeFile] = (file, int.MinValue, "BaseModification", info);
                    }
                }
            }

            foreach (var mod in activeMods)
            {
                string modSource = Path.Combine(Paths.Modifications, mod.FolderName);
                if (!Directory.Exists(modSource))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Skipping mod '{mod.FolderName}': directory not found");
                    continue;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Processing mod '{mod.FolderName}' (priority: {mod.Priority})");

                foreach (string file in Directory.GetFiles(modSource, "*.*", SearchOption.AllDirectories))
                {
                    string relativeFile = Path.GetRelativePath(modSource, file);

                    if (relativeFile == "README.txt" ||
                        relativeFile.EndsWith("info.json") ||
                        relativeFile.EndsWith(".lock") ||
                        relativeFile.StartsWith("ClientSettings\\"))
                        continue;

                    string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(relativeFile);
                    if (fileNameWithoutExt != null && fileNameWithoutExt.EndsWith("_Delete"))
                        continue;

                    var info = new FileInfo(file);

                    allModFiles[relativeFile] = (file, mod.Priority, mod.FolderName, info);
                }
            }

            var filesToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in activeMods)
            {
                string modSource = Path.Combine(Paths.Modifications, mod.FolderName);
                if (!Directory.Exists(modSource)) continue;

                foreach (string file in Directory.GetFiles(modSource, "*_Delete.*", SearchOption.AllDirectories))
                {
                    string relativeFile = Path.GetRelativePath(modSource, file);
                    string actualFile = relativeFile;
                    string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(relativeFile);
                    if (fileNameWithoutExt != null && fileNameWithoutExt.EndsWith("_Delete"))
                    {
                        string directory = Path.GetDirectoryName(relativeFile) ?? "";
                        string originalName = fileNameWithoutExt[..^7];
                        actualFile = Path.Combine(directory, originalName + Path.GetExtension(relativeFile));
                    }
                    filesToDelete.Add(actualFile);
                }
            }

            foreach (string relPath in filesToDelete)
                allModFiles.Remove(relPath);

            foreach (string relPath in filesToDelete)
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

            foreach (var entry in allModFiles)
            {
                if (_cancelTokenSource.IsCancellationRequested) return true;

                string relativeFile = entry.Key;
                var (sourceFile, priority, modName, sourceInfo) = entry.Value;
                string fileVersionFolder = Path.Combine(contentDirectory, relativeFile);

                fileTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        bool needsCopy = true;

                        if (File.Exists(fileVersionFolder))
                        {
                            var targetInfo = new FileInfo(fileVersionFolder);

                            if (targetInfo.Length == sourceInfo.Length &&
                                targetInfo.LastWriteTime == sourceInfo.LastWriteTime)
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

                        lock (currentModManifest)
                        {
                            currentModManifest[relativeFile] = new ModFileEntry
                            {
                                Size = sourceInfo.Length,
                                LastModified = sourceInfo.LastWriteTime
                            };
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to apply ({relativeFile}) from mod '{modName}': {ex.Message}");
                        return false;
                    }
                    finally { semaphore.Release(); }
                }));
            }

            var fileResults = await Task.WhenAll(fileTasks);
            success = success && fileResults.All(r => r);

            if (App.Settings.Prop.UseFastFlagManager && (!OperatingSystem.IsLinux() || IsStudioLaunch))
            {
                string source = Path.Combine(Paths.Modifications, "ClientSettings", "ClientAppSettings.json");
                if (File.Exists(source))
                {
                    string rel = Path.Combine("ClientSettings", "ClientAppSettings.json");
                    string dest = Path.Combine(contentDirectory, rel);
                    var info = new FileInfo(source);

                    lock (currentModManifest)
                        currentModManifest[rel] = new ModFileEntry { Size = info.Length, LastModified = info.LastWriteTime };

                    try
                    {
                        bool match = File.Exists(dest) &&
                            (await Task.Run(() => MD5Hash.FromFile(source)) == await Task.Run(() => MD5Hash.FromFile(dest)));
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

            var fileRestoreMap = new Dictionary<string, List<string>>();
            foreach (string fileLocation in AppData.DistributionState.ModManifest)
            {
                if (currentModManifest.ContainsKey(fileLocation))
                    continue;

                string actualFile = fileLocation;
                string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileLocation);

                if (fileNameWithoutExt != null && fileNameWithoutExt.EndsWith("_Delete") && OperatingSystem.IsLinux() && !IsStudioLaunch)
                    continue;

                if (OperatingSystem.IsMacOS())
                {
                    string backupDir = GetResourcesBackupPath(_latestVersionGuid);
                    string sourceFile = Path.Combine(backupDir, actualFile);
                    string destFile = Path.Combine(contentDirectory, actualFile);
                    if (File.Exists(sourceFile))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                        File.Copy(sourceFile, destFile, true);
                        App.Logger.WriteLine(LOG_IDENT, $"Restored '{actualFile}' from backup");
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Backup file not found: {actualFile}");
                    }
                    continue;
                }

                string? packageName = null;
                string? packageDir = null;

                foreach (var kvp in PackageDirectoryMap)
                {
                    if (!string.IsNullOrEmpty(kvp.Value) && actualFile.StartsWith(kvp.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        packageName = kvp.Key;
                        packageDir = kvp.Value;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageDir))
                {
                    string versionFileLocation = Path.Combine(_latestVersionDirectory, actualFile);
                    if (File.Exists(versionFileLocation))
                    {
                        Filesystem.AssertReadOnly(versionFileLocation);
                        File.Delete(versionFileLocation);
                        App.Logger.WriteLine(LOG_IDENT, $"Deleted orphaned file {actualFile}");
                    }
                    continue;
                }

                string internalZipPath = actualFile[packageDir.Length..].TrimStart(Path.DirectorySeparatorChar);

                if (!fileRestoreMap.ContainsKey(packageName))
                    fileRestoreMap[packageName] = [];

                fileRestoreMap[packageName].Add(internalZipPath);
                App.Logger.WriteLine(LOG_IDENT, $"Restoring '{internalZipPath}' from package {packageName}");
            }

            if (!OperatingSystem.IsLinux() || IsStudioLaunch)
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

        private static string GetResourcesBackupPath(string versionGuid)
        {
            return Path.Combine(Paths.Base, "ModBackup", versionGuid);
        }

        private static void CopyDirectory(string sourceDir, string destDir, bool overwrite = true)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDir, file);
                string dest = Path.Combine(destDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite);
            }
        }

        private static void EnsureMacResourcesBackup(string resourcesDir, string versionGuid)
        {
            const string LOG_IDENT = "Bootstrapper::EnsureMacResourcesBackup";
            string backupDir = GetResourcesBackupPath(versionGuid);

            if (Directory.Exists(backupDir))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Resources backup for version {versionGuid} already exists.");
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Creating Resources backup for version {versionGuid}...");
            Directory.CreateDirectory(backupDir);
            CopyDirectory(resourcesDir, backupDir, true);
            App.Logger.WriteLine(LOG_IDENT, "Resources backup created.");
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

                    string? packageDir = PackageDirectoryMap.GetValueOrDefault(package.Name);
                    if (packageDir is null)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"WARNING: {package.Name} not found in package map, skipping.");
                        return true;
                    }

                    string targetFolder = Path.Combine(_latestVersionDirectory, packageDir);

                    Directory.CreateDirectory(targetFolder);

                    if (files != null && files.Count > 0)
                    {
                        foreach (string relativePath in files)
                        {
                            string fullPath = Path.Combine(targetFolder, relativePath);
                            if (File.Exists(fullPath))
                            {
                                try { File.SetAttributes(fullPath, FileAttributes.Normal); File.Delete(fullPath); }
                                catch (Exception ex) { App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {fullPath}: {ex.Message}"); }
                            }
                        }
                    }

                    string? fileFilter = null;
                    if (files != null && files.Count > 0)
                    {
                        var regexList = new List<string>();
                        foreach (string file in files)
                            regexList.Add("^" + Regex.Escape(file) + "$");
                        fileFilter = string.Join(';', regexList);
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Extracting {package.Name} (Attempt {attempts}/{maxAttempts})...");

                    if (OperatingSystem.IsLinux() && IsStudioLaunch)
                    {
                        await ExtractZipLinux(package.DownloadPath, targetFolder, fileFilter, _cancelTokenSource.Token);
                    }
                    else
                    {
                        var fastZip = new FastZip(_fastZipEvents);
                        fastZip.ExtractZip(package.DownloadPath, targetFolder, fileFilter);
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Finished extracting {package.Name}");
                    return true;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Extraction failed on attempt {attempts}: {ex.Message}");

                    if (ex.Message.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Ignoring non‑critical extraction failure for font file.");
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
                    SetStatus(string.Format(Strings.Bootstrapper_Status_RetryingPackage, package.Name));
                    await Task.Delay(1000);
                    await DownloadPackage(package);
                }
            }

            return false;
        }

        private static async Task ExtractZipLinux(string zipPath, string targetFolder, string? fileFilter, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                using var zipFile = new ZipFile(File.OpenRead(zipPath));
                foreach (ZipEntry entry in zipFile)
                {
                    if (entry.IsDirectory)
                        continue;

                    string entryName = entry.Name.Replace('\\', '/');

                    if (!string.IsNullOrEmpty(fileFilter))
                    {
                        var patterns = fileFilter.Split(';');
                        bool matched = false;
                        foreach (var pattern in patterns)
                        {
                            if (Regex.IsMatch(entryName, pattern))
                            {
                                matched = true;
                                break;
                            }
                        }
                        if (!matched)
                            continue;
                    }

                    string targetPath = Path.Combine(targetFolder, entryName);
                    string? targetDir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(targetDir))
                        Directory.CreateDirectory(targetDir);

                    using var stream = zipFile.GetInputStream(entry);
                    using var fileStream = File.Create(targetPath);
                    stream.CopyTo(fileStream);
                }
            }, cancellationToken);
        }
        #endregion
    }
}