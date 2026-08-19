using NLog;
using Avalonia;
using Avalonia.Labs.Notifications;
using System.Runtime.InteropServices;

namespace Froststrap;

sealed class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    public static bool NoGPU { get; private set; }

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
            
    [STAThread]
    public static void Main(string[] args)
    {
        NLog.GlobalDiagnosticsContext.Set("startTime", DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"));
        if (args.Any(a => a.Equals("-attachConsole", StringComparison.OrdinalIgnoreCase)) && OperatingSystem.IsWindows()) {
            AllocConsole();
        }
        Logger.Debug($"Log file: {Logging.FileLocation}");

        NoGPU = args.Any(a => a.Equals("-nogpu", StringComparison.OrdinalIgnoreCase));

        if (NoGPU)
        {
            Environment.SetEnvironmentVariable("AVALONIA_GPU", "0");
        }

        try
        {
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
