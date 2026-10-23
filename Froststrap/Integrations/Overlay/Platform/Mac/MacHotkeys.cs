// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Avalonia.Input;
using Avalonia.Threading;

using Froststrap.Enums;
using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay.Platform.Mac
{
    internal static class MacKeyMap
    {
        private static readonly Dictionary<Key, uint> KeyCodes = BuildKeyCodes();

        public static bool TryGetKeyCode(Key key, out uint code) =>
            KeyCodes.TryGetValue(key, out code);

        public static uint ToCarbonModifiers(KeyModifiers modifiers)
        {
            uint carbon = 0;

            if (modifiers.HasFlag(KeyModifiers.Meta))
                carbon |= MacCarbon.CmdKey;

            if (modifiers.HasFlag(KeyModifiers.Shift))
                carbon |= MacCarbon.ShiftKey;

            if (modifiers.HasFlag(KeyModifiers.Alt))
                carbon |= MacCarbon.OptionKey;

            if (modifiers.HasFlag(KeyModifiers.Control))
                carbon |= MacCarbon.ControlKey;

            return carbon;
        }

        private static Dictionary<Key, uint> BuildKeyCodes()
        {
            var keys = new Dictionary<Key, uint>
            {
                [Key.A] = 0x00,
                [Key.S] = 0x01,
                [Key.D] = 0x02,
                [Key.F] = 0x03,
                [Key.H] = 0x04,
                [Key.G] = 0x05,
                [Key.Z] = 0x06,
                [Key.X] = 0x07,
                [Key.C] = 0x08,
                [Key.V] = 0x09,
                [Key.B] = 0x0B,
                [Key.Q] = 0x0C,
                [Key.W] = 0x0D,
                [Key.E] = 0x0E,
                [Key.R] = 0x0F,
                [Key.Y] = 0x10,
                [Key.T] = 0x11,
                [Key.D1] = 0x12,
                [Key.D2] = 0x13,
                [Key.D3] = 0x14,
                [Key.D4] = 0x15,
                [Key.D6] = 0x16,
                [Key.D5] = 0x17,
                [Key.D9] = 0x19,
                [Key.D7] = 0x1A,
                [Key.D8] = 0x1C,
                [Key.D0] = 0x1D,
                [Key.O] = 0x1F,
                [Key.U] = 0x20,
                [Key.I] = 0x22,
                [Key.P] = 0x23,
                [Key.Enter] = 0x24,
                [Key.L] = 0x25,
                [Key.J] = 0x26,
                [Key.K] = 0x28,
                [Key.N] = 0x2D,
                [Key.M] = 0x2E,
                [Key.Tab] = 0x30,
                [Key.Space] = 0x31,
                [Key.Back] = 0x33,
                [Key.Escape] = 0x35,
                [Key.CapsLock] = 0x39,
                [Key.F17] = 0x40,
                [Key.F18] = 0x4F,
                [Key.F19] = 0x50,
                [Key.F20] = 0x5A,
                [Key.F5] = 0x60,
                [Key.F6] = 0x61,
                [Key.F7] = 0x62,
                [Key.F3] = 0x63,
                [Key.F8] = 0x64,
                [Key.F9] = 0x65,
                [Key.F11] = 0x67,
                [Key.F13] = 0x69,
                [Key.F16] = 0x6A,
                [Key.F14] = 0x6B,
                [Key.F10] = 0x6D,
                [Key.F12] = 0x6F,
                [Key.F15] = 0x71,
                [Key.F4] = 0x76,
                [Key.F2] = 0x78,
                [Key.F1] = 0x7A,
                [Key.Home] = 0x73,
                [Key.PageUp] = 0x74,
                [Key.Delete] = 0x75,
                [Key.End] = 0x77,
                [Key.PageDown] = 0x79,
                [Key.Left] = 0x7B,
                [Key.Right] = 0x7C,
                [Key.Down] = 0x7D,
                [Key.Up] = 0x7E,
                [Key.OemMinus] = 0x1B,
                [Key.OemPlus] = 0x18,
                [Key.OemOpenBrackets] = 0x21,
                [Key.OemCloseBrackets] = 0x1E,
                [Key.OemSemicolon] = 0x29,
                [Key.OemQuotes] = 0x27,
                [Key.OemComma] = 0x2B,
                [Key.OemPeriod] = 0x2F,
                [Key.OemQuestion] = 0x2C,
                [Key.OemTilde] = 0x32,
                [Key.OemPipe] = 0x2A,
                [Key.OemBackslash] = 0x2A,
                [Key.NumPad0] = 0x52,
                [Key.NumPad1] = 0x53,
                [Key.NumPad2] = 0x54,
                [Key.NumPad3] = 0x55,
                [Key.NumPad4] = 0x56,
                [Key.NumPad5] = 0x57,
                [Key.NumPad6] = 0x58,
                [Key.NumPad7] = 0x59,
                [Key.NumPad8] = 0x5B,
                [Key.NumPad9] = 0x5C,
                [Key.NumLock] = 0x47,
                [Key.Add] = 0x45,
                [Key.Subtract] = 0x4E,
                [Key.Multiply] = 0x43,
                [Key.Divide] = 0x4B,
                [Key.Decimal] = 0x41
            };

            return keys;
        }
    }

    internal static class MacHotkeys
    {
        private const uint Signature = 0x46525354;

        private static readonly Dictionary<int, IntPtr> Registrations = [];
        private static bool _handlerInstalled;
        private static IntPtr _handlerRef;

        public static event Action<int>? Pressed;

        public static bool Register(
            int id,
            KeyModifiers modifiers,
            Key key,
            KeyModifiers defaultModifiers,
            Key defaultKey,
            string description)
        {
            if (!EnsureHandler())
                return false;

            Unregister(id);

            (KeyModifiers resolvedModifiers, Key resolvedKey) = OverlayHotkey.Resolve(
                modifiers,
                key,
                defaultModifiers,
                defaultKey,
                description);

            if (!MacKeyMap.TryGetKeyCode(resolvedKey, out uint keyCode))
            {
                App.Logger.Warn($"MacHotkeys: {description} uses unsupported key {resolvedKey}");
                return false;
            }

            var hotKeyId = new EventHotKeyID
            {
                Signature = Signature,
                Id = (uint)id
            };

            int status = MacCarbon.RegisterEventHotKey(
                keyCode,
                MacKeyMap.ToCarbonModifiers(resolvedModifiers),
                hotKeyId,
                MacCarbon.GetApplicationEventTarget(),
                0,
                out IntPtr hotKeyRef);

            if (status != 0)
            {
                App.Logger.Warn(
                    $"MacHotkeys: could not register {description} " +
                    $"({OverlayHotkey.Describe(resolvedModifiers, resolvedKey)}), status={status}");
                return false;
            }

            Registrations[id] = hotKeyRef;

            App.Logger.Info(
                $"MacHotkeys: registered {description} as " +
                $"{OverlayHotkey.Describe(resolvedModifiers, resolvedKey)}");

            return true;
        }

        public static void Unregister(int id)
        {
            if (!Registrations.Remove(id, out IntPtr hotKeyRef))
                return;

            int status = MacCarbon.UnregisterEventHotKey(hotKeyRef);

            if (status != 0)
                App.Logger.Warn($"MacHotkeys: could not unregister hotkey {id}, status={status}");
        }

        private static bool EnsureHandler()
        {
            if (_handlerInstalled)
                return true;

            var spec = new EventTypeSpec
            {
                EventClass = MacCarbon.EventClassKeyboard,
                EventKind = MacCarbon.EventHotKeyPressed
            };

            unsafe
            {
                IntPtr callback =
                    (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int>)&OnHotkey;

                int status = MacCarbon.InstallEventHandler(
                    MacCarbon.GetApplicationEventTarget(),
                    callback,
                    1,
                    ref spec,
                    IntPtr.Zero,
                    out _handlerRef);

                if (status != 0)
                {
                    App.Logger.Error($"MacHotkeys: InstallEventHandler failed, status={status}");
                    return false;
                }
            }

            _handlerInstalled = true;
            return true;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static int OnHotkey(IntPtr callRef, IntPtr eventRef, IntPtr userData)
        {
            var hotKeyId = new EventHotKeyID();

            int status = MacCarbon.GetEventParameter(
                eventRef,
                MacCarbon.EventParamDirectObject,
                MacCarbon.TypeEventHotKeyID,
                IntPtr.Zero,
                (nuint)Marshal.SizeOf<EventHotKeyID>(),
                IntPtr.Zero,
                ref hotKeyId);

            if (status == 0)
            {
                int id = (int)hotKeyId.Id;
                Dispatcher.UIThread.Post(() => Pressed?.Invoke(id));
            }

            return 0;
        }
    }

    internal static class MacSoftKeyLayout
    {
        public static (uint Up, uint Left, uint Down, uint Right) Resolve(SoftKeyProfile profile) => profile switch
        {
            SoftKeyProfile.AZERTY => (0x0D, 0x00, 0x01, 0x02),
            SoftKeyProfile.ESDF => (0x0E, 0x01, 0x02, 0x03),
            SoftKeyProfile.ArrowKeys => (0x7E, 0x7B, 0x7D, 0x7C),
            _ => (0x0D, 0x00, 0x01, 0x02)
        };
    }
}