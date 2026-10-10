// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Specialized;
using System.Web;

namespace Froststrap
{
    internal class GameJoin
    {
        /// Converts a pasted Roblox link into a launch command
        public static async Task<string?> GetLaunchCommandByLink(string link)
        {
            link = link.Trim();
            if (link.Length == 0)
                return null;

            // protocol uris are already launchable
            if (link.StartsWith("roblox://", StringComparison.OrdinalIgnoreCase)
                || link.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase)
                || link.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase))
                return link;

            Uri uri;
            try
            {
                uri = new Uri(link, UriKind.Absolute);
            }
            catch (UriFormatException)
            {
                // tolerate schemeless links (e.g. "www.roblox.com/games/123")
                try
                {
                    uri = new Uri("https://" + link, UriKind.Absolute);
                }
                catch (UriFormatException)
                {
                    return null;
                }
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return null;

            string host = uri.Host;

            // appsflyer links redirect to a roblox:// or roblox.com link
            if (host == "ro.blox.com" || host.EndsWith(".ro.blox.com", StringComparison.OrdinalIgnoreCase))
            {
                string? mobile = GetQueryValue(uri, "af_dp") ?? GetQueryValue(uri, "deep_link_value");
                if (!string.IsNullOrEmpty(mobile))
                    return await GetLaunchCommandByLink(mobile);

                string? web = GetQueryValue(uri, "af_web_dp");
                if (!string.IsNullOrEmpty(web))
                    return await GetLaunchCommandByLink(web);

                return null;
            }

            if (!IsRobloxHost(host))
                return null;

            string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // strip locale prefix (e.g. /en-gb/games/123)
            if (segments.Length > 1
                && Regex.IsMatch(segments[0], @"^[a-z]{2}(-[a-z]{2,3})?$", RegexOptions.IgnoreCase)
                && !segments[0].Equals("js", StringComparison.OrdinalIgnoreCase)
                && !segments[0].Equals("my", StringComparison.OrdinalIgnoreCase))
                segments = segments.Skip(1).ToArray();

            // private server share link (roblox.com/share?code=...&type=Server)
            if (segments.Length == 1 && segments[0].Equals("share", StringComparison.OrdinalIgnoreCase))
            {
                string? code = GetQueryValue(uri, "code");
                string? type = GetQueryValue(uri, "type");

                if (string.IsNullOrEmpty(code) || !string.Equals(type, "Server", StringComparison.OrdinalIgnoreCase))
                    return null;

                string deepLink = $"roblox://navigation/share_links?code={Uri.EscapeDataString(code)}&type=Server";
                App.Logger.Info($"Converted share link to navigation/share_links deep link (code: {code})");

                if (!App.Cookies.Loaded)
                    return deepLink;

                string? csrf = null;
                for (int i = 0; i < 2; i++)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, UrlBuilder.BuildApiUrl("apis", "sharelinks/v1/resolve-link", secure: true));
                    request.Headers.Add("Cookie", $".ROBLOSECURITY={App.Cookies.GetAuthCookie()}");
                    request.Content = new StringContent($"{{\"linkId\":\"{code}\",\"linkType\":\"Server\"}}", Encoding.UTF8, "application/json");
                    if (csrf is not null)
                        request.Headers.Add("X-CSRF-TOKEN", csrf);

                    using var response = await App.HttpClient.SendAsync(request);

                    if (response.StatusCode == HttpStatusCode.Forbidden && response.Headers.TryGetValues("x-csrf-token", out var token))
                    {
                        csrf = token.First();
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                        return deepLink;

                    string body = await response.Content.ReadAsStringAsync();
                    using var json = JsonDocument.Parse(body);

                    if (!json.RootElement.TryGetProperty("privateServerInviteData", out var data))
                        return deepLink;

                    if (data.TryGetProperty("status", out var status) && status.GetString() != "Valid")
                        return deepLink;

                    if (!data.TryGetProperty("placeId", out var placeIdProp))
                        return deepLink;

                    if (!placeIdProp.TryGetInt64(out long pid))
                        return deepLink;

                    if (!data.TryGetProperty("linkCode", out var linkCode))
                        return deepLink;

                    string? resolvedCode = linkCode.GetString();
                    if (string.IsNullOrEmpty(resolvedCode))
                        return deepLink;

                    return $"roblox://experiences/start?placeId={pid}&linkCode={Uri.EscapeDataString(resolvedCode)}";
                }

                return deepLink;
            }

            // experience page (roblox.com/games/{placeId}/name[?privateServerLinkCode=...])
            if (segments.Length >= 2 && segments[0].Equals("games", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(segments[1], out long placeId) && placeId > 0)
                return BuildStartExperienceUrl(placeId, uri);

            // any page carrying a placeId query parameter (e.g. roblox.com/play?placeId=...)
            if (long.TryParse(GetQueryValue(uri, "placeId"), out long placeIdFromQuery) && placeIdFromQuery > 0)
                return BuildStartExperienceUrl(placeIdFromQuery, uri);

            return null;
        }

        private static string BuildStartExperienceUrl(long placeId, Uri uri)
        {
            App.Logger.Info($"Extracted place ID from web link: {placeId}");

            string url = $"roblox://experiences/start?placeId={placeId}";

            string? linkCode = GetQueryValue(uri, "privateServerLinkCode");
            if (!string.IsNullOrEmpty(linkCode))
            {
                url += $"&linkCode={Uri.EscapeDataString(linkCode)}";
                App.Logger.Info($"Extracted private server link code from web link: {linkCode}");
            }

            string? serverId = GetQueryValue(uri, "gameInstanceId");
            if (!string.IsNullOrEmpty(serverId))
            {
                url += $"&gameInstanceId={Uri.EscapeDataString(serverId)}";
                App.Logger.Info($"Extracted server ID from web link: {serverId}");
            }

            string? accessCode = GetQueryValue(uri, "accessCode");
            if (!string.IsNullOrEmpty(accessCode))
            {
                url += $"&accessCode={Uri.EscapeDataString(accessCode)}";
                App.Logger.Info($"Extracted access code from web link: {accessCode}");
            }

            return url;
        }

        private static bool IsRobloxHost(string host) =>
            host.Equals("roblox.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".roblox.com", StringComparison.OrdinalIgnoreCase);

        private static string? GetQueryValue(Uri uri, string name)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);

            foreach (string key in query.Keys)
                if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return query[key];

            return null;
        }

        private static long RegexMatchLong(string url, string query, string pattern)
        {
            Match match = Regex.Match(url, query + pattern);

            if (!match.Success)
                return 0;

            _ = long.TryParse(match.Groups[1].Value, out long result);

            return result;
        }

        private static string RegexMatchString(string url, string query, string pattern)
        {
            Match match = Regex.Match(url, query + pattern);

            if (!match.Success)
                return string.Empty;

            return match.Groups[1].Value;
        }

        public static GameJoinData GetJoinDataByLaunchCommand(string launchCommandLine)
        {
            const string placelauncherPattern = @"placelauncherurl:(.+?)(\+|$)";
            const string requestTypePattern = @"request=(.+?)&";
            const string commonIntPattern = @"([0-9]+)";
            const string commonIdPattern = @"([a-zA-Z0-9-]+?)(&|\+|$)";

            GameJoinData joinData = new();

            if (!launchCommandLine.StartsWith("roblox-player:", StringComparison.Ordinal) &&
                !launchCommandLine.StartsWith("roblox://", StringComparison.Ordinal))
                return joinData;

            if (launchCommandLine.StartsWith("roblox://", StringComparison.Ordinal))
            {
                App.Logger.Info($"Processing roblox:// URI: {launchCommandLine}");

                var placeIdMatch = Regex.Match(launchCommandLine, @"placeId=([0-9]+)");
                if (placeIdMatch.Success)
                {
                    _ = long.TryParse(placeIdMatch.Groups[1].Value, out long placeId);
                    if (placeId > 0)
                    {
                        joinData.JoinType = GameJoinType.RequestGame;
                        joinData.PlaceId = placeId;
                        joinData.PlaceLauncherUrl = launchCommandLine;
                        App.Logger.Info($"Extracted place ID from roblox:// URI: {placeId}");
                    }
                }

                var jobIdMatch = Regex.Match(launchCommandLine, @"gameInstanceId=([a-zA-Z0-9-]+)");
                if (jobIdMatch.Success)
                {
                    joinData.JobId = jobIdMatch.Groups[1].Value;
                    App.Logger.Info($"Extracted job ID from roblox:// URI: {joinData.JobId}");
                }

                var accessCodeMatch = Regex.Match(launchCommandLine, @"accessCode=([a-zA-Z0-9-]+)");
                if (accessCodeMatch.Success)
                {
                    joinData.AccessCode = accessCodeMatch.Groups[1].Value;
                    App.Logger.Info($"Extracted access code from roblox:// URI: {joinData.AccessCode}");
                }

                var originMatch = Regex.Match(launchCommandLine, @"joinAttemptOrigin=([a-zA-Z0-9-]+)");
                if (originMatch.Success)
                {
                    joinData.JoinOrigin = originMatch.Groups[1].Value;
                    App.Logger.Info($"Extracted join origin from roblox:// URI: {joinData.JoinOrigin}");
                }

                return joinData;
            }

            Match urlMatch = Regex.Match(launchCommandLine, placelauncherPattern);
            if (!urlMatch.Success || urlMatch.Groups.Count != 3)
                return joinData;

            string rawPlaceLancherUrl = urlMatch.Groups[1].Value;
            joinData.PlaceLauncherUrl = rawPlaceLancherUrl;

            string url = HttpUtility.UrlDecode(rawPlaceLancherUrl);
            if (string.IsNullOrEmpty(url))
                return joinData;

            Match typeMatch = Regex.Match(url, requestTypePattern);
            if (!typeMatch.Success || typeMatch.Groups.Count != 2)
                return joinData;

            App.Logger.Debug("Detecting join type");

            // yuck
            switch (typeMatch.Groups[1].Value)
            {
                case "RequestGame":
                    {
                        joinData.JoinType = GameJoinType.RequestGame;

                        string joinOrigin = RegexMatchString(url, "joinAttemptOrigin=", commonIdPattern);
                        long placeId = RegexMatchLong(url, "placeId=", commonIntPattern);

                        if (placeId == 0) return joinData;

                        joinData.PlaceId = placeId;
                        joinData.JoinOrigin = joinOrigin;
                        break;
                    }
                case "RequestGameJob":
                    {
                        joinData.JoinType = GameJoinType.RequestGameJob;

                        string joinOrigin = RegexMatchString(url, "joinAttemptOrigin=", commonIdPattern);
                        string jobId = RegexMatchString(url, "gameId=", commonIdPattern);
                        long placeId = RegexMatchLong(url, "placeId=", commonIntPattern);

                        if (string.IsNullOrEmpty(jobId) || placeId == 0) return joinData;

                        joinData.PlaceId = placeId;
                        joinData.JobId = jobId;
                        joinData.JoinOrigin = joinOrigin;
                        break;
                    }
                case "RequestPrivateGame":
                    {
                        joinData.JoinType = GameJoinType.RequestPrivateGame;

                        string accessCode = RegexMatchString(url, "accessCode=", commonIdPattern);
                        long placeId = RegexMatchLong(url, "placeId=", commonIntPattern);

                        if (string.IsNullOrEmpty(accessCode) || placeId == 0) return joinData;

                        joinData.PlaceId = placeId;
                        joinData.AccessCode = accessCode;
                        break;
                    }
                case "RequestFollowUser":
                    {
                        joinData.JoinType = GameJoinType.RequestFollowUser;

                        long userId = RegexMatchLong(url, "userId=", commonIntPattern);

                        if (userId == 0) return joinData;

                        joinData.UserId = userId;
                        break;
                    }
                case "RequestPlayTogetherGame":
                    {
                        joinData.JoinType = GameJoinType.RequestPlayTogetherGame;

                        long placeId = RegexMatchLong(url, "placeId=", commonIntPattern);
                        string conversationId = RegexMatchString(url, "conversationId=", commonIdPattern);

                        if (string.IsNullOrEmpty(conversationId) || placeId == 0) return joinData;

                        joinData.PlaceId = placeId;
                        joinData.JobId = conversationId;
                        break;
                    }
            }

            App.Logger.Info($"Join type: {joinData.JoinType}");

            return joinData;
        }
    }
}
