#if WINDOWS
using System.Security.Cryptography;
#endif

// TODO: Make Linux equivalent for reading Cookie (Sober puts your cookie in plain text so its not that hard)
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
        private bool Enabled => App.Settings.Prop.AllowCookieAccess;

        private string AuthCookie = string.Empty;
        private const string AuthCookieName = ".ROBLOSECURITY";
        private const string SupportedVersion = "1";
        private const string AuthPattern = $@"\t{AuthCookieName}\t(.+?)(;|$)";
        private string CookiesPath => Path.Combine(Paths.Roblox, "LocalStorage", "RobloxCookies.dat");

        public async Task<HttpResponseMessage> AuthRequest(HttpRequestMessage request)
        {
            string? host = request.RequestUri?.Host;
            if (host is null)
                throw new ArgumentNullException("Host cannot be null");

            if (!host.Equals("roblox.com", StringComparison.OrdinalIgnoreCase) &&
                !host.EndsWith(".roblox.com", StringComparison.OrdinalIgnoreCase))
                throw new HttpRequestException("Host must end with roblox.com");

            if (!Enabled)
                throw new NullReferenceException("Cookie access is not enabled");

            request.Headers.Add("Cookie", $".ROBLOSECURITY={AuthCookie}");
            return await App.HttpClient.SendAsync(request);
        }

        public async Task<HttpResponseMessage> AuthGet(string uri) =>
            await AuthRequest(new HttpRequestMessage { RequestUri = new Uri(uri), Method = HttpMethod.Get });

        public async Task<HttpResponseMessage> AuthPost(string uri, HttpContent? content) =>
            await AuthRequest(new HttpRequestMessage { RequestUri = new Uri(uri), Content = content, Method = HttpMethod.Post });

        public async Task<AuthenticatedUser?> GetAuthenticated()
        {
            const string LOG_IDENT = "CookiesManager::GetAuthenticated";
            try
            {
                HttpResponseMessage response = await AuthGet("https://users.roblox.com/v1/users/authenticated");
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
                string content = File.ReadAllText(CookiesPath);
                var cookies = JsonSerializer.Deserialize<RobloxCookies>(content)!;

                if (cookies.Version != SupportedVersion)
                    App.Logger.WriteLine(LOG_IDENT, $"Unknown cookie version: {cookies.Version}");

                byte[] encryptedData = Convert.FromBase64String(cookies.Cookies);

#if WINDOWS
                byte[] unencryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
#else
                byte[] unencryptedData = Array.Empty<byte>();
#endif

                string rawCookies = Encoding.UTF8.GetString(unencryptedData);
                Match authCookieMatch = Regex.Match(rawCookies, AuthPattern);

                if (!authCookieMatch.Success)
                {
                    State = CookieState.Invalid;
                    App.Logger.WriteLine(LOG_IDENT, "Regex failed for cookies");
                    return;
                }

                AuthCookie = authCookieMatch.Groups[1].Value;

                AuthenticatedUser? user = await GetAuthenticated();
                if (user is null || user.Id == 0)
                {
                    State = CookieState.Invalid;
                    App.Logger.WriteLine(LOG_IDENT, "Cookie is invalid");
                    return;
                }

                State = CookieState.Success;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load cookie!");
                App.Logger.WriteException(LOG_IDENT, ex);
                State = CookieState.Failed;
            }
        }
    }
}
