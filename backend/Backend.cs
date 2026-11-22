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

internal partial class InternalOverlay
{
    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_init")]
    public static partial int Init();

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_shutdown")]
    public static partial void Shutdown();

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_create_surface")]
    public static partial int CreateSurface(int tracked);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_destroy_surface")]
    public static partial void DestroySurface(int id);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_set_visible")]
    public static partial void SetVisible(int id, int visible);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_set_rect")]
    public static partial void SetRect(int id, int x, int y, int width, int height);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_set_keyboard")]
    public static partial void SetKeyboard(int id, int mode);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_set_regions")]
    public static partial int SetRegions(int id, [In] int[] rects, int count);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_submit")]
    public static partial int Submit(int id, IntPtr pixels, int width, int height, int stride);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_game_state")]
    public static partial int GameState(out LinuxOverlay.GameState state);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_focus_game")]
    public static partial int FocusGame();

    [LibraryImport(
        "froststrap_overlay_layer",
        EntryPoint = "set_logger"
    )]
    public static partial void SetLogger(IntPtr callback);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_poll_input")]
    public static partial int PollInput(IntPtr events, int max);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_hotkey_register")]
    public static partial int HotkeyRegister(int id, uint code, uint modifiers);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_hotkey_unregister")]
    public static partial void HotkeyUnregister(int id);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_poll_hotkey")]
    public static partial int PollHotkey();

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_key_state")]
    public static partial int KeyState(IntPtr state);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_portal_supported")]
    public static partial int PortalSupported();

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_portal_register")]
    public static partial int PortalRegister(
        int id,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string shortcutId,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string description,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string trigger);

    [LibraryImport("froststrap_overlay_layer", EntryPoint = "overlay_portal_unregister")]
    public static partial int PortalUnregister(int id);
}

public class LinuxOverlay
{
    public const uint GamePresent = 1;
    public const uint GameVisible = 2;
    public const uint GameForeground = 4;
    public const uint GameMinimised = 8;
    public const uint GameMaximised = 16;

    [StructLayout(LayoutKind.Sequential)]
    public struct GameState
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InputEvent
    {
        public int Surface;
        public int Kind;
        public uint Code;
        public uint Modifiers;
        public float X;
        public float Y;
        public float Dx;
        public float Dy;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Text;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyState
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)]
        public byte[] Keys;

        public uint Mouse;
        public float LeftCps;
        public float RightCps;
    }

    public static int KeyStateSize => Marshal.SizeOf<KeyState>();

    public static bool Init() => InternalOverlay.Init() == 0;

    public static void Shutdown() => InternalOverlay.Shutdown();

    public static int CreateSurface(bool tracked) => InternalOverlay.CreateSurface(tracked ? 1 : 0);

    public static void DestroySurface(int id) => InternalOverlay.DestroySurface(id);

    public static void SetVisible(int id, bool visible) => InternalOverlay.SetVisible(id, visible ? 1 : 0);

    public static void SetRect(int id, int x, int y, int width, int height) =>
        InternalOverlay.SetRect(id, x, y, width, height);

    public static void SetKeyboard(int id, int mode) => InternalOverlay.SetKeyboard(id, mode);

    public static bool SetRegions(int id, int[] rects) =>
        InternalOverlay.SetRegions(id, rects, rects.Length / 4) == 0;

    public static bool Submit(int id, IntPtr pixels, int width, int height, int stride) =>
        InternalOverlay.Submit(id, pixels, width, height, stride) == 0;

    public static bool GetGameState(out GameState state) => InternalOverlay.GameState(out state) == 0;

    public static bool FocusGame() => InternalOverlay.FocusGame() != 0;

    public static int PollInput(IntPtr events, int max) => InternalOverlay.PollInput(events, max);

    public static bool RegisterHotkey(int id, uint code, uint modifiers) =>
        InternalOverlay.HotkeyRegister(id, code, modifiers) == 0;

    public static void UnregisterHotkey(int id) => InternalOverlay.HotkeyUnregister(id);

    public static int PollHotkey() => InternalOverlay.PollHotkey();

    public static bool GetKeyState(IntPtr state) => InternalOverlay.KeyState(state) == 0;

    public static bool PortalSupported() => InternalOverlay.PortalSupported() != 0;

    public static bool PortalRegister(int id, string shortcutId, string description, string trigger) =>
        InternalOverlay.PortalRegister(id, shortcutId, description, trigger) == 0;

    public static bool PortalUnregister(int id) => InternalOverlay.PortalUnregister(id) == 0;
}