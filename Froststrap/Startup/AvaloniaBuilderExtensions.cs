using Froststrap;
using Avalonia;
using NLog;

namespace Froststrap.Startup;

internal static class AvaloniaBuilderExtensions
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static AppBuilder ConfigurePlatformRendering(this AppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (OperatingSystem.IsMacOS())
            builder = ConfigureMacOS(builder);

        if (OperatingSystem.IsLinux())
            builder = ConfigureLinux(builder);

        if (OperatingSystem.IsWindows())
            builder = ConfigureWindows(builder);

        return builder;
    }

    private static AppBuilder ConfigureMacOS(AppBuilder builder)
    {
        return builder.With(new AvaloniaNativePlatformOptions
        {
            // Remove render with deprecated Core OpenGL, want best experience for macOS.
            // https://developer.apple.com/library/archive/documentation/GraphicsImaging/Conceptual/OpenGL-MacProgGuide/opengl_pg_concepts/opengl_pg_concepts.html
            //
            // > Important: OpenGL was deprecated in macOS 10.14.
            // > To create high-performance code on GPUs, use the Metal framework instead.
            RenderingMode = new []
            {
                AvaloniaNativeRenderingMode.Metal,
                AvaloniaNativeRenderingMode.Software
            },
        });
    }

    private static AppBuilder ConfigureLinux(AppBuilder builder)
    {
        builder = builder.With(new X11PlatformOptions
        {
            // https://docs.avaloniaui.net/api/avalonia/x11platformoptions#uid-1eb188c693
            //
            // Add Vulkan as a possible method, possibly unstable?
            RenderingMode = new[]
            {
                X11RenderingMode.Glx,
                X11RenderingMode.Vulkan,
                X11RenderingMode.Software
            },
        });

        if (!string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("FROSTSTRAP_FORCE_WAYLAND")))
        {
            Logger.Debug("Using Wayland backend (FROSTSTRAP_FORCE_WAYLAND)");

            builder = builder.UseWayland().With(new WaylandPlatformOptions
            {
                UseDmabufSwapchain = true
            });
        }

        return builder;
    }

    private static AppBuilder ConfigureWindows(AppBuilder builder)
    {
        // https://docs.avaloniaui.net/docs/platform-specific-guides/windows#rendering-mode
        //
        // All options in here might actually be nice to have available.
        // `AngleGL` is noted to only work with `WinUIComposition` and `DirectComposition`
        // which possibly is dependents of the backdrops we have, haven't tested.
        var renderingModes = App.LaunchSettings?.NoGpuFlag.Active == true
            ? new[] { Win32RenderingMode.Software }
            : new[]
            {
                Win32RenderingMode.AngleEgl,
                Win32RenderingMode.Wgl,
                Win32RenderingMode.Vulkan,
                Win32RenderingMode.Software
            };

        return builder.With(new Win32PlatformOptions
        {
            RenderingMode = renderingModes
        });
    }
}
