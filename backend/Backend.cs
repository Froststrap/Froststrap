// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

// Big ass file with all mappings

using System.Runtime.InteropServices;

namespace Froststrap.Backend;

/// A Virtual Display mechanism for macOS
internal partial class InternalVirtualDisplay
{
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
}

/// A Virtual Display mechanism for macOS
public class VirtualDisplay
{
    /// Wrapper around starting the virtual display
    public static void Start()
    {
        var result = InternalVirtualDisplay.Start();

        Console.WriteLine($"Virtual display started: {result}");
    }

    /// Checks with Rust implementation if the ring has `Some()`
    /// Which implies it's active or not
    public static bool Running()
    {
        return InternalVirtualDisplay.IsRunning() != 0;
    }

    /// Instructs to shut up the NSApplication worker thread
    public static void End()
    {
        InternalVirtualDisplay.End();
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
    public static partial int Init();
}

/// A native notifier
public class NativeNotify
{
    public static void Init()
    {
        InternalNativeNotify.Init();
    }

    public static void SendMessage(
        string title,
        string description,
        int duration = 5
    )
    {
        Task.Run(() =>
        {
            InternalNativeNotify.SendMessage(title, description, duration);
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
}

public class MobileAppReg
{
    public static void Init()
    {
        InternalMobileAppReg.Init();
    }
}

internal partial class InternalOverlay
{
    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_init")]
    public static partial int Init();

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_width")]
    public static partial int Width();

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_height")]
    public static partial int Height();

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_submit")]
    public static partial int Submit(IntPtr pixels, int width, int height, int stride);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_set_visible")]
    public static partial void SetVisible(int visible);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_shutdown")]
    public static partial void Shutdown();
}

public class LinuxOverlay
{
    public static bool Init()
    {
        return InternalOverlay.Init() == 0;
    }

    public static (int Width, int Height) Size()
    {
        return (InternalOverlay.Width(), InternalOverlay.Height());
    }

    public static bool Submit(IntPtr pixels, int width, int height, int stride)
    {
        return InternalOverlay.Submit(pixels, width, height, stride) == 0;
    }

    public static void SetVisible(bool visible)
    {
        InternalOverlay.SetVisible(visible ? 1 : 0);
    }

    public static void Shutdown()
    {
        InternalOverlay.Shutdown();
    }
}
