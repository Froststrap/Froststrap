using NLog;
using Avalonia;
using CommandLine;
using CommandLine.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Froststrap;

sealed class Program
{
    /// Here for arg parser, helpful to also know all
    /// possible arguments within Froststrap.
    public class Options
    {
        [Option('c', "console", HelpText = "Attaches a console window for debugging.")]
        public bool AttachConsole { get; set; }
        [Option('g', "nogpu", HelpText = "Sets env AVALONIA_GPU to 0 on runtime.")]
        public bool NoGPU { get; set; }
    }

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [STAThread]
    public static void Main(string[] args)
    {
        GlobalDiagnosticsContext.Set("logRoot", Paths.Logs);
        GlobalDiagnosticsContext.Set("startTime", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));

        var parser = new Parser(settings =>
        {
            settings.AutoHelp = true;
            settings.AutoVersion = true;
            settings.IgnoreUnknownArguments = true;
            settings.HelpWriter = null;
        });

        var argsResult = parser.ParseArguments<Options>(args);

        if (argsResult is NotParsed<Options> notParsed)
        {
            if (notParsed.Errors.Any(e=> e.Tag == ErrorType.HelpRequestedError))
            {
                Console.WriteLine(
                    HelpText.AutoBuild(argsResult, h => {
                        h.AdditionalNewLineAfterOption = false;
                        h.Heading = "Froststrap";
                        h.Copyright = "(c) Froststrap Team";
                        return HelpText.DefaultParsingErrorsHandler(argsResult, h);
                    })
                );
                return;
            }

            if (notParsed.Errors.Any(e=> e.Tag == ErrorType.VersionRequestedError))
            {
                Console.WriteLine($"Froststrap v{
                    typeof(Program)
                        .Assembly
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                        .InformationalVersion.Split("+")[0]
                        ?? "0.0.0"
                }");
                return;
            }

            Logger.Warn("Arg parse failed: {0}",
            string.Join(", ", notParsed.Errors.Select(e => e.Tag)));
            Environment.Exit(1);
            return;
        }

        var opts = ((Parsed<Options>)argsResult).Value;

        if (opts.AttachConsole) AllocConsole();
        if (opts.NoGPU) Environment.SetEnvironmentVariable("AVALONIA_GPU", "0");

        try
        {
            Logger.Debug($"Log file: {Logging.FileLocation}");
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