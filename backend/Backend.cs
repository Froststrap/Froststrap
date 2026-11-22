// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

// Big ass file with all mappings

using System.Runtime.InteropServices;
using NLog;

namespace Froststrap.Backend;

internal static class NativeLibraryFailure
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly Lock Sync = new();
    private static readonly HashSet<string> Reported = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsLoadFailure(Exception ex) =>
        ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException
        || (ex is TypeInitializationException { InnerException: { } inner } && IsLoadFailure(inner));

    public static void Report(string library, Exception ex)
    {
        lock (Sync)
        {
            if (!Reported.Add(library))
                return;
        }

        Logger.Error(
            ex,
            "Failed to load '{Library}'. The file couln't be searched for in known resolver paths.",
            library
        );
    }
}

/// A Virtual Display mechanism for macOS
internal partial class InternalVirtualDisplay
{
    [LibraryImport(
        "virtualdisplay",
        EntryPoint = "init"
    )]
    public unsafe static partial int Init(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        nuint width,
        nuint height,
        double* item,
        nuint capacity,
        uint serial
    );
    [LibraryImport(
        "virtualdisplay",
        EntryPoint = "start_display"
    )]
    public static partial int Start();
    [LibraryImport(
        "virtualdisplay",
        EntryPoint = "is_running"
    )]
    public static partial int IsRunning();
    [LibraryImport(
        "virtualdisplay",
        EntryPoint = "end_display"
    )]
    public static partial int End();

    [LibraryImport(
        "virtualdisplay",
        EntryPoint = "set_logger"
    )]
    public static partial void SetLogger(IntPtr callback);
}

/// A Virtual Display mechanism for macOS
public class VirtualDisplay
{
    /// Initialize datapoints around being able to
    /// create the virtual display
    public unsafe static int Initialize(
        string name,
        int width,
        int height,
        double[] rates,
        uint serial
    )
    {
        try
        {
            fixed (double* ptr = rates)
            {
                var result = InternalVirtualDisplay.Init(
                    name,
                    (nuint)width,
                    (nuint)height,
                    ptr,
                    (nuint)rates.Length,
                    serial
                );
                Console.WriteLine($"Virtual display initialized: {result}");
                return result;
            }
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("virtualdisplay", ex);
            return -1;
        }
    }

    /// Wrapper around starting the virtual display
    public static int Start()
    {
        try
        {
            var result = InternalVirtualDisplay.Start();

            Console.WriteLine($"Virtual display started: {result}");
            return result;
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("virtualdisplay", ex);
            return -1;
        }
    }

    /// Checks with Rust implementation if the ring has `Some()`
    /// Which implies it's active or not
    public static bool Running()
    {
        try
        {
            return InternalVirtualDisplay.IsRunning() != 0;
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("virtualdisplay", ex);
            return false;
        }
    }

    /// Instructs to shut up the NSApplication worker thread
    public static void End()
    {
        try
        {
            InternalVirtualDisplay.End();
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("virtualdisplay", ex);
        }
    }
}

/// A native notifier
internal partial class InternalNativeNotify
{
    [LibraryImport(
        "notify",
        EntryPoint = "send_notification_message"
    )]
    public static partial int SendMessage(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string description,
        int duration
    );
    [LibraryImport(
        "notify",
        EntryPoint = "init"
    )]
    public static partial int Init(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string bundle_id
    );

    [LibraryImport(
        "notify",
        EntryPoint = "set_logger"
    )]
    public static partial void SetLogger(IntPtr callback);
}

/// A native notifier
public class NativeNotify
{
    private static string _bundleId = string.Empty;
    private static readonly Lazy<bool> Initialized = new(InitializeNative);

    private static bool InitializeNative()
    {
        try
        {
            InternalNativeNotify.Init(_bundleId);
            return true;
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("notify", ex);
            return false;
        }
    }

    public static bool Available => Initialized.Value;

    public static void Init(string bundleId)
    {
        _bundleId = bundleId;
        _ = Initialized.Value;
    }

    public static void SendMessage(
        string title,
        string description,
        int duration = 5
    )
    {
        Task.Run(() =>
        {
            if (!Initialized.Value)
                return;

            try
            {
                InternalNativeNotify.SendMessage(title, description, duration);
            }
            catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
            {
                NativeLibraryFailure.Report("notify", ex);
            }
        });
    }
}

internal partial class InternalMobileAppReg
{
    [LibraryImport(
        "mobileappreg",
        EntryPoint = "init"
    )]
    public static partial int Init();

    [LibraryImport(
        "mobileappreg",
        EntryPoint = "set_logger"
    )]
    public static partial void SetLogger(IntPtr callback);
}

public class MobileAppReg
{
    public static void Init()
    {
        try
        {
            InternalMobileAppReg.Init();
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("mobileappreg", ex);
        }
    }
}

/// Recoloring from the Rust mod generator
internal partial class InternalModGenerator
{
    [LibraryImport(
        "mod_generator",
        EntryPoint = "recolor_fonts"
    )]
    public static partial int RecolorFonts(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string color,
        int angle,
        ushort bands,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? imageMap,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? skipGlyphs
    );

    [LibraryImport(
        "mod_generator",
        EntryPoint = "recolor_images"
    )]
    public static partial int RecolorImages(
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] paths,
        nuint count,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string color,
        float angle
    );

    [LibraryImport(
        "mod_generator",
        EntryPoint = "recolor_sprite_sheets"
    )]
    public static partial int RecolorSpriteSheets(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string imageSetData,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string color,
        float angle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? customLogo,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? customSpinner
    );

    [LibraryImport(
        "mod_generator",
        EntryPoint = "set_logger"
    )]
    public static partial void SetLogger(IntPtr callback);
}

/// Recoloring from the Rust mod generator
///
/// Every call returns how many items failed, or -1 if the arguments
/// were rejected or the library couldn't load
public class ModGeneratorNative
{
    private static int Call(Func<int> call)
    {
        try
        {
            return call();
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("mod_generator", ex);
            return -1;
        }
    }

    /// Recolors every font under `path`
    public static int RecolorFonts(string path, string color, int angle, ushort bands, string? imageMap, string? skipGlyphs)
        => Call(() => InternalModGenerator.RecolorFonts(path, color, angle, bands, imageMap, skipGlyphs));

    /// Recolors the given PNGs in place
    public static int RecolorImages(string[] paths, string color, float angle)
        => Call(() => InternalModGenerator.RecolorImages(paths, (nuint)paths.Length, color, angle));

    /// Recolors the sprite sheets described by `GetImageSetData.lua`
    public static int RecolorSpriteSheets(string imageSetData, string color, float angle, string? customLogo, string? customSpinner)
        => Call(() => InternalModGenerator.RecolorSpriteSheets(imageSetData, color, angle, customLogo, customSpinner));
}
