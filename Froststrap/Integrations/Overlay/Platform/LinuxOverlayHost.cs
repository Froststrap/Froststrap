// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using Froststrap.Backend;
using Froststrap.UI.Elements.Overlay;

namespace Froststrap.Integrations.Overlay.Platform
{
    internal sealed class LinuxOverlayHost : UnsupportedOverlayHost
    {
        private LinuxOverlayRenderer? renderer;

        public LinuxOverlayHost(int robloxProcessId, ActivityWatcher? activityWatcher)
            : base(robloxProcessId, activityWatcher)
        {
            App.Logger.Info($"LinuxOverlayHost: created pid={robloxProcessId}");

            bool initialised;
            try
            {
                initialised = LinuxOverlay.Init();
            }
            catch (Exception ex)
            {
                App.Logger.Error("LinuxOverlayHost: init threw");
                App.Logger.Error(ex);
                return;
            }

            App.Logger.Info($"LinuxOverlayHost: init result={initialised}");
            if (!initialised)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var overlay = new GameOverlay(this);
                    App.Logger.Info("LinuxOverlayHost: GameOverlay constructed");
                    renderer = new LinuxOverlayRenderer(overlay);
                    renderer.Start();
                    App.Logger.Info($"LinuxOverlayHost: renderer started size={LinuxOverlay.Size()}");
                }
                catch (Exception ex)
                {
                    App.Logger.Error("LinuxOverlayHost: renderer setup threw");
                    App.Logger.Error(ex);
                }
            });
        }
    }
}
