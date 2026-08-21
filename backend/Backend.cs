// Big ass file with all mappings
// Licence: MPL-2.0

using System.Runtime.InteropServices;

namespace Froststrap.Backend;

/// A native notifier
internal partial class INNotify {
	[LibraryImport(
		"backend",
		EntryPoint = "send_notification"
	)]
    public static partial int Send(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string description,
        byte[]? imageData,
        nuint imageLen
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
		// run on another thread, i don't care if it fails or not.
		Task.Run(()=>{
			INNotify.Send(title, description, imageData, imgLen);
		});
	}
}
