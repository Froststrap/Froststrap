// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;

namespace Froststrap.Models.Overlay
{
    internal static class KeyOverlayDisplay
    {
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
