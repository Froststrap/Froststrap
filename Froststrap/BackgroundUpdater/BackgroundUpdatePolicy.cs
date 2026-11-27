// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.AppData;
using Froststrap.RobloxInterfaces;

namespace Froststrap.BackgroundUpdater;

internal static class BackgroundUpdatePolicy
{
    public static bool IsEligible(Version? latestVersion, IAppData appData)
    {
        if (!App.Settings.Prop.BackgroundUpdatesEnabled)
        {
            App.Logger.Debug("Not eligible: Background updates disabled");
            return false;
        }

        if (!string.IsNullOrEmpty(Deployment.ChannelToken))
        {
            App.Logger.Debug("Not eligible: Private channel enrollment");
            return false;
        }

        // at least 3GB of free space
        const long minimumFreeSpace = 3_000_000_000;
        long space = Filesystem.GetFreeDiskSpace(Paths.Base);
        if (space < minimumFreeSpace)
        {
            App.Logger.Info($"Not eligible: User has {space} free space, at least {minimumFreeSpace} is required");
            return false;
        }

        if (latestVersion == default)
        {
            App.Logger.Info("Not eligible: Latest version is undefined");
            return false;
        }

        Version? currentVersion = Versioning.GetRobloxVersion(appData);
        if (currentVersion == default)
        {
            App.Logger.Info("Not eligible: Current version is undefined");
            return false;
        }

        // always normally upgrade for downgrades
        if (currentVersion.Minor > latestVersion.Minor)
        {
            App.Logger.Info("Not eligible: Downgrade");
            return false;
        }

        // only background update if we're:
        // - one major update behind
        // - the same major update
        int diff = latestVersion.Minor - currentVersion.Minor;
        if (diff == 0 || diff == 1)
        {
            App.Logger.Info("Eligible");
            return true;
        }

        App.Logger.Info($"Not eligible: Major version diff is {diff}");
        return false;
    }
}
