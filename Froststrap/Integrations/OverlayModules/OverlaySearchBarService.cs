// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;

using Froststrap.Integrations.Overlay;
using Froststrap.Models.Overlay;
using Froststrap.UI.Elements.Overlay;

namespace Froststrap.Integrations.OverlayModules
{
    internal static class OverlaySearchBarService
    {
        private static OverlaySearchBar? _instance;
        private static bool _started;

        public static OverlayBounds? GameBounds { get; private set; }

        public static void SetGameBounds(OverlayBounds? bounds) => GameBounds = bounds;

        public static void Start(IOverlayHost? host)
        {
            if (_started)
                return;

            _started = true;

            Dispatcher.UIThread.Post(() =>
            {
                if (_instance is not null)
                    return;

                _instance = new OverlaySearchBar();
                _instance.Prepare(host);
            });
        }

        public static void Stop()
        {
            _started = false;
            GameBounds = null;

            Dispatcher.UIThread.Post(() =>
            {
                _instance?.Close();
                _instance = null;
            });
        }
    }
}