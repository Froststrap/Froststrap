/*
*  Froststrap
*  Copyright (c) Froststrap Team
*
*  This file is part of Froststrap and is distributed under the terms of the
*  GNU Affero General Public License, version 3 or later.
*
*  SPDX-License-Identifier: AGPL-3.0-or-later
*/

using Avalonia.Threading;
using Froststrap.UI.Elements.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Web;

namespace Froststrap.Integrations
{
    public class AccountManager
    {
        private const string LOG_IDENT = "AccountManager";
        private const string AccountsFile = "AccountManager.json";

        public event Action<AccountManagerAccount?>? ActiveAccountChanged;

        private readonly string _accountsLocation;
        private List<AccountManagerAccount> _accounts = [];
        private readonly Dictionary<long, string?> _avatarUrlCache = [];

        private Browser? _browser;
        private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("Froststrap_DPAPI_v1");

        public AccountManagerAccount? ActiveAccount { get; private set; }
        public long CurrentPlaceId { get; set; }
        public string CurrentServerInstanceId { get; set; } = "";

        public static AccountManager Shared { get; } = new AccountManager();
        public IReadOnlyList<AccountManagerAccount> Accounts => _accounts;

        public AccountManager()
        {
            _accountsLocation = Path.Combine(Paths.Cache, AccountsFile);
            LoadAccounts();
        }

        private static string Protect(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return text;

            try
            {
                return Convert.ToBase64String(ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(text), DpapiEntropy, DataProtectionScope.CurrentUser));
            }
            catch
            {
                return text;
            }
        }

        private static string Unprotect(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return text;

            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(
                    Convert.FromBase64String(text), DpapiEntropy, DataProtectionScope.CurrentUser));
            }
            catch
            {
                return text;
            }
        }

        public void LoadAccounts()
        {
            if (!File.Exists(_accountsLocation)) return;
            try
            {
                var data = JsonConvert.DeserializeObject<AccountManagerData>(File.ReadAllText(_accountsLocation));
                if (data?.Accounts != null)
                {
                    _accounts = [.. data.Accounts.Select(acc => acc with { SecurityToken = Unprotect(acc.SecurityToken) })];
                    if (data.ActiveAccountId.HasValue)
                        ActiveAccount = _accounts.Find(a => a.UserId == data.ActiveAccountId);
                }
            }
            catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
        }

        public void SaveAccounts()
        {
            try
            {
                var data = new AccountManagerData
                {
                    Accounts = [.. _accounts.Select(acc => acc with { SecurityToken = Protect(acc.SecurityToken) })],
                    ActiveAccountId = ActiveAccount?.UserId,
                    LastUpdated = DateTime.UtcNow,
                };
                File.WriteAllText(_accountsLocation, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
        }

        public void SetActiveAccount(long? userId)
        {
            var acc = _accounts.Find(a => a.UserId == userId);
            if (acc != null)
            {
                ActiveAccount = acc;
                ActiveAccountChanged?.Invoke(acc);
                SaveAccounts();
            }
        }

        public string? GetRoblosecurityForUser(long userId)
        {
            var a = _accounts.FirstOrDefault(x => x.UserId == userId);
            return a?.SecurityToken;
        }

        // https://devforum.roblox.com/t/how-to-generate-a-roblosecurity-token-from-quick-login/3147931
        public static async Task<AccountManagerAccount?> AddAccountByQuickSignInAsync(QuickSignCodeDialog dialog, CancellationToken cancellationToken)
        {
            const string LOG_IDENT_QS = $"{LOG_IDENT}::QuickSignIn";

            try
            {
                using var client = new HttpClient();

                var createUrl = UrlBuilder.BuildApiUrl("apis", "auth-token-service/v1/login/create", secure: true);
                var createResponse = await client.PostAsync(createUrl,
                    new StringContent("{}", Encoding.UTF8, "application/json"), cancellationToken);
                createResponse.EnsureSuccessStatusCode();

                var createJson = JObject.Parse(await createResponse.Content.ReadAsStringAsync(cancellationToken));
                string code = createJson["code"]!.Value<string>()!;
                string privateKey = createJson["privateKey"]!.Value<string>()!;
                DateTime expirationTime = createJson["expirationTime"]!.Value<DateTime>();

                await Dispatcher.UIThread.InvokeAsync(() => dialog.StartNewSignIn(code));

                var statusUrl = UrlBuilder.BuildApiUrl("apis", "auth-token-service/v1/login/status", secure: true);
                string? status = null;

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(4000, cancellationToken);

                    var statusPayload = new { code, privateKey };
                    var statusContent = new StringContent(
                        JsonConvert.SerializeObject(statusPayload), Encoding.UTF8, "application/json");

                    HttpResponseMessage statusResponse = await client.PostAsync(statusUrl, statusContent, cancellationToken);

                    if ((statusResponse.StatusCode == HttpStatusCode.Forbidden ||
                         statusResponse.StatusCode == HttpStatusCode.BadRequest) &&
                        statusResponse.Headers.TryGetValues("x-csrf-token", out var csrfVals))
                    {
                        string csrfToken = csrfVals.First();
                        var retryRequest = new HttpRequestMessage(HttpMethod.Post, statusUrl)
                        {
                            Content = statusContent
                        };
                        retryRequest.Headers.Add("x-csrf-token", csrfToken);
                        statusResponse = await client.SendAsync(retryRequest, cancellationToken);
                    }

                    string body = await statusResponse.Content.ReadAsStringAsync(cancellationToken);

                    if (statusResponse.StatusCode == HttpStatusCode.BadRequest)
                    {
                        if (body.Trim().StartsWith('{'))
                        {
                            var errJson = JObject.Parse(body);
                            var errorMsg = errJson["errors"]?[0]?["message"]?.Value<string>() ?? "Unknown error";
                            App.Logger.WriteLine(LOG_IDENT_QS, $"Status API returned error: {errorMsg}");
                            await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus("Cancelled"));
                        }
                        else if (body.Trim().Equals("\"CodeInvalid\"", StringComparison.OrdinalIgnoreCase) ||
                                 body.Trim().Equals("CodeInvalid", StringComparison.OrdinalIgnoreCase))
                        {
                            App.Logger.WriteLine(LOG_IDENT_QS, "Code invalid/expired.");
                            await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus("Cancelled"));
                        }
                        else
                        {
                            App.Logger.WriteLine(LOG_IDENT_QS, $"Unexpected 400 response: {body}");
                            await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus("Error: unexpected response"));
                        }
                        return null;
                    }

                    JObject statusJson;
                    try
                    {
                        statusJson = JObject.Parse(body);
                    }
                    catch (JsonReaderException)
                    {
                        App.Logger.WriteLine(LOG_IDENT_QS, $"Status endpoint returned non‑JSON: {body}");
                        await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus("Error: invalid response"));
                        return null;
                    }

                    status = (string?)statusJson["status"];
                    string? accountName = (string?)statusJson["accountName"];

                    if (string.IsNullOrEmpty(status))
                    {
                        var errors = statusJson["errors"] as JArray;
                        if (errors is { Count: > 0 })
                        {
                            var errorMessage = errors[0]?["message"]?.Value<string>() ?? "Unknown error";
                            App.Logger.WriteLine(LOG_IDENT_QS, $"API error: {errorMessage}");
                            await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus($"Error: {errorMessage}"));
                            return null;
                        }

                        App.Logger.WriteLine(LOG_IDENT_QS, $"Missing 'status' field in response: {body}");
                        await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus("Error: unexpected status"));
                        return null;
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        switch (status)
                        {
                            case "Created":
                                dialog.UpdateStatus("Waiting for Quick Sign-In...");
                                break;
                            case "UserLinked":
                                dialog.UpdateStatus("UserLinked", accountName);
                                break;
                            case "Validated":
                                dialog.UpdateStatus("Validated", accountName);
                                break;
                            case "Cancelled":
                                dialog.UpdateStatus("Cancelled");
                                break;
                            default:
                                dialog.UpdateStatus(status, accountName);
                                break;
                        }
                    });

                    if (status == "Validated" || status == "Cancelled")
                        break;

                    if (DateTime.UtcNow > expirationTime)
                    {
                        App.Logger.WriteLine(LOG_IDENT_QS, "Code timed out.");
                        await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus("TimedOut"));
                        return null;
                    }
                }

                if (cancellationToken.IsCancellationRequested || status == "Cancelled")
                    return null;

                var loginUrl = UrlBuilder.BuildApiUrl("auth", "v2/login", secure: true);
                var loginData = new
                {
                    ctype = "AuthToken",
                    cvalue = code,
                    password = privateKey
                };
                var loginContent = new StringContent(
                    JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");

                using var cookieHandler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer(),
                    UseCookies = true
                };
                using var loginClient = new HttpClient(cookieHandler);

                HttpResponseMessage loginResponse = await loginClient.PostAsync(loginUrl, loginContent, cancellationToken);

                if ((loginResponse.StatusCode == HttpStatusCode.Forbidden ||
                     loginResponse.StatusCode == HttpStatusCode.BadRequest) &&
                    loginResponse.Headers.TryGetValues("x-csrf-token", out var csrfValues))
                {
                    string csrfToken = csrfValues.First();
                    var retryRequest = new HttpRequestMessage(HttpMethod.Post, loginUrl)
                    {
                        Content = loginContent
                    };
                    retryRequest.Headers.Add("x-csrf-token", csrfToken);
                    loginResponse = await loginClient.SendAsync(retryRequest, cancellationToken);
                }

                loginResponse.EnsureSuccessStatusCode();

                var cookies = cookieHandler.CookieContainer.GetCookies(new Uri("https://roblox.com"));
                string? robloSecurity = cookies[".ROBLOSECURITY"]?.Value;

                if (string.IsNullOrEmpty(robloSecurity))
                {
                    App.Logger.WriteLine(LOG_IDENT_QS, "No .ROBLOSECURITY cookie in response.");
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        dialog.UpdateStatus("Failed: no cookie received"));
                    return null;
                }

                var account = await GetAccountInfoFromCookie(robloSecurity);
                if (account == null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        dialog.UpdateStatus("Failed: invalid account"));
                    return null;
                }

                await Dispatcher.UIThread.InvokeAsync(() => dialog.CompleteSignIn());
                return account;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_QS, ex);
                await Dispatcher.UIThread.InvokeAsync(() =>
                    dialog.UpdateStatus($"Error: {ex.Message}"));
                return null;
            }
        }

        public async Task<AccountManagerAccount?> AddAccountByBrowser()
        {
            const string LOG_IDENT_BROWSER = $"{LOG_IDENT}::AddAccountByBrowser";
            var completionSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                App.Logger.WriteLine(LOG_IDENT_BROWSER, "Launching browser for account login...");

                string? executablePath = GetSystemBrowserPath();

                if (executablePath == null)
                {
                    var fetcher = new BrowserFetcher();
                    var installed = fetcher.GetInstalledBrowsers().FirstOrDefault(b => b.Browser == SupportedBrowser.Chromium);
                    if (installed != null) executablePath = installed.GetExecutablePath();

                    if (executablePath == null)
                    {
                        var localAppData = Paths.LocalAppData;
                        var specificPath = Path.Combine(localAppData, "PuppeteerSharp");
                        if (Directory.Exists(specificPath))
                        {
                            var chromeFiles = Directory.GetFiles(specificPath, "chrome.exe", SearchOption.AllDirectories);
                            if (chromeFiles.Length > 0) executablePath = chromeFiles[0];
                        }
                    }

                    if (executablePath == null)
                    {
                        App.Logger.WriteLine(LOG_IDENT_BROWSER, "No browser found, downloading Chromium...");
                        var browserInfo = await fetcher.DownloadAsync();
                        executablePath = browserInfo.GetExecutablePath();
                    }
                }

                _browser = (Browser)await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = false,
                    DefaultViewport = null,
                    ExecutablePath = executablePath,
                    Args =
                    [
                        "--disable-notifications",
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-blink-features=AutomationControlled"
                    ],
                    IgnoredDefaultArgs = ["--enable-automation"]
                });

                if (_browser == null) return null;

                var mainPage = await _browser.NewPageAsync();
                await mainPage.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                var pages = await _browser.PagesAsync();
                foreach (var p in pages) if (p != mainPage) await p.CloseAsync();

                _browser.Disconnected += (s, e) => completionSource.TrySetResult(null);
                mainPage.Close += (s, e) => completionSource.TrySetResult(null);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!completionSource.Task.IsCompleted)
                        {
                            if (mainPage == null || mainPage.IsClosed) break;

                            var cookies = await mainPage.GetCookiesAsync("https://www.roblox.com/");
                            var securityCookie = cookies.FirstOrDefault(c => c.Name == ".ROBLOSECURITY");

                            if (securityCookie != null)
                            {
                                App.Logger.WriteLine(LOG_IDENT_BROWSER, "Successfully captured cookie.");
                                completionSource.TrySetResult(securityCookie.Value);
                                break;
                            }
                            await Task.Delay(1000);
                        }
                    }
                    catch { /* Page closed or disposed */ }
                });

                try
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, "Navigating to Roblox...");
                    await mainPage.GoToAsync("https://www.roblox.com/login", new NavigationOptions
                    {
                        WaitUntil = [WaitUntilNavigation.Networkidle2]
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, $"Initial nav failed ({ex.Message}), trying JS fallback...");
                    try
                    {
                        if (!mainPage.IsClosed)
                            await mainPage.EvaluateExpressionAsync("window.location.href = 'https://www.roblox.com/login'");
                    }
                    catch { /* Ignore if closed */ }
                }

                var resultTask = await Task.WhenAny(completionSource.Task, Task.Delay(TimeSpan.FromMinutes(10)));
                string? newCookie = null;

                if (resultTask == completionSource.Task)
                {
                    newCookie = await completionSource.Task;
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, "Login timed out after 10 minutes.");
                }

                if (string.IsNullOrEmpty(newCookie))
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, "Account add process cancelled or failed.");
                    return null;
                }

                var accountInfo = await GetAccountInfoFromCookie(newCookie);
                if (accountInfo == null) return null;

                var existing = _accounts.FirstOrDefault(acc => acc.UserId == accountInfo.UserId);
                if (existing == null)
                {
                    _accounts.Add(accountInfo);
                    SaveAccounts();
                    return accountInfo;
                }

                return existing;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_BROWSER, ex);
                return null;
            }
            finally
            {
                if (_browser != null && !_browser.IsClosed)
                {
                    await _browser.CloseAsync();
                    _browser = null;
                }
            }
        }

        // this sucks less (I'm guessing these paths bro)
        private static string? GetSystemBrowserPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetWindowsBrowserPath();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return GetLinuxBrowserPath();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetMacOsBrowserPath();

            return null;
        }

        private static string? GetWindowsBrowserPath()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            string[] paths =
            [
            // Google Chrome
            Path.Combine(programFiles,    "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(localAppData,    "Google", "Chrome", "Application", "chrome.exe"),

            // Microslop Edge
            Path.Combine(programFiles,    "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),

            // Brave
            Path.Combine(programFiles,    "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(programFilesX86, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(localAppData,    "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),

            // Vivaldi
            Path.Combine(programFiles,    "Vivaldi", "Application", "vivaldi.exe"),
            Path.Combine(localAppData,    "Vivaldi", "Application", "vivaldi.exe"),

            // Opera
            Path.Combine(programFiles,    "Opera", "opera.exe"),
            Path.Combine(localAppData,    "Programs", "Opera", "opera.exe"),

            // Opera GX
            Path.Combine(localAppData,    "Programs", "Opera GX", "opera.exe"),

            // Ungoogled Chromium
            Path.Combine(programFiles,    "Chromium", "Application", "chrome.exe"),
            Path.Combine(programFilesX86, "Chromium", "Application", "chrome.exe"),
            Path.Combine(localAppData,    "Chromium", "Application", "chrome.exe"),

            // Thorium
            Path.Combine(programFiles,    "Thorium", "Application", "thorium.exe"),
            Path.Combine(programFilesX86, "Thorium", "Application", "thorium.exe"),
            Path.Combine(localAppData,    "Thorium", "Application", "thorium.exe"),

            // Helium
            Path.Combine(programFiles,    "Helium", "Application", "chrome.exe"),
            Path.Combine(programFilesX86, "Helium", "Application", "chrome.exe"),
            Path.Combine(localAppData,    "Helium", "Application", "chrome.exe"),

            // Arc
            Path.Combine(localAppData,    "Programs", "Arc", "Arc.exe"),
            ];

            return paths.FirstOrDefault(File.Exists);
        }

        private static string? GetLinuxBrowserPath()
        {
            string[] candidates =
            [
            // Google Chrome
            "google-chrome",
            "google-chrome-stable",
            "google-chrome-beta",
            "google-chrome-unstable",

            // Chromium
            "chromium",
            "chromium-browser",

            // Microslop Edge
            "microsoft-edge",
            "microsoft-edge-stable",
            "microsoft-edge-beta",
            "microsoft-edge-dev",

            // Brave
            "brave-browser",
            "brave",

            // Vivaldi
            "vivaldi",
            "vivaldi-stable",

            // Opera
            "opera",

            // Ungoogled Chromium
            "ungoogled-chromium",

            // Thorium
            "thorium-browser",
            "thorium",

            // Helium?
            "helium",
            ];

            foreach (var candidate in candidates)
            {
                try
                {
                    // Use 'which' to find the binary in PATH
                    var result = Process.Start(new ProcessStartInfo
                    {
                        FileName = "which",
                        Arguments = candidate,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });

                    if (result != null)
                    {
                        string output = result.StandardOutput.ReadToEnd().Trim();
                        result.WaitForExit();
                        if (!string.IsNullOrEmpty(output) && File.Exists(output))
                            return output;
                    }
                }
                catch { }
            }

            string[] fixedPaths =
            [
            "/usr/bin/google-chrome",
            "/usr/bin/google-chrome-stable",
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser",
            "/usr/bin/microsoft-edge",
            "/usr/bin/brave-browser",
            "/usr/bin/vivaldi",
            "/usr/bin/opera",
            "/snap/bin/chromium",
            "/snap/bin/brave",
            "/opt/google/chrome/chrome",
            "/opt/microsoft/msedge/msedge",
            "/opt/brave.com/brave/brave-browser",
            "/opt/vivaldi/vivaldi",
            ];

            return fixedPaths.FirstOrDefault(File.Exists);
        }

        private static string? GetMacOsBrowserPath()
        {
            string[] paths =
            [
            // Google Chrome
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Google Chrome.app", "Contents", "MacOS", "Google Chrome"),

            // Microslop Edge
            "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",

            // Brave
            "/Applications/Brave Browser.app/Contents/MacOS/Brave Browser",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Brave Browser.app", "Contents", "MacOS", "Brave Browser"),

            // Vivaldi
            "/Applications/Vivaldi.app/Contents/MacOS/Vivaldi",

            // Opera
            "/Applications/Opera.app/Contents/MacOS/Opera",
            "/Applications/Opera GX.app/Contents/MacOS/Opera GX",

            // Arc
            "/Applications/Arc.app/Contents/MacOS/Arc",

            // Ungoogled Chromium
            "/Applications/Chromium.app/Contents/MacOS/Chromium",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Chromium.app", "Contents", "MacOS", "Chromium"),

            // Thorium
            "/Applications/Thorium.app/Contents/MacOS/Thorium",

            // Helium
            "/Applications/Helium.app/Contents/MacOS/Helium",
            ];

            return paths.FirstOrDefault(File.Exists);
        }

        private static async Task<AccountManagerAccount?> GetAccountInfoFromCookie(string securityCookie)
        {
            const string LOG_IDENT_GET_INFO = $"{LOG_IDENT}::GetAccountInfoFromCookie";

            try
            {
                var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
                handler.CookieContainer.Add(new System.Net.Cookie(".ROBLOSECURITY", securityCookie, "/", ".roblox.com"));

                using var client = new HttpClient(handler);

                long userId = 0;
                string username = string.Empty;
                string displayName = string.Empty;

                try
                {
                    var response = await client.GetAsync(UrlBuilder.BuildApiUrl("users", "v1/users/authenticated", secure: true));
                    response.EnsureSuccessStatusCode();

                    string json = await response.Content.ReadAsStringAsync();
                    var jo = JsonConvert.DeserializeObject<JObject>(json);

                    if (jo == null) return null;

                    userId = jo["id"]?.Value<long>() ?? 0;
                    username = jo["name"]?.Value<string>() ?? string.Empty;
                    displayName = jo["displayName"]?.Value<string>() ?? string.Empty;
                }
                catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException || ex.Message.Contains("canceled"))
                {
                    App.Logger.WriteLine(LOG_IDENT_GET_INFO, "Network socket not ready or canceled. skipping info fetch.");
                    return null;
                }

                return new AccountManagerAccount(securityCookie, userId, username, displayName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_GET_INFO, ex);
                return null;
            }
        }

        public static async Task<UserPresence?> GetUserPresenceAsync(long userId)
        {
            const string LOG_IDENT_PRESENCE = $"{LOG_IDENT}::GetUserPresence";

            try
            {
                var requestData = new { userIds = new[] { userId } };
                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(requestData);

                using var request = new HttpRequestMessage(HttpMethod.Post, UrlBuilder.BuildApiUrl("presence", "v1/presence/users", secure: true));
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var result = await Http.SendJson<UserPresenceResponse>(request).ConfigureAwait(false);

                return result?.UserPresences?.FirstOrDefault(x => x.UserId == userId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_PRESENCE, ex);
                return null;
            }
        }

        public static async Task<bool> ValidateAccountAsync(AccountManagerAccount account)
        {
            const string LOG_IDENT_VALIDATE = $"{LOG_IDENT}::ValidateAccount";

            try
            {
                string decryptedCookie = Unprotect(account.SecurityToken);

                if (string.IsNullOrEmpty(decryptedCookie))
                {
                    App.Logger.WriteLine(LOG_IDENT_VALIDATE, $"Account {account.Username}: No valid cookie found");
                    return false;
                }

                var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
                handler.CookieContainer.Add(new Cookie(".ROBLOSECURITY", decryptedCookie, "/", ".roblox.com"));

                using var client = new HttpClient(handler);
                var response = await client.GetAsync(UrlBuilder.BuildApiUrl("users", "v1/users/authenticated", secure: true));

                bool isValid = response.StatusCode == HttpStatusCode.OK;
                App.Logger.WriteLine(LOG_IDENT_VALIDATE, $"Account {account.Username}: {(isValid ? "Valid" : "Invalid")} (Status: {response.StatusCode})");

                return isValid;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_VALIDATE, ex);
                return false;
            }
        }

        public bool RemoveAccount(AccountManagerAccount account)
        {
            const string LOG_IDENT_REMOVE = $"{LOG_IDENT}::RemoveAccount";

            try
            {
                bool wasActive = (ActiveAccount?.UserId == account.UserId);
                int removed = _accounts.RemoveAll(a => a.UserId == account.UserId);

                if (removed > 0)
                {
                    if (wasActive)
                    {
                        if (_accounts.Count == 0)
                        {
                            ActiveAccount = null;
                            ActiveAccountChanged?.Invoke(null);
                        }
                        else
                        {
                            SetActiveAccount(_accounts.First().UserId);
                        }
                    }

                    SaveAccounts();

                    App.Logger.WriteLine(LOG_IDENT_REMOVE, $"Removed account {account.Username} ({account.UserId}).");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_REMOVE, ex);
                return false;
            }
        }

        private static async Task<string> GetCsrfToken(string cookie)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, UrlBuilder.BuildApiUrl("auth", "v1/logout", secure: true));
            request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");

            var resp = await App.HttpClient.SendAsync(request);
            return resp.Headers.TryGetValues("X-CSRF-TOKEN", out var tokens) ? tokens.First() : "";
        }

        public static async Task<string?> GetAuthTicket(string cookie, string csrf, long placeId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, UrlBuilder.BuildApiUrl("auth", "v1/authentication-ticket/", secure: true));
            request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
            request.Headers.Add("X-CSRF-TOKEN", csrf);
            request.Headers.Add("Referer", $"https://www.roblox.com/games/{placeId}/");
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var resp = await App.HttpClient.SendAsync(request);
            return resp.Headers.TryGetValues("rbx-authentication-ticket", out var vals) ? vals.First() : null;
        }

        public static async Task<string> JoinServer(AccountManagerAccount account, long placeId, string jobId = "", bool followUser = false, bool joinVip = false)
        {
            string csrf = await GetCsrfToken(account.SecurityToken);
            string? ticket = await GetAuthTicket(account.SecurityToken, csrf, placeId);

            if (string.IsNullOrEmpty(ticket))
                return "ERROR: Failed to obtain authentication ticket.";

            var browserTrackerId = new Random().Next(1000000, 9999999);
            var launchTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            string requestType = followUser ? "RequestFollowUser" : (joinVip ? "RequestPrivateGame" : "RequestGame");
            var launcherUri = UrlBuilder.BuildApiUrl("assetgame", $"game/PlaceLauncher.ashx", secure: true);
            var uriBuilder = new UriBuilder(launcherUri);
            var query = $"request={requestType}&placeId={placeId}";

            if (joinVip) query += $"&accessCode={jobId}";
            else if (!string.IsNullOrEmpty(jobId)) query += $"&gameId={jobId}";

            uriBuilder.Query = query;
            string launcherUrl = uriBuilder.Uri.ToString();

            string launchArgs = $"roblox-player:1+launchmode:play+gameinfo:{ticket}+launchtime:{launchTime}+placelauncherurl:{HttpUtility.UrlEncode(launcherUrl)}+browsertrackerid:{browserTrackerId}+LaunchExp:InApp";

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", $"\"{launchArgs}\"");
                }
                else
                {
                    Process.Start(new ProcessStartInfo(launchArgs) { UseShellExecute = true });
                }
                return "Success";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public async Task<Dictionary<long, string?>> GetAvatarUrlsBulkAsync(List<long> userIds)
        {
            const string LOG_IDENT_AVATARS = $"{LOG_IDENT}::GetAvatarUrlsBulk";
            var result = new Dictionary<long, string?>();
            if (userIds == null || userIds.Count == 0) return result;

            const int batchSize = 100;

            for (int i = 0; i < userIds.Count; i += batchSize)
            {
                var batch = userIds.Skip(i).Take(batchSize).ToList();
                string idsParam = string.Join(',', batch);
                var uriBuilder = new UriBuilder(UrlBuilder.BuildApiUrl("thumbnails", "v1/users/avatar-headshot", secure: true))
                {
                    Query = $"userIds={idsParam}&size=75x75&format=Png&isCircular=true"
                };
                Uri url = uriBuilder.Uri;

                try
                {
                    var response = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(url);

                    if (response?.Data != null)
                    {
                        foreach (var item in response.Data)
                        {
                            if (item.TargetId > 0 && !string.IsNullOrEmpty(item.ImageUrl))
                            {
                                result[item.TargetId] = item.ImageUrl;
                                _avatarUrlCache[item.TargetId] = item.ImageUrl;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    App.Logger.WriteLine(LOG_IDENT_AVATARS, "Avatar fetch was canceled by the system (SocketException 89).");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_AVATARS, $"Batch failed: {ex.Message}");
                }
            }

            return result;
        }

        public string? GetCachedAvatarUrl(long userId)
        {
            return _avatarUrlCache.TryGetValue(userId, out var url) ? url : null;
        }

        public void AddAccount(AccountManagerAccount account)
        {
            if (_accounts.Any(a => a.UserId == account.UserId))
                return;
            _accounts.Add(account);
            SaveAccounts();
        }
    }
}