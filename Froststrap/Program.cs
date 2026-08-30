using NLog;
using Avalonia;
using System.CommandLine;
using System.Reflection;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace Froststrap;

sealed class Program
{
    /// Here for arg parser, helpful to also know all
    /// possible arguments within Froststrap.
    public class Options
    {
#if WINDOWS
        public bool AttachConsole { get; set; }
#endif
        public bool NoGPU { get; set; }
    }

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

#if WINDOWS
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
#endif

    [STAThread]
    public static int Main(string[] args)
    {
        var assembly = typeof(App).Assembly;
        LogManager.Setup().LoadConfigurationFromAssemblyResource(assembly, "NLog.config");
        GlobalDiagnosticsContext.Set("logRoot", Paths.Logs);
        GlobalDiagnosticsContext.Set("startTime", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));

        var noGpuOption = new Option<bool>("--nogpu")
        {
            Description = "Sets env AVALONIA_GPU to 0 on runtime."
        };
        noGpuOption.Aliases.Add("-g");

#if WINDOWS
        var consoleOption = new Option<bool>("--console")
        {
            Description = "Attaches a console window for debugging."
        };
        consoleOption.Aliases.Add("-c");
#endif

        var rootCommand = new RootCommand("Froststrap");
        rootCommand.Options.Add(noGpuOption);
#if WINDOWS
        rootCommand.Options.Add(consoleOption);
#endif

        rootCommand.SetAction(parseResult =>
        {
            var opts = new Options
            {
                NoGPU = parseResult.GetValue(noGpuOption),
#if WINDOWS
                AttachConsole = parseResult.GetValue(consoleOption)
#endif
            };

#if WINDOWS
            if (opts.AttachConsole) AllocConsole();
#endif
            if (opts.NoGPU) Environment.SetEnvironmentVariable("AVALONIA_GPU", "0");

            try
            {
                Logger.Debug($"Log file: {Logging.FileLocation}");
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
        });

        return rootCommand.Parse(args).Invoke();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    // TODO: Strip out notification config, and do it all in Rust-side.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

        /*// We won't enable Wayland by default until its merged into Avalonia upstream
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
        }*/

        builder = builder.UsePlatformDetect();

        return builder;
    }
}
