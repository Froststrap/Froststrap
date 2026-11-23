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
        int duration = 2
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

/// The Rust mod generator
internal partial class InternalModGenerator
{
    [LibraryImport(
        "mod_generator",
        EntryPoint = "generate_mod"
    )]
    public static partial int GenerateMod(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string configJson,
        IntPtr progress
    );

    [LibraryImport(
        "mod_generator",
        EntryPoint = "zip_mod"
    )]
    public static partial int ZipMod(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string source,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string destination
    );

    [LibraryImport(
        "mod_generator",
        EntryPoint = "last_error"
    )]
    public static partial IntPtr LastError();

    [LibraryImport(
        "mod_generator",
        EntryPoint = "set_logger"
    )]
    public static partial void SetLogger(IntPtr callback);
}

/// The Rust mod generator
///
/// Calls throw `InvalidOperationException` with the generator's error message on failure
public static unsafe class ModGeneratorNative
{
    private static readonly Lock Sync = new();
    private static Action<int, ulong, ulong>? _progress;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnProgress(int stage, ulong done, ulong total) => _progress?.Invoke(stage, done, total);

    private static void ThrowIfFailed(int result, string fallback)
    {
        if (result == 0)
            return;

        string? message = result < 0 ? Marshal.PtrToStringUTF8(InternalModGenerator.LastError()) : null;
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? fallback : message);
    }

    private static void Call(Func<int> call, string fallback)
    {
        int result;
        try
        {
            result = call();
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("mod_generator", ex);
            throw new InvalidOperationException("The mod generator library couldn't be loaded.", ex);
        }
        ThrowIfFailed(result, fallback);
    }

    /// Generates a complete mod from a JSON configuration, reporting stages
    /// (1 downloading, 2 extracting, 3 recoloring, 4 finishing) to `onProgress`
    public static void Generate(string configJson, Action<int, ulong, ulong>? onProgress)
    {
        lock (Sync)
        {
            _progress = onProgress;
            try
            {
                var callback = (IntPtr)(delegate* unmanaged[Cdecl]<int, ulong, ulong, void>)&OnProgress;
                Call(() => InternalModGenerator.GenerateMod(configJson, callback), "Failed to generate the mod.");
            }
            finally
            {
                _progress = null;
            }
        }
    }

    /// Zips the contents of `source` into `destination`
    public static void Zip(string source, string destination)
        => Call(() => InternalModGenerator.ZipMod(source, destination), "Failed to zip the mod.");
}
