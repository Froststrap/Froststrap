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
using System.ComponentModel;
using System.Data;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Web;

namespace Froststrap
{
    internal class Bootstrapper : IDisposable
    {
        #region Constants

        private const int ProgressBarMaximum = 10000;
        private const double TaskbarProgressMaximum = 1.0;
        private const int DownloadBufferSize = 4096;
        private const int MaxDownloadAttempts = 5;
        private const string SoberFlatpakId = "org.vinegarhq.Sober";
        public const string BackgroundUpdaterLockName = "BackgroundUpdater";
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
        private Version? _latestVersion;
        private string _latestVersionGuid = null!;
        private string _latestVersionDirectory = null!;
        private PackageManifest _versionPackageManifest = null!;
        private readonly GameJoinData _joinData = null!;

        private static bool AutomaticallyUpdateSober => OperatingSystem.IsLinux() && App.Settings.Prop.AutomaticallyUpdateSober;
        private bool MustUpgrade => App.LaunchSettings.ForceFlag.Active
            || App.State.Prop.ForceReinstall
            || ((!OperatingSystem.IsLinux() || (OperatingSystem.IsLinux() && IsStudioLaunch)) && (String.IsNullOrEmpty(AppData.DistributionState.VersionGuid)
            || (OperatingSystem.IsMacOS() ? !Directory.Exists(AppData.ExecutablePath) : !File.Exists(AppData.ExecutablePath))))
            || (OperatingSystem.IsWindows() && !IsStudioLaunch && !File.Exists(Path.Combine(_latestVersionDirectory, "WebView2Loader.dll")))
            || (OperatingSystem.IsWindows() && !IsStudioLaunch && !File.Exists(Path.Combine(_latestVersionDirectory, "RobloxPlayerBeta.dll")));

        private bool _isInstalling;
        private double _progressIncrement;
        private double _taskbarProgressIncrement;
        private double _taskbarProgressMaximum;
        private long _totalDownloadedBytes;
        private long _totalPackagedBytes;
        private bool _packageExtractionSuccess = true;

        private bool _matchmakingInProgress;
        private bool _skipMatchmaking;
        private CancellationTokenSource? _matchmakingCts;

        private bool _noConnection;

        private InterProcessLock? _appLock;
        private int _appPid;
        private bool _disposed;

        public IBootstrapperDialog? Dialog;
        public bool IsStudioLaunch => _launchMode != LaunchMode.Player;
        public string LockName { get; set; } = "Bootstrapper";

        public bool QuitIfLockExists { get; set; }

        #endregion

        #region Core

        public Bootstrapper(LaunchMode launchMode)
        {
            _launchMode = launchMode;

            // https://github.com/icsharpcode/SharpZipLib/blob/master/src/ICSharpCode.SharpZipLib/Zip/FastZip.cs/#L669-L680
            // exceptions don't get thrown if we define events without actually binding to the failure events. probably a bug. ¯\_(ツ)_/¯
            _fastZipEvents.FileFailure += (_, e) =>
            {
                if (!e.Name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    throw e.Exception;

                App.Logger.Error($"Failed to extract {e.Name}");
                _packageExtractionSuccess = false;
            };
            _fastZipEvents.DirectoryFailure += (_, e) => throw e.Exception;
            _fastZipEvents.ProcessFile += (_, e) => e.ContinueRunning = !_cancelTokenSource.IsCancellationRequested;

            SetupAppData();

            Deployment.Channel = IsStudioLaunch ? App.Settings.Prop.StudioChannel : App.Settings.Prop.PlayerChannel;

            App.Logger.Info($"Using {(IsStudioLaunch ? "Studio" : "Player")} channel: {Deployment.Channel}");

            _joinData = GameJoin.GetJoinDataByLaunchCommand(_launchCommandLine);
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
            message = message.Replace("{product}", AppData.ProductName, StringComparison.Ordinal);
            Dialog?.Message = message;
        }

        private static string FormatBytes(long bytes)
        {
            // How funny would it be if i just kept going up to quettabytes lol
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

        private void UpdateProgressBar(bool updateStatus = true)
        {
            long current = Interlocked.Read(ref _totalDownloadedBytes);
            if (Dialog is null)
                return;

            if (updateStatus)
            {
                SetStatus(string.Format(CultureInfo.InvariantCulture,
                    Strings.Bootstrapper_Status_DownloadingPackages,
                    FormatBytes(current),
                    FormatBytes(_totalPackagedBytes)
                ));
            }

            int progressValue = (int)Math.Floor(_progressIncrement * current);
            progressValue = Math.Clamp(progressValue, 0, ProgressBarMaximum);
            Dialog.ProgressValue = progressValue;

            double taskbarProgressValue = _taskbarProgressIncrement * current;
            taskbarProgressValue = Math.Clamp(taskbarProgressValue, 0, _taskbarProgressMaximum);
            Dialog.TaskbarProgressValue = taskbarProgressValue;
        }

        private async Task HandleConnectionError(Exception ex)
        {
            _noConnection = true;

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

            // this is now always enabled as of v2.8.0
            Dialog?.CancelEnabled = true;

            if (AutomaticallyUpdateSober && _launchMode == LaunchMode.Player)
                await UpdateSoberFlatpakAsync();

            SetStatus(Strings.Bootstrapper_Status_Connecting);

            // Skip the Roblox deployment API connectivity check entirely.
            if (OperatingSystem.IsLinux() && !IsStudioLaunch)
            {
                _noConnection = true;
                _latestVersionDirectory = Paths.SoberAssetOverlay;
                App.Logger.Info("Linux (Player): skipping connectivity check — Sober manages Roblox.");
            }
            else
            {
                var connectionResult = await Deployment.InitializeConnectivity();
                App.Logger.Info("Connectivity check finished");

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

                CleanupVersionsFolder(); // cleanup after background updater

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
                            Utilities.KillBackgroundUpdater();
                            backgroundUpdaterLockOpen = false;
                        }

                        if (!backgroundUpdaterLockOpen)
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
                if (OperatingSystem.IsWindows())
                {
                    if (IsStudioLaunch)
                    {
                        WindowsRegistry.RegisterStudio();
                        App.Logger.Debug("Studio launch detected, syncing RPC plugin...");
                        StudioPluginManager.Sync();
                    }
                    else
                    {
                        WindowsRegistry.RegisterPlayer();
                    }

                    WindowsRegistry.RegisterClientLocation(IsStudioLaunch, _latestVersionDirectory); // if it for some reason doesnt exist
                    WindowsRegistry.UpdateEstimatedSize();
                }
                else
                {
                    if (IsStudioLaunch)
                    {
                        App.Logger.Debug("Studio launch detected, syncing RPC plugin...");
                        StudioPluginManager.Sync();
                    }
                }

                if (!App.LaunchSettings.NoLaunchFlag.Active && !_cancelTokenSource.IsCancellationRequested)
                {
                    if (!App.LaunchSettings.QuietFlag.Active)
                    {
                        // show tips
                        if (!_packageExtractionSuccess)
                            Backend.NativeNotify.SendMessage(Strings.Bootstrapper_ExtractionFailed_Title, Strings.Bootstrapper_ExtractionFailed_Message);
                        else if (!allModificationsApplied)
                            Backend.NativeNotify.SendMessage(Strings.Bootstrapper_ModificationsFailed_Title, Strings.Bootstrapper_ModificationsFailed_Message);
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

                    Dialog?.CloseBootstrapper();
                }
            }
            finally
            {
                _appLock?.Dispose();
            }
        }

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
                            String.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Bootstrapper_Dialog_PromptChannelChange, displayChannel, Deployment.Channel),
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
                if (!String.IsNullOrEmpty(channelFlagData))
                {
                    App.Logger.Debug($"Forcing channel {channelFlagData}");
                    EnrollChannel(channelFlagData);
                }
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

                    if (!App.LaunchSettings.QuietFlag.Active)
                    {
                        await Frontend.ShowMessageBox(
                            string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_InvalidOverride, overrideHash, error),
                            MessageBoxImage.Warning,
                            MessageBoxButton.OK
                        );
                    }

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

                if (OperatingSystem.IsWindows())
                {
                    using var key = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\ROBLOX Corporation\\Environments\\{AppData.RegistryName}\\Channel");
                    key.SetValueSafe("www." + Deployment.RobloxDomain, Deployment.IsDefaultChannel ? "" : Deployment.Channel);
                }

                _latestVersionGuid = clientVersion.VersionGuid;
                _latestVersion = Utilities.ParseVersionSafe(clientVersion.Version);
            }
            else if (!overrideUsed)
            {
                string? versionData = App.LaunchSettings.VersionFlag.Data;
                if (string.IsNullOrEmpty(versionData))
                {
                    App.Logger.Error("VersionFlag.Data was unexpectedly null or empty. Falling back to default channel.");
                    var fallbackInfo = await Deployment.GetInfo(Deployment.DefaultChannel, false, false, AppData.BinaryType);
                    _latestVersionGuid = fallbackInfo.VersionGuid;
                    _latestVersion = Utilities.ParseVersionSafe(fallbackInfo.Version);
                }
                else
                {
                    App.Logger.Debug($"Version set to {versionData} from arguments");
                    _latestVersionGuid = versionData;
                }
            }

            if (App.Settings.Prop.StaticDirectory)
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
                var pkgManifestData = await App.HttpClient.GetStringAsync(new Uri(pkgManifestUrl));
                _versionPackageManifest = new(pkgManifestData);
            }

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

        private bool IsEligibleForBackgroundUpdate()
        {
            if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.Debug("Not eligible: Is the background updater process");
                return false;
            }

            if (!App.Settings.Prop.BackgroundUpdatesEnabled)
            {
                App.Logger.Debug("Not eligible: Background updates disabled");
                return false;
            }

            if (IsStudioLaunch)
            {
                App.Logger.Debug("Not eligible: Studio launch");
                return false;
            }

            if (MustUpgrade)
            {
                App.Logger.Debug("Not eligible: Must upgrade is true");
                return false;
            }

            if (!string.IsNullOrEmpty(Deployment.ChannelToken))
            {
                App.Logger.Debug("Not eligible: Private channel enrollment");
                return false;
            }

            // at least 3GB of free space
            const long minimumFreeSpace = 3_000_000_000;
            long space = Filesystem.GetFreeDiskSpace(Paths.Base);
            if (space < minimumFreeSpace)
            {
                App.Logger.Info($"Not eligible: User has {space} free space, at least {minimumFreeSpace} is required");
                return false;
            }

            if (_latestVersion == default)
            {
                App.Logger.Info("Not eligible: Latest version is undefined");
                return false;
            }

            Version? currentVersion = Utilities.GetRobloxVersion(AppData);
            if (currentVersion == default)
            {
                App.Logger.Info("Not eligible: Current version is undefined");
                return false;
            }

            // always normally upgrade for downgrades
            if (currentVersion.Minor > _latestVersion.Minor)
            {
                App.Logger.Info("Not eligible: Downgrade");
                return false;
            }

            // only background update if we're:
            // - one major update behind
            // - the same major update
            int diff = _latestVersion.Minor - currentVersion.Minor;
            if (diff == 0 || diff == 1)
            {
                App.Logger.Info("Eligible");
                return true;
            }

            App.Logger.Info($"Not eligible: Major version diff is {diff}");
            return false;
        }

        private async Task<string> GetBetterMatchmakingServerID(CancellationToken cancellationToken = default)
        {
            string sortOrder = App.Settings.Prop.SelectedServerSortOrder ?? "BestLatency";
            string selectedRegion = App.Settings.Prop.SelectedRegion ?? "";

            bool shouldUseRegion = sortOrder != "BestLatency" &&
                                   !string.IsNullOrEmpty(selectedRegion) &&
                                   !selectedRegion.Equals("Auto", StringComparison.OrdinalIgnoreCase);

            if (shouldUseRegion)
            {
                App.Logger.Debug($"User selected specific region: {selectedRegion}, sort order: {sortOrder}");

                using var selectedRegionFetcher = new Integrations.RobloxServerFetcher();
                string? selectedRegionCookie = await selectedRegionFetcher.ResolveCookieAsync();
                if (string.IsNullOrEmpty(selectedRegionCookie))
                    throw new HttpRequestException("Could not obtain a valid .ROBLOSECURITY cookie");

                SetStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_SearchingServers, selectedRegion));

                var selectedRegionResult = await selectedRegionFetcher.FindBestServerInSelectedRegionAsync(
                    (long)_joinData.PlaceId!,
                    selectedRegion,
                    sortOrder,
                    App.Settings.Prop.MaxServerCheck,
                    cookie: selectedRegionCookie,
                    cancellationToken: cancellationToken);

                if (selectedRegionResult.Found)
                {
                    App.Logger.Info($"Found server in selected region {selectedRegion}: {selectedRegionResult.ServerId} (players: {selectedRegionResult.Players})");
                    return selectedRegionResult.ServerId!;
                }

                App.Logger.Info($"No servers found in selected region {selectedRegion}. Falling back to Auto mode.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                App.Logger.Debug("Matchmaking was cancelled before auto mode could start.");
                return "";
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var autoFetcher = new Integrations.RobloxServerFetcher();

            if (cancellationToken.IsCancellationRequested)
                return "";

            SetStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_FindingTopRegions, App.Settings.Prop.BestRegionAmounts));

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
                    App.Logger.Info($"Default server is already in the closest region. Keeping it.");
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
                "BestLatency",
                App.Settings.Prop.MaxServerCheck,
                cookie: autoCookie,
                cancellationToken: cancellationToken);

            if (autoResult.Found)
            {
                App.Logger.Info($"Selected best server in {autoResult.Region} (rank {autoResult.Rank}, players: {autoResult.Players})");
                return autoResult.ServerId!;
            }

            App.Logger.Warn("No server found in any of the top regions.");
            return "";
        }

        private static async Task<string?> GetServerRegionAsync(string jobId, long placeId, CancellationToken cancellationToken = default)
        {
            using var fetcher = new Integrations.RobloxServerFetcher();
            string? cookie = await fetcher.ResolveCookieAsync();
            if (string.IsNullOrEmpty(cookie))
                return null;

            var datacentersResult = await fetcher.GetDatacentersAsync(cancellationToken);
            if (datacentersResult == null)
                return null;

            var url = UrlBuilder.BuildApiUrl("gamejoin", "v1/join-game-instance");
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
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

        private async Task StartRoblox()
        {
            if (_launchMode == LaunchMode.Player)
            {
                if (_joinData.JoinType == GameJoinType.Unknown)
                    App.Logger.Warn("Unable to get join data");

                App.Logger.Info($"Join Type: {_joinData.JoinType}");
                App.Logger.Info($"Join Origin: {_joinData.JoinOrigin ?? "null"}");
                App.Logger.Info($"Place ID: {_joinData.PlaceId?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
                App.Logger.Info($"Job ID: {_joinData.JobId ?? "null"}");
                App.Logger.Info($"Access Code: {_joinData.AccessCode ?? "null"}");

                bool isRobloxUri = _launchCommandLine.StartsWith("roblox://", StringComparison.Ordinal);
                if (isRobloxUri)
                    App.Logger.Info("Joining through roblox:// URI - skipping Better Matchmaking");
                else
                {
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
                                App.Logger.Info("Matchmaking was skipped due to user cancellation.");
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
                        App.Logger.Debug("Better Matchmaking was Skipped, joining original server.");
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
                App.Logger.Warn($"{expectedName} not found at {expectedPath}, triggering upgrade...");
                await UpgradeRoblox();
            }

            if (!Directory.Exists(expectedPath) && !File.Exists(expectedPath))
                throw new FileNotFoundException($"Roblox application not found at expected path after upgrade: {expectedPath}");

            App.Logger.Info($"Resolved Roblox path: {expectedPath}");

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
                    LaunchIntegration(integration, autoclosePids);
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
                        App.Logger.Warn("Could not locate Roblox process, falling back to open PID.");
                    }
                    else
                    {
                        App.Logger.Info($"Detected Roblox process PID: {_appPid}");
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

            App.Logger.Debug($"Started Roblox (PID {_appPid}). Launching Watcher...");

            if (!IsStudioLaunch)
            {
                // launch custom integrations now if normal roblox
                foreach (var integration in App.Settings.Prop.CustomIntegrations)
                {
                    if (integration == null || integration.PreLaunch || integration.SpecifyGame)
                        continue;

                    LaunchIntegration(integration, autoclosePids);
                }
            }

            await LaunchWatcherIfNeededAsync(autoclosePids);

            // allow for window to show, since the log is created pretty far beforehand
            await Task.Delay(1000);
        }

        private async Task LaunchViaSober(List<int> autoclosePids)
        {
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

            if (_joinData.JoinType == GameJoinType.Unknown)
                App.Logger.Warn("Unable to get join data");

            App.Logger.Info($"Join Type: {_joinData.JoinType}");
            App.Logger.Info($"Join Origin: {_joinData.JoinOrigin ?? "null"}");
            App.Logger.Info($"Place ID: {_joinData.PlaceId?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
            App.Logger.Info($"Job ID: {_joinData.JobId ?? "null"}");
            App.Logger.Info($"Access Code: {_joinData.AccessCode ?? "null"}");

            bool isRobloxUri = _launchCommandLine.StartsWith("roblox://", StringComparison.Ordinal);
            if (isRobloxUri)
                App.Logger.Info("Joining through roblox:// URI - skipping Better Matchmaking");
            else
            {
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
                            App.Logger.Info("Matchmaking was skipped due to user cancellation.");
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
                    App.Logger.Info("Better Matchmaking was Skipped, joining original server.");
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
                }
            }

            SetStatus(Strings.Bootstrapper_Status_StartingSober);

            Utilities.KillSober();
            App.Logger.Debug($"Launching Sober via flatpak with args: {_launchCommandLine}");

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
                App.Logger.Info($"Sober launched with PID {_appPid}");
                App.Logger.Debug("Launching Watcher...");
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
                            App.Logger.Info($"latest.log not found at {latestLog}, closing dialog.");
                            return;
                        }

                        App.Logger.Info($"Tailing {latestLog} for ready signal...");

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
                                App.Logger.Debug("Sober window ready — closing bootstrapper dialog.");
                                Dialog?.CloseBootstrapper();
                                return;
                            }
                        }

                        App.Logger.Warn("Timed out waiting for Sober ready signal — closing dialog.");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"Log watcher error: {ex.Message} — closing dialog.");
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

                App.Logger.Info("Sober process exited");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Unhandled Exception - Failed to launch Sober via flatpak! {ex}");
                string detailsPart = string.IsNullOrWhiteSpace(ex.Message) ? "" : $"\n\n{ex.Message}";
                await Frontend.ShowMessageBox(
                    string.Format(CultureInfo.InvariantCulture, Strings.Sober_LaunchFailed, SoberFlatpakId, detailsPart),
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
                LaunchMode = _launchMode,
                AccessCode = _joinData.AccessCode
            };

            string watcherDataArg = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(watcherData)));

            string args = $"-watcher \"{watcherDataArg}\"";

            if (App.LaunchSettings.TestModeFlag.Active)
                args += " -testmode";

            Process.Start(Paths.Process, args);
        }

        private static void LaunchIntegration(CustomIntegration integration, List<int> autoclosePids)
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
                Thread.Sleep(integration.Delay);

        }

        private static async Task<int> GetRobloxProcessIdAsync(string expectedName, TimeSpan timeout)
        {
            string processName = expectedName.Replace(".app", "", StringComparison.Ordinal);
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
                    if (OperatingSystem.IsWindows())
                        WindowsRegistry.RegisterClientLocation(IsStudioLaunch, null);

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
                            App.Logger.Info($"Killing sober process (PID {soberProcess.Id})");
                            soberProcess.Kill(true);
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Error($"Failed to kill sober process (PID {soberProcess.Id}) {ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to enumerate sober processes. {ex}");
                }
            }

            _appLock?.Dispose();

            Dialog?.CloseBootstrapper();
            App.SoftTerminate(ErrorCode.ERROR_CANCELLED);
            return false;
        }
        #endregion

        #region App Install
        private async Task<bool> CheckForUpdates()
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

            SetStatus(Strings.Bootstrapper_Status_CheckingUpdates);

            App.Logger.Info("Checking for updates...");

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

                SetStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_DownloadingUpdate, releaseVer));

                string downloadPath = Path.Combine(Paths.TempUpdates, asset.Name);
                Directory.CreateDirectory(Paths.TempUpdates);

                App.Logger.Info($"Downloading update from {asset.BrowserDownloadUrl}");

                await DownloadFileWithProgressAsync(asset.BrowserDownloadUrl, downloadPath);

                App.Logger.Info($"Download complete: {downloadPath}");

                Dialog?.ProgressIndeterminate = true;
                Dialog?.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;

                SetStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_InstallingUpdate, releaseVer));

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
                return ["Froststrap-Setup.exe", "-Setup.exe"];
            }
            else if (OperatingSystem.IsMacOS())
            {
                return ["Froststrap.pkg", ".pkg"];
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
            if (OperatingSystem.IsLinux())
            {
                bool isStudio = App.Bootstrapper?.IsStudioLaunch ?? false;
                if (!isStudio)
                {
                    App.Logger.Info("Skipping cleanup on Linux (Player) to protect Sober's data directory.");
                    return;
                }
            }

            if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.Info("Background updater tried to cleanup, stopping!");
                return;
            }

            if (!Directory.Exists(Paths.Versions))
            {
                App.Logger.Info("Versions directory does not exist, skipping cleanup.");
                return;
            }

            foreach (string dir in Directory.GetDirectories(Paths.Versions))
            {
                string dirName = Path.GetFileName(dir);

                // to make static directory work on studio linux
                if (OperatingSystem.IsLinux() && dirName == "Sober")
                    continue;

                bool shouldDelete = App.Settings.Prop.StaticDirectory
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
                        App.Logger.Error(ex, $"Failed to delete {dir}");
                    }
                }
                catch (IOException ex)
                {
                    App.Logger.Error(ex, $"Failed to delete {dir}");
                }
            }
        }

        private void MigrateCompatibilityFlags()
        {
            if (!OperatingSystem.IsWindows())
                return;

            string oldClientLocation = Path.Combine(Paths.Versions, AppData.DistributionState.VersionGuid, AppData.ExecutableName);
            string newClientLocation = Path.Combine(_latestVersionDirectory, AppData.ExecutableName);

            // move old compatibility flags for the old location
            using RegistryKey appFlagsKey = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers");
            string? appFlags = appFlagsKey.GetValue(oldClientLocation) as string;

            if (appFlags is not null)
            {
                App.Logger.Info($"Migrating app compatibility flags from {oldClientLocation} to {newClientLocation}...");
                appFlagsKey.SetValueSafe(newClientLocation, appFlags);
                appFlagsKey.DeleteValueSafe(oldClientLocation);
            }
        }

        private static void KillRobloxPlayers()
        {
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
                    App.Logger.Error(ex, $"Failed to close process {process.Id}");
                }
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
                    App.Logger.Error(ex, "Failed to delete the latest version directory");
                }
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
                    await DownloadPackage(package);

                    // we'll extract the runtime installer later if we need to
                    if (package.Name != "WebView2RuntimeInstaller.zip")
                        await ExtractPackage(package);

                    downloadSemaphore.Release();
                }, _cancelTokenSource.Token);

                packageTasks.Add(task);
            }
            await Task.WhenAll(packageTasks);

            if (_cancelTokenSource.IsCancellationRequested)
                return;

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

                if (hklmKey is not null || hkcuKey is not null)
                {
                    // reset prompt state if the user has it installed
                    App.State.Prop.PromptWebView2Install = true;
                }
                else
                {
                    var result = await Frontend.ShowMessageBox(Strings.Bootstrapper_WebView2NotFound, MessageBoxImage.Warning, MessageBoxButton.YesNo, MessageBoxResult.Yes);

                    if (result != MessageBoxResult.Yes)
                    {
                        App.State.Prop.PromptWebView2Install = false;
                    }
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

                    string macOsDir = Path.Combine(appPath, "Contents", "MacOS");
                    if (Directory.Exists(macOsDir))
                    {
                        foreach (string file in Directory.GetFiles(macOsDir))
                        {
                            var fileInfo = new FileInfo(file);
                            fileInfo.UnixFileMode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                        }
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

            if (!App.Settings.Prop.DebugDisableVersionPackageCleanup)
            {
                foreach (string hash in cachedPackageHashes)
                {
                    if (!allPackageHashes.Contains(hash))
                    {
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
                }
            }

            App.State.Prop.ForceReinstall = false;
            App.State.Save();
            AppData.DistributionStateManager.Save();

            if (!IsStudioLaunch)
                InitializeModFolders();

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

                await checkProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));

                if (checkProcess.ExitCode != 0)
                    throw new InvalidOperationException("Flatpak returned a non-zero exit code.");
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
                        App.Logger.Info("Sober is already installed.");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to check Sober installation status.");
            }

            App.Logger.Info("Installing Sober...");

            if (Dialog is not null)
            {
                Dialog.ProgressIndeterminate = true;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
            }

            SetStatus(Strings.Bootstrapper_Status_InstallingSober);

            var installStartInfo = new ProcessStartInfo
            {
                FileName = "flatpak",
                Arguments = $"install --assumeyes --noninteractive --user flathub {SoberFlatpakId}",
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

                    App.Logger.Info($"[flatpak] {line}");

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
                string message = string.Format(CultureInfo.InvariantCulture, Strings.Sober_FlatpakInstallFailed, SoberFlatpakId, detailsPart);
                await Frontend.ShowMessageBox(message, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_CANCELLED);
                return false;
            }

            App.Logger.Info("Sober installation complete.");
            SetStatus(Strings.Bootstrapper_Status_StartingSober);
            return true;
        }

        private async Task UpdateSoberFlatpakAsync()
        {
            App.Logger.Info($"Running 'flatpak update {SoberFlatpakId}'.");
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
                Arguments = $"update --user {SoberFlatpakId} --assumeyes",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process? updateProcess = null;

            var timeout = TimeSpan.FromMinutes(20);
            using var cts = new CancellationTokenSource(timeout);

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
                    App.Logger.Error("Failed to start flatpak update process.");
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
                                App.Logger.Info($"[flatpak] {line}");

                                var progressMatch = progressRegex.Match(line);
                                int current = 0, total = 0;
                                if (progressMatch.Success)
                                {
                                    current = int.Parse(progressMatch.Groups["current"].Value, CultureInfo.InvariantCulture);
                                    total = int.Parse(progressMatch.Groups["total"].Value, CultureInfo.InvariantCulture);
                                }

                                var percentMatch = percentRegex.Match(line);
                                int percent = -1;
                                if (percentMatch.Success)
                                {
                                    percent = int.Parse(percentMatch.Groups["percent"].Value, CultureInfo.InvariantCulture);
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

                                    if (Dialog is not null)
                                    {
                                        Dialog.ProgressValue = overallPercent;
                                        Dialog.TaskbarProgressValue = overallPercent / 100.0;
                                        SetStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_UpdatingSoberProgress, current, total, percent));
                                    }

                                }
                                else if (progressMatch.Success)
                                {
                                    totalUpdates = total;
                                    currentUpdate = current;
                                    double segmentStart = (current - 1) * (100.0 / total);
                                    int overallPercent = (int)Math.Round(segmentStart);
                                    overallPercent = Math.Clamp(overallPercent, 0, 100);
                                    if (Dialog is not null)
                                    {
                                        Dialog.ProgressValue = overallPercent;
                                        Dialog.TaskbarProgressValue = overallPercent / 100.0;
                                        SetStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_UpdatingSoberBasic, current, total));
                                    }
                                }
                                else
                                {
                                    string trimmed = line.Trim();
                                    if (!string.IsNullOrEmpty(trimmed) && trimmed.Length < 80)
                                    {
                                        SetStatus(trimmed);
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        App.Logger.Info("Output reading cancelled.");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Warn($"Error reading flatpak output: {ex.Message}");
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
                                App.Logger.Warn($"[flatpak-err] {line}");
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        App.Logger.Warn($"Error reading flatpak stderr: {ex.Message}");
                    }
                }, linkedCts.Token);

                await Task.WhenAny(
                    updateProcess.WaitForExitAsync(linkedCts.Token),
                    Task.Delay(Timeout.Infinite, linkedCts.Token)
                );

                if (!updateProcess.HasExited && linkedCts.IsCancellationRequested)
                {
                    App.Logger.Warn("Update cancelled by user or timeout. Killing process.");
                    try { updateProcess.Kill(true); } catch { }
                }

                if (!updateProcess.HasExited)
                {
                    await updateProcess.WaitForExitAsync();
                }

                if (updateProcess.ExitCode != 0 && updateProcess.ExitCode != -1)
                {
                    App.Logger.Warn($"flatpak update exited with code {updateProcess.ExitCode}.");
                }
                else if (updateProcess.ExitCode == 0)
                {
                    App.Logger.Info("Sober update finished successfully.");
                    if (Dialog is not null)
                    {
                        Dialog.ProgressValue = 100;
                        Dialog.TaskbarProgressValue = 1.0;
                        SetStatus(Strings.Bootstrapper_Status_SoberUpdateComplete);
                    }
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                App.Logger.Warn("Update timed out after 10 minutes.");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to update Sober.");
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

        private static async Task ExtractTarXzAsync(string archivePath, string outputDir, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo("tar", $"-xJf \"{archivePath}\" -C \"{outputDir}\"")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            using var process = Process.Start(psi);
            _ = process ?? throw new InvalidOperationException("Failed to start tar");
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                var err = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException($"tar extraction failed: {err}");
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

            if (renderer == StudioRenderer.DXVK)
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
                    if (entryName.Contains("/x64/", StringComparison.OrdinalIgnoreCase) && entryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
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
                    catch (Exception ex) { App.Logger.Error("CleanupDxvkDlls", $"Failed to delete {dll}: {ex.Message}"); }
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
                .Where(kv => kv.Key.StartsWith("FFlagDebugGraphicsPrefer", StringComparison.Ordinal) || kv.Key.StartsWith("FFlagDebugGraphicsDisable", StringComparison.Ordinal))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            foreach (var kv in rendererFlags)
                existing[kv.Key] = kv.Value;

            string mergedJson = JsonSerializer.Serialize(existing, _indentedJsonOptions);
            File.WriteAllText(destPath, mergedJson);
        }

        private async Task SetupWebView2Async(WineManager wineMgr)
        {
            if (!OperatingSystem.IsLinux() || !IsStudioLaunch)
                return;

            string? installedVersion = await wineMgr.QueryRegistryValueAsync(@"HKLM\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView", "DisplayVersion", _cancelTokenSource.Token);
            bool isInstalled = !string.IsNullOrEmpty(installedVersion);

            if (App.Settings.Prop.EnableWebView2 == isInstalled)
            {
                App.Logger.Info(App.Settings.Prop.EnableWebView2
                    ? "WebView2 already installed, skipping."
                    : "WebView2 already not installed, skipping.");
                return;
            }

            if (App.Settings.Prop.EnableWebView2)
            {
                App.Logger.Info("Downloading WebView2 Runtime via Wine...");
                SetStatus(Strings.Bootstrapper_Status_DownloadingWebView2);

                try
                {
                    if (!await wineMgr.RegistryKeyExistsAsync(@"HKCU\Software\Wine\AppDefaults\msedgewebview2.exe", _cancelTokenSource.Token))
                        await wineMgr.AddRegistryValueAsync(@"HKCU\Software\Wine\AppDefaults\msedgewebview2.exe", "Version", "win7", cancellationToken: _cancelTokenSource.Token);
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to set WebView2 AppDefaults override: {ex.Message}");
                }

                string? version = await GetWebView2LatestVersionAsync();
                if (version is null)
                {
                    App.Logger.Warn("Could not resolve the latest WebView2 Runtime version, skipping install.");
                    return;
                }

                var download = await GetWebView2RuntimeDownloadAsync(version);
                if (download is null)
                {
                    App.Logger.Warn("Could not resolve a WebView2 Runtime download, skipping install.");
                    return;
                }

                string tempFile = Path.Combine(Path.GetTempPath(), download.Value.FileId);
                try
                {
                    await DownloadFileWithProgressAsync(download.Value.Url, tempFile);
                    Dialog?.ProgressIndeterminate = true;

                    SetStatus(Strings.Bootstrapper_Status_InstallingWebView2);

                    int exitCode = await wineMgr.RunAsync(tempFile,
                        ["--msedgewebview", "--do-not-launch-msedge", "--system-level"],
                        cancellationToken: _cancelTokenSource.Token);

                    App.Logger.Info(exitCode == 0
                        ? $"WebView2 Runtime {version} installed successfully."
                        : $"WebView2 installer exited with code {exitCode}.");
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to install WebView2: {ex.Message}");
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
            else
            {
                App.Logger.Info("Uninstalling WebView2 Runtime via Wine...");
                SetStatus(Strings.Bootstrapper_Status_UninstallingWebView2);

                string uninstallerPath = Path.Combine(wineMgr.PrefixDir, "drive_c", "Program Files (x86)",
                    "Microsoft", "EdgeWebView", "Application", installedVersion!, "Installer", "setup.exe");

                if (!File.Exists(uninstallerPath))
                {
                    App.Logger.Warn($"WebView2 uninstaller not found at {uninstallerPath}, skipping uninstall.");
                    return;
                }

                try
                {
                    int exitCode = await wineMgr.RunAsync(uninstallerPath,
                        ["--msedgewebview", "--uninstall", "--system-level", "--force-uninstall"],
                        cancellationToken: _cancelTokenSource.Token);

                    string? stillInstalled = await wineMgr.QueryRegistryValueAsync(@"HKLM\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView", "DisplayVersion", _cancelTokenSource.Token);
                    App.Logger.Info(string.IsNullOrEmpty(stillInstalled)
                        ? "WebView2 Runtime uninstalled successfully."
                        : $"WebView2 uninstaller exited with code {exitCode}, but WebView2 still appears installed.");
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to uninstall WebView2: {ex.Message}");
                }
            }
        }

        private static readonly Lazy<HttpClient> _webView2HttpClient = new(CreateWebView2HttpClient);

        private static HttpClient CreateWebView2HttpClient()
        {
            using var rootCert = X509Certificate2.CreateFromPem(WebView2MicrosoftRootPem);

            using var handler = new HttpClientHandler
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
            try
            {
                string requestUrl = "https://msedge.api.cdp.microsoft.com/api/v1.1/contents/Browser/namespaces/Default/names/msedge-stable-win-x64/versions/latest?action=select";
                using var content = new StringContent(
                    "{\"targetingAttributes\":{\"Updater\":\"MicrosoftEdgeUpdate\"}}",
                    Encoding.UTF8, "application/json");

                using var response = await _webView2HttpClient.Value.PostAsync(new Uri(requestUrl), content, _cancelTokenSource.Token);
                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.Warn($"Bad status: {(int)response.StatusCode} {response.StatusCode}");
                    return null;
                }

                var data = await response.Content.ReadFromJsonAsync<WebView2LatestResponse>(cancellationToken: _cancelTokenSource.Token);
                return data?.ContentId?.Version;
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Failed to query latest version: {ex.Message}");
                return null;
            }
        }

        private async Task<(string Url, string FileId)?> GetWebView2RuntimeDownloadAsync(string version)
        {
            try
            {
                string requestUrl = $"https://msedge.api.cdp.microsoft.com/api/v1.1/contents/Browser/namespaces/Default/names/msedge-stable-win-x64/versions/{version}/files?action=GenerateDownloadInfo";
                using var response = await _webView2HttpClient.Value.PostAsync(new Uri(requestUrl), null, _cancelTokenSource.Token);
                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.Warn($"Bad status: {(int)response.StatusCode} {response.StatusCode}");
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

                App.Logger.Warn("No standalone Runtime entry found among CDP download results.");
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Failed to query downloads: {ex.Message}");
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
            string baseWineDir = Path.Combine(Paths.Base, "Wine");
            string symlinkPath = Path.Combine(baseWineDir, "kombucha");
            string wineExe = Path.Combine(symlinkPath, "bin", "wine");

            if (!File.Exists(wineExe))
            {
                App.Logger.Info("Wine not found – downloading Kombucha.");

                var release = await GetLatestKombuchaReleaseAsync();
                _ = release ?? throw new InvalidOperationException("Could not fetch latest Kombucha release.");

                var asset = release.Assets?.FirstOrDefault(a => a.Name?.EndsWith(".tar.xz", StringComparison.Ordinal) == true);
                _ = asset ?? throw new InvalidOperationException("No .tar.xz asset found in Kombucha release.");

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
                        throw new InvalidOperationException($"Symlink creation failed: {err}");
                    }

                    App.Logger.Info($"Kombucha {versionTag} installed.");
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
            else
            {
                App.Logger.Info("Wine already installed.");
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
                finalCommand = customLauncher.Replace("%command%", finalCommand, StringComparison.Ordinal);
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
                    LaunchIntegration(integration, autoclosePids);

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    App.Logger.Error("Failed to start Roblox Studio process.");
                    await Frontend.ShowMessageBox("Could not start Roblox Studio. Please check your Wine installation.", MessageBoxImage.Error);
                    App.Terminate(ErrorCode.ERROR_CANCELLED);
                    return;
                }
                _appPid = process.Id;
                App.Logger.Info($"Roblox Studio started with Wine (PID {_appPid})");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to launch Studio: {ex.Message}");
                await Frontend.ShowMessageBox("Could not start Roblox Studio. Please check your Wine installation.", MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_CANCELLED);
                return;
            }

            foreach (var integration in App.Settings.Prop.CustomIntegrations)
                if (integration != null && !integration.PreLaunch && !integration.SpecifyGame)
                    LaunchIntegration(integration, autoclosePids);

            string wineLogDir = Path.Combine(wineMgr.PrefixDir, "drive_c", "users", Environment.UserName, "AppData", "Local", "Roblox", "logs");
            Directory.CreateDirectory(wineLogDir);
            await LaunchWatcherIfNeededAsync(autoclosePids, logDirectory: wineLogDir);

            await Task.Delay(1000);
        }

        private async Task DownloadFileWithProgressAsync(string url, string destination)
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
            Process.Start(Paths.Process, "-backgroundupdater");
        }

        private async Task<bool> ApplyModifications()
        {
            bool success = true;
            SetStatus(Strings.Bootstrapper_Status_ApplyingModifications);
            App.Logger.Info("Checking file mods...");

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
                EnsureMacResourcesBackup(contentDirectory, _latestVersionGuid);

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

                App.Logger.Info($"Begin font check using '{customFontFilename}' from '{customFontPath}' saving to '{fontFamiliesFolder}'");
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
                    var fontFamilyData = JsonSerializer.Deserialize<FontFamily>(await File.ReadAllTextAsync(jsonFilePath));
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
                        await File.WriteAllTextAsync(modFilepath, JsonSerializer.Serialize(fontFamilyData, _indentedJsonOptions));
                }
                App.Logger.Info("End font check");
            }
            else
            {
                string flatFontFamiliesFolder = Path.Combine(Paths.Modifications, "content", "fonts", "families");
                if (Directory.Exists(flatFontFamiliesFolder))
                {
                    Directory.Delete(flatFontFamiliesFolder, true);
                }
            }

            App.Logger.Info("Writing AppSettings.xml...");
            if (!File.Exists(Path.Combine(Paths.Modifications, "AppSettings.xml"))
                && (!OperatingSystem.IsLinux() || IsStudioLaunch))
            {
                Directory.CreateDirectory(_latestVersionDirectory);
                await File.WriteAllTextAsync(
                    Path.Combine(_latestVersionDirectory, "AppSettings.xml"),
                    AppSettings.Replace("roblox.com", Deployment.RobloxDomain, StringComparison.Ordinal)
                );
            }

            var allModFiles = new Dictionary<string, (string SourcePath, int Priority, string ModName, FileInfo Info)>(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(Paths.Modifications))
            {
                App.Logger.Info("Processing PresetModifications (Flat folder)...");

                foreach (string file in Directory.GetFiles(Paths.Modifications))
                {
                    string relativeFile = Path.GetFileName(file);
                    if (relativeFile == "README.txt" ||
                        relativeFile == "info.json" ||
                        relativeFile.EndsWith(".lock", StringComparison.Ordinal) ||
                        relativeFile.EndsWith(".dll", StringComparison.Ordinal) ||
                        relativeFile.EndsWith(".exe", StringComparison.Ordinal) ||
                        relativeFile.StartsWith("ClientSettings\\", StringComparison.Ordinal))
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
                            relativeFile == "info.json" ||
                            relativeFile.EndsWith(".lock", StringComparison.Ordinal) ||
                            relativeFile.EndsWith(".dll", StringComparison.Ordinal) ||
                            relativeFile.EndsWith(".exe", StringComparison.Ordinal) ||
                            relativeFile.StartsWith("ClientSettings\\", StringComparison.Ordinal))
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
                    App.Logger.Warn($"Skipping mod '{mod.FolderName}': directory not found");
                    continue;
                }

                App.Logger.Info($"Processing mod '{mod.FolderName}' (priority: {mod.Priority})");

                foreach (string file in Directory.GetFiles(modSource, "*.*", SearchOption.AllDirectories))
                {
                    string relativeFile = Path.GetRelativePath(modSource, file);

                    if (relativeFile == "README.txt" ||
                        relativeFile.EndsWith("info.json", StringComparison.Ordinal) ||
                        relativeFile.EndsWith(".lock", StringComparison.Ordinal) ||
                        relativeFile.StartsWith("ClientSettings\\", StringComparison.Ordinal))
                        continue;

                    string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(relativeFile);
                    if (fileNameWithoutExt != null && fileNameWithoutExt.EndsWith("_Delete", StringComparison.Ordinal))
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
                    if (fileNameWithoutExt != null && fileNameWithoutExt.EndsWith("_Delete", StringComparison.Ordinal))
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
                    App.Logger.Info($"{relPath} deleted via _Delete flag");

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
                                string sourceHash = await Task.Run(() => SHA256Hash.FromFile(sourceFile));
                                string targetHash = await Task.Run(() => SHA256Hash.FromFile(fileVersionFolder));

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
                            App.Logger.Info($"{relativeFile} applied");
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
                        App.Logger.Error($"Failed to apply ({relativeFile}) from mod '{modName}': {ex.Message}");
                        return false;
                    }
                    finally { semaphore.Release(); }
                }));
            }

            var fileResults = await Task.WhenAll(fileTasks);
            success = success && fileResults.All(r => r);

            if (App.Settings.Prop.UseFastFlagManager)
            {
                bool profileApplied = false;
                string source = Path.Combine(Paths.Modifications, "ClientSettings", "ClientAppSettings.json");
                string rel = Path.Combine("ClientSettings", "ClientAppSettings.json");
                string dest = Path.Combine(contentDirectory, rel);

                if (_joinData.PlaceId.HasValue && _joinData.PlaceId.Value > 0)
                {
                    profileApplied = await ApplyFastFlagsBasedOnPlaceId(_joinData.PlaceId.Value, contentDirectory);
                    if (profileApplied && File.Exists(dest))
                    {
                        var destInfo = new FileInfo(dest);
                        lock (currentModManifest)
                            currentModManifest[rel] = new ModFileEntry { Size = destInfo.Length, LastModified = destInfo.LastWriteTime };
                    }
                }

                if (!profileApplied)
                {
                    if (!OperatingSystem.IsLinux() || IsStudioLaunch)
                    {
                        if (File.Exists(source))
                        {
                            try
                            {
                                bool match = File.Exists(dest) &&
                                    (await Task.Run(() => SHA256Hash.FromFile(source)) == await Task.Run(() => SHA256Hash.FromFile(dest)));
                                if (!match)
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                                    File.Copy(source, dest, true);
                                    App.Logger.Info("FastFlags Applied (normal source).");
                                }

                                if (File.Exists(dest))
                                {
                                    var info = new FileInfo(dest);
                                    lock (currentModManifest)
                                        currentModManifest[rel] = new ModFileEntry { Size = info.Length, LastModified = info.LastWriteTime };
                                }
                            }
                            catch (Exception ex) { App.Logger.Error(ex); }
                        }
                    }
                }
            }
            else
            {
                string rel = Path.Combine("ClientSettings", "ClientAppSettings.json");
                string dest = Path.Combine(contentDirectory, rel);
                if (File.Exists(dest))
                {
                    try
                    {
                        File.Delete(dest);
                        lock (currentModManifest)
                            currentModManifest.Remove(rel);
                        App.Logger.Info("ClientSettings deleted because UseFastFlagManager is false.");
                    }
                    catch (Exception ex) { App.Logger.Error(ex); }
                }
            }

            var fileRestoreMap = new Dictionary<string, List<string>>();
            foreach (string fileLocation in AppData.DistributionState.ModManifest)
            {
                if (currentModManifest.ContainsKey(fileLocation))
                    continue;

                string actualFile = fileLocation;
                string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileLocation);

                if (fileNameWithoutExt != null && fileNameWithoutExt.EndsWith("_Delete", StringComparison.Ordinal) && OperatingSystem.IsLinux() && !IsStudioLaunch)
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
                        App.Logger.Info($"Restored '{actualFile}' from backup");
                    }
                    else
                    {
                        App.Logger.Warn($"Backup file not found: {actualFile}");
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
                        App.Logger.Info($"Deleted orphaned file {actualFile}");
                    }
                    continue;
                }

                string internalZipPath = actualFile[packageDir.Length..].TrimStart(Path.DirectorySeparatorChar);

                if (!fileRestoreMap.ContainsKey(packageName))
                    fileRestoreMap[packageName] = [];

                fileRestoreMap[packageName].Add(internalZipPath);
                App.Logger.Info($"Restoring '{internalZipPath}' from package {packageName}");
            }

            if (!OperatingSystem.IsLinux() || IsStudioLaunch)
            {
                foreach (var entry in fileRestoreMap)
                {
                    var package = _versionPackageManifest.Find(x => x.Name == entry.Key);
                    if (package is not null)
                    {
                        await DownloadPackage(package, updateProgress: false);
                        await ExtractPackage(package, entry.Value);
                    }
                }
            }

            if (App.LaunchSettings.BackgroundUpdaterFlag.Active || !AppData.DistributionStateManager.HasFileOnDiskChanged())
            {
                AppData.DistributionState.ModManifest = [.. currentModManifest.Keys];
                AppData.DistributionStateManager.Save();
            }

            App.Logger.Info("Finished checking file mods");
            return success;
        }

        private void InitializeModFolders()
        {
            if (string.IsNullOrEmpty(_latestVersionDirectory) || !Directory.Exists(_latestVersionDirectory))
            {
                App.Logger.Warn("Version directory does not exist, skipping.");
                return;
            }

            if (OperatingSystem.IsLinux() && !IsStudioLaunch)
            {
                App.Logger.Info("Skipping mod folder initialization on Linux Player (Sober).");
                return;
            }

            string contentRoot = OperatingSystem.IsMacOS()
                ? Path.Combine(_latestVersionDirectory, AppData.ExecutableName, "Contents", "Resources")
                : _latestVersionDirectory;
            if (!Directory.Exists(contentRoot))
            {
                App.Logger.Warn($"Content root not found: {contentRoot}, skipping.");
                return;
            }

            string[] topFolders = ["ExtraContent", "content", "PlatformContent"];

            string modsRoot = Paths.Modifications;
            Directory.CreateDirectory(modsRoot);

            App.Logger.Info("Initializing mod folders for ExtraContent, content, and PlatformContent...");

            foreach (string topFolder in topFolders)
            {
                string sourceFolder = Path.Combine(contentRoot, topFolder);
                if (!Directory.Exists(sourceFolder))
                {
                    App.Logger.Warn($"Top folder '{topFolder}' not found, skipping.");
                    continue;
                }

                var directories = Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories);
                var allDirs = new List<string> { topFolder };
                allDirs.AddRange(directories.Select(d => Path.GetRelativePath(contentRoot, d)));

                foreach (string relPath in allDirs)
                {
                    string target = Path.Combine(modsRoot, relPath);
                    Directory.CreateDirectory(target);
                }

                App.Logger.Info($"Mirrored {allDirs.Count} directories for '{topFolder}'.");
            }

            App.Logger.Info("Mod folder initialization complete.");
        }

        private static async Task<bool> ApplyFastFlagsBasedOnPlaceId(long placeId, string contentDirectory)
        {
            if (placeId <= 0 || !App.Settings.Prop.UseFastFlagManager)
                return false;

            App.Logger.Info($"Checking for FastFlag profile matching place ID: {placeId}");

            foreach (var kvp in App.Settings.Prop.ProfilePlaceIds)
            {
                string profileName = kvp.Key;
                List<string> placeIds = kvp.Value;

                if (placeIds.Contains(placeId.ToString(CultureInfo.InvariantCulture)))
                {
                    App.Logger.Info($"Found matching profile '{profileName}' for place ID {placeId}");

                    try
                    {
                        string profilePath = Path.Combine(Paths.SavedFlagProfiles, profileName);

                        if (!File.Exists(profilePath))
                        {
                            App.Logger.Warn($"Profile file '{profileName}' not found at {profilePath}");
                            return false;
                        }

                        string profileJson = await File.ReadAllTextAsync(profilePath);
                        var flags = JsonSerializer.Deserialize<Dictionary<string, object>>(profileJson);

                        if (flags == null || flags.Count == 0)
                        {
                            App.Logger.Warn($"Profile '{profileName}' is empty or invalid");
                            return false;
                        }

                        Directory.CreateDirectory(Path.Combine(contentDirectory, "ClientSettings"));
                        string destPath = Path.Combine(contentDirectory, "ClientSettings", "ClientAppSettings.json");

                        await File.WriteAllTextAsync(destPath, profileJson);

                        App.Logger.Info($"Successfully applied FastFlag profile '{profileName}' for place ID {placeId} ({flags.Count} flags)");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"Failed to apply FastFlag profile '{profileName}': {ex.Message}");
                        return false;
                    }
                }
            }

            App.Logger.Info($"No FastFlag profile found for place ID {placeId}");
            return false;
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
            string backupDir = GetResourcesBackupPath(versionGuid);

            if (Directory.Exists(backupDir))
            {
                App.Logger.Info($"Resources backup for version {versionGuid} already exists.");
                return;
            }

            App.Logger.Info($"Creating Resources backup for version {versionGuid}...");
            Directory.CreateDirectory(backupDir);
            CopyDirectory(resourcesDir, backupDir, true);
            App.Logger.Info("Resources backup created.");
        }

        private static string GetMacArchPath()
        {
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
                == System.Runtime.InteropServices.Architecture.Arm64
                ? "/mac/arm64"
                : "/mac";
        }

        private async Task DownloadPackage(Package package, bool updateProgress = true)
        {
            if (_cancelTokenSource.IsCancellationRequested)
                return;

            Directory.CreateDirectory(Paths.Downloads);

            string packageUrl = OperatingSystem.IsMacOS()
                ? Deployment.GetLocation($"{GetMacArchPath()}/{_latestVersionGuid}-{package.Name}")
                : Deployment.GetLocation($"/{_latestVersionGuid}-{package.Name}");
            string robloxPackageLocation = Path.Combine(Paths.LocalAppData, "Roblox", "Downloads", package.Signature);

            if (File.Exists(package.DownloadPath))
            {
                string calculatedMD5 = SHA256Hash.FromFile(package.DownloadPath);

                // Skip hash validation for macOS as the mock manifest lacks actual signature MD5s
                if (!OperatingSystem.IsMacOS() && calculatedMD5 != package.Signature)
                {
                    App.Logger.Warn($"Package is corrupted ({calculatedMD5} != {package.Signature})! Deleting and re-downloading...");
                    File.Delete(package.DownloadPath);
                }
                else
                {
                    App.Logger.Info("Package is already downloaded, skipping...");
                    if (updateProgress)
                    {
                        Interlocked.Add(ref _totalDownloadedBytes, package.PackedSize);
                        UpdateProgressBar();
                    }
                    return;
                }
            }
            else if (File.Exists(robloxPackageLocation))
            {
                // let's cheat! if the stock bootstrapper already previously downloaded the file,
                // then we can just copy the one from there

                App.Logger.Info($"Found existing copy at '{robloxPackageLocation}'! Copying to Downloads folder...");
                File.Copy(robloxPackageLocation, package.DownloadPath);
                if (updateProgress)
                {
                    _totalDownloadedBytes += package.PackedSize;
                    UpdateProgressBar();
                }
                return;
            }

            if (File.Exists(package.DownloadPath))
                return;

            App.Logger.Info("Downloading...");

            var buffer = new byte[DownloadBufferSize];

            for (int i = 1; i <= MaxDownloadAttempts; i++)
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                int totalBytesRead = 0;

                try
                {
                    var response = await App.HttpClient.GetAsync(new Uri(packageUrl), HttpCompletionOption.ResponseHeadersRead, _cancelTokenSource.Token);
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
                        UpdateProgressBar();
                    }

                    string hash = SHA256Hash.FromStream(fileStream);

                    if (!OperatingSystem.IsMacOS() && hash != package.Signature)
                        throw new ChecksumFailedException($"Failed to verify download of {packageUrl}\n\nExpected hash: {package.Signature}\nGot hash: {hash}");

                    App.Logger.Info($"Finished downloading! ({totalBytesRead} bytes total)");
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, $"An exception occurred after downloading {totalBytesRead} bytes. ({i}/{MaxDownloadAttempts})");

                    if (ex.GetType() == typeof(ChecksumFailedException))
                    {
                        await Frontend.ShowConnectivityDialog(
                            Strings.Dialog_Connectivity_UnableToDownload,
                            String.Format(CultureInfo.InvariantCulture, Strings.Dialog_Connectivity_UnableToDownloadReason, "[https://github.com/bloxstraplabs/bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox](https://github.com/bloxstraplabs/bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox)"),
                            MessageBoxImage.Error,
                            ex
                        );

                        App.Terminate(ErrorCode.ERROR_CANCELLED);
                    }
                    else if (i >= MaxDownloadAttempts)
                        throw;

                    if (File.Exists(package.DownloadPath))
                        File.Delete(package.DownloadPath);

                    _totalDownloadedBytes -= totalBytesRead;
                    UpdateProgressBar();

                    // attempt download over HTTP
                    // this isn't actually that unsafe - signatures were fetched earlier over HTTPS
                    // so we've already established that our signatures are legit, and that there's very likely no MITM anyway
                    if (ex.GetType() == typeof(IOException) && !packageUrl.StartsWith("http://", StringComparison.Ordinal))
                    {
                        App.Logger.Info("Retrying download over HTTP...");
                        packageUrl = packageUrl.Replace("https://", "http://", StringComparison.Ordinal);
                    }
                }
            }
        }

        private async Task<bool> ExtractPackage(Package package, List<string>? files = null)
        {
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
                        App.Logger.Warn($"WARNING: {package.Name} not found in package map, skipping.");
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
                                catch (Exception ex) { App.Logger.Error($"Failed to delete {fullPath}: {ex.Message}"); }
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

                    App.Logger.Info($"Extracting {package.Name} (Attempt {attempts}/{maxAttempts})...");

                    if (OperatingSystem.IsLinux() && IsStudioLaunch)
                    {
                        await ExtractZipLinux(package.DownloadPath, targetFolder, fileFilter, _cancelTokenSource.Token);
                    }
                    else
                    {
                        var fastZip = new FastZip(_fastZipEvents);
                        fastZip.ExtractZip(package.DownloadPath, targetFolder, fileFilter);
                    }

                    App.Logger.Info($"Finished extracting {package.Name}");
                    return true;
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Extraction failed on attempt {attempts}: {ex.Message}");

                    if (ex.Message.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    {
                        App.Logger.Warn($"Ignoring non‑critical extraction failure for font file.");
                        return true;
                    }

                    if (File.Exists(package.DownloadPath))
                    {
                        App.Logger.Info("Deleting corrupted package for retry...");
                        File.Delete(package.DownloadPath);
                    }

                    string? retryDir = PackageDirectoryMap.GetValueOrDefault(package.Name);
                    if (retryDir != null)
                    {
                        string retryTargetFolder = Path.Combine(_latestVersionDirectory, retryDir);
                        try
                        {
                            if (Directory.Exists(retryTargetFolder))
                                Directory.Delete(retryTargetFolder, true);
                        }
                        catch (Exception cleanupEx)
                        {
                            App.Logger.Error($"Failed to clean up partial extraction: {cleanupEx.Message}");
                        }
                    }

                    if (attempts >= maxAttempts)
                    {
                        App.Logger.Error($"Max extraction attempts reached for {package.Name}. Aborting install.");
                        throw new InvalidOperationException($"Failed to extract package {package.Name} after {maxAttempts} attempts.", ex);
                    }

                    App.Logger.Info("Retrying download...");
                    SetStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_RetryingPackage, package.Name));
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
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
}
