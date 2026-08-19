using NLog;
using Avalonia;
using CommandLine;
using Avalonia.Labs.Notifications;
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
        [Option('v', "version", HelpText = "Version number")]
        public bool Verbose { get; set; }
        [Option('g', "nogpu", HelpText = "Sets env AVALONIA_GPU to 0 on runtime")]
        public bool NoGPU { get; set; }
    }

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
            
    [STAThread]
    public static void Main(string[] args)
    {
        NLog.GlobalDiagnosticsContext.Set("startTime", DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"));

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
            bool isHelpOrVersion = notParsed.Errors.Any(e =>
            e.Tag == ErrorType.HelpRequestedError || e.Tag == ErrorType.VersionRequestedError);

            if (isHelpOrVersion)
            {
                Environment.Exit(0);
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
    public static AppBuilder BuildAvaloniaApp()
    {
        string iconPath = ExtractToTemp("IconFroststrap.ico", "IconFroststrap.ico");

        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

        if (!OperatingSystem.IsMacOS())
        {
            builder = builder.WithAppNotifications(new AppNotificationOptions
            {
                AppName = "Froststrap",
                AppUserModelId = "Icon.Froststrap",
                AppIcon = iconPath,
                DisableComServer = true
            });
        }

        return builder;
    }

    public static string ExtractToTemp(string name, string fileName)
    {
        string tempFilePath = Path.Combine(Paths.Temp, fileName);

        if (!File.Exists(tempFilePath))
        {
            using var stream = Resource.GetStream(name);
            Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath)!);
            using var fileStream = File.Create(tempFilePath);
            stream.CopyTo(fileStream);
        }
        return tempFilePath;
    }
}
