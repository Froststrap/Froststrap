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

internal static unsafe partial class InternalAccountVault
{
    [LibraryImport(
        "accountvault",
        EntryPoint = "vault_seal"
    )]
    public static partial int Seal(byte* password, nuint passwordLen, byte* plain, nuint plainLen, out byte* output, out nuint outputLen);

    [LibraryImport(
        "accountvault",
        EntryPoint = "vault_open"
    )]
    public static partial int Open(byte* password, nuint passwordLen, byte* data, nuint dataLen, out byte* output, out nuint outputLen);

    [LibraryImport(
        "accountvault",
        EntryPoint = "vault_free"
    )]
    public static partial void Free(byte* data, nuint len);
}

public enum AccountVaultResult
{
    Ok = 0,
    InvalidArgument = -1,
    WrongPassword = -2,
    NotAVault = -3,
    UnsupportedVersion = -4,
    Internal = -5,
    LibraryUnavailable = -100,
}

/// Password-protected blobs (Argon2id + AES-256-GCM). Slow on purpose, so call it off the UI thread.
public static unsafe class AccountVault
{
    public static AccountVaultResult Seal(ReadOnlySpan<byte> password, ReadOnlySpan<byte> plain, out byte[] sealedData)
        => Call(password, plain, seal: true, out sealedData);

    /// The caller should wipe `plain` when done with it.
    public static AccountVaultResult Open(ReadOnlySpan<byte> password, ReadOnlySpan<byte> sealedData, out byte[] plain)
        => Call(password, sealedData, seal: false, out plain);

    private static AccountVaultResult Call(ReadOnlySpan<byte> password, ReadOnlySpan<byte> input, bool seal, out byte[] result)
    {
        result = [];

        try
        {
            byte* output = null;
            nuint outputLen = 0;
            int code;

            fixed (byte* pw = password)
            fixed (byte* data = input)
            {
                code = seal
                    ? InternalAccountVault.Seal(pw, (nuint)password.Length, data, (nuint)input.Length, out output, out outputLen)
                    : InternalAccountVault.Open(pw, (nuint)password.Length, data, (nuint)input.Length, out output, out outputLen);
            }

            if (code != 0)
                return Enum.IsDefined((AccountVaultResult)code) ? (AccountVaultResult)code : AccountVaultResult.Internal;

            try
            {
                result = new ReadOnlySpan<byte>(output, checked((int)outputLen)).ToArray();
            }
            finally
            {
                InternalAccountVault.Free(output, outputLen);
            }

            return AccountVaultResult.Ok;
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report("accountvault", ex);
            return AccountVaultResult.LibraryUnavailable;
        }
    }
}
