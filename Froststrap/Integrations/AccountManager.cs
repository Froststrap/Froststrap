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
    internal class AccountManager
    {
        private const string AccountsFile = "AccountManager.json";

        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        public event Action<AccountManagerAccount?>? ActiveAccountChanged;

        private readonly string _accountsLocation;
        private List<AccountManagerAccount> _accounts = [];
        private readonly Dictionary<long, string?> _avatarUrlCache = [];

        private Browser? _browser;

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
                    Encoding.UTF8.GetBytes(text), null, DataProtectionScope.CurrentUser));
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
                    Convert.FromBase64String(text), null, DataProtectionScope.CurrentUser));
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
            catch (Exception ex) { App.Logger.Error("Unhandled exception: ", ex); }
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
            catch (Exception ex) { App.Logger.Error("Unhandled exception: ", ex); }
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
        public static async Task<AccountManagerAccount?> AddAccountByQuickSignInAsync(
            QuickSignCodeDialog dialog,
            CancellationToken cancellationToken)
        {
            try
            {
                using var client = new HttpClient();

                // --- Step 1: Create sign-in code ---
                var createUrl = UrlBuilder.BuildApiUrl("apis", "auth-token-service/v1/login/create", secure: true);
                using var createContent = new StringContent("{}", Encoding.UTF8, "application/json");

                HttpResponseMessage? createResponse = null;
                try
                {
                    createResponse = await client.PostAsync(createUrl, createContent, cancellationToken);
                    createResponse.EnsureSuccessStatusCode();

                    var createJson = JObject.Parse(await createResponse.Content.ReadAsStringAsync(cancellationToken));
                    string code = createJson["code"]!.Value<string>()!;
                    string privateKey = createJson["privateKey"]!.Value<string>()!;
                    DateTime expirationTime = createJson["expirationTime"]!.Value<DateTime>();

                    await Dispatcher.UIThread.InvokeAsync(() => dialog.StartNewSignIn(code));

                    // --- Step 2: Poll for status ---
                    var statusUrl = UrlBuilder.BuildApiUrl("apis", "auth-token-service/v1/login/status", secure: true);
                    string? status = null;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(4000, cancellationToken);

                        var statusPayload = new { code, privateKey };
                        using var statusContent = new StringContent(
                            JsonConvert.SerializeObject(statusPayload), Encoding.UTF8, "application/json");

                        HttpResponseMessage? statusResponse = null;
                        try
                        {
                            statusResponse = await client.PostAsync(statusUrl, statusContent, cancellationToken);

                            // Retry with CSRF token if needed
                            if ((statusResponse.StatusCode == HttpStatusCode.Forbidden ||
                                 statusResponse.StatusCode == HttpStatusCode.BadRequest) &&
                                statusResponse.Headers.TryGetValues("x-csrf-token", out var csrfVals))
                            {
                                string csrfToken = csrfVals.First();
                                using var retryRequest = new HttpRequestMessage(HttpMethod.Post, statusUrl)
                                {
                                    Content = statusContent
                                };
                                retryRequest.Headers.Add("x-csrf-token", csrfToken);
                                statusResponse.Dispose();
                                statusResponse = await client.SendAsync(retryRequest, cancellationToken);
                            }

                            string body = await statusResponse.Content.ReadAsStringAsync(cancellationToken);

                            // ---- Process response ----
                            if (statusResponse.StatusCode == HttpStatusCode.BadRequest)
                            {
                                if (body.Trim().StartsWith('{'))
                                {
                                    var errJson = JObject.Parse(body);
                                    var errorMsg = errJson["errors"]?[0]?["message"]?.Value<string>() ?? "Unknown error";
                                    App.Logger.Error($"Status API returned error: {errorMsg}");
                                    await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus("Cancelled"));
                                }
                                else if (body.Trim().Equals("\"CodeInvalid\"", StringComparison.OrdinalIgnoreCase) ||
                                         body.Trim().Equals("CodeInvalid", StringComparison.OrdinalIgnoreCase))
                                {
                                    App.Logger.Info("Code invalid/expired.");
                                    await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus("Cancelled"));
                                }
                                else
                                {
                                    App.Logger.Warn($"Unexpected 400 response: {body}");
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
                                App.Logger.Warn($"Status endpoint returned non‑JSON: {body}");
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
                                    App.Logger.Warn($"API error: {errorMessage}");
                                    await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus($"Error: {errorMessage}"));
                                    return null;
                                }

                                App.Logger.Warn($"Missing 'status' field in response: {body}");
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
                                App.Logger.Info("Code timed out.");
                                await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus("TimedOut"));
                                return null;
                            }
                        }
                        finally
                        {
                            statusResponse?.Dispose();
                        }
                    }

                    // ---- If cancelled or invalid ----
                    if (cancellationToken.IsCancellationRequested || status == "Cancelled")
                        return null;

                    // --- Step 3: Final login ---
                    var loginUrl = UrlBuilder.BuildApiUrl("auth", "v2/login", secure: true);
                    var loginData = new
                    {
                        ctype = "AuthToken",
                        cvalue = code,
                        password = privateKey
                    };
                    using var loginContent = new StringContent(
                        JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");

                    using var cookieHandler = new HttpClientHandler
                    {
                        CookieContainer = new CookieContainer(),
                        UseCookies = true,
                        CheckCertificateRevocationList = true
                    };
                    using var loginClient = new HttpClient(cookieHandler);

                    HttpResponseMessage? loginResponse = null;
                    try
                    {
                        loginResponse = await loginClient.PostAsync(loginUrl, loginContent, cancellationToken);

                        // Retry with CSRF token if needed
                        if ((loginResponse.StatusCode == HttpStatusCode.Forbidden ||
                             loginResponse.StatusCode == HttpStatusCode.BadRequest) &&
                            loginResponse.Headers.TryGetValues("x-csrf-token", out var csrfValues))
                        {
                            string csrfToken = csrfValues.First();
                            using var retryRequest = new HttpRequestMessage(HttpMethod.Post, loginUrl)
                            {
                                Content = loginContent
                            };
                            retryRequest.Headers.Add("x-csrf-token", csrfToken);
                            loginResponse.Dispose();
                            loginResponse = await loginClient.SendAsync(retryRequest, cancellationToken);
                        }

                        loginResponse.EnsureSuccessStatusCode();

                        var cookies = cookieHandler.CookieContainer.GetCookies(new Uri("https://roblox.com"));
                        string? robloSecurity = cookies[".ROBLOSECURITY"]?.Value;

                        if (string.IsNullOrEmpty(robloSecurity))
                        {
                            App.Logger.Warn("No .ROBLOSECURITY cookie in response.");
                            await Dispatcher.UIThread.InvokeAsync(() =>
                                dialog.UpdateStatus("Failed: no cookie received"));
                            return null;
                        }

                        var account = await GetAccountInfoFromCookie(robloSecurity);
                        if (account == null)
                        {
                            App.Logger.Warn("Failed: invalid account");
                            await Dispatcher.UIThread.InvokeAsync(() =>
                                dialog.UpdateStatus("Failed: invalid account"));
                            return null;
                        }

                        await Dispatcher.UIThread.InvokeAsync(() => dialog.CompleteSignIn());
                        return account;
                    }
                    finally
                    {
                        loginResponse?.Dispose();
                    }
                }
                finally
                {
                    createResponse?.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                await Dispatcher.UIThread.InvokeAsync(() =>
                    dialog.UpdateStatus($"Error: {ex.Message}"));
                return null;
            }
        }

        public async Task<AccountManagerAccount?> AddAccountByBrowser()
        {
            var completionSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                App.Logger.Info("Launching browser for account login...");

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
                        App.Logger.Info("No browser found, downloading Chromium...");
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
                                App.Logger.Info("Successfully captured cookie.");
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
                    App.Logger.Info("Navigating to Roblox...");
                    await mainPage.GoToAsync("https://www.roblox.com/login", new NavigationOptions
                    {
                        WaitUntil = [WaitUntilNavigation.Networkidle2]
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Initial nav failed ({ex.Message}), trying JS fallback...");
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
                    App.Logger.Info("Login timed out after 10 minutes.");
                }

                if (string.IsNullOrEmpty(newCookie))
                {
                    App.Logger.Info("Account add process cancelled or failed.");
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
                App.Logger.Error(ex);
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
            try
            {
                using var handler = new HttpClientHandler
                {
                    CookieContainer = new System.Net.CookieContainer(),
                    CheckCertificateRevocationList = true
                };
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
                catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException || ex.Message.Contains("canceled", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.Info("Network socket not ready or canceled. skipping info fetch.");
                    return null;
                }

                return new AccountManagerAccount(securityCookie, userId, username, displayName);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                return null;
            }
        }

        public static async Task<UserPresence?> GetUserPresenceAsync(long userId)
        {
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
                App.Logger.Error(ex);
                return null;
            }
        }

        public static async Task<bool?> ValidateAccountAsync(AccountManagerAccount account)
        {
            try
            {
                string decryptedCookie = Unprotect(account.SecurityToken);
                if (string.IsNullOrEmpty(decryptedCookie))
                {
                    App.Logger.Info($"Account {account.Username}: No valid cookie found");
                    return false;
                }

                using var handler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer(),
                    CheckCertificateRevocationList = true
                };
                handler.CookieContainer.Add(new Cookie(".ROBLOSECURITY", decryptedCookie, "/", ".roblox.com"));

                using var client = new HttpClient(handler);
                var response = await client.GetAsync(UrlBuilder.BuildApiUrl("users", "v1/users/authenticated", secure: true));

                if (response.StatusCode == HttpStatusCode.OK)
                    return true;
                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                    return false;

                App.Logger.Info($"Account {account.Username}: Unexpected status {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                return null;
            }
        }

        public bool RemoveAccount(AccountManagerAccount account)
        {
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

                    App.Logger.Info($"Removed account {account.Username} ({account.UserId}).");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                return false;
            }
        }

        public async Task<Dictionary<long, string?>> GetAvatarUrlsBulkAsync(List<long> userIds)
        {
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
                    App.Logger.Info("Avatar fetch was canceled by the system (SocketException 89).");
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Batch failed: {ex.Message}");
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

        public static bool WriteCookieFileForAccount(AccountManagerAccount account)
        {
            string plainCookie = account.SecurityToken;
            if (string.IsNullOrEmpty(plainCookie))
            {
                App.Logger.Info("Account has no valid cookie.");
                return false;
            }

            string filePath = CookiesManager.CookiesPath;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return WriteWindowsCookieFile(plainCookie, filePath);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    return WriteMacCookieFile(plainCookie, filePath);
                }
                else if (OperatingSystem.IsLinux())
                {
                    return WriteLinuxCookieFile(plainCookie, filePath);
                }
                else
                {
                    App.Logger.Info("Unsupported OS.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                return false;
            }
        }

        private static bool WriteWindowsCookieFile(string plainCookie, string filePath)
        {
            string guestData = GenerateGuestData();
            string trackerData = GenerateTrackerData();

            string fullNetscape =
                $"#HttpOnly_.roblox.com\tTRUE\t/\tFALSE\t0\tGuestData\t{guestData}\n" +
                $"#HttpOnly_.roblox.com\tTRUE\t/\tFALSE\t0\tRBXEventTrackerV2\t{trackerData}\n" +
                $"#HttpOnly_.roblox.com\tTRUE\t/\tTRUE\t1817995500\t.ROBLOSECURITY\t{plainCookie}";

            byte[] encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(fullNetscape),
                null,
                DataProtectionScope.CurrentUser);

            var cookieData = new RobloxCookies
            {
                Version = "1",
                Cookies = Convert.ToBase64String(encrypted)
            };

            string json = System.Text.Json.JsonSerializer.Serialize(cookieData, _jsonOptions);
            BackupFile(filePath);
            File.WriteAllText(filePath, json);
            return true;
        }

        private static bool WriteMacCookieFile(string plainCookie, string filePath)
        {
            var staticCookies = new[]
            {
                new BinaryCookie("rbx-ip2", "rbx-ip2"),
                new BinaryCookie("RBXPaymentsFlowContext", "98619ec1-af61-4739-a12b-c8fc4abbab79,"),
                new BinaryCookie("rbxas", "40ac424f7d0844b99d92179bdb9e22341d7e2c49cc7ae6c5101cca1c9a67b365"),
                new BinaryCookie("ARID", "fc r502pFLAl14bk47SYcQoDVdTFrOeqwK6AxopmW+t1Ke66bZfJX7J3KniMv+tn8NuVRLyJkFkO5z3XXzJm1ANOvLfHLkBv9q1aIoq/MT4rKYzZ6KXnZb+xnOMsu43OExsFnQhxZy2LCOzbYJMnuidqjZSIH6qQSU=**uKVnWDwQcFDl0aet")
            };

            var guestCookie = new BinaryCookie("GuestData", GenerateGuestData());
            var trackerCookie = new BinaryCookie("RBXEventTrackerV2", GenerateTrackerData());
            var securityCookie = new BinaryCookie(".ROBLOSECURITY", plainCookie, isSecure: true)
            {
                Expiry = MacTimeFromDateTime(DateTime.UtcNow.AddDays(365))
            };

            var allCookies = new List<BinaryCookie>(staticCookies) { guestCookie, trackerCookie, securityCookie };

            for (int i = 0; i < allCookies.Count; i++)
            {
                var c = allCookies[i];
                c.Domain = ".roblox.com";
                c.Path = "/";
                if (!c.Expiry.HasValue) c.Expiry = 0;
                if (!c.Creation.HasValue) c.Creation = MacTimeFromDateTime(DateTime.UtcNow);
                allCookies[i] = c;
            }

            byte[] binaryData = SerializeBinaryCookies(allCookies);
            BackupFile(filePath);
            File.WriteAllBytes(filePath, binaryData);
            return true;
        }

        private struct BinaryCookie
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Domain { get; set; }
            public string Path { get; set; }
            public int Flags { get; set; }
            public double? Expiry { get; set; }
            public double? Creation { get; set; }

            public BinaryCookie(string name, string value, bool isSecure = false)
            {
                Name = name;
                Value = value;
                Domain = ".roblox.com";
                Path = "/";
                Flags = isSecure ? 1 : 0;
                Expiry = null;
                Creation = null;
            }
        }

        private static byte[] SerializeBinaryCookies(List<BinaryCookie> cookies)
        {
            var records = new List<byte[]>();
            foreach (var cookie in cookies)
                records.Add(BuildCookieRecord(cookie));

            int numCookies = records.Count;
            int pageHeaderSize = 4 + 4 + numCookies * 4;
            int totalPageSize = pageHeaderSize + records.Sum(b => b.Length);

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(0x00000100);
            writer.Write(numCookies);
            int offset = pageHeaderSize;
            foreach (var rec in records)
            {
                writer.Write(offset);
                offset += rec.Length;
            }
            foreach (var rec in records)
                writer.Write(rec);

            var pageData = ms.ToArray();

            using var finalMs = new MemoryStream();
            using var finalWriter = new BinaryWriter(finalMs);
            finalWriter.Write([0x63, 0x6F, 0x6F, 0x6B]);
            finalWriter.Write(IPAddress.HostToNetworkOrder(1));
            finalWriter.Write(IPAddress.HostToNetworkOrder(totalPageSize));
            finalWriter.Write(pageData);
            return finalMs.ToArray();
        }

        private static byte[] BuildCookieRecord(BinaryCookie cookie)
        {
            byte[] domainBytes = Encoding.UTF8.GetBytes(cookie.Domain + "\0");
            byte[] nameBytes = Encoding.UTF8.GetBytes(cookie.Name + "\0");
            byte[] pathBytes = Encoding.UTF8.GetBytes(cookie.Path + "\0");
            byte[] valueBytes = Encoding.UTF8.GetBytes(cookie.Value + "\0");

            int fixedSize = 52;
            int totalSize = fixedSize + domainBytes.Length + nameBytes.Length + pathBytes.Length + valueBytes.Length;
            byte[] record = new byte[totalSize];
            using var ms = new MemoryStream(record);
            using var writer = new BinaryWriter(ms);

            writer.Write(0);
            writer.Write(cookie.Flags);
            writer.Write(new byte[12]);

            int currentOffset = fixedSize;
            int urlOffset = currentOffset;
            int nameOffset = urlOffset + domainBytes.Length;
            int pathOffset = nameOffset + nameBytes.Length;
            int valueOffset = pathOffset + pathBytes.Length;

            writer.Write(urlOffset);
            writer.Write(nameOffset);
            writer.Write(pathOffset);
            writer.Write(valueOffset);
            writer.Write(cookie.Expiry ?? 0.0);
            writer.Write(cookie.Creation ?? 0.0);

            writer.Write(domainBytes);
            writer.Write(nameBytes);
            writer.Write(pathBytes);
            writer.Write(valueBytes);

            return record;
        }

        private static double MacTimeFromDateTime(DateTime dt)
        {
            DateTime epoch = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (dt.ToUniversalTime() - epoch).TotalSeconds;
        }

        private static bool WriteLinuxCookieFile(string plainCookie, string filePath)
        {
            string content = $".ROBLOSECURITY={plainCookie};";
            BackupFile(filePath);
            File.WriteAllText(filePath, content);
            return true;
        }

        private static string GenerateGuestData()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            int randomPositive = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
            int userId = -(randomPositive % 999999999) - 1;
            return $"UserID={userId};";
        }

        private static string GenerateTrackerData()
        {
            string createDate = DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            long browserId = GenerateRandomBrowserId();
            return $"CreateDate={createDate}&rbxid=&rbxuid=&browserid={browserId};";
        }

        private static long GenerateRandomBrowserId()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] bytes = new byte[8];
            rng.GetBytes(bytes);
            long value = BitConverter.ToInt64(bytes, 0) & long.MaxValue;
            if (value < 1_000_000_000_000_000L)
                value += 1_000_000_000_000_000L;
            return value;
        }

        private static void BackupFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try { File.Copy(filePath, filePath + ".bak", overwrite: true); }
                catch { /* ignore */ }
            }
        }
    }
}
