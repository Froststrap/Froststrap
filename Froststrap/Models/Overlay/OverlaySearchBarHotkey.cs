// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Input;
using Avalonia.Threading;

namespace Froststrap.Models.Overlay
{
    internal static class OverlaySearchBarHotkey
    {
        public const KeyModifiers DefaultModifiers = KeyModifiers.Control | KeyModifiers.Alt;
        public const Key DefaultKey = Key.G;

        public static event EventHandler? Changed;

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
    }
}