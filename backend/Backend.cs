// Big ass file with all mappings
// Licence: MPL-2.0

using System.Runtime.InteropServices;

namespace Froststrap.Backend;

/// A native notifier
internal partial class INNotify
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
    public static partial int SetApplication();
}

/// A native notifier
public class NNotify
{
    private static readonly Lazy<int> _appInit = new(() => INNotify.SetApplication());

    public static void SendMessage(
        string title,
        string description,
        int duration = 5
    )
    {
        Task.Run(() =>
        {
            _ = _appInit.Value;
            INNotify.SendMessage(title, description, duration);
        });
    }
}
