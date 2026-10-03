// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Integrations
{
    internal static class OverlayHostFactory
    {
        public static IOverlayHost? Create(int robloxProcessId, ActivityWatcher? activityWatcher)
        {
            if (!OperatingSystem.IsWindows())
                return null;

            return new WindowsOverlayHost(robloxProcessId, activityWatcher);
        }
    }
}
