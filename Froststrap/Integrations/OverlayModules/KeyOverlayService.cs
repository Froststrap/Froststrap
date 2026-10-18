// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;

using Froststrap.Integrations.Overlay;
using Froststrap.UI.Elements.Overlay;

namespace Froststrap.Integrations.OverlayModules
{
    internal static class KeyOverlayService
    {
        private static KeyOverlay? _window;
        private static bool _started;
        private static bool _sessionEnabled = true;

        public static event EventHandler<bool>? SessionEnabledChanged;

        public static bool SessionEnabled => _sessionEnabled;

        public static void Start(IOverlayHost? host)
        {
            if (_started || !OperatingSystem.IsWindows() || !App.Settings.Prop.KeyOverlayEnabled)
                return;

            _started = true;
            _sessionEnabled = true;

            Dispatcher.UIThread.Post(() =>
            {
                if (_window is not null)
                    return;

                _window = new KeyOverlay();
                _window.Prepare(host);
            });
        }

        public static void Stop()
        {
            _started = false;
            _sessionEnabled = true;

            Dispatcher.UIThread.Post(() =>
            {
                _window?.SafeClose();
                _window = null;
            });
        }

        public static void SetSessionEnabled(bool enabled)
        {
            if (_sessionEnabled == enabled)
                return;

            _sessionEnabled = enabled;

            _window?.SetSessionVisible(enabled);

            SessionEnabledChanged?.Invoke(null, enabled);
        }

        public static void Toggle() => SetSessionEnabled(!_sessionEnabled);
    }
}
