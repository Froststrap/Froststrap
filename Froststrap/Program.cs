using NLog;
using Avalonia;
using Froststrap.Backend;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace Froststrap;

sealed class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

#if WINDOWS
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
#endif

    [STAThread]
    public static void Main(string[] args)
    {
        ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);

#if WINDOWS
        if (args.Any(a => a is "-c" or "--console"))
            AllocConsole();
#endif

        var assembly = typeof(App).Assembly;
        LogManager.Setup().LoadConfigurationFromAssemblyResource(assembly, "NLog.config");
        GlobalDiagnosticsContext.Set("logRoot", Paths.Logs);
        GlobalDiagnosticsContext.Set("startTime", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture));

        App.LaunchSettings = new LaunchSettings(Environment.GetCommandLineArgs());

        if (App.LaunchSettings.NoGpuFlag.Active) Environment.SetEnvironmentVariable("AVALONIA_GPU", "0");

        try
        {
            Logger.Debug($"Log file: {Logging.FileLocation}");
            NativeNotify.InitRing();
            AppInitializer.InitializeNativeResolvers();
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
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();

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
        else
        {
            builder = builder.UsePlatformDetect();
        }

        return builder;
    }
}
