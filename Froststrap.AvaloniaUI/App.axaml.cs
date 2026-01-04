using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Win32;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Froststrap.AvaloniaUI.Views;
using Froststrap.AvaloniaUI.ViewModels;
using Froststrap.Integrations;

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
    public const string ProjectHelpLink = "https://github.com/bloxstraplabs/bloxstrap/wiki"; // Most likely need to make our own wiki after we finish rewrite
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
    
    public static Bootstrapper? Bootstrapper { get; set; } = null!;
    public FroststrapRichPresence RichPresence { get; private set; } = null!;
    public static MemoryCleaner MemoryCleaner { get; private set; } = null!; // doubt this is necessary on Linux
    
    public static bool IsActionBuild => !String.IsNullOrEmpty(BuildMetadata.CommitRef);
    public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);
    public static bool IsPlayerInstalled => App.PlayerState.IsSaved && !String.IsNullOrEmpty(App.PlayerState.Prop.VersionGuid);
    public static bool IsStudioInstalled => App.StudioState.IsSaved && !String.IsNullOrEmpty(App.StudioState.Prop.VersionGuid);

    
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

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            LaunchSettings = new LaunchSettings(Environment.GetCommandLineArgs());

            Logger.WriteLine("App::Startup", $"Starting {ProjectName} v{Version}");

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
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

    public static void FinalizeExceptionHandling(Exception ex)
    {
        if (_showingExceptionDialog)
            return;

        _showingExceptionDialog = true;

        Logger.WriteException("App::Fatal", ex);

        Dispatcher.UIThread.Post(() =>
        {
            // TODO: replace with MessageBox.Avalonia dialog 
            // Frontend.ShowExceptionDialog(ex);

            Environment.Exit((int)ErrorCode.ERROR_INSTALL_FAILURE);
        });
    }

    public static void Terminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
    {
        Logger.WriteLine("App::Terminate", exitCode.ToString());
        Environment.Exit((int)exitCode);
    }

    public static void SoftTerminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown((int)exitCode);
        }
        else
        {
            Environment.Exit((int)exitCode);
        }
    }
}
