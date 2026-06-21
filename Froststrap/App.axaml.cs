using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Froststrap.AppData;
using Froststrap.Integrations;
using Froststrap.UI.Elements.Base;
using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Froststrap;

public partial class App : Application
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
    public const string ProjectRemoteDataLink = "https://raw.githubusercontent.com/RealMeddsam/config/refs/heads/main/Data.json";

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

    public static string GetUpdateCheckVersion() => MockCurrentVersion ?? Version;

    public static bool IsPlayerInstalled => PlayerData.IsInstalled;

    public static bool IsStudioInstalled => StudioData.IsInstalled;

    public static readonly RobloxPlayerData PlayerData = new();

    public static readonly RobloxStudioData StudioData = new();

    public static readonly MD5 MD5Provider = MD5.Create();

    public static readonly Logger Logger = new();

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

    private static bool _showingExceptionDialog = false;

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

        Logger.WriteLine("App::Terminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

        Environment.Exit(exitCodeNum);
    }

    public static void SoftTerminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
    {
        int exitCodeNum = (int)exitCode;

        Logger.WriteLine("App::SoftTerminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

        Dispatcher.UIThread.Invoke(() =>
        {

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown((int)exitCode);
        });
    }

    async void GlobalExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        Logger.WriteLine("App::GlobalExceptionHandler", "An exception occurred");

        await FinalizeExceptionHandling(e.Exception);
    }

    public static async Task FinalizeExceptionHandling(AggregateException ex)
    {
        foreach (var innerEx in ex.InnerExceptions)
            Logger.WriteException("App::FinalizeExceptionHandling", innerEx);

        await FinalizeExceptionHandling(ex.GetBaseException(), false);
    }

    public static async Task FinalizeExceptionHandling(Exception ex, bool log = true)
    {
        if (log)
            Logger.WriteException("App::FinalizeExceptionHandling", ex);

        // IOException wrapping SocketException(125 = ECANCELED). This is normal shutdown, not an error.
        if (ex is IOException && ex.InnerException is System.Net.Sockets.SocketException se && se.ErrorCode == 125)
        {
            Logger.WriteLine("App::FinalizeExceptionHandling", "Ignoring expected cancellation IOException on shutdown (ECANCELED).");
            return;
        }

        // Also swallow bare OperationCanceledException — these are always intentional cancellations.
        if (ex is OperationCanceledException)
        {
            Logger.WriteLine("App::FinalizeExceptionHandling", "Ignoring OperationCanceledException on shutdown.");
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
                "Internet",
                MessageBoxImage.Error,
                ex
            );
        }
        else
        {
            await Frontend.ShowExceptionDialog(ex);
        }

        Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
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

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static FroststrapRichPresence? FrostRPC
    {
        get => (Current as App)?.RichPresence;
        set { if (Current is App app) app.RichPresence = value!; }
    }

    public static async Task<GithubRelease?> GetLatestRelease(bool includePreRelease = false)
    {
        const string LOG_IDENT = "App::GetLatestRelease";

        try
        {
            if (IsMockReleaseEnabled)
            {
                string mockTag = MockReleaseTag ?? "v0.0.0-mock";
                Logger.WriteLine(LOG_IDENT, $"Using mocked release {mockTag}");
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
                    Logger.WriteLine(LOG_IDENT, "No releases found");
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
            Logger.WriteException(LOG_IDENT, ex);
            return null;
        }
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        const string LOG_IDENT = "App::OnStartup";

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Logger.WriteLine(LOG_IDENT, $"Starting {ProjectName} v{Version}");
            Logger.WriteLine(LOG_IDENT, $"OS Description: {RuntimeInformation.OSDescription}");
            Logger.WriteLine(LOG_IDENT, $"OS Architecture: {RuntimeInformation.OSArchitecture}");

            var userAgent = new StringBuilder($"{ProjectName}/{Version}");

            if (IsActionBuild)
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from commit {BuildMetadata.CommitHash} ({BuildMetadata.CommitRef})");
                userAgent.Append(IsProductionBuild ? " (Production)" : $" (Artifact {BuildMetadata.CommitHash}, {BuildMetadata.CommitRef})");
            }
            else
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()}");
#if QA_BUILD
                userAgent.Append(" (QA)");
#else
                userAgent.Append($" (Build {Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildMetadata.Machine))})");
#endif
            }

            Logger.WriteLine(LOG_IDENT, $"Loaded from {Paths.Process}");

            HttpClient.Timeout = TimeSpan.FromSeconds(60);
            if (HttpClient.DefaultRequestHeaders.UserAgent.Count == 0)
                HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent.ToString());

            LaunchSettings = new LaunchSettings(Environment.GetCommandLineArgs());

            string? installLocation = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
                    Logger.Initialize(true);
                    Logger.WriteLine(LOG_IDENT, "No install location could be resolved, terminating.");
                    Terminate();
                    return;
                }

                Paths.Initialize(installLocation);

                Logger.Initialize(LaunchSettings.UninstallFlag.Active);

                Logger.WriteLine(LOG_IDENT, $"Not installed, running in portable mode from '{installLocation}'");
            }
            else
            {
                Paths.Initialize(installLocation);
                Logger.Initialize(LaunchSettings.UninstallFlag.Active);
            }

            if (Paths.Process != Paths.Application)
            {
                if (OperatingSystem.IsLinux())
                {
                    string escapedProcessPath = Paths.Process.Replace("\"", "\\\"");
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

            if (!Logger.Initialized && !Logger.NoWriteMode)
            {
                Logger.WriteLine(LOG_IDENT, "Possible duplicate launch detected, terminating.");
                Terminate();
                return;
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

            await Installer.RunMigrations();

            if (!LaunchSettings.BypassUpdateCheck)
                await Installer.HandleUpgrade();

            if (Settings.Prop.AllowCookieAccess)
                await Task.Run(Cookies.LoadCookies);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsRegistry.RegisterApis();

                try
                {
                    var appUserModelId = "Froststrap.Froststrap";

                    using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + appUserModelId))
                    {
                        key.SetValue("DisplayName", "Froststrap");
                        key.SetValue("IconUri", "avares://Froststrap/Froststrap.ico");
                    }

                    Logger.WriteLine("App::OnFrameworkInitializationCompleted", "Registered app for notifications");
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("App::OnFrameworkInitializationCompleted", $"Failed to register app: {ex.Message}");
                }
            }

            PlatformSettings?.ColorValuesChanged += (sender, args) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AvaloniaWindow.ApplyTheme();
                });
            };

            LaunchHandler.ProcessLaunchArgs();
        }

        base.OnFrameworkInitializationCompleted();
    }
}