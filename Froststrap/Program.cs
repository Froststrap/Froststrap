using NLog;
using Avalonia;
using CommandLine;
using CommandLine.Text;
using System.Reflection;
using Froststrap.Backend;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace Froststrap;

sealed class Program
{
    /// Here for arg parser, helpful to also know all
    /// possible arguments within Froststrap.
    internal class Options
    {
#if WINDOWS
        [Option('c', "console", HelpText = "Attaches a console window for debugging.")]
        public bool AttachConsole { get; set; }
#endif
        [Option('g', "nogpu", HelpText = "Sets env AVALONIA_GPU to 0 on runtime.")]
        public bool NoGPU { get; set; }
    }

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

#if WINDOWS
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern bool AllocConsole();
#endif

    [STAThread]
    public static void Main(string[] args)
    {
        var assembly = typeof(App).Assembly;
        LogManager.Setup().LoadConfigurationFromAssemblyResource(assembly, "NLog.config");
        GlobalDiagnosticsContext.Set("logRoot", Paths.Logs);
        GlobalDiagnosticsContext.Set("startTime", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture));

        using var parser = new Parser(settings =>
        {
            settings.AutoHelp = true;
            settings.AutoVersion = true;
            settings.IgnoreUnknownArguments = true;
            settings.HelpWriter = null;
        });

        var argsResult = parser.ParseArguments<Options>(args);

        if (argsResult is NotParsed<Options> notParsed)
        {
            if (notParsed.Errors.Any(e => e.Tag == ErrorType.HelpRequestedError))
            {
                Console.WriteLine(
                    HelpText.AutoBuild(argsResult, h =>
                    {
                        h.AdditionalNewLineAfterOption = false;
                        h.Heading = "Froststrap";
                        h.Copyright = "(c) Froststrap Team";
                        return HelpText.DefaultParsingErrorsHandler(argsResult, h);
                    })
                );
                return;
            }

            if (notParsed.Errors.Any(e => e.Tag == ErrorType.VersionRequestedError))
            {
                Console.WriteLine($"Froststrap v{typeof(Program)
                        .Assembly
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                        .InformationalVersion.Split("+")[0]
                        ?? "0.0.0"}");
                return;
            }

            Logger.Warn("Arg parse failed: {0}",
            string.Join(", ", notParsed.Errors.Select(e => e.Tag)));
            Environment.Exit(1);
            return;
        }

        var opts = ((Parsed<Options>)argsResult).Value;

#if WINDOWS
        if (opts.AttachConsole) AllocConsole();
#endif
        if (opts.NoGPU) Environment.SetEnvironmentVariable("AVALONIA_GPU", "0");

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
