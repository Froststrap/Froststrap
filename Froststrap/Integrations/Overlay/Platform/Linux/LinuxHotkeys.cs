// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Input;

using Froststrap.Backend;
using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay.Platform.Linux;

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

internal static class LinuxHotkeys
{
    private static bool _portalChecked;
    private static bool _portalSupported;

    public static bool PortalSupported
    {
        get
        {
            if (_portalChecked)
                return _portalSupported;

            _portalChecked = true;

            try
            {
                _portalSupported = LinuxOverlay.PortalSupported();
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Linux portal global shortcuts unavailable: {ex.Message}");
                _portalSupported = false;
            }

            return _portalSupported;
        }
    }

    public static bool Register(
        int id,
        KeyModifiers modifiers,
        Key key,
        KeyModifiers defaultModifiers,
        Key defaultKey,
        string description)
    {
        Unregister(id);

        (KeyModifiers resolvedModifiers, Key resolvedKey) = OverlayHotkey.Resolve(
            modifiers,
            key,
            defaultModifiers,
            defaultKey,
            description);

        if (PortalSupported && TryRegisterPortal(id, resolvedModifiers, resolvedKey, description))
            return true;

        uint code = LinuxKeyMap.Code(resolvedKey);
        uint mapped = LinuxKeyMap.Modifiers(resolvedModifiers);

        if (code == 0)
        {
            App.Logger.Warn($"Could not map the {description} hotkey to a Linux key code");
            return false;
        }

        bool registered = LinuxOverlay.RegisterHotkey(id, code, mapped);

        if (registered)
            App.Logger.Info($"{description} hotkey is {OverlayHotkey.Describe(resolvedModifiers, resolvedKey)}");
        else
            App.Logger.Warn($"Could not register the {description} hotkey through /dev/input");

        return registered;
    }

    public static void Unregister(int id)
    {
        if (PortalSupported)
            LinuxOverlay.PortalUnregister(id);

        LinuxOverlay.UnregisterHotkey(id);
    }

    private static bool TryRegisterPortal(int id, KeyModifiers modifiers, Key key, string description)
    {
        string trigger = ToPortalTrigger(modifiers, key);

        if (string.IsNullOrEmpty(trigger))
            return false;

        try
        {
            if (LinuxOverlay.PortalRegister(id, $"froststrap-{id}", description, trigger))
            {
                App.Logger.Info($"{description} hotkey is {OverlayHotkey.Describe(modifiers, key)} (portal)");
                return true;
            }
        }
        catch (Exception ex)
        {
            App.Logger.Warn($"Portal registration for {description} failed: {ex.Message}");
        }

        return false;
    }

    private static string ToPortalTrigger(KeyModifiers modifiers, Key key)
    {
        var parts = new List<string>(5);

        if (modifiers.HasFlag(KeyModifiers.Control))
            parts.Add("CTRL");

        if (modifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("ALT");

        if (modifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("SHIFT");

        if (modifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("LOGO");

        string name = KeyName(key);

        if (name.Length == 0)
            return string.Empty;

        parts.Add(name);

        return string.Join('+', parts);
    }

    private static string KeyName(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
            return ((char)('A' + (key - Key.A))).ToString();

        if (key >= Key.D0 && key <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();

        if (key >= Key.F1 && key <= Key.F12)
            return $"F{(key - Key.F1) + 1}";

        return key switch
        {
            Key.Escape => "ESC",
            Key.Back => "BACKSPACE",
            Key.Tab => "TAB",
            Key.Enter => "ENTER",
            Key.Space => "SPACE",
            Key.Home => "HOME",
            Key.End => "END",
            Key.PageUp => "PAGEUP",
            Key.PageDown => "PAGEDOWN",
            Key.Insert => "INSERT",
            Key.Delete => "DELETE",
            Key.Left => "LEFT",
            Key.Right => "RIGHT",
            Key.Up => "UP",
            Key.Down => "DOWN",
            Key.OemMinus => "MINUS",
            Key.OemPlus => "EQUAL",
            Key.OemComma => "COMMA",
            Key.OemPeriod => "PERIOD",
            Key.OemQuestion => "SLASH",
            Key.OemSemicolon => "SEMICOLON",
            Key.OemQuotes => "APOSTROPHE",
            Key.OemOpenBrackets => "BRACKETLEFT",
            Key.OemCloseBrackets => "BRACKETRIGHT",
            Key.OemPipe => "BACKSLASH",
            Key.OemBackslash => "BACKSLASH",
            Key.OemTilde => "GRAVE",
            _ => string.Empty
        };
    }
}