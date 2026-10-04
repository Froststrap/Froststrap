// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Integrations.Overlay.Platform
{
    internal sealed class LinuxOverlayHost : UnsupportedOverlayHost
    {
        public LinuxOverlayHost(int robloxProcessId, ActivityWatcher? activityWatcher)
            : base(robloxProcessId, activityWatcher)
        {
        }
    }
}
