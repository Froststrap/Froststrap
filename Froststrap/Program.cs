// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using NLog;
using Avalonia;
using Avalonia.Media.Fonts;
using Velopack;
using Froststrap;
using Froststrap.Backend;
using Froststrap.Startup;
using Froststrap.Cli;

namespace Froststrap;

sealed partial class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    internal static readonly Stopwatch StartupTimer = Stopwatch.StartNew();

#if _WIN32
    private const int AttachParentProcess = -1;

    [System.Runtime.InteropServices.LibraryImport("kernel32.dll")]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    private static partial int AllocConsole();

    [System.Runtime.InteropServices.LibraryImport("kernel32.dll")]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    private static partial int AttachConsole(int dwProcessId);
#endif

    private static void AttachParentConsole()
    {
#if _WIN32
        _ = AttachConsole(AttachParentProcess);
#endif
    }

    [STAThread]
    public static int Main(string[] args)
    {
        AppInitializer.InitializeNativeResolvers();

        var velopackBuilder = VelopackApp.Build()
            .SetAutoApplyOnStartup(false);

        if (OperatingSystem.IsWindows())
        {
            velopackBuilder = velopackBuilder
                .OnAfterInstallFastCallback(v => UriHandler.RegisterProtocolHandlers())
                .OnAfterUpdateFastCallback(v => UriHandler.RegisterProtocolHandlers())
                .OnBeforeUninstallFastCallback(v => UriHandler.UnregisterProtocolHandlers());
        }

        velopackBuilder.Run();

        string[] launchArgs = args;

#if _WIN32
        if (launchArgs.Any(static a => a is "--console" or "-c"))
            _ = AllocConsole();
#endif

        Logging.Configure(Paths.Logs);
        _ = Task.Run(() => AppInitializer.RunMacSecurityDiagnostics());
        NativeLog.Register();

        if (launchArgs.Length == 0)
        {
            App.LaunchSettings = new LaunchSettings(new FroststrapCliCommand(), launchArgs);
        }
        else
        {
            var parseResult = DotMake.CommandLine.Cli.Parse<FroststrapCliCommand>(launchArgs);

            if (launchArgs.Any(static arg => arg is "--help" or "-h" or "--version"))
            {
                AttachParentConsole();
                int exitCode = parseResult.ParseResult.Invoke();
                LogManager.Shutdown();
                return exitCode;
            }

            if (parseResult.ParseResult.Errors.Count > 0)
            {
                AttachParentConsole();

                foreach (var parseError in parseResult.ParseResult.Errors)
                {
                    Console.Error.WriteLine(parseError.Message);
                    Logger.Error(parseError.Message);
                }

                LogManager.Shutdown();
                return (int)ErrorCode.ERROR_CANCELLED;
            }

            var cli = parseResult.Bind<FroststrapCliCommand>();
            App.LaunchSettings = new LaunchSettings(cli, launchArgs);
        }

        var launchSettings = App.LaunchSettings;

        if (launchSettings.UnknownArgs.Count > 0)
        {
            var unknown = launchSettings.UnknownArgs;
            Logger.Error($"{unknown.Count} unrecognized argument(s): {string.Join(", ", unknown)}");
            LogManager.Shutdown();
            return (int)ErrorCode.ERROR_CANCELLED;
        }

        if (launchArgs.Length != 0)
            Logger.Debug($"Arguments: {string.Join(" ", launchArgs)}");

        Logger.Debug($"Log file: {Logging.FileLocation}");

        try
        {
#if __APPLE__
            NativeNotify.Init(ProjectInfo.BundleId);
#endif
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Logger.Fatal(ex, "Unhandled exception during startup");
            throw;
        }
        finally
        {
            LogManager.Shutdown();
        }

        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
    #if DEBUG
        Logger.Warn("Debug build! Expect shit performance due to use of debuggers.");
    #endif

    return AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .ConfigureFonts(fontManager => fontManager.AddFontCollection(
            new EmbeddedFontCollection(
                new Uri("fonts:Froststrap", UriKind.Absolute),
                new Uri("avares://Froststrap/Resources/Fonts", UriKind.Absolute))))
#if DEBUG
        .WithDeveloperTools()
#endif
        .LogToTrace()
        .ConfigurePlatformRendering();
    }
}
