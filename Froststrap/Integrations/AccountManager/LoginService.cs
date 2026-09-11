// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using Froststrap.UI.Elements.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace Froststrap.Integrations.AccountManager
{
    internal class LoginService
    {
        private Browser? _browser;

        // https://devforum.roblox.com/t/how-to-generate-a-roblosecurity-token-from-quick-login/3147931
        public static async Task<AccountManagerAccount?> AddAccountByQuickSignInAsync(QuickSignCodeDialog dialog, CancellationToken cancellationToken)
        {
            try
            {
                using var client = new HttpClient();

                var createUrl = UrlBuilder.BuildApiUrl("apis", "auth-token-service/v1/login/create", secure: true);
                using var createContent = new StringContent("{}", Encoding.UTF8, "application/json");

                using var createResponse = await client.PostAsync(createUrl, createContent, cancellationToken);
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
                    using var statusContent = new StringContent(JsonConvert.SerializeObject(statusPayload), Encoding.UTF8, "application/json");

                    using var statusResponse = await client.PostAsync(statusUrl, statusContent, cancellationToken);

                    if ((statusResponse.StatusCode == HttpStatusCode.Forbidden || statusResponse.StatusCode == HttpStatusCode.BadRequest) &&
                        statusResponse.Headers.TryGetValues("x-csrf-token", out var csrfVals))
                    {
                        string csrfToken = csrfVals.First();
                        using var retryRequest = new HttpRequestMessage(HttpMethod.Post, statusUrl) { Content = statusContent };
                        retryRequest.Headers.Add("x-csrf-token", csrfToken);
                        using var retryResponse = await client.SendAsync(retryRequest, cancellationToken);
                        status = await ProcessStatusBody(retryResponse, dialog);
                    }
                    else
                    {
                        status = await ProcessStatusBody(statusResponse, dialog);
                    }

                    if (status == "Validated" || status == "Cancelled") break;

                    if (DateTime.UtcNow > expirationTime)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus("TimedOut"));
                        return null;
                    }
                }

                if (cancellationToken.IsCancellationRequested || status == "Cancelled") return null;

                return await PerformFinalLoginAsync(code, privateKey, dialog, cancellationToken);
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus($"Error: {ex.Message}"));
                return null;
            }
        }

        private static async Task<string?> ProcessStatusBody(HttpResponseMessage response, QuickSignCodeDialog dialog)
        {
            string body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus("Cancelled"));
                return "Cancelled";
            }

            var statusJson = JObject.Parse(body);
            string? status = (string?)statusJson["status"];
            string? accountName = (string?)statusJson["accountName"];

            await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus(status ?? "Error", accountName));
            return status;
        }

        private static async Task<AccountManagerAccount?> PerformFinalLoginAsync(string code, string privateKey, QuickSignCodeDialog dialog, CancellationToken token)
        {
            var loginUrl = UrlBuilder.BuildApiUrl("auth", "v2/login", secure: true);
            var loginData = new { ctype = "AuthToken", cvalue = code, password = privateKey };
            using var loginContent = new StringContent(JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");

            using var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                CheckCertificateRevocationList = true
            };
            using var client = new HttpClient(handler);

            HttpResponseMessage? loginResponse = null;
            try
            {
                loginResponse = await client.PostAsync(loginUrl, loginContent, token);

                if ((loginResponse.StatusCode == HttpStatusCode.Forbidden || loginResponse.StatusCode == HttpStatusCode.BadRequest) &&
                    loginResponse.Headers.TryGetValues("x-csrf-token", out var csrfValues))
                {
                    string csrfToken = csrfValues.First();

                    using var retryContent = new StringContent(JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");
                    using var retryRequest = new HttpRequestMessage(HttpMethod.Post, loginUrl)
                    {
                        Content = retryContent
                    };
                    retryRequest.Headers.Add("x-csrf-token", csrfToken);

                    loginResponse.Dispose();
                    loginResponse = await client.SendAsync(retryRequest, token);
                }

                loginResponse.EnsureSuccessStatusCode();

                var cookies = handler.CookieContainer.GetCookies(new Uri("https://roblox.com"));
                string? robloSecurity = cookies[".ROBLOSECURITY"]?.Value;

                if (string.IsNullOrEmpty(robloSecurity)) return null;

                var account = await RobloxApiService.GetAccountInfoFromCookieAsync(robloSecurity);
                if (account != null)
                    await Dispatcher.UIThread.InvokeAsync(() => dialog.CompleteSignIn());

                return account;
            }
            finally
            {
                loginResponse?.Dispose();
            }
        }

        public async Task<AccountManagerAccount?> AddAccountByBrowserAsync(Func<string, Task<AccountManagerAccount?>> accountResolver)
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
                        var specificPath = Path.Combine(Paths.LocalAppData, "PuppeteerSharp");
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
                    Args = ["--disable-notifications", "--no-sandbox", "--disable-setuid-sandbox", "--disable-blink-features=AutomationControlled"],
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
                    catch { /* Page closed */ }
                });

                try
                {
                    await mainPage.GoToAsync("https://www.roblox.com/login", new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle2] });
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Initial nav failed ({ex.Message}), trying JS fallback...");
                    try
                    {
                        if (!mainPage.IsClosed)
                            await mainPage.EvaluateExpressionAsync("window.location.href = 'https://www.roblox.com/login'");
                    }
                    catch { }
                }

                var resultTask = await Task.WhenAny(completionSource.Task, Task.Delay(TimeSpan.FromMinutes(10)));
                string? newCookie = resultTask == completionSource.Task ? await completionSource.Task : null;

                if (string.IsNullOrEmpty(newCookie)) return null;

                return await accountResolver(newCookie);
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

        private static string? GetSystemBrowserPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return GetWindowsBrowserPath();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return GetLinuxBrowserPath();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return GetMacOsBrowserPath();
            return null;
        }

        private static string? GetWindowsBrowserPath()
        {
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string[] paths =
            [
                Path.Combine(pf, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(pfx86, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(pf, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(pfx86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(pf, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
                Path.Combine(local, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
                Path.Combine(pf, "Vivaldi", "Application", "vivaldi.exe"),
                Path.Combine(local, "Programs", "Opera", "opera.exe"),
                Path.Combine(local, "Programs", "Opera GX", "opera.exe"),
                Path.Combine(local, "Programs", "Arc", "Arc.exe"),
            ];

            return paths.FirstOrDefault(File.Exists);
        }

        private static string? GetLinuxBrowserPath()
        {
            string[] candidates = ["google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "microsoft-edge", "brave-browser", "vivaldi", "opera"];

            foreach (var candidate in candidates)
            {
                try
                {
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
                        if (!string.IsNullOrEmpty(output) && File.Exists(output)) return output;
                    }
                }
                catch { }
            }

            string[] fixedPaths = ["/usr/bin/google-chrome", "/usr/bin/chromium", "/usr/bin/microsoft-edge", "/usr/bin/brave-browser", "/snap/bin/chromium"];
            return fixedPaths.FirstOrDefault(File.Exists);
        }

        private static string? GetMacOsBrowserPath()
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] paths =
            [
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                Path.Combine(userHome, "Applications", "Google Chrome.app", "Contents", "MacOS", "Google Chrome"),
                "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
                "/Applications/Brave Browser.app/Contents/MacOS/Brave Browser",
                "/Applications/Vivaldi.app/Contents/MacOS/Vivaldi",
                "/Applications/Opera.app/Contents/MacOS/Opera",
                "/Applications/Arc.app/Contents/MacOS/Arc"
            ];

            return paths.FirstOrDefault(File.Exists);
        }
    }
}
