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
        EntryPoint = "init"
    )]
    public unsafe static partial int Init(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        nuint width,
        nuint height,
        double* item,
        nuint capacity
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
}

/// A Virtual Display mechanism for macOS
public class VirtualDisplay
{
    /// Intiialize datapoints around being able to
    /// create the virtual display
    public unsafe static void Initialize(
        string name,
        int width,
        int height,
        double[] rates
    )
    {                    
        fixed (double* ptr = rates)
        {
            var result = InternalVirtualDisplay.Init(
                name,
                (nuint)width,
                (nuint)height,
                ptr,
                (nuint)rates.Length
            );
            Console.WriteLine($"Virtual display initialized: {result}");
        }
    }

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
