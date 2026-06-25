using System.Text.RegularExpressions;
using System.Web;

namespace Froststrap
{
    public class GameJoin
    {
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
            const string LOG_IDENT = "Bootstrapper::GetJoinDataByLaunchCommand";

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
                App.Logger.WriteLine(LOG_IDENT, $"Processing roblox:// URI: {launchCommandLine}");

                var placeIdMatch = Regex.Match(launchCommandLine, @"placeId=([0-9]+)");
                if (placeIdMatch.Success)
                {
                    _ = long.TryParse(placeIdMatch.Groups[1].Value, out long placeId);
                    if (placeId > 0)
                    {
                        joinData.JoinType = GameJoinType.RequestGame;
                        joinData.PlaceId = placeId;
                        joinData.PlaceLauncherUrl = launchCommandLine;
                        App.Logger.WriteLine(LOG_IDENT, $"Extracted place ID from roblox:// URI: {placeId}");
                    }
                }

                var jobIdMatch = Regex.Match(launchCommandLine, @"gameInstanceId=([a-zA-Z0-9-]+)");
                if (jobIdMatch.Success)
                {
                    joinData.JobId = jobIdMatch.Groups[1].Value;
                    App.Logger.WriteLine(LOG_IDENT, $"Extracted job ID from roblox:// URI: {joinData.JobId}");
                }

                var accessCodeMatch = Regex.Match(launchCommandLine, @"accessCode=([a-zA-Z0-9-]+)");
                if (accessCodeMatch.Success)
                {
                    joinData.AccessCode = accessCodeMatch.Groups[1].Value;
                    App.Logger.WriteLine(LOG_IDENT, $"Extracted access code from roblox:// URI: {joinData.AccessCode}");
                }

                var originMatch = Regex.Match(launchCommandLine, @"joinAttemptOrigin=([a-zA-Z0-9-]+)");
                if (originMatch.Success)
                {
                    joinData.JoinOrigin = originMatch.Groups[1].Value;
                    App.Logger.WriteLine(LOG_IDENT, $"Extracted join origin from roblox:// URI: {joinData.JoinOrigin}");
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

            App.Logger.WriteLine(LOG_IDENT, "Detecting join type");

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

            App.Logger.WriteLine(LOG_IDENT, $"Join type: {joinData.JoinType}");

            return joinData;
        }
    }
}