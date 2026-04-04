using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.UI;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.Settings;
using Froststrap.UI.ViewModels;
using Froststrap.UI.ViewModels.Settings;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Froststrap;

public partial class App : Application
{
    private const string MockReleaseTagEnvironmentVariable = "MOCK_RELEASE_TAG";
    private const string MockCurrentVersionEnvironmentVariable = "MOCK_CURRENT_VERSION";

    public static bool UpdateCheckCompleted { get; private set; }

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

    public const string RobloxPlayerAppName = "RobloxPlayerBeta.exe";
    public const string RobloxStudioAppName = "RobloxStudioBeta.exe";

    // simple shorthand for extremely frequently used and long string - this goes under HKCU
    public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProjectName}";

    public const string ApisKey = $"Software\\{ProjectName}";
    public static LaunchSettings LaunchSettings { get; private set; } = null!;

    public static BuildMetadataAttribute BuildMetadata = Assembly.GetExecutingAssembly().GetCustomAttribute<BuildMetadataAttribute>()!;

    public static string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString()[..^2];

    public static Bootstrapper? Bootstrapper { get; set; } = null!;

    public FroststrapRichPresence RichPresence { get; private set; } = null!;

    public static bool IsActionBuild => !String.IsNullOrEmpty(BuildMetadata.CommitRef);

    public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);

    public static string? MockReleaseTag => GetEnvironmentVariable(MockReleaseTagEnvironmentVariable);

    public static bool IsMockReleaseEnabled => !string.IsNullOrWhiteSpace(MockReleaseTag);

    public static string? MockCurrentVersion => GetEnvironmentVariable(MockCurrentVersionEnvironmentVariable);

    public static string GetUpdateCheckVersion() => MockCurrentVersion ?? Version;

    public static bool IsPlayerInstalled => PlayerState.IsSaved && !String.IsNullOrEmpty(PlayerState.Prop.VersionGuid);

    public static bool IsStudioInstalled => StudioState.IsSaved && !String.IsNullOrEmpty(StudioState.Prop.VersionGuid);

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

    public static async Task<bool> UpdateApplicationAsync()
    {
        const string LOG_IDENT = "App::UpdateApplicationAsync";

        if (UpdateCheckCompleted)
        {
            Logger.WriteLine(LOG_IDENT, "Skipping because update check already ran this session.");
            return false;
        }

        try
        {
            if (Settings.Prop.UpdateChecks == UpdateCheck.Disabled)
            {
                Logger.WriteLine(LOG_IDENT, "Update checking is disabled in settings.");
                return false;
            }

            UpdateCheck preference = Settings.Prop.UpdateChecks;
            bool includePreRelease = preference is UpdateCheck.Both or UpdateCheck.Test;

            Logger.WriteLine(LOG_IDENT, $"Checking for app updates (current={GetUpdateCheckVersion()}, prereleases={includePreRelease}, mockRelease={(IsMockReleaseEnabled ? MockReleaseTag : "<none>")}, mockCurrent={(MockCurrentVersion ?? "<none>")})");

            GithubRelease? releaseInfo = await GetLatestRelease(includePreRelease);
            if (releaseInfo is null)
                return false;

            string currentVer = GetUpdateCheckVersion();
            string releaseVer = releaseInfo.TagName;

            var versionComparison = Utilities.CompareVersions(currentVer, releaseVer);
            if (versionComparison != VersionComparison.LessThan)
            {
                Logger.WriteLine(LOG_IDENT, $"No update required. Current: {currentVer}, Latest: {releaseVer}");
                return false;
            }

            string releaseType = releaseInfo.Prerelease ? "Pre-release" : "Stable";
            Logger.WriteLine(LOG_IDENT, $"{releaseType} update available: {currentVer} -> {releaseVer}");

            var result = await Frontend.ShowMessageBox(
                $"A new {releaseType.ToLower()} version {releaseVer} is available. Would you like to update now?",
                MessageBoxImage.Question,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
            {
                Logger.WriteLine(LOG_IDENT, "User declined the update.");
                return false;
            }

            if (releaseInfo.Assets is null || releaseInfo.Assets.Count == 0)
            {
                Logger.WriteLine(LOG_IDENT, "Release found but no assets were available for download.");
                return false;
            }

            string downloadLocation;

            if (IsMockReleaseEnabled)
            {
                downloadLocation = Path.Combine(Paths.TempUpdates, Path.GetFileName(Paths.Process));
                Directory.CreateDirectory(Paths.TempUpdates);

                Logger.WriteLine(LOG_IDENT, $"Using local mock updater payload for {releaseVer}.");
                File.Copy(Paths.Process, downloadLocation, overwrite: true);
            }
            else
            {
                var asset = releaseInfo.Assets[0];
                downloadLocation = Path.Combine(Paths.TempUpdates, asset.Name);
                Directory.CreateDirectory(Paths.TempUpdates);

                Logger.WriteLine(LOG_IDENT, $"Downloading {releaseVer}...");

                if (!File.Exists(downloadLocation))
                {
                    using var response = await HttpClient.GetAsync(asset.BrowserDownloadUrl);
                    response.EnsureSuccessStatusCode();

                    await using var fileStream = new FileStream(downloadLocation, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fileStream);
                }
            }

            Logger.WriteLine(LOG_IDENT, $"Starting updater {releaseVer}...");

            var startInfo = new ProcessStartInfo(downloadLocation)
            {
                UseShellExecute = true,
            };

            startInfo.ArgumentList.Add("-upgrade");

            foreach (var arg in LaunchSettings.Args.Skip(1))
                startInfo.ArgumentList.Add(arg);

            Settings.Save();

            using var updateLock = new InterProcessLock("AutoUpdater");

            var process = Process.Start(startInfo);
            if (process is null)
            {
                var fallback = await Frontend.ShowMessageBox(
                    string.Format(Strings.Bootstrapper_AutoUpdateFailed, releaseVer),
                    MessageBoxImage.Information,
                    MessageBoxButton.YesNo);

                if (fallback == MessageBoxResult.Yes)
                    Utilities.ShellExecute(ProjectDownloadLink);

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteLine(LOG_IDENT, "An exception occurred when running the auto-updater");
            Logger.WriteException(LOG_IDENT, ex);

            var result = await Frontend.ShowMessageBox(
                Strings.Bootstrapper_AutoUpdateFailed,
                MessageBoxImage.Information,
                MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
                Utilities.ShellExecute(ProjectDownloadLink);

            return false;
        }
        finally
        {
            UpdateCheckCompleted = true;
        }
    }

    public static readonly MD5 MD5Provider = MD5.Create();

    public static readonly Logger Logger = new();

    public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();

    // Disambiguate Settings so we use the persistable Settings (Bloxstrap.Models.Persistable.Settings),
    // not the auto-generated Properties.Settings which doesn't contain the clicker fields.
    public static readonly JsonManager<Settings> Settings = new();

    public static readonly JsonManager<State> State = new();

    public static readonly AppStorageManager StorageSettings = new();

    public static readonly LazyJsonManager<DistributionState> PlayerState = new(nameof(PlayerState));

    public static readonly LazyJsonManager<DistributionState> StudioState = new(nameof(StudioState));

    public static readonly RemoteDataManager RemoteData = new();

    public static readonly FastFlagManager FastFlags = new();

    public static readonly GBSEditor GlobalSettings = new();

    public static readonly CookiesManager Cookies = new();

    public static readonly HttpClient HttpClient = new(new HttpClientLoggingHandler(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }));

    private static bool _showingExceptionDialog = false;

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

        if (_showingExceptionDialog)
            return;

        _showingExceptionDialog = true;

        if (Bootstrapper?.Dialog != null)
        {
            if (Bootstrapper.Dialog.TaskbarProgressValue == 0)
                Bootstrapper.Dialog.TaskbarProgressValue = 1; // make sure it's visible

            Bootstrapper.Dialog.TaskbarProgressState = TaskbarItemProgressState.Error;
        }

        await Frontend.ShowExceptionDialog(ex);

        Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
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
        set{ if (Current is App app) app.RichPresence = value!; }
    }

    public static void WindowsBackdrop()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ApplyBackdropToAllWindows();
        });
    }

    private static void ApplyBackdropToAllWindows()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                if (Settings.Prop.SelectedBackdrop != WindowsBackdrops.None)
                {
                    window.TransparencyLevelHint = Settings.Prop.SelectedBackdrop switch
                    {
                        WindowsBackdrops.Acrylic => new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None },
                        _ => new[] { WindowTransparencyLevel.None }
                    };

                    window.Background = Brushes.Transparent;
                }
            }
        }
    }

    public static async Task<GithubRelease?> GetLatestRelease(bool includePreRelease = false)
    {
        const string LOG_IDENT = "App::GetLatestRelease";

        try
        {
            if (includePreRelease && IsMockReleaseEnabled)
            {
                string mockTag = MockReleaseTag!;
                Logger.WriteLine(LOG_IDENT, $"Using mocked prerelease {mockTag} from environment.");

                return new GithubRelease
                {
                    TagName = mockTag,
                    Name = mockTag,
                    Body = "Local prerelease mock.",
                    Prerelease = true,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    Assets = []
                };
            }

            string url = includePreRelease ? $"https://api.github.com/repos/{ProjectRepository}/releases" : $"https://api.github.com/repos/{ProjectRepository}/releases/latest";

            if (includePreRelease)
            {
                var releases = await Http.GetJson<List<GithubRelease>>(url);

                if (releases is null || releases.Count == 0)
                {
                    Logger.WriteLine(LOG_IDENT, "No releases found in the repository.");
                    return null;
                }

                return releases[0];
            }
            else
            {
                return await Http.GetJson<GithubRelease>(url);
            }
        }
        catch (Exception ex)
        {
            Logger.WriteException(LOG_IDENT, ex);
        }

        return null;
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        const string LOG_IDENT = "App::OnStartup";

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Logger.WriteLine(LOG_IDENT, $"Starting {ProjectName} v{Version}");
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

            Logger.WriteLine(LOG_IDENT, $"OSVersion: {Environment.OSVersion}");
            Logger.WriteLine(LOG_IDENT, $"Loaded from {Paths.Process}");

            HttpClient.Timeout = TimeSpan.FromSeconds(60);
            if (!HttpClient.DefaultRequestHeaders.UserAgent.Any())
                HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent.ToString());

            LaunchSettings = new LaunchSettings(Environment.GetCommandLineArgs());

            string? installLocation = null;
            bool fixInstallLocation = false;

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
                                fixInstallLocation = true;
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
                    fixInstallLocation = true;
                }
            }

            if (fixInstallLocation && installLocation != null)
            {
                var installer = new Installer
                {
                    InstallLocation = installLocation,
                    IsImplicitInstall = true
                };

                if (await installer.CheckInstallLocation())
                {
                    Logger.WriteLine(LOG_IDENT, $"Changing install location to '{installLocation}'");
                    await installer.DoInstall();
                }
                else
                {
                    installLocation = null;
                }
            }

            if (installLocation == null)
            {
                Logger.Initialize(true);
                Logger.WriteLine(LOG_IDENT, "Not installed, launching the installer");

                await LaunchHandler.LaunchInstaller();
                return;
            }
            else
            {
                Paths.Initialize(installLocation);

                if (Paths.Process != Paths.Application && !File.Exists(Paths.Application))
                    File.Copy(Paths.Process, Paths.Application);

                Logger.Initialize(LaunchSettings.UninstallFlag.Active);

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
                StorageSettings.Load();
                GlobalSettings.Load();

                if (Settings.Prop.Theme > Theme.Custom)
                {
                    Settings.Prop.Theme = Theme.Dark;
                    Settings.Save();
                }

                AvaloniaWindow.ApplyTheme();
                Locale.Set(Settings.Prop.Locale);

                if (Settings.Prop.AllowCookieAccess)
                    await Task.Run(Cookies.LoadCookies);

                if (!LaunchSettings.BypassUpdateCheck)
                {
                    bool updateFound = await UpdateApplicationAsync();
                    if (updateFound)
                        return;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    WindowsRegistry.RegisterApis();

                LaunchHandler.ProcessLaunchArgs();
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
