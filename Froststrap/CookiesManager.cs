// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.RobloxInterfaces;
using System.Net.WebSockets;
using System.Security.Cryptography;

namespace Froststrap
{
    internal class CookiesManager
    {
        private CookieState _state = CookieState.Unknown;

        public EventHandler<CookieState>? StateChanged;
        public CookieState State
        {
            get => _state;
            set
            {
                _state = value;
                StateChanged?.Invoke(this, value);
            }
        }
        public bool Loaded => Enabled && State == CookieState.Success;
        private static bool Enabled => App.Settings.Prop.AllowCookieAccess;

        public AuthenticatedUser? CurrentUser { get; private set; }

        public bool IsAuthenticated => CurrentUser is not null;

        private string AuthCookie = string.Empty;
        private const string AuthCookieName = ".ROBLOSECURITY";
        private const string AuthPattern = $@"\t{AuthCookieName}\t(.+?)(;|$)";

        private const string TrackerCookieName = "RBXEventTrackerV2";
        private const string TrackerPattern = $@"\t{TrackerCookieName}\t(.+?)(;|$)";

        private string BrowserTracker = string.Empty;
        public string GetAuthCookie() => AuthCookie;

        public static string CookiesPath => OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "HTTPStorages", "com.roblox.RobloxPlayer.binarycookies")
            : OperatingSystem.IsLinux()
                ? Path.Combine(Paths.Roblox, "data", "sober", "cookies")
                : Path.Combine(Paths.Roblox, "LocalStorage", Deployment.IsDefaultRobloxDomain ? "RobloxCookies.dat" : $"{Deployment.RobloxDomain}_RobloxCookies.dat");

        public async Task<string> GetXCSRF()
        {
            Uri logoutUrl = UrlBuilder.BuildApiUrl("auth", "v2/logout");

            HttpResponseMessage response = await AuthPost(logoutUrl, null);

            response.Headers.TryGetValues("x-csrf-token", out IEnumerable<string>? values);

            if (values is null)
                throw new HttpRequestException("Failed to get x-csrf-token from response");

            return values.First();
        }

        public async Task<HttpResponseMessage> AuthRequest(HttpRequestMessage request, string csrf = "")
        {
            string? host = request.RequestUri?.Host;

            ArgumentNullException.ThrowIfNull(host);

            if (
                !host.Equals(Deployment.RobloxDomain, StringComparison.OrdinalIgnoreCase) &&
                !host.EndsWith("." + Deployment.RobloxDomain, StringComparison.OrdinalIgnoreCase)
                )
                throw new HttpRequestException($"Host must end with Roblox domain ({Deployment.RobloxDomain})");

            if (!Enabled)
                throw new InvalidOperationException("Cookie access is not enabled");

            if (!String.IsNullOrEmpty(csrf))
                request.Headers.Add("x-csrf-token", csrf);

            request.Headers.Add("Cookie", String.IsNullOrEmpty(BrowserTracker)
                ? $".ROBLOSECURITY={AuthCookie}"
                : $".ROBLOSECURITY={AuthCookie}; {TrackerCookieName}={BrowserTracker}");
            return await App.HttpClient.SendAsync(request);
        }

        public async Task<HttpResponseMessage> AuthGet(Uri? uri, string csrf = "") => await AuthRequest(new HttpRequestMessage { RequestUri = uri, Method = HttpMethod.Get }, csrf);
        public async Task<HttpResponseMessage> AuthPost(Uri? uri, HttpContent? content, string csrf = "") => await AuthRequest(new HttpRequestMessage { RequestUri = uri, Content = content, Method = HttpMethod.Post }, csrf);

        public void AuthWebsocket(ClientWebSocket webSocket)
        {
            if (!Enabled)
                throw new NullReferenceException("Cookie access is not enabled");

            webSocket.Options.SetRequestHeader("Cookie", $".ROBLOSECURITY={AuthCookie}");
        }

        public async Task EnsureBrowserTrackerAsync()
        {
            if (!String.IsNullOrEmpty(BrowserTracker))
                return;

            try
            {
                using var handler = new HttpClientHandler { UseCookies = false };
                using var client = new HttpClient(handler);

                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

                using var response = await client.GetAsync($"https://www.{Deployment.RobloxDomain}/");

                string? tracker = null;

                if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies))
                {
                    tracker = cookies
                        .Select(x => Regex.Match(x, $@"^{TrackerCookieName}=([^;]+)"))
                        .FirstOrDefault(x => x.Success)?
                        .Groups[1].Value;
                }

                if (String.IsNullOrEmpty(tracker))
                {
                    App.Logger.Warn("Roblox didn't issue a browser tracker");
                    return;
                }

                BrowserTracker = tracker;

                App.Logger.Info("Roblox issued a browser tracker");
            }
            catch (Exception ex)
            {
                App.Logger.Warn("Failed to get a browser tracker");
                App.Logger.Error(ex);
            }
        }

        public async Task<AuthenticatedUser?> GetAuthenticated()
        {
            try
            {
                Uri apiUrl = UrlBuilder.BuildApiUrl("users", "v1/users/authenticated");
                HttpResponseMessage response = await AuthGet(apiUrl);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AuthenticatedUser>(content);
            }
            catch (HttpRequestException ex)
            {
                App.Logger.Error($"Failed to get authenticated user: {ex.Message}");
            }

            return null;
        }

        private record struct CookieLoadResult(string AuthCookie, string BrowserTracker);

        public async Task LoadCookies()
        {
            // we use the status to infrom user about it in the menu
            if (!Enabled)
            {
                State = CookieState.NotAllowed;
                App.Logger.Error("Cookie access not allowed");
                return;
            }

            if (!string.IsNullOrEmpty(AuthCookie))
            {
                App.Logger.Error("Cookie was already loaded!");
                return;
            }

            if (!File.Exists(CookiesPath))
            {
                State = CookieState.NotFound;
                App.Logger.Error("Cookie file not found");
                return;
            }

            try
            {
                CookieLoadResult result = default;

                if (OperatingSystem.IsWindows())
                {
                    result = await LoadWindowsCookies();
                }
                else if (OperatingSystem.IsMacOS())
                {
                    result = await LoadMacCookies();
                }
                else if (OperatingSystem.IsLinux())
                {
                    result = await LoadLinuxCookies();
                }

                if (string.IsNullOrEmpty(result.AuthCookie))
                {
                    State = CookieState.Invalid;
                    return;
                }

                AuthCookie = result.AuthCookie;

                if (!string.IsNullOrEmpty(result.BrowserTracker))
                {
                    BrowserTracker = result.BrowserTracker;
                    App.Logger.Info("Found a browser tracker");
                }
                else
                {
                    App.Logger.Info("No browser tracker in the cookie store");
                }

                AuthenticatedUser? user = await GetAuthenticated();
                State = (user != null && user.Id != 0) ? CookieState.Success : CookieState.Invalid;
                CurrentUser = user;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to load cookies: {ex.Message}");
                State = CookieState.Failed;
            }
        }

        private static async Task<CookieLoadResult> LoadWindowsCookies()
        {
            string content = await File.ReadAllTextAsync(CookiesPath);
            var cookies = JsonSerializer.Deserialize<RobloxCookies>(content)!;

            byte[] encryptedData = Convert.FromBase64String(cookies.Cookies);
            byte[] unencryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);

            string rawCookies = Encoding.UTF8.GetString(unencryptedData);

            Match authCookieMatch = Regex.Match(rawCookies, AuthPattern);
            string authCookie = authCookieMatch.Success ? authCookieMatch.Groups[1].Value : string.Empty;

            Match trackerMatch = Regex.Match(rawCookies, TrackerPattern);
            string tracker = trackerMatch.Success ? trackerMatch.Groups[1].Value : string.Empty;

            return new CookieLoadResult(authCookie, tracker);
        }

        private static async Task<CookieLoadResult> LoadMacCookies()
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(CookiesPath);
            var cookies = ParseBinaryCookies(fileBytes);

            string authCookie = ExtractRoblosecurity(cookies) ?? string.Empty;

            var trackerCookie = cookies.FirstOrDefault(c =>
                c.Name == TrackerCookieName &&
                c.Domain.Contains(".roblox.com", StringComparison.Ordinal));
            string tracker = trackerCookie.Value ?? string.Empty;

            return new CookieLoadResult(authCookie, tracker);
        }

        private static async Task<CookieLoadResult> LoadLinuxCookies()
        {
            string cookieText = await File.ReadAllTextAsync(CookiesPath);
            if (string.IsNullOrWhiteSpace(cookieText))
                return new CookieLoadResult(string.Empty, string.Empty);

            string authCookie = string.Empty;
            string tracker = string.Empty;

            var cookieParts = cookieText.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in cookieParts)
            {
                var trimmed = part.Trim();
                int eqIndex = trimmed.IndexOf('=', StringComparison.Ordinal);
                if (eqIndex > 0)
                {
                    string name = trimmed[..eqIndex].Trim();
                    string value = trimmed[(eqIndex + 1)..].Trim();

                    if (name == AuthCookieName)
                        authCookie = value;
                    else if (name == TrackerCookieName)
                        tracker = value;
                }
            }

            return new CookieLoadResult(authCookie, tracker);
        }

        private struct BinaryCookie
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Domain { get; set; }
            public string Path { get; set; }
            public int Flags { get; set; }
            public DateTime? Expiry { get; set; }
            public DateTime? Creation { get; set; }
        }

        private static List<BinaryCookie> ParseBinaryCookies(byte[] data)
        {
            var cookies = new List<BinaryCookie>();
            int offset = 0;

            if (data.Length < 4 || data[0] != 0x63 || data[1] != 0x6F || data[2] != 0x6F || data[3] != 0x6B)
                throw new InvalidOperationException("Not a binarycookies file");

            offset += 4;
            int numPages = ReadBigEndianInt32(data, ref offset);

            int[] pageSizes = new int[numPages];
            for (int i = 0; i < numPages; i++)
                pageSizes[i] = ReadBigEndianInt32(data, ref offset);

            for (int p = 0; p < numPages; p++)
            {
                int pageStart = offset;
                int pageEnd = pageStart + pageSizes[p];

                int pageHeader = ReadLittleEndianInt32(data, pageStart);
                if (pageHeader != 0x00000100 && pageHeader != 0x00010000)
                    App.Logger.Error($"Unexpected page header: 0x{pageHeader:X} at offset {pageStart}");

                int numCookies = ReadLittleEndianInt32(data, pageStart + 4);
                int cookieOffsetsOffset = pageStart + 8;

                for (int c = 0; c < numCookies; c++)
                {
                    int cookieOffset = pageStart + ReadLittleEndianInt32(data, cookieOffsetsOffset + c * 4);
                    try
                    {
                        var cookie = ParseSingleCookie(data, cookieOffset);
                        cookies.Add(cookie);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"Failed to parse cookie {c} in page {p}: {ex.Message}");
                    }
                }

                offset = pageEnd;
            }

            return cookies;
        }

        private static BinaryCookie ParseSingleCookie(byte[] data, int offset)
        {
            int flags = ReadLittleEndianInt32(data, offset + 4);
            int urlOffset = ReadLittleEndianInt32(data, offset + 16);
            int nameOffset = ReadLittleEndianInt32(data, offset + 20);
            int pathOffset = ReadLittleEndianInt32(data, offset + 24);
            int valueOffset = ReadLittleEndianInt32(data, offset + 28);
            double expiryTime = BitConverter.ToDouble(data, offset + 40);
            double creationTime = BitConverter.ToDouble(data, offset + 48);

            string domain = ReadNullTerminatedString(data, offset + urlOffset);
            string name = ReadNullTerminatedString(data, offset + nameOffset);
            string path = ReadNullTerminatedString(data, offset + pathOffset);
            string value = ReadNullTerminatedString(data, offset + valueOffset);

            return new BinaryCookie
            {
                Name = name,
                Value = value,
                Domain = domain,
                Path = path,
                Flags = flags,
                Expiry = MacTimeToDateTime(expiryTime),
                Creation = MacTimeToDateTime(creationTime)
            };
        }

        private static string ReadNullTerminatedString(byte[] data, int offset)
        {
            int end = offset;
            while (end < data.Length && data[end] != 0) end++;
            return Encoding.UTF8.GetString(data, offset, end - offset);
        }

        private static int ReadBigEndianInt32(byte[] data, ref int offset)
        {
            int value = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
            offset += 4;
            return value;
        }

        private static int ReadLittleEndianInt32(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
        }

        private static DateTime? MacTimeToDateTime(double macTime)
        {
            if (macTime == 0) return null;
            DateTime epoch = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(macTime).ToLocalTime();
        }

        private static string? ExtractRoblosecurity(List<BinaryCookie> cookies)
        {
            var cookie = cookies.FirstOrDefault(c =>
                c.Name == ".ROBLOSECURITY" &&
                c.Domain.Contains(".roblox.com", StringComparison.Ordinal));
            return cookie.Value;
        }
    }
}
