// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using Froststrap.Backend;

namespace Froststrap.Integrations.Overlay.Platform
{
    internal sealed class LinuxOverlayHost : UnsupportedOverlayHost
    {
        private LinuxOverlayRenderer? renderer;

        public LinuxOverlayHost(int robloxProcessId, ActivityWatcher? activityWatcher)
            : base(robloxProcessId, activityWatcher)
        {
            Console.WriteLine($"overlay host created pid={robloxProcessId}");

            bool initialised;
            try
            {
                initialised = LinuxOverlay.Init();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"overlay init threw: {ex}");
                return;
            }

            Console.WriteLine($"overlay init result={initialised}");
            if (!initialised)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                renderer = new LinuxOverlayRenderer();
                renderer.Start();
                Console.WriteLine($"overlay renderer started size={LinuxOverlay.Size()}");
            });
        }
    }
}
