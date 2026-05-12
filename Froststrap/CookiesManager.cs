using Froststrap.RobloxInterfaces;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Runtime.Versioning;

namespace Froststrap
{
    public class CookiesManager
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

        private string AuthCookie = string.Empty;
        private const string AuthCookieName = ".ROBLOSECURITY";
        private const string SupportedVersion = "1";
        private const string AuthPattern = $@"\t{AuthCookieName}\t(.+?)(;|$)";

        private static string CookiesPath => RuntimeInformation.IsOSPlatform(OSPlatform.OSX)

            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "HTTPStorages", "com.roblox.RobloxPlayer.binarycookies")

            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)

                ? Path.Combine(Paths.Roblox, "data", "sober", "cookies")

                : Path.Combine(Paths.Roblox, "LocalStorage", Deployment.IsDefaultRobloxDomain ? "RobloxCookies.dat" : $"{Deployment.RobloxDomain}_RobloxCookies.dat");

        public async Task<HttpResponseMessage> AuthRequest(HttpRequestMessage request)
        {
            string? host = request.RequestUri?.Host;

            ArgumentNullException.ThrowIfNull(host);

            if (
                !host.Equals(Deployment.RobloxDomain, StringComparison.OrdinalIgnoreCase) &&
                !host.EndsWith("." + Deployment.RobloxDomain, StringComparison.OrdinalIgnoreCase)
                )
                throw new HttpRequestException($"Host must end with Roblox domain ({Deployment.RobloxDomain})");

            if (!Enabled)
                throw new NullReferenceException("Cookie access is not enabled");

            request.Headers.Add("Cookie", $".ROBLOSECURITY={AuthCookie}");
            return await App.HttpClient.SendAsync(request);
        }

        public async Task<HttpResponseMessage> AuthGet(Uri? uri) => await AuthRequest(new HttpRequestMessage { RequestUri = uri, Method = HttpMethod.Get });
        public async Task<HttpResponseMessage> AuthPost(Uri? uri, HttpContent? content) => await AuthRequest(new HttpRequestMessage { RequestUri = uri, Content = content, Method = HttpMethod.Post });

        public async Task<AuthenticatedUser?> GetAuthenticated()
        {
            const string LOG_IDENT = "CookiesManager::GetAuthenticated";

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
                App.Logger.WriteLine(LOG_IDENT, "Failed to get authenticated user");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return null;
        }

        public async Task LoadCookies()
        {
            const string LOG_IDENT = "CookiesManager::LoadCookies";

            // we use the status to infrom user about it in the menu
            if (!Enabled)
            {
                State = CookieState.NotAllowed;
                App.Logger.WriteLine(LOG_IDENT, "Cookie access not allowed");
                return;
            }

            if (!string.IsNullOrEmpty(AuthCookie))
            {
                App.Logger.WriteLine(LOG_IDENT, "Cookie was already loaded!");
                return;
            }

            if (!File.Exists(CookiesPath))
            {
                State = CookieState.NotFound;
                App.Logger.WriteLine(LOG_IDENT, "Cookie file not found");
                return;
            }

            try
            {
                string authCookie = string.Empty;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    authCookie = await LoadWindowsCookies();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    authCookie = await LoadMacCookies(LOG_IDENT);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    authCookie = await LoadLinuxCookies(LOG_IDENT);
                }

                if (string.IsNullOrEmpty(authCookie))
                {
                    State = CookieState.Invalid;
                    return;
                }

                AuthCookie = authCookie;
                AuthenticatedUser? user = await GetAuthenticated();
                State = (user != null && user.Id != 0) ? CookieState.Success : CookieState.Invalid;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                State = CookieState.Failed;
            }
        }

        [SupportedOSPlatform("windows")]
        private static async Task<string> LoadWindowsCookies()
        {
            string content = await File.ReadAllTextAsync(CookiesPath);
            var cookies = JsonSerializer.Deserialize<RobloxCookies>(content)!;

            byte[] encryptedData = Convert.FromBase64String(cookies.Cookies);
            byte[] unencryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);

            string rawCookies = Encoding.UTF8.GetString(unencryptedData);
            Match authCookieMatch = Regex.Match(rawCookies, AuthPattern);

            return authCookieMatch.Success ? authCookieMatch.Groups[1].Value : string.Empty;
        }

        private static async Task<string> LoadMacCookies(string logIdent)
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(CookiesPath);
            string fileString = Encoding.Latin1.GetString(fileBytes);

            var match = Regex.Match(fileString, @"_\|WARNING:-DO-NOT-SHARE-.*\|_");
            if (match.Success) return match.Value;

            App.Logger.WriteLine(logIdent, "Could not find .ROBLOSECURITY in binary cookies file.");
            return string.Empty;
        }

        private static async Task<string> LoadLinuxCookies(string logIdent)
        {
            // TODO: add actual cookie support, last time I checked Sober just uses plaintext in their COOKIES file.
            // Possibly add GNOME keyring/ KWallet support using Tmds.DBus.Protocol.
            App.Logger.WriteLine(logIdent, "Linux: attempting plaintext cookie read (keyring backend not yet implemented).");

            string content = await File.ReadAllTextAsync(CookiesPath);
            var cookies = JsonSerializer.Deserialize<RobloxCookies>(content)!;

            byte[] data = Convert.FromBase64String(cookies.Cookies);
            string rawCookies = Encoding.UTF8.GetString(data);
            Match authCookieMatch = Regex.Match(rawCookies, AuthPattern);

            return authCookieMatch.Success ? authCookieMatch.Groups[1].Value : string.Empty;
        }
    }
}
