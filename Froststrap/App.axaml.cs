using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.Settings;
using Froststrap.UI.ViewModels;
using Froststrap.UI.ViewModels.Settings;
using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Froststrap;


/*
 * shits currently broken right now
 * - WPF-only Base Types
 * - WPF UI Libraries
 * - Global Exception Handling (we gonna have fun fixing that one)
 * - Fonts (Avalonia uses asset based font loading)
 * - Rendering & GPU Control
 * - Message Boxes (MessageBox.Avalonia comes in clutch)
 * - Taskbar Integration (Does anyone even realize we have this????)
 * - Window Enumeration & Lifetime
 * - WPF Resource System (Probably easy to fix)
 * - WPF Startup & Exit Hooks (Also probably easy to fix)
 * - Dispatcher-Based Threading (Use: Dispatcher.UIThread)
 * - Windows-Only UX APIs. Backdrop, Transparency etc. (Probably just make these Windows exclusive)
 *
 * This is just ONE file, alebit a fairly big and important one.
 */ 

public partial class App : Application
{
#if QA_BUILD
    public const string ProjectName = "Froststrap-QA";
#else
    public const string ProjectName = "Froststrap";
#endif

    public const string ProjectOwner = "RealMeddsam";
    public const string ProjectRepository = "RealMeddsam/Froststrap";
    public const string ProjectDownloadLink = "https://github.com/RealMeddsam/Froststrap/releases";
    public const string ProjectHelpLink = "https://github.com/bloxstraplabs/bloxstrap/wiki"; // We made our own wiki but its very bad and we need to rework it, so maybe change after that
    public const string ProjectSupportLink = "https://github.com/RealMeddsam/Froststrap/issues/new";
    public const string ProjectRemoteDataLink = "https://raw.githubusercontent.com/RealMeddsam/config/refs/heads/main/Data.json";

    // Windows only for now
    public const string RobloxPlayerAppName = "RobloxPlayerBeta.exe";
    public const string RobloxStudioAppName = "RobloxStudioBeta.exe";

    // one day ill add studio support
    // edit: are you sure about that?
    public const string RobloxAnselAppName = "eurotrucks2.exe";
    
    // Not yet sure whats the point of this but ok
    public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProjectName}";
    public const string ApisKey = $"Software\\{ProjectName}";

    
    public static LaunchSettings LaunchSettings { get; private set; } = null!;
    public static readonly MD5 MD5Provider = MD5.Create();
    public static readonly Logger Logger = new();
    public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();

    public static Bootstrapper? Bootstrapper { get; set; } = null!;
    public FroststrapRichPresence RichPresence { get; private set; } = null!;
    public static MemoryCleaner MemoryCleaner { get; private set; } = null!; // doubt this is necessary on Linux
    
    public static bool IsActionBuild => !String.IsNullOrEmpty(BuildMetadata.CommitRef);
    public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);
    public static bool IsPlayerInstalled => PlayerState.IsSaved && !String.IsNullOrEmpty(PlayerState.Prop.VersionGuid);
    public static bool IsStudioInstalled => StudioState.IsSaved && !String.IsNullOrEmpty(StudioState.Prop.VersionGuid);

    
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
    
    public static BuildMetadataAttribute BuildMetadata =
        Assembly.GetExecutingAssembly().GetCustomAttribute<BuildMetadataAttribute>()!;

    public static string Version =
        Assembly.GetExecutingAssembly().GetName().Version!.ToString();

    private static bool _showingExceptionDialog;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Global exception handlers (Avalonia replacement)
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }
    
    public static FroststrapRichPresence? FrostRPC
    {
        get => (Current as App)?.RichPresence;
        set
        {
            if (Current is App app)
                app.RichPresence = value!;
        }
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


				if (avaloniaBackdrop != WindowTransparencyLevel.None)
				{
                    window.Background = Brushes.Transparent;
				}
				else
				{
					window.Background = null;
				}
			}
		}
	}


	public static async Task<GithubRelease?> GetLatestRelease()
    {
        const string LOG_IDENT = "App::GetLatestRelease";

        try
        {
            var releaseInfo = await Http.GetJson<GithubRelease>($"https://api.github.com/repos/{ProjectRepository}/releases/latest");

            if (releaseInfo is null || releaseInfo.Assets is null)
            {
                Logger.WriteLine(LOG_IDENT, "Encountered invalid data");
                return null;
            }

            return releaseInfo;
        }
        catch (Exception ex)
        {
            Logger.WriteException(LOG_IDENT, ex);
        }

        return null;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        const string LOG_IDENT = "App::OnStartup";

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Logger.WriteLine(LOG_IDENT, $"Starting {ProjectName} v{Version}");

            var userAgent = new StringBuilder($"{ProjectName}/{Version}");

            if (IsActionBuild)
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from commit {BuildMetadata.CommitHash} ({BuildMetadata.CommitRef})");

                if (IsProductionBuild)
                    userAgent.Append(" (Production)");
                else
                    userAgent.Append($" (Artifact {BuildMetadata.CommitHash}, {BuildMetadata.CommitRef})");
            }
            else
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from {BuildMetadata.Machine}");

#if QA_BUILD
                userAgent.Append(" (QA)");
#else
                userAgent.Append($" (Build {Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildMetadata.Machine))})");
#endif
            }

            Logger.WriteLine(LOG_IDENT, $"OSVersion: {Environment.OSVersion}");
            Logger.WriteLine(LOG_IDENT, $"Loaded from {Paths.Process}");
            Logger.WriteLine(LOG_IDENT, $"Temp path is {Paths.Temp}");
            Logger.WriteLine(LOG_IDENT, $"WindowsStartMenu path is {Paths.WindowsStartMenu}");

            HttpClient.Timeout = TimeSpan.FromSeconds(60);

            if (!HttpClient.DefaultRequestHeaders.UserAgent.Any())
                HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent.ToString());

            LaunchSettings = new LaunchSettings(Environment.GetCommandLineArgs());

            using var uninstallKey = Registry.CurrentUser.OpenSubKey(UninstallKey);
            string? installLocation = null;
            bool fixInstallLocation = false;

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

                if (installer.CheckInstallLocation())
                {
                    Logger.WriteLine(LOG_IDENT, $"Changing install location to '{installLocation}'");
                    installer.DoInstall();
                }
                else
                {
                    installLocation = null; // force reinstall
                }
            }

            if (installLocation == null)
            {
                Logger.Initialize(true);
                Logger.WriteLine(LOG_IDENT, "Not installed, launching the installer");
                LaunchHandler.LaunchInstaller();
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
                }

                Task.Run(RemoteData.LoadData); // ok

                Settings.Load();
                State.Load();
                FastFlags.Load();
                GlobalSettings.Load();

                // to fix error System.IO.IOException: No se encuentra el recurso 'ui/style/.xaml'.
                // when i put in installer dosent work
                // if i try to fix in wpfuiwindow also dosent work
                if (Settings.Prop.Theme > Enums.Theme.Custom)
                {
                    Settings.Prop.Theme = Enums.Theme.Dark;
                    Settings.Save();
                }

                if (Settings.Prop.AllowCookieAccess)
                    Task.Run(Cookies.LoadCookies);

                if (!Locale.SupportedLocales.ContainsKey(Settings.Prop.Locale))
                {
                    Settings.Prop.Locale = "nil";
                    Settings.Save();
                }

                Logger.WriteLine(LOG_IDENT, $"Developer mode: {Settings.Prop.DeveloperMode}");
                Logger.WriteLine(LOG_IDENT, $"Web environment: {Settings.Prop.WebEnvironment}");

                Locale.Set(Settings.Prop.Locale);

                if (!LaunchSettings.BypassUpdateCheck)
                    Installer.HandleUpgrade();

                WindowsRegistry.RegisterApis();

                LaunchHandler.ProcessLaunchArgs();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            FinalizeExceptionHandling(ex);
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

        Dispatcher.UIThread.Invoke(() => Dispatcher.UIThread.BeginInvokeShutdown(exitCodeNum));
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

    public static void FinalizeExceptionHandling(Exception ex, bool log = true)
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

        Frontend.ShowExceptionDialog(ex);

        Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
    }
}
