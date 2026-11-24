// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using NLog;
using Avalonia;
using Avalonia.Media;
using Froststrap;
using Froststrap.Backend;
using Froststrap.Startup;
using Froststrap.Cli;

namespace Froststrap;

sealed class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    internal static readonly Stopwatch StartupTimer = Stopwatch.StartNew();

    [STAThread]
    public static int Main(string[] args)
    {
        AppInitializer.InitializeNativeResolvers();
        VelopackSetup.Run();

        var cli = CliInvocation.Parse(args);

        if (cli.ShowConsole)
            WindowsConsole.Allocate();

        Logging.Configure(Paths.Logs);
#if DEBUG
        Logger.Warn("Debug build! Expect shit performance due to use of debuggers.");
#endif
        _ = Task.Run(() => AppInitializer.RunMacSecurityDiagnostics());
        NativeLog.Register();

        if (!cli.TryCreateLaunchSettings(out var launchSettings, out int exitCode))
        {
            LogManager.Shutdown();
            return exitCode;
        }

        App.LaunchSettings = launchSettings;

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
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace()
            .ConfigurePlatformRendering();
}
