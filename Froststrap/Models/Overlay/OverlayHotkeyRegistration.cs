// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Input;
using Avalonia.Win32.Input;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Froststrap.Models.Overlay
{
    internal static class OverlayHotkeyRegistration
    {
        public static bool Register(HWND hwnd, int id, KeyModifiers modifiers, Key key, string description)
        {
            Unregister(hwnd, id);

            var native = HOT_KEY_MODIFIERS.MOD_NOREPEAT;

            if (modifiers.HasFlag(KeyModifiers.Control)) native |= HOT_KEY_MODIFIERS.MOD_CONTROL;
            if (modifiers.HasFlag(KeyModifiers.Alt)) native |= HOT_KEY_MODIFIERS.MOD_ALT;
            if (modifiers.HasFlag(KeyModifiers.Shift)) native |= HOT_KEY_MODIFIERS.MOD_SHIFT;
            if (modifiers.HasFlag(KeyModifiers.Meta)) native |= HOT_KEY_MODIFIERS.MOD_WIN;

            string shortcut = OverlayHotkey.Describe(modifiers, key);

            bool registered = PInvoke.RegisterHotKey(
                hwnd, id, native, (uint)KeyInterop.VirtualKeyFromKey(key));

            if (registered)
                App.Logger.Info($"{description} hotkey is {shortcut}");
            else
                App.Logger.Warn($"Could not register the {description} hotkey, something else owns {shortcut}");

            return registered;
        }

        public static void Unregister(HWND hwnd, int id)
        {
            if (hwnd != HWND.Null && OperatingSystem.IsWindows())
                PInvoke.UnregisterHotKey(hwnd, id);
        }

        public static (KeyModifiers Modifiers, Key Key) Resolve(
            KeyModifiers modifiers,
            Key key,
            KeyModifiers defaultModifiers,
            Key defaultKey,
            string description)
        {
            if (OverlayHotkey.IsAllowed(modifiers, key))
                return (modifiers, key);

            App.Logger.Warn($"The saved {description} hotkey ({OverlayHotkey.Describe(modifiers, key)}) isn't usable, falling back to the default");

            return (defaultModifiers, defaultKey);
        }
    }
}