// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System;

using Avalonia.Input;
using Avalonia.Threading;

namespace Froststrap.Models.Overlay
{
    internal static class KeyOverlayHotkey
    {
        public static readonly KeyModifiers DefaultModifiers = OperatingSystem.IsMacOS()
            ? KeyModifiers.Meta | KeyModifiers.Shift
            : KeyModifiers.Control | KeyModifiers.Alt;

        public const Key DefaultKey = Key.M;

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
    }
}