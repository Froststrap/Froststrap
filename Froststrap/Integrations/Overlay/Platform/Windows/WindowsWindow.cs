// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Froststrap.Models.Overlay;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Froststrap.Integrations.Overlay.Platform.Windows
{
    internal sealed class WindowsWindow
    {
        internal enum WindowKind
        {
            Game,
            KeyOverlay,
            SearchBar
        }

        private const int WM_HOTKEY = 0x0312;

        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_TRANSPARENT = 0x00000020;
        private const uint WS_EX_LAYERED = 0x00080000;
        private const uint WS_EX_NOACTIVATE = 0x08000000;

        private readonly Window _window;
        private readonly Action<int> _hotkeyPressed;
        private readonly HashSet<int> _hotkeys = [];

        private readonly Win32Properties.CustomWndProcHookCallback _wndProc;
        private Win32Properties.CustomWindowStylesCallback? _styles;

        private WindowKind _kind;
        private bool _clickThrough;
        private bool _cloaked;

        private OverlayRegion _lastClip;
        private Occluder[] _lastOccluders = [];
        private bool _hasRegionState;

        public HWND Handle { get; private set; }

        public WindowsWindow(Window window, Action<int> hotkeyPressed)
        {
            _window = window;
            _hotkeyPressed = hotkeyPressed;
            _wndProc = OnMessage;
        }

        public bool Attach(WindowKind kind)
        {
            _kind = kind;

            var handle = _window.TryGetPlatformHandle();

            if (handle is null)
                return false;

            Handle = new HWND(handle.Handle);

            ApplyStyles();

            Win32Properties.AddWndProcHookCallback(_window, _wndProc);

            if (kind == WindowKind.KeyOverlay)
            {
                _clickThrough = true;
                _styles = OnStyles;

                Win32Properties.AddWindowStylesCallback(_window, _styles);

                ApplyStyles();
            }

            return true;
        }

        public void Release()
        {
            if (Handle == HWND.Null)
                return;

            foreach (int id in _hotkeys)
                WindowsHotkeys.Unregister(Handle, id);

            _hotkeys.Clear();

            Win32Properties.RemoveWndProcHookCallback(_window, _wndProc);

            if (_styles is not null)
            {
                Win32Properties.RemoveWindowStylesCallback(_window, _styles);
                _styles = null;
            }

            ClearRegion();

            Handle = HWND.Null;
        }

        public void SetClickThrough(bool clickThrough)
        {
            if (_kind != WindowKind.KeyOverlay || _clickThrough == clickThrough)
                return;

            _clickThrough = clickThrough;

            ApplyStyles();
        }

        public void SetCloaked(bool cloaked)
        {
            if (Handle == HWND.Null || _cloaked == cloaked)
                return;

            _cloaked = cloaked;

            unsafe
            {
                int value = cloaked ? 1 : 0;

                _ = PInvoke.DwmSetWindowAttribute(Handle, DWMWINDOWATTRIBUTE.DWMWA_CLOAK, &value, sizeof(int));
            }
        }

        public bool RegisterHotkey(
            int id,
            KeyModifiers modifiers,
            Key key,
            KeyModifiers defaultModifiers,
            Key defaultKey,
            string description)
        {
            if (Handle == HWND.Null)
                return false;

            bool registered = WindowsHotkeys.Register(
                Handle,
                id,
                modifiers,
                key,
                defaultModifiers,
                defaultKey,
                description);

            if (registered)
                _hotkeys.Add(id);
            else
                _hotkeys.Remove(id);

            return registered;
        }

        public void UnregisterHotkey(int id)
        {
            if (Handle != HWND.Null)
                WindowsHotkeys.Unregister(Handle, id);

            _hotkeys.Remove(id);
        }

        public bool IsForeground() =>
            Handle != HWND.Null && PInvoke.GetForegroundWindow() == Handle;

        public static bool IsOwnProcessForeground()
        {
            uint processId = 0;

            unsafe
            {
                _ = PInvoke.GetWindowThreadProcessId(PInvoke.GetForegroundWindow(), &processId);
            }

            return processId == Environment.ProcessId;
        }

        public void Raise()
        {
            if (Handle == HWND.Null)
                return;

            _ = PInvoke.SetWindowPos(
                Handle,
                HWND.Null,
                0, 0, 0, 0,
                SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
                SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }

        public void SetRegion(IReadOnlyList<OverlayRegion> regions)
        {
            if (Handle == HWND.Null)
                return;

            if (regions.Count == 0)
            {
                ClearRegion();
                return;
            }

            HRGN combined = PInvoke.CreateRectRgn(
                regions[0].Left,
                regions[0].Top,
                regions[0].Right,
                regions[0].Bottom);

            for (int i = 1; i < regions.Count; i++)
            {
                OverlayRegion region = regions[i];
                HRGN rect = PInvoke.CreateRectRgn(region.Left, region.Top, region.Right, region.Bottom);

                _ = PInvoke.CombineRgn(combined, combined, rect, RGN_COMBINE_MODE.RGN_OR);
                _ = PInvoke.DeleteObject(rect);
            }

            if (PInvoke.SetWindowRgn(Handle, combined, true) == 0)
                _ = PInvoke.DeleteObject(combined);
        }

        public void ClearRegion()
        {
            if (Handle == HWND.Null)
                return;

            _hasRegionState = false;

            _ = PInvoke.SetWindowRgn(Handle, HRGN.Null, true);
        }

        public void ClipToGame(OverlayRegion gameClip, HWND gameWindow, uint gameProcessId)
        {
            if (Handle == HWND.Null)
                return;

            bool hasClip = gameClip.Right > gameClip.Left && gameClip.Bottom > gameClip.Top;

            var occluders = new List<Occluder>();

            if (hasClip && !CollectOccluders(occluders, gameProcessId, gameClip))
            {
                occluders.Clear();

                if (TryGetForegroundOccluder(gameProcessId, gameClip, out Occluder foreground))
                    occluders.Add(foreground);
            }

            if (HasSameRegion(gameClip, occluders))
                return;

            _lastClip = gameClip;
            _lastOccluders = [.. occluders];
            _hasRegionState = true;

            HRGN region = hasClip
                ? PInvoke.CreateRectRgn(gameClip.Left, gameClip.Top, gameClip.Right, gameClip.Bottom)
                : PInvoke.CreateRectRgn(0, 0, 0, 0);

            PixelPoint position = _window.Position;

            foreach (Occluder occluder in occluders)
            {
                HRGN shape = PInvoke.CreateRectRgn(0, 0, 0, 0);

                if (PInvoke.GetWindowRgn(occluder.Handle, shape) == 0)
                {
                    _ = PInvoke.DeleteObject(shape);

                    shape = PInvoke.CreateRectRgn(
                        occluder.Screen.Left - position.X,
                        occluder.Screen.Top - position.Y,
                        occluder.Screen.Right - position.X,
                        occluder.Screen.Bottom - position.Y);
                }
                else
                {
                    _ = PInvoke.OffsetRgn(
                        shape,
                        occluder.Screen.Left - position.X,
                        occluder.Screen.Top - position.Y);
                }

                _ = PInvoke.CombineRgn(region, region, shape, RGN_COMBINE_MODE.RGN_DIFF);
                _ = PInvoke.DeleteObject(shape);
            }

            if (PInvoke.SetWindowRgn(Handle, region, true) == 0)
            {
                _hasRegionState = false;
                _ = PInvoke.DeleteObject(region);
            }
        }

        private void ApplyStyles()
        {
            if (Handle == HWND.Null)
                return;

            uint exStyle = (uint)PInvoke.GetWindowLong(Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            uint updated = ApplyExStyle(exStyle);

            if (updated != exStyle)
                _ = PInvoke.SetWindowLong(Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)updated);
        }

        private uint ApplyExStyle(uint exStyle)
        {
            switch (_kind)
            {
                case WindowKind.Game:
                case WindowKind.SearchBar:
                    return exStyle | WS_EX_TOOLWINDOW;

                case WindowKind.KeyOverlay:
                    exStyle |= WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;

                    return _clickThrough
                        ? exStyle | WS_EX_TRANSPARENT
                        : exStyle & ~WS_EX_TRANSPARENT;

                default:
                    return exStyle;
            }
        }

        private (uint Style, uint ExStyle) OnStyles(uint style, uint exStyle) =>
            (style, ApplyExStyle(exStyle));

        private IntPtr OnMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_HOTKEY)
                return IntPtr.Zero;

            int id = wParam.ToInt32();

            if (!_hotkeys.Contains(id))
                return IntPtr.Zero;

            _hotkeyPressed(id);

            handled = true;

            return IntPtr.Zero;
        }

        private bool HasSameRegion(OverlayRegion clip, List<Occluder> occluders)
        {
            if (!_hasRegionState || _lastClip != clip || _lastOccluders.Length != occluders.Count)
                return false;

            for (int i = 0; i < occluders.Count; i++)
            {
                if (_lastOccluders[i] != occluders[i])
                    return false;
            }

            return true;
        }

        private bool CollectOccluders(List<Occluder> results, uint gameProcessId, OverlayRegion clip)
        {
            PixelPoint position = _window.Position;

            HWND hwnd = PInvoke.GetTopWindow(HWND.Null);
            int guard = 0;

            while (hwnd != HWND.Null && guard++ < 1024)
            {
                HWND next = PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_HWNDNEXT);

                if (hwnd != Handle)
                {
                    _ = PInvoke.GetWindowThreadProcessId(hwnd, out uint processId);

                    if (processId == gameProcessId)
                    {
                        if (PInvoke.IsWindowVisible(hwnd) && !PInvoke.IsIconic(hwnd))
                            return true;

                        hwnd = next;
                        continue;
                    }

                    if (processId == (uint)Environment.ProcessId)
                    {
                        hwnd = next;
                        continue;
                    }

                    if (IsOccluderWindow(hwnd) && TryGetWindowRect(hwnd, out RECT rect))
                    {
                        var candidate = new OverlayRegion(
                            rect.left - position.X,
                            rect.top - position.Y,
                            rect.right - position.X,
                            rect.bottom - position.Y);

                        if (candidate.Right > clip.Left && candidate.Left < clip.Right
                            && candidate.Bottom > clip.Top && candidate.Top < clip.Bottom
                            && !IsCloaked(hwnd))
                        {
                            results.Add(new Occluder(
                                hwnd,
                                new OverlayRegion(rect.left, rect.top, rect.right, rect.bottom)));
                        }
                    }
                }

                hwnd = next;
            }

            return false;
        }

        private bool TryGetForegroundOccluder(uint gameProcessId, OverlayRegion clip, out Occluder occluder)
        {
            occluder = default;

            HWND foreground = PInvoke.GetForegroundWindow();

            if (foreground == HWND.Null)
                return false;

            _ = PInvoke.GetWindowThreadProcessId(foreground, out uint processId);

            if (processId == gameProcessId
                || processId == (uint)Environment.ProcessId
                || !IsOccluderWindow(foreground)
                || !TryGetWindowRect(foreground, out RECT rect))
            {
                return false;
            }

            PixelPoint position = _window.Position;

            var candidate = new OverlayRegion(
                rect.left - position.X,
                rect.top - position.Y,
                rect.right - position.X,
                rect.bottom - position.Y);

            if (candidate.Right <= clip.Left || candidate.Left >= clip.Right
                || candidate.Bottom <= clip.Top || candidate.Top >= clip.Bottom)
            {
                return false;
            }

            occluder = new Occluder(
                foreground,
                new OverlayRegion(rect.left, rect.top, rect.right, rect.bottom));

            return true;
        }

        private static bool IsOccluderWindow(HWND hwnd)
        {
            if (!PInvoke.IsWindowVisible(hwnd) || PInvoke.IsIconic(hwnd) || IsShellWindow(hwnd))
                return false;

            uint exStyle = (uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

            return (exStyle & (WS_EX_TRANSPARENT | WS_EX_LAYERED)) == 0;
        }

        private static bool TryGetWindowRect(HWND hwnd, out RECT bounds)
        {
            unsafe
            {
                RECT rect = default;

                var result = PInvoke.DwmGetWindowAttribute(
                    hwnd,
                    DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
                    &rect,
                    (uint)sizeof(RECT));

                if (result.Failed && !PInvoke.GetWindowRect(hwnd, out rect))
                {
                    bounds = default;
                    return false;
                }

                bounds = rect;
            }

            return bounds.right > bounds.left && bounds.bottom > bounds.top;
        }

        private static bool IsCloaked(HWND hwnd)
        {
            int[] buffer = new int[1];

            unsafe
            {
                fixed (int* pointer = buffer)
                {
                    var result = PInvoke.DwmGetWindowAttribute(
                        hwnd,
                        DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
                        pointer,
                        (uint)sizeof(int));

                    return result.Succeeded && buffer[0] != 0;
                }
            }
        }

        private static bool IsShellWindow(HWND hwnd)
        {
            string className = GetWindowClass(hwnd);

            return className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
        }

        private static string GetWindowClass(HWND hwnd)
        {
            Span<char> buffer = stackalloc char[64];

            unsafe
            {
                fixed (char* pointer = buffer)
                {
                    int length = PInvoke.GetClassName(hwnd, pointer, buffer.Length);

                    return length > 0 ? new string(pointer, 0, length) : String.Empty;
                }
            }
        }

        private readonly record struct Occluder(HWND Handle, OverlayRegion Screen);
    }
}