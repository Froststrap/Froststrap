// Big ass file with all mappings
// Licence: MPL-2.0

using System.Runtime.InteropServices;

namespace Froststrap.Backend;

/// A native notifier
internal partial class INNotify {
	[LibraryImport(
		"rbackend",
		EntryPoint = "send_notification"
	)]
    public static partial int Send(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string description,
        byte[]? imageData,
        nuint imageLen
    );
	[LibraryImport(
		"rbackend",
		EntryPoint = "send_notification_message"
	)]
    public static partial int SendMessage(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string description,
		int duration
    );
}

/// A native notifier
public class NNotify {
	public static void Send(
		string title,
		string description,
		byte[]? imageData,
		nuint imgLen
	) {
		// Run on another thread, I don't care if it fails or not.
		Task.Run(()=>{
			INNotify.Send(title, description, imageData, imgLen);
		});
	}
	public static void SendMessage(
		string title,
		string description,
		int duration = 5
	) {
		// Run on another thread, I don't care if it fails or not.
		Task.Run(()=>{
			INNotify.SendMessage(title, description, duration);
		});
	}
}
