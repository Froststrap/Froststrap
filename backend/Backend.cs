// Big ass file with all mappings
// Licence: MPL-2.0

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
        EntryPoint = "end_display"
    )]
    public static partial int End();
}

/// A Virtual Display mechanism for macOS
public class VirtualDisplay
{
    /// Wrapper around starting the virtual display
    public static void Start() {
        var result = InternalVirtualDisplay.Start();

        Console.WriteLine($"Virtual display started: {result}");
    }

    /// Instructs the Swift ABI to shut up the NSApplication worker thread
    public static void End() {
        InternalVirtualDisplay.End();
    }
}

/// A native notifier
internal partial class InternalNativeNotify
{
    [LibraryImport(
        "rbackend",
        EntryPoint = "send_notification_message"
    )]
    public static partial int SendMessage(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string description,
        int duration
    );
    [LibraryImport(
        "rbackend",
        EntryPoint = "set_application"
    )]
    public static partial int SetApplication(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string bundleIdentifier
    );
}

/// A native notifier
public class NativeNotify
{
    public static void InitRing() {
        InternalNativeNotify.SetApplication("xyz.froststrap.desktop");    
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
