using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Froststrap.AppData;
using Froststrap.Integrations;
using Froststrap.UI.Elements.Base;
using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Froststrap;

internal partial class App : Application
{
    private const string MockReleaseTagEnvironmentVariable = "MOCK_RELEASE_TAG";
    private const string MockCurrentVersionEnvironmentVariable = "MOCK_CURRENT_VERSION";

#if QA_BUILD
    public const string ProjectName = "Froststrap-QA";
#else
    public const string ProjectName = "Froststrap";
#endif
    public const string ProjectOwner = "Froststrap";
    public const string ProjectRepository = "Froststrap/Froststrap";
    public const string ProjectDownloadLink = "https://github.com/Froststrap/Froststrap/releases";
    public const string ProjectHelpLink = "https://github.com/bloxstraplabs/bloxstrap/wiki";
    public const string ProjectSupportLink = "https://github.com/Froststrap/Froststrap/issues/new";
    public const string ProjectRemoteDataLink = "https://raw.githubusercontent.com/Froststrap/config/refs/heads/main/Data.json";

    public static string RobloxPlayerAppName => OperatingSystem.IsMacOS() ? "RobloxPlayer.app" : "RobloxPlayerBeta.exe";
    public static string RobloxStudioAppName => OperatingSystem.IsMacOS() ? "RobloxStudio.app" : "RobloxStudioBeta.exe";

    // simple shorthand for extremely frequently used and long string - this goes under HKCU
    public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProjectName}";

    public const string ApisKey = $"Software\\{ProjectName}";
    public static LaunchSettings LaunchSettings { get; private set; } = null!;

    public static readonly BuildMetadataAttribute BuildMetadata = Assembly.GetExecutingAssembly().GetCustomAttribute<BuildMetadataAttribute>()!;

    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString()[..^2];

    public static Bootstrapper? Bootstrapper { get; set; } = null!;

    public FroststrapRichPresence RichPresence { get; private set; } = null!;

    public static bool IsActionBuild => !String.IsNullOrEmpty(BuildMetadata.CommitRef);

    public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);

    public static string? MockReleaseTag => GetEnvironmentVariable(MockReleaseTagEnvironmentVariable);

    public static bool IsMockReleaseEnabled => !string.IsNullOrWhiteSpace(MockReleaseTag);

    public static string? MockCurrentVersion => GetEnvironmentVariable(MockCurrentVersionEnvironmentVariable);

    public static bool IsPlayerInstalled => PlayerData.IsInstalled;

    public static bool IsStudioInstalled => StudioData.IsInstalled;

    public static readonly RobloxPlayerData PlayerData = new();

    public static readonly RobloxStudioData StudioData = new();

    public static readonly SHA256 SHA256Provider = SHA256.Create();

    public static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public static readonly Dictionary<string, BaseTask> PendingSettingTasks = [];

    // Disambiguate Settings so we use the persistable Settings (Bloxstrap.Models.Persistable.Settings),
    // not the auto-generated Properties.Settings which doesn't contain the clicker fields.
    public static readonly JsonManager<Settings> Settings = new();

    public static readonly JsonManager<State> State = new();

    public static readonly AppStorageManager AppStorage = new();

    public static readonly SoberSettingsManager SoberSettings = new();

    public static readonly LazyJsonManager<DistributionState> PlayerState = new(nameof(PlayerState));

    public static readonly LazyJsonManager<DistributionState> StudioState = new(nameof(StudioState));

    public static readonly RemoteDataManager RemoteData = new();

    public static readonly FastFlagManager FastFlags = new();

    public static readonly GBSEditor GlobalSettings = new();

    public static readonly CookiesManager Cookies = new();

    public static readonly HttpClient HttpClient = new(new HttpClientLoggingHandler(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }));

    private static bool _showingExceptionDialog;
    private static readonly Lock ActivationLock = new();
    private static string? _pendingActivationUri;
    private static bool _launchArgsProcessed;

    private static string? GetEnvironmentVariable(params string[] names)
    {
        foreach (string name in names)
        {
            string? value = Environment.GetEnvironmentVariable(name);

            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private async void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) await FinalizeExceptionHandling(ex);
    }

    private async void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        await FinalizeExceptionHandling(e.Exception);
    }

    public static void Terminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
    {
        int exitCodeNum = (int)exitCode;

        Logger.Debug($"Terminating with exit code {exitCodeNum} ({exitCode})");

        Environment.Exit(exitCodeNum);
    }

    public static void SoftTerminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
    {
        int exitCodeNum = (int)exitCode;

        Logger.Debug($"Terminating with exit code {exitCodeNum} ({exitCode})");

        Dispatcher.UIThread.Invoke(() =>
        {

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown((int)exitCode);
        });
    }

    async void GlobalExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        Logger.Error($"An exception occurred: {e.Exception.Message}");

        await FinalizeExceptionHandling(e.Exception);
    }

    public static async Task FinalizeExceptionHandling(AggregateException ex)
    {
        foreach (var innerEx in ex.InnerExceptions)
            Logger.Error("Unhandled exception: ", innerEx);

        await FinalizeExceptionHandling(ex.GetBaseException(), false);
    }

    public static async Task FinalizeExceptionHandling(Exception ex, bool log = true)
    {
        if (log) Logger.Error($"Unhandled exception {ex.Message}");

        // IOException wrapping SocketException(125 = ECANCELED). This is normal shutdown, not an error.
        if (ex is IOException && ex.InnerException is System.Net.Sockets.SocketException se && se.ErrorCode == 125)
        {
            Logger.Error("Ignoring expected cancellation IOException on shutdown (ECANCELED).");
            return;
        }

        // Also swallow bare OperationCanceledException — these are always intentional cancellations.
        if (ex is OperationCanceledException)
        {
            Logger.Error("Ignoring OperationCanceledException on shutdown.");
            return;
        }

        if (_showingExceptionDialog)
            return;

        _showingExceptionDialog = true;

        if (Bootstrapper?.Dialog != null)
        {
            if (Bootstrapper.Dialog.TaskbarProgressValue == 0)
                Bootstrapper.Dialog.TaskbarProgressValue = 1; // make sure it's visible

            Bootstrapper.Dialog.TaskbarProgressState = TaskbarItemProgressState.Error;
        }

        if (IsNetworkException(ex))
        {
            await Frontend.ShowConnectivityDialog(
                Strings.Dialog_Connectivity_UnableToConnect,
                "Network Error",
                MessageBoxImage.Warning,
                ex
            );

            using var checkLock = new InterProcessLock("Bootstrapper", TimeSpan.Zero);
            if (!checkLock.IsAcquired)
            {
                Logger.Error("Bootstrapper is running, closing.");
                Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
            }
        }
        else
        {
            await Frontend.ShowExceptionDialog(ex);
            Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
        }
    }

    private static bool IsNetworkException(Exception? ex)
    {
        while (ex is not null)
        {
            if (ex is HttpRequestException ||
                ex is System.Net.Sockets.SocketException ||
                ex is WebException ||
                ex is TaskCanceledException)
            {
                return true;
            }
            ex = ex.InnerException;
        }
        return false;
    }

    /// TODO: remove this,useless function
    public static async Task AssertWindowsOSVersionAsync()
    {
        if (!OperatingSystem.IsWindows())
            return;

        int major = Environment.OSVersion.Version.Major;
        if (major < 10)
        {
            Logger.Error($"Detected unsupported Windows version ({Environment.OSVersion.Version}).");

            if (!LaunchSettings.QuietFlag.Active)
                await Frontend.ShowMessageBox(Strings.App_OSDeprecation_Win7_81, MessageBoxImage.Error);

            Terminate(ErrorCode.ERROR_INVALID_FUNCTION);
        }
    }

    public static string ExtractToTemp(string name, string fileName)
    {
        string tempFilePath = Path.Combine(Paths.Temp, fileName);

        if (!File.Exists(tempFilePath))
        {
            using var stream = Resource.GetStream(name);
            Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath)!);
            using var fileStream = File.Create(tempFilePath);
            stream.CopyTo(fileStream);
        }
        return tempFilePath;
    }

    // verifies and makes sure the registry exists
    public static async Task AssertWindowsAUMIDAsync()
    {
        if (!OperatingSystem.IsWindows()) return;
        Logger.Debug("Verifying AUMID creation");

        string iconPath = ExtractToTemp("IconFroststrap.ico", "IconFroststrap.ico");

        using var AUMIDKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\xyz.froststrap.desktop");
        using var uninstallKey = Registry.CurrentUser.OpenSubKey(UninstallKey);
        if (uninstallKey?.GetValue("InstallLocation") is string installLocValue) {
            AUMIDKey.SetValue("DisplayName", "Froststrap");
            AUMIDKey.SetValue("IconBackgroundColor", "FFDDDDDD");
            AUMIDKey.SetValue("IconUri", iconPath);
            Logger.Info("Created keys");
        } else {
            Logger.Error("Couldn't create key, uninstallKey doesnt exist.");
        }
        AUMIDKey.Close();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (OperatingSystem.IsMacOS()
            && TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatableLifetime)
        {
            activatableLifetime.Activated += OnAppActivated;
        }
    }

    private static void OnAppActivated(object? sender, ActivatedEventArgs e)
    {
        if (!OperatingSystem.IsMacOS())
            return;

        if (e is not ProtocolActivatedEventArgs { Kind: ActivationKind.OpenUri } protocolArgs)
            return;

        string uri = protocolArgs.Uri.ToString();

        lock (ActivationLock)
        {
            if (!_launchArgsProcessed)
            {
                if (LaunchSettings is not null && LaunchSettings.RobloxLaunchMode == LaunchMode.None)
                    LaunchSettings.TryResolveRobloxUri([uri]);
                else if (LaunchSettings is null)
                    _pendingActivationUri = uri;

                return;
            }
        }

        Logger.Info($"Received activation URI: {uri}");
        LaunchHandler.HandleActivationUri(uri);
    }

    public static FroststrapRichPresence? FrostRPC
    {
        get => (Current as App)?.RichPresence;
        set { if (Current is App app) app.RichPresence = value!; }
    }

    public static async Task<GithubRelease?> GetLatestRelease(bool includePreRelease = false)
    {
        try
        {
            if (IsMockReleaseEnabled)
            {
                string mockTag = MockReleaseTag ?? "v0.0.0-mock";
                Logger.Debug($"Using mocked release {mockTag}");
                return new GithubRelease
                {
                    TagName = mockTag,
                    Name = mockTag,
                    Body = "Mock release",
                    Prerelease = true,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    Assets = []
                };
            }

            string url = includePreRelease
                ? $"https://api.github.com/repos/{ProjectRepository}/releases"
                : $"https://api.github.com/repos/{ProjectRepository}/releases/latest";

            if (includePreRelease)
            {
                var releases = await Http.GetJson<List<GithubRelease>>(new Uri(url));
                if (releases is null || releases.Count == 0)
                {
                    Logger.Info("No releases found");
                    return null;
                }
                return releases[0];
            }
            else
            {
                return await Http.GetJson<GithubRelease>(new Uri(url));
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Unhandled exception {ex}");
            return null;
        }
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string? installLocation = null;

            if (OperatingSystem.IsWindows())
            {
                using var uninstallKey = Registry.CurrentUser.OpenSubKey(UninstallKey);
                if (uninstallKey?.GetValue("InstallLocation") is string installLocValue)
                {
                    if (Directory.Exists(installLocValue))
                    {
                        installLocation = installLocValue;
                    }
                    else
                    {
                        var match = Regex.Match(installLocValue, @"^[a-zA-Z]:\\Users\\([^\\]+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string newLocation = installLocValue.Replace(match.Value, Paths.UserProfile, StringComparison.InvariantCultureIgnoreCase);
                            if (Directory.Exists(newLocation))
                            {
                                installLocation = newLocation;
                            }
                        }
                    }
                }
            }

            if (installLocation == null && Directory.GetParent(Paths.Process)?.FullName is string processDir)
            {
                var files = Directory.GetFiles(processDir).Select(Path.GetFileName).ToArray();
                if (files.Length <= 3 && files.Contains("Settings.json") && files.Contains("State.json"))
                {
                    installLocation = processDir;
                }
            }

            if (installLocation == null)
            {
                installLocation = Directory.GetParent(Paths.Process)?.FullName;

                if (string.IsNullOrWhiteSpace(installLocation))
                {
                    Logger.Error("No install location could be resolved, terminating.");
                    Terminate();
                    return;
                }

                Paths.Initialize(installLocation);
                Logger.Debug($"Not installed, running in portable mode from '{installLocation}'");
            }
            else
            {
                Paths.Initialize(installLocation);
            }

            NLog.GlobalDiagnosticsContext.Set("logRoot", Paths.Logs);
            NLog.GlobalDiagnosticsContext.Set("startTime", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture));

            Logger.Debug($"Starting {ProjectName} v{Version}");
            Logger.Debug($"OS Description: {RuntimeInformation.OSDescription}");
            Logger.Debug($"OS Architecture: {RuntimeInformation.OSArchitecture}");

            var userAgent = new StringBuilder($"{ProjectName}/{Version}");

            if (IsActionBuild)
            {
                Logger.Debug($"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from commit {BuildMetadata.CommitHash} ({BuildMetadata.CommitRef})");
                userAgent.Append(IsProductionBuild ? " (Production)" : $" (Artifact {BuildMetadata.CommitHash}, {BuildMetadata.CommitRef})");
            }
            else
            {
                Logger.Debug($"Compiled {BuildMetadata.Timestamp.ToFriendlyString()}");
#if QA_BUILD
            userAgent.Append(" (QA)");
#else
                userAgent.Append(string.Format(CultureInfo.InvariantCulture, " (Build {0})", Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildMetadata.Machine))));
#endif
            }

            Logger.Debug($"Loaded from {Paths.Process}");

            HttpClient.Timeout = TimeSpan.FromSeconds(60);
            if (HttpClient.DefaultRequestHeaders.UserAgent.Count == 0)
                HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent.ToString());

            LaunchSettings = new LaunchSettings(Environment.GetCommandLineArgs());

            lock (ActivationLock)
            {
                if (LaunchSettings.RobloxLaunchMode == LaunchMode.None && _pendingActivationUri is not null)
                    LaunchSettings.TryResolveRobloxUri([_pendingActivationUri]);
            }

            if (Paths.Process != Paths.Application)
            {
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    string escapedProcessPath = Paths.Process.Replace("\"", "\\\"", StringComparison.Ordinal);
                    string launcherScript = $"#!/bin/sh\nexec \"{escapedProcessPath}\" \"$@\"\n";

                    bool needsUpdate = !File.Exists(Paths.Application)
                        || File.ReadAllText(Paths.Application) != launcherScript;

                    if (needsUpdate)
                        File.WriteAllText(Paths.Application, launcherScript);

                    Process.Start("chmod", $"+x \"{Paths.Application}\"")?.WaitForExit();
                }
                else if (!File.Exists(Paths.Application))
                {
                    File.Copy(Paths.Process, Paths.Application);
                }
            }

            _ = Task.Run(RemoteData.LoadData);
            Settings.Load();
            State.Load();
            FastFlags.Load();
            AppStorage.Load();
            GlobalSettings.Load();

            if (OperatingSystem.IsLinux())
                SoberSettings.Load();

            if (Settings.Prop.Theme > Theme.Custom)
            {
                Settings.Prop.Theme = Theme.Dark;
                Settings.Save();
            }

            AvaloniaWindow.ApplyTheme();
            Locale.Set(Settings.Prop.Locale);

            await AssertWindowsOSVersionAsync();
            await AssertWindowsAUMIDAsync();

            await Installer.RunMigrations();

            if (!LaunchSettings.BypassUpdateCheck && !OperatingSystem.IsLinux())
                await Installer.HandleUpgrade();

            if (Settings.Prop.AllowCookieAccess)
                await Task.Run(Cookies.LoadCookies);

            if (OperatingSystem.IsLinux())
            {
                LinuxRegistry.RegisterAll();
            }

            PlatformSettings?.ColorValuesChanged += (sender, args) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AvaloniaWindow.ApplyTheme();
                });
            };

            // Should we disable onboarding until it actually looks good ? cuz it lowkey looks ass rn.
            if (State.Prop.IsFirstLaunch)
            {
                LaunchSettings.OnboardingFlag.Active = true;
                Logger.Info("First launch detected, launching onboarding.");
            }

            lock (ActivationLock)
                _launchArgsProcessed = true;

            await LaunchHandler.ProcessLaunchArgs();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
