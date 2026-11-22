// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.RobloxInterfaces;

namespace Froststrap.Utility
{
    internal static class UrlBuilder
    {
        private const string PlacelauncherBaseUrl = "https://www.roblox.com/Game/PlaceLauncher.ashx";

        public static Uri BuildApiUrl(string service, string path, bool secure = true)
        {
            string domain = Deployment.RobloxDomain;
            string url = secure ? "https://" : "http://";
            url += service + ".";
            url += domain + "/";
            url += path;

            return new(url);
        }

        public static string BuildPlacelauncherUrl(long placeId, string? jobId)
        {
            string url = PlacelauncherBaseUrl;
            url += "?request=RequestGameJob&placeId=";
            url += placeId;

            if (jobId is not null)
            {
                url += "&gameId=";
                url += jobId;
            }

            return url;
        }
    }
}