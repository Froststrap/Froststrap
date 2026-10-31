// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NLog;

namespace Froststrap.Backend;

/// Routes log records from the native libraries into the host's NLog pipeline.
public static unsafe class NativeLog
{
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnLog(int level, byte* target, byte* message)
    {
        var loggerName = Marshal.PtrToStringUTF8((IntPtr)target) ?? "native";
        var logger = LogManager.GetLogger(loggerName);
        var logLevel = level switch
        {
            1 => LogLevel.Error,
            2 => LogLevel.Warn,
            3 => LogLevel.Info,
            4 => LogLevel.Debug,
            _ => LogLevel.Trace,
        };

        var logEvent = new LogEventInfo(
            logLevel,
            loggerName,
            Marshal.PtrToStringUTF8((IntPtr)message) ?? string.Empty
        );
        logEvent.Properties["RustCallsite"] = loggerName;

        logger.Log(logEvent);
    }

    /// Installs the callback in every native library that supports logging.
    /// Must run after NLog has been configured.
    internal static IntPtr Callback =>
        (IntPtr)(delegate* unmanaged[Cdecl]<int, byte*, byte*, void>)&OnLog;

    public static void Register()
    {
        var callback = Callback;

        SetLogger("notify", InternalNativeNotify.SetLogger, callback);

        if (OperatingSystem.IsMacOS())
        {
            SetLogger("virtualdisplay", InternalVirtualDisplay.SetLogger, callback);
            SetLogger("mobileappreg", InternalMobileAppReg.SetLogger, callback);
        }
    }

    private static void SetLogger(string library, Action<IntPtr> setLogger, IntPtr callback)
    {
        try
        {
            setLogger(callback);
        }
        catch (Exception ex) when (NativeLibraryFailure.IsLoadFailure(ex))
        {
            NativeLibraryFailure.Report(library, ex);
        }
    }
}
