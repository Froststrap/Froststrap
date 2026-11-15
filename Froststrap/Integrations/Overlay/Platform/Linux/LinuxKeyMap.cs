// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Input;

namespace Froststrap.Integrations.Overlay.Platform.Linux
{
    internal static class LinuxKeyMap
    {
        public const uint ModifierAlt = 1;
        public const uint ModifierControl = 2;
        public const uint ModifierShift = 4;
        public const uint ModifierSuper = 8;

        public static (uint Code, uint Modifiers) Resolve(
            KeyModifiers modifiers,
            Key key,
            KeyModifiers defaultModifiers,
            Key defaultKey,
            string description)
        {
            (KeyModifiers resolvedModifiers, Key resolvedKey) = OverlayHotkey.Resolve(
                modifiers,
                key,
                defaultModifiers,
                defaultKey,
                description);

            return (Code(resolvedKey), Modifiers(resolvedModifiers));
        }

        public static uint Modifiers(KeyModifiers modifiers)
        {
            uint result = 0;

            if (modifiers.HasFlag(KeyModifiers.Alt))
                result |= ModifierAlt;

            if (modifiers.HasFlag(KeyModifiers.Control))
                result |= ModifierControl;

            if (modifiers.HasFlag(KeyModifiers.Shift))
                result |= ModifierShift;

            if (modifiers.HasFlag(KeyModifiers.Meta))
                result |= ModifierSuper;

            return result;
        }

        public static uint Code(Key key) => key switch
        {
            Key.A => 30,
            Key.B => 48,
            Key.C => 46,
            Key.D => 32,
            Key.E => 18,
            Key.F => 33,
            Key.G => 34,
            Key.H => 35,
            Key.I => 23,
            Key.J => 36,
            Key.K => 37,
            Key.L => 38,
            Key.M => 50,
            Key.N => 49,
            Key.O => 24,
            Key.P => 25,
            Key.Q => 16,
            Key.R => 19,
            Key.S => 31,
            Key.T => 20,
            Key.U => 22,
            Key.V => 47,
            Key.W => 17,
            Key.X => 45,
            Key.Y => 21,
            Key.Z => 44,
            Key.D0 => 11,
            Key.D1 => 2,
            Key.D2 => 3,
            Key.D3 => 4,
            Key.D4 => 5,
            Key.D5 => 6,
            Key.D6 => 7,
            Key.D7 => 8,
            Key.D8 => 9,
            Key.D9 => 10,
            Key.F1 => 59,
            Key.F2 => 60,
            Key.F3 => 61,
            Key.F4 => 62,
            Key.F5 => 63,
            Key.F6 => 64,
            Key.F7 => 65,
            Key.F8 => 66,
            Key.F9 => 67,
            Key.F10 => 68,
            Key.F11 => 87,
            Key.F12 => 88,
            Key.Escape => 1,
            Key.Back => 14,
            Key.Tab => 15,
            Key.Enter => 28,
            Key.Space => 57,
            Key.CapsLock => 58,
            Key.NumLock => 69,
            Key.Scroll => 70,
            Key.PrintScreen => 99,
            Key.Pause => 119,
            Key.Insert => 110,
            Key.Delete => 111,
            Key.Home => 102,
            Key.End => 107,
            Key.PageUp => 104,
            Key.PageDown => 109,
            Key.Left => 105,
            Key.Right => 106,
            Key.Up => 103,
            Key.Down => 108,
            Key.OemTilde => 41,
            Key.OemMinus => 12,
            Key.OemPlus => 13,
            Key.OemOpenBrackets => 26,
            Key.OemCloseBrackets => 27,
            Key.OemPipe => 43,
            Key.OemBackslash => 43,
            Key.OemSemicolon => 39,
            Key.OemQuotes => 40,
            Key.OemComma => 51,
            Key.OemPeriod => 52,
            Key.OemQuestion => 53,
            Key.NumPad0 => 82,
            Key.NumPad1 => 79,
            Key.NumPad2 => 80,
            Key.NumPad3 => 81,
            Key.NumPad4 => 75,
            Key.NumPad5 => 76,
            Key.NumPad6 => 77,
            Key.NumPad7 => 71,
            Key.NumPad8 => 72,
            Key.NumPad9 => 73,
            Key.Multiply => 55,
            Key.Add => 78,
            Key.Subtract => 74,
            Key.Divide => 98,
            Key.Decimal => 83,
            Key.LeftShift => 42,
            Key.RightShift => 54,
            Key.LeftCtrl => 29,
            Key.RightCtrl => 97,
            Key.LeftAlt => 56,
            Key.RightAlt => 100,
            Key.LWin => 125,
            Key.RWin => 126,
            _ => 0
        };
    }
}