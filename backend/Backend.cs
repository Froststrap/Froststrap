// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

// Big ass file with all mappings

using System.Runtime.CompilerServices;
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
            "Failed to load the '{Library}' library. The file has been removed or quarantined by antivirus software." +
            "Reinstall Froststrap or add an exclusion for its installation folder. Features that depend on this library are disabled.",
            library);
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
    /// Intiialize datapoints around being able to
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
    public static partial int Init();

    [LibraryImport(
        "notify",
        EntryPoint = "set_logger"
    )]
    public static partial void SetLogger(IntPtr callback);
}

/// A native notifier
public class NativeNotify
{
    private static readonly Lazy<bool> Initialized = new(InitializeNative);

    private static bool InitializeNative()
    {
        try
        {
            InternalNativeNotify.Init();
            return true;
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("notify", ex);
            return false;
        }
    }

    public static bool Available => Initialized.Value;

    public static void Init()
    {
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

internal partial class InternalAudioPlayer
{
    [LibraryImport(
        "audioplayer",
        EntryPoint = "play_track"
    )]
    public static partial int PlayTrack(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        float volume,
        ulong id
    );

    [LibraryImport(
        "audioplayer",
        EntryPoint = "set_track_end_callback"
    )]
    public static partial void SetTrackEndCallback(IntPtr callback);

    [LibraryImport(
        "audioplayer",
        EntryPoint = "set_volume"
    )]
    public static partial void SetVolume(float volume);

    [LibraryImport(
        "audioplayer",
        EntryPoint = "stop"
    )]
    public static partial void Stop();

    [LibraryImport(
        "audioplayer",
        EntryPoint = "set_paused"
    )]
    public static partial void SetPaused(int paused);

    [LibraryImport(
        "audioplayer",
        EntryPoint = "set_logger"
    )]
    public static partial void SetLogger(IntPtr callback);

    [LibraryImport(
        "audioplayer",
        EntryPoint = "track_info_open"
    )]
    public static partial IntPtr TrackInfoOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [LibraryImport(
        "audioplayer",
        EntryPoint = "track_info_title"
    )]
    public static partial IntPtr TrackInfoTitle(IntPtr info);

    [LibraryImport(
        "audioplayer",
        EntryPoint = "track_info_artist"
    )]
    public static partial IntPtr TrackInfoArtist(IntPtr info);

    [LibraryImport(
        "audioplayer",
        EntryPoint = "track_info_album"
    )]
    public static partial IntPtr TrackInfoAlbum(IntPtr info);

    [LibraryImport(
        "audioplayer",
        EntryPoint = "track_info_duration"
    )]
    public static partial double TrackInfoDuration(IntPtr info);

    [LibraryImport(
        "audioplayer",
        EntryPoint = "track_info_art"
    )]
    public static partial IntPtr TrackInfoArt(IntPtr info, out nuint length);

    [LibraryImport(
        "audioplayer",
        EntryPoint = "track_info_free"
    )]
    public static partial void TrackInfoFree(IntPtr info);
}

public enum AudioPlayerResult
{
    Ok = 0,
    InvalidArgument = 1,
    FileNotReadable = 2,
    UnsupportedFormat = 3,
    NoOutputDevice = 4,
    LibraryUnavailable = -1
}

/// Tags, length and encoded cover image (usually JPEG or PNG, empty if none) of one file.
internal sealed record TrackInfo(string? Title, string? Artist, string? Album, TimeSpan? Duration, ReadOnlyMemory<byte> Art);

public static unsafe class AudioPlayer
{
    private static bool _initialized;

    /// Raised on the audio thread when a track ends on its own (id, failed). Keep handlers short.
    public static event Action<ulong, bool>? TrackEnded;

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnTrackEnded(ulong id, int failed)
    {
        // an exception escaping a native callback would take the whole process down
        try
        {
            TrackEnded?.Invoke(id, failed != 0);
        }
        catch (Exception ex)
        {
            try { Logger.Error(ex, "Track end handler failed"); } catch { }
        }
    }

    /// Plays a file once. `id` comes back through TrackEnded when it finishes.
    public static AudioPlayerResult PlayTrack(string path, double volume, ulong id)
    {
        try
        {
            Initialize();
            return (AudioPlayerResult)InternalAudioPlayer.PlayTrack(path, (float)volume, id);
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("audioplayer", ex);
            return AudioPlayerResult.LibraryUnavailable;
        }
    }

    public static void SetVolume(double volume)
    {
        try
        {
            InternalAudioPlayer.SetVolume((float)volume);
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("audioplayer", ex);
        }
    }

    public static void Stop()
    {
        try
        {
            InternalAudioPlayer.Stop();
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("audioplayer", ex);
        }
    }

    public static void SetPaused(bool paused)
    {
        try
        {
            InternalAudioPlayer.SetPaused(paused ? 1 : 0);
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("audioplayer", ex);
        }
    }

    /// Null if the file can't be read or the library is missing. Reads the whole cover, so call it off the UI thread.
    internal static TrackInfo? ReadTrackInfo(string path)
    {
        IntPtr info = IntPtr.Zero;

        try
        {
            Initialize();

            info = InternalAudioPlayer.TrackInfoOpen(path);
            if (info == IntPtr.Zero)
                return null;

            double seconds = InternalAudioPlayer.TrackInfoDuration(info);
            IntPtr art = InternalAudioPlayer.TrackInfoArt(info, out nuint length);

            return new TrackInfo(
                Marshal.PtrToStringUTF8(InternalAudioPlayer.TrackInfoTitle(info)),
                Marshal.PtrToStringUTF8(InternalAudioPlayer.TrackInfoArtist(info)),
                Marshal.PtrToStringUTF8(InternalAudioPlayer.TrackInfoAlbum(info)),
                seconds >= 0 ? TimeSpan.FromSeconds(seconds) : null,
                art == IntPtr.Zero || length == 0 ? ReadOnlyMemory<byte>.Empty : new ReadOnlySpan<byte>((void*)art, checked((int)length)).ToArray());
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("audioplayer", ex);
            return null;
        }
        finally
        {
            if (info != IntPtr.Zero)
                InternalAudioPlayer.TrackInfoFree(info);
        }
    }

    // loaded on first use so it isn't touched while music is off
    private static void Initialize()
    {
        if (_initialized)
            return;

        InternalAudioPlayer.SetLogger(NativeLog.Callback);
        InternalAudioPlayer.SetTrackEndCallback(
            (IntPtr)(delegate* unmanaged[Cdecl]<ulong, int, void>)&OnTrackEnded);
        _initialized = true;
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
