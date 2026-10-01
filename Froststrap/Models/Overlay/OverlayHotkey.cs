// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia.Input;
using Avalonia.Threading;

namespace Froststrap.Models.Overlay
{
    internal static class OverlayHotkey
    {
        public const KeyModifiers DefaultModifiers = KeyModifiers.Control | KeyModifiers.Alt;

        public const Key DefaultKey = Key.L;

        public static event EventHandler? Changed;

        public static event EventHandler<bool>? SuspendedChanged;

        public static bool IsSuspended { get; private set; }

        private static bool _notifyPending;

        public static void NotifyChanged()
        {
            if (_notifyPending)
                return;

            _notifyPending = true;

            Dispatcher.UIThread.Post(() =>
            {
                _notifyPending = false;
                Changed?.Invoke(null, EventArgs.Empty);
            }, DispatcherPriority.Background);
        }

        public static void SetSuspended(bool suspended)
        {
            if (IsSuspended == suspended)
                return;

            IsSuspended = suspended;

            SuspendedChanged?.Invoke(null, suspended);
        }

        public static KeyModifiers ModifierOf(Key key) => key switch
        {
            Key.LeftCtrl or Key.RightCtrl => KeyModifiers.Control,
            Key.LeftAlt or Key.RightAlt => KeyModifiers.Alt,
            Key.LeftShift or Key.RightShift => KeyModifiers.Shift,
            Key.LWin or Key.RWin => KeyModifiers.Meta,
            _ => KeyModifiers.None
        };

        public static bool IsModifier(Key key) => ModifierOf(key) != KeyModifiers.None;

        public static bool IsAllowed(KeyModifiers modifiers, Key key)
        {
            if (key is Key.None or Key.Escape || IsModifier(key))
                return false;

            if (key >= Key.F1 && key <= Key.F24)
                return true;

            if (IsTypingKey(key))
                return (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != KeyModifiers.None;

            return modifiers != KeyModifiers.None;
        }

        public static string Describe(KeyModifiers modifiers, Key key)
        {
            string keyName = key == Key.None ? String.Empty : KeyName(key);

            if (OperatingSystem.IsMacOS())
            {
                string mac =
                    (modifiers.HasFlag(KeyModifiers.Control) ? "⌃" : String.Empty) +
                    (modifiers.HasFlag(KeyModifiers.Alt) ? "⌥" : String.Empty) +
                    (modifiers.HasFlag(KeyModifiers.Shift) ? "⇧" : String.Empty) +
                    (modifiers.HasFlag(KeyModifiers.Meta) ? "⌘" : String.Empty) +
                    keyName;

                return mac;
            }

            var parts = new List<string>();

            if (modifiers.HasFlag(KeyModifiers.Control))
                parts.Add("Ctrl");

            if (modifiers.HasFlag(KeyModifiers.Alt))
                parts.Add("Alt");

            if (modifiers.HasFlag(KeyModifiers.Shift))
                parts.Add("Shift");

            if (modifiers.HasFlag(KeyModifiers.Meta))
                parts.Add(OperatingSystem.IsWindows() ? "Win" : "Super");

            if (keyName.Length > 0)
                parts.Add(keyName);

            return String.Join(" + ", parts);
        }

        private static bool IsTypingKey(Key key) =>
            (key >= Key.A && key <= Key.Z)
            || (key >= Key.D0 && key <= Key.D9)
            || (key >= Key.NumPad0 && key <= Key.Divide)
            || (key >= Key.OemSemicolon && key <= Key.OemBackslash)
            || key == Key.Space;

        private static readonly Dictionary<Key, string> KeyNames = new()
        {
            [Key.OemTilde] = "`",
            [Key.OemMinus] = "-",
            [Key.OemPlus] = "=",
            [Key.OemOpenBrackets] = "[",
            [Key.OemCloseBrackets] = "]",
            [Key.OemPipe] = "\\",
            [Key.OemSemicolon] = ";",
            [Key.OemQuotes] = "'",
            [Key.OemComma] = ",",
            [Key.OemPeriod] = ".",
            [Key.OemQuestion] = "/",
            [Key.Enter] = "Enter",
            [Key.Back] = "Backspace",
            [Key.PageUp] = "Page Up",
            [Key.PageDown] = "Page Down",
            [Key.CapsLock] = "Caps Lock",
            [Key.PrintScreen] = "Print Screen",
            [Key.Scroll] = "Scroll Lock"
        };

        private static string KeyName(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9)
                return ((int)key - (int)Key.D0).ToString(CultureInfo.InvariantCulture);

            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return "Num " + ((int)key - (int)Key.NumPad0).ToString(CultureInfo.InvariantCulture);

            return KeyNames.TryGetValue(key, out string? name) ? name : key.ToString();
        }
    }
}