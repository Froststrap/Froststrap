using Avalonia.Input;
using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay.Platform.Linux
{
    internal static class LinuxHotkeys
    {
        private const uint ModShift = 1;
        private const uint ModControl = 4;
        private const uint ModAlt = 8;
        private const uint ModMeta = 64;

        internal static bool Register(
            int id,
            KeyModifiers modifiers,
            Key key,
            KeyModifiers defaultModifiers,
            Key defaultKey,
            string description)
        {
            Unregister(id);

            (KeyModifiers resolvedModifiers, Key resolvedKey) = OverlayHotkey.Resolve(
                modifiers, key, defaultModifiers, defaultKey, description);

            uint keysym = ToKeysym(resolvedKey);
            uint evdev = ToEvdevKeycode(resolvedKey);

            if (keysym == 0)
            {
                App.Logger.Warn($"{description} uses unsupported key {resolvedKey}");
                return false;
            }

            uint native = 0;

            if (resolvedModifiers.HasFlag(KeyModifiers.Shift)) native |= ModShift;
            if (resolvedModifiers.HasFlag(KeyModifiers.Control)) native |= ModControl;
            if (resolvedModifiers.HasFlag(KeyModifiers.Alt)) native |= ModAlt;
            if (resolvedModifiers.HasFlag(KeyModifiers.Meta)) native |= ModMeta;

            string shortcut = OverlayHotkey.Describe(resolvedModifiers, resolvedKey);
            int result = LinuxOverlay.RegisterHotkey(id, keysym, evdev, native);

            if (result == 0)
                App.Logger.Info($"{description} hotkey is {shortcut}");
            else
                App.Logger.Warn($"Could not register the {description} hotkey ({shortcut})");

            return result == 0;
        }

        internal static void Unregister(int id) => LinuxOverlay.UnregisterHotkey(id);

        private static uint ToKeysym(Key key) => key switch
        {
            Key.A => 0x61, Key.B => 0x62, Key.C => 0x63, Key.D => 0x64, Key.E => 0x65,
            Key.F => 0x66, Key.G => 0x67, Key.H => 0x68, Key.I => 0x69, Key.J => 0x6A,
            Key.K => 0x6B, Key.L => 0x6C, Key.M => 0x6D, Key.N => 0x6E, Key.O => 0x6F,
            Key.P => 0x70, Key.Q => 0x71, Key.R => 0x72, Key.S => 0x73, Key.T => 0x74,
            Key.U => 0x75, Key.V => 0x76, Key.W => 0x77, Key.X => 0x78, Key.Y => 0x79,
            Key.Z => 0x7A,
            Key.D0 => 0x30, Key.D1 => 0x31, Key.D2 => 0x32, Key.D3 => 0x33, Key.D4 => 0x34,
            Key.D5 => 0x35, Key.D6 => 0x36, Key.D7 => 0x37, Key.D8 => 0x38, Key.D9 => 0x39,
            Key.F1 => 0xFFBE, Key.F2 => 0xFFBF, Key.F3 => 0xFFC0, Key.F4 => 0xFFC1,
            Key.F5 => 0xFFC2, Key.F6 => 0xFFC3, Key.F7 => 0xFFC4, Key.F8 => 0xFFC5,
            Key.F9 => 0xFFC6, Key.F10 => 0xFFC7, Key.F11 => 0xFFC8, Key.F12 => 0xFFC9,
            Key.Escape => 0xFF1B, Key.Tab => 0xFF09, Key.Space => 0x20, Key.Enter => 0xFF0D,
            Key.Back => 0xFF08, Key.Delete => 0xFFFF, Key.Home => 0xFF50, Key.End => 0xFF57,
            Key.PageUp => 0xFF55, Key.PageDown => 0xFF56, Key.Insert => 0xFF63,
            Key.Up => 0xFF52, Key.Down => 0xFF54, Key.Left => 0xFF51, Key.Right => 0xFF53,
            Key.OemSemicolon => 0x3B, Key.OemPlus => 0x3D, Key.OemComma => 0x2C,
            Key.OemMinus => 0x2D, Key.OemPeriod => 0x2E, Key.OemQuestion => 0x2F,
            Key.OemTilde => 0x60, Key.OemOpenBrackets => 0x5B, Key.OemPipe => 0x5C,
            Key.OemCloseBrackets => 0x5D, Key.OemQuotes => 0x27,
            _ => 0
        };

        private static uint ToEvdevKeycode(Key key) => key switch
        {
            Key.A => 30, Key.B => 48, Key.C => 46, Key.D => 32, Key.E => 18,
            Key.F => 33, Key.G => 34, Key.H => 35, Key.I => 23, Key.J => 36,
            Key.K => 37, Key.L => 38, Key.M => 50, Key.N => 49, Key.O => 24,
            Key.P => 25, Key.Q => 16, Key.R => 19, Key.S => 31, Key.T => 20,
            Key.U => 22, Key.V => 47, Key.W => 17, Key.X => 45, Key.Y => 21,
            Key.Z => 44,
            Key.D1 => 2, Key.D2 => 3, Key.D3 => 4, Key.D4 => 5, Key.D5 => 6,
            Key.D6 => 7, Key.D7 => 8, Key.D8 => 9, Key.D9 => 10, Key.D0 => 11,
            Key.F1 => 59, Key.F2 => 60, Key.F3 => 61, Key.F4 => 62, Key.F5 => 63,
            Key.F6 => 64, Key.F7 => 65, Key.F8 => 66, Key.F9 => 67, Key.F10 => 68,
            Key.F11 => 87, Key.F12 => 88,
            Key.Escape => 1, Key.Tab => 15, Key.Space => 57, Key.Enter => 28,
            Key.Back => 14, Key.Delete => 111, Key.Home => 102, Key.End => 107,
            Key.PageUp => 104, Key.PageDown => 109, Key.Insert => 110,
            Key.Up => 103, Key.Down => 108, Key.Left => 105, Key.Right => 106,
            Key.OemMinus => 12, Key.OemPlus => 13, Key.OemOpenBrackets => 26,
            Key.OemCloseBrackets => 27, Key.OemSemicolon => 39, Key.OemQuotes => 40,
            Key.OemTilde => 41, Key.OemPipe => 43, Key.OemComma => 51,
            Key.OemPeriod => 52, Key.OemQuestion => 53,
            _ => 0
        };
    }
}