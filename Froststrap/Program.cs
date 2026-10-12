// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using NLog;
using Avalonia;
using Velopack;
using Froststrap;
using Froststrap.Backend;
using Froststrap.Cli;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace Froststrap;

sealed class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

#if WINDOWS
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
#endif

    [STAThread]
    public static int Main(string[] args)
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .OnAfterInstallFastCallback(v => UriHandler.RegisterProtocolHandlers())
            .OnAfterUpdateFastCallback(v => UriHandler.RegisterProtocolHandlers())
            .OnBeforeUninstallFastCallback(v => UriHandler.UnregisterProtocolHandlers())
            .Run();

        ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);

        // shim to convert old args to new ones
        string[] launchArgs = LegacyArgs.Normalize(args);

#if WINDOWS
        if (launchArgs.Any(static a => a is "--console" or "-c"))
            AllocConsole();
#endif

        var assembly = typeof(App).Assembly;
        LogManager.Setup().LoadConfigurationFromAssemblyResource(assembly, "NLog.config");
        GlobalDiagnosticsContext.Set("logRoot", Paths.Logs);
        GlobalDiagnosticsContext.Set("startTime", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture));

        var parseResult = DotMake.CommandLine.Cli.Parse<FroststrapCliCommand>(launchArgs);

        if (LegacyArgs.RequestsHelpOrVersion(launchArgs))
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
        var launchSettings = new LaunchSettings(cli, launchArgs);

        if (launchSettings.UnknownArgs.Count > 0)
        {
            var unknown = launchSettings.UnknownArgs;
            Logger.Error($"{unknown.Count} unrecognized argument(s): {string.Join(", ", unknown)}");
            LogManager.Shutdown();
            return (int)ErrorCode.ERROR_CANCELLED;
        }

        App.LaunchSettings = launchSettings;

        if (launchArgs.Length != 0)
            Logger.Debug($"Arguments: {string.Join(" ", launchArgs)}");

        Logger.Debug($"Log file: {Logging.FileLocation}");
        AppInitializer.InitializeNativeResolvers();

        try
        {
            NativeNotify.Init();
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

    private static void AttachParentConsole()
    {
#if WINDOWS
        AttachConsole(AttachParentProcess);
#endif
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();

        if (OperatingSystem.IsWindows() && App.LaunchSettings?.NoGpuFlag.Active == true)
        {
            builder = builder.With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.Software]
            });
        }

        if (OperatingSystem.IsLinux() &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FROSTSTRAP_FORCE_WAYLAND")))
        {
            App.Logger.Debug("Using Wayland backend (FROSTSTRAP_FORCE_WAYLAND)");

            builder = builder.UseWayland()
                .With(new WaylandPlatformOptions
                {
                    UseDmabufSwapchain = true
                });
        }

        return builder;
    }
}
