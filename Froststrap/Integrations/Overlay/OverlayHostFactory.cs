// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Integrations.Overlay.Platform;

namespace Froststrap.Integrations.Overlay
{
    internal static class OverlayHostFactory
    {
        public static IOverlayHost Create(int robloxProcessId, ActivityWatcher? activityWatcher) =>
            OperatingSystem.IsWindows() ? new WindowsOverlayHost(robloxProcessId, activityWatcher)
            : OperatingSystem.IsLinux() ? new LinuxOverlayHost(robloxProcessId, activityWatcher)
            : OperatingSystem.IsMacOS() ? new MacOverlayHost(robloxProcessId, activityWatcher)
            : new UnsupportedOverlayHost(robloxProcessId, activityWatcher);
    }
}