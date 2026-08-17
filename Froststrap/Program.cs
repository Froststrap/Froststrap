using Avalonia;
using Avalonia.Wayland;

namespace Froststrap;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    public static bool NoGPU { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        NoGPU = args.Any(a => a.Equals("-nogpu", StringComparison.OrdinalIgnoreCase));

        if (NoGPU)
        {
            Environment.SetEnvironmentVariable("AVALONIA_GPU", "0");
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        string iconPath = ExtractToTemp("IconFroststrap.ico", "IconFroststrap.ico");

        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

        if (OperatingSystem.IsLinux() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
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
