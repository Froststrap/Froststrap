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
using ReactiveUI;
using System;
using System.Reactive.Concurrency;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Froststrap;

public partial class App : Application
{
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

    public static bool IsPlayerInstalled => App.PlayerState.IsSaved && !String.IsNullOrEmpty(App.PlayerState.Prop.VersionGuid);

    public static bool IsStudioInstalled => App.StudioState.IsSaved && !String.IsNullOrEmpty(App.StudioState.Prop.VersionGuid);

    public static readonly MD5 MD5Provider = MD5.Create();

    public static readonly Logger Logger = new();

    public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();

    // Disambiguate Settings so we use the persistable Settings (Bloxstrap.Models.Persistable.Settings),
    // not the auto-generated Properties.Settings which doesn't contain the clicker fields.
    public static readonly JsonManager<Settings> Settings = new();

    public static readonly JsonManager<State> State = new();

    public static readonly LazyJsonManager<DistributionState> PlayerState = new(nameof(PlayerState));

    public static readonly LazyJsonManager<DistributionState> StudioState = new(nameof(StudioState));

    public static readonly RemoteDataManager RemoteData = new();

    public static readonly FastFlagManager FastFlags = new();

    public static readonly GBSEditor GlobalSettings = new();

    public static readonly CookiesManager Cookies = new();

    public static readonly HttpClient HttpClient = new(new HttpClientLoggingHandler(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }));


    private static bool _showingExceptionDialog = false;

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) FinalizeExceptionHandling(ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        FinalizeExceptionHandling(e.Exception);
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

    void GlobalExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        Logger.WriteLine("App::GlobalExceptionHandler", "An exception occurred");

        FinalizeExceptionHandling(e.Exception);
    }

    public static void FinalizeExceptionHandling(AggregateException ex)
    {
        foreach (var innerEx in ex.InnerExceptions)
            Logger.WriteException("App::FinalizeExceptionHandling", innerEx);

        FinalizeExceptionHandling(ex.GetBaseException(), false);
    }

    public static async void FinalizeExceptionHandling(Exception ex, bool log = true)
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

        RxApp.MainThreadScheduler = new SynchronizationContextScheduler(SynchronizationContext.Current!);

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
            var backdropType = Settings.Prop.SelectedBackdrop;
            ApplyBackdropToAllWindows(backdropType);
        });
    }

    private static void ApplyBackdropToAllWindows(WindowsBackdrops backdropType)
    {
        var avaloniaBackdrop = backdropType switch
        {
            WindowsBackdrops.None => WindowTransparencyLevel.None,
            WindowsBackdrops.Mica => WindowTransparencyLevel.Mica,
            WindowsBackdrops.Acrylic => WindowTransparencyLevel.AcrylicBlur,
            WindowsBackdrops.Aero => WindowTransparencyLevel.Blur,
            _ => WindowTransparencyLevel.None
        };

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                window.TransparencyLevelHint = new[] { avaloniaBackdrop };
                window.Background = avaloniaBackdrop != WindowTransparencyLevel.None ? Brushes.Transparent : null;
            }
        }
    }

    public static async Task<GithubRelease?> GetLatestRelease(bool includePreRelease = false)
    {
        const string LOG_IDENT = "App::GetLatestRelease";

        try
        {
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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
                installLocation = key?.GetValue("InstallLocation") as string;
            }

            installLocation ??= AppContext.BaseDirectory;

            Paths.Initialize(installLocation);
            Logger.Initialize(LaunchSettings.UninstallFlag.Active);

            if (!Logger.Initialized && !Logger.NoWriteMode)
            {
                Logger.WriteLine(LOG_IDENT, "Possible duplicate launch detected, terminating.");
                Terminate();
            }

            _ = Task.Run(RemoteData.LoadData);
            await Settings.Load();
            await State.Load();
            await FastFlags.Load();
            GlobalSettings.Load();

            if (Settings.Prop.Theme > Theme.Custom)
            {
                Settings.Prop.Theme = Theme.Dark;
                Settings.Save();
            }

            AvaloniaWindow.ApplyGlobalTheme();

            Locale.Set(Settings.Prop.Locale);

            if (Settings.Prop.AllowCookieAccess)  
                await Task.Run(Cookies.LoadCookies);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                WindowsRegistry.RegisterApis();

            LaunchHandler.ProcessLaunchArgs();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
