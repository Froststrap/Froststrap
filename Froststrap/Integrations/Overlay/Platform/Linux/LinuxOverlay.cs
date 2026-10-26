using System.Runtime.InteropServices;

namespace Froststrap.Integrations.Overlay.Platform.Linux
{
    internal static class LinuxOverlay
    {
        private const string Library = "froststrap_overlay_layer";

        internal const int SurfaceGame = 0;
        internal const int SurfaceKey = 1;
        internal const int SurfaceSearch = 2;

        internal const int KindEnter = 0;
        internal const int KindLeave = 1;
        internal const int KindMotion = 2;
        internal const int KindButtonDown = 3;
        internal const int KindButtonUp = 4;
        internal const int KindAxis = 5;
        internal const int KindKeyDown = 6;
        internal const int KindKeyUp = 7;
        internal const int KindHotkey = 8;
        internal const int KindForeground = 9;
        internal const int KindText = 10;

        internal const int TextLength = 64;

        [StructLayout(LayoutKind.Sequential)]
        internal struct InputEvent
        {
            public int Kind;
            public uint Code;
            public uint Keysym;
            public float X;
            public float Y;
            public float Dx;
            public float Dy;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = TextLength)]
            public byte[] Text;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GameRect
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public int Valid;
        }

        internal static bool Initialised { get; private set; }
        internal static bool UsesNativeWindow { get; private set; }

        internal static bool Init(int processId)
        {
            if (Initialised)
                return true;

            int result = overlay_init(processId);

            if (result != 0 && result != 1)
            {
                App.Logger.Warn($"LinuxOverlay: overlay_init failed ({result})");
                return false;
            }

            Initialised = true;
            UsesNativeWindow = overlay_uses_native_window() == 1;

            App.Logger.Info($"LinuxOverlay: initialised, native={UsesNativeWindow}");

            return true;
        }

        internal static void Shutdown()
        {
            if (!Initialised)
                return;

            overlay_shutdown();
            Initialised = false;
            UsesNativeWindow = false;
        }

        internal static int PollInput(IntPtr buffer, int max) => overlay_poll_input(buffer, max);

        internal static bool TryGetGameRect(out GameRect rect)
        {
            rect = default;
            unsafe { fixed (GameRect* ptr = &rect) return overlay_get_game_rect(ptr) == 0; }
        }

        internal static int RegisterHotkey(int id, uint keysym, uint evdevKeycode, uint modifiers) =>
            overlay_register_hotkey(id, keysym, evdevKeycode, modifiers);

        internal static void UnregisterHotkey(int id) => overlay_unregister_hotkey(id);

        internal static void SetKeyMonitoring(bool enabled)
        {
            if (overlay_set_key_monitoring(enabled ? 1 : 0) != 0)
                App.Logger.Warn("LinuxOverlay: set_key_monitoring failed");
        }

        internal static bool IsForeground(int pid) => overlay_is_foreground(pid) == 1;
        internal static bool IsMinimised() => overlay_is_minimised() == 1;
        internal static bool FocusGame(int pid) => overlay_focus_game(pid) == 0;

        internal static bool FocusKnown => overlay_focus_known() != 0;

        internal static int Scale(int surface) => overlay_scale(surface);

        internal static bool SetInputRegion(int surface, int[] rects)
        {
            int result = rects.Length == 0
                ? overlay_set_input_region(surface, null, 0)
                : overlay_set_input_region(surface, rects, rects.Length / 4);

            if (result != 0)
                App.Logger.Warn($"LinuxOverlay: set_input_region({surface}) failed");

            return result == 0;
        }

        internal static bool ClearInputRegion(int surface) =>
            overlay_clear_input_region(surface) == 0;

        internal static bool SetSurfaceRect(int surface, int x, int y, int width, int height) =>
            overlay_set_surface_rect(surface, x, y, width, height) == 0;

        internal static (int Width, int Height) Size(int surface) =>
            (overlay_width(surface), overlay_height(surface));

        internal static void Submit(int surface, IntPtr buffer, int width, int height, int stride) =>
            _ = overlay_submit(surface, buffer, width, height, stride);

        internal static void SetVisible(int surface, bool visible) =>
            _ = overlay_set_visible(surface, visible ? 1 : 0);

        [DllImport(Library)] private static extern int overlay_init(int processId);
        [DllImport(Library)] private static extern void overlay_shutdown();
        [DllImport(Library)] private static extern int overlay_uses_native_window();
        [DllImport(Library)] private static extern int overlay_poll_input(IntPtr buffer, int max);
        [DllImport(Library)] private static extern unsafe int overlay_get_game_rect(GameRect* rect);
        [DllImport(Library)] private static extern int overlay_register_hotkey(int id, uint keysym, uint evdevKeycode, uint modifiers);
        [DllImport(Library)] private static extern void overlay_unregister_hotkey(int id);
        [DllImport(Library)] private static extern int overlay_set_key_monitoring(int enabled);
        [DllImport(Library)] private static extern int overlay_is_foreground(int pid);
        [DllImport(Library)] private static extern int overlay_is_minimised();
        [DllImport(Library)] private static extern int overlay_focus_known();
        [DllImport(Library)] private static extern int overlay_scale(int surface);
        [DllImport(Library)] private static extern int overlay_focus_game(int pid);
        [DllImport(Library)] private static extern int overlay_set_input_region(int surface, int[]? rects, int count);
        [DllImport(Library)] private static extern int overlay_clear_input_region(int surface);
        [DllImport(Library)] private static extern int overlay_set_surface_rect(int surface, int x, int y, int width, int height);
        [DllImport(Library)] private static extern int overlay_width(int surface);
        [DllImport(Library)] private static extern int overlay_height(int surface);
        [DllImport(Library)] private static extern int overlay_submit(int surface, IntPtr pixels, int width, int height, int stride);
        [DllImport(Library)] private static extern int overlay_set_visible(int surface, int visible);
    }
}