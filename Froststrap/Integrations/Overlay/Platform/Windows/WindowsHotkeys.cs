// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Input;
using Avalonia.Win32.Input;

using Froststrap.Models.Overlay;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Froststrap.Integrations.Overlay.Platform.Windows
{
    internal static class WindowsHotkeys
    {
        public static bool Register(
            HWND hwnd,
            int id,
            KeyModifiers modifiers,
            Key key,
            KeyModifiers defaultModifiers,
            Key defaultKey,
            string description)
        {
            Unregister(hwnd, id);

            (KeyModifiers resolvedModifiers, Key resolvedKey) = OverlayHotkey.Resolve(
                modifiers,
                key,
                defaultModifiers,
                defaultKey,
                description);

            var native = HOT_KEY_MODIFIERS.MOD_NOREPEAT;

            if (resolvedModifiers.HasFlag(KeyModifiers.Control))
                native |= HOT_KEY_MODIFIERS.MOD_CONTROL;

            if (resolvedModifiers.HasFlag(KeyModifiers.Alt))
                native |= HOT_KEY_MODIFIERS.MOD_ALT;

            if (resolvedModifiers.HasFlag(KeyModifiers.Shift))
                native |= HOT_KEY_MODIFIERS.MOD_SHIFT;

            if (resolvedModifiers.HasFlag(KeyModifiers.Meta))
                native |= HOT_KEY_MODIFIERS.MOD_WIN;

            string shortcut = OverlayHotkey.Describe(resolvedModifiers, resolvedKey);

            bool registered = PInvoke.RegisterHotKey(
                hwnd,
                id,
                native,
                (uint)KeyInterop.VirtualKeyFromKey(resolvedKey));

            if (registered)
                App.Logger.Info($"{description} hotkey is {shortcut}");
            else
                App.Logger.Warn($"Could not register the {description} hotkey, something else owns {shortcut}");

            return registered;
        }

        public static void Unregister(HWND hwnd, int id)
        {
            if (hwnd != HWND.Null)
                PInvoke.UnregisterHotKey(hwnd, id);
        }
    }
}
