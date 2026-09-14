// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using FluentAvalonia.UI.Controls;
using Froststrap.UI.Elements.Dialogs;
using Froststrap.UI.Elements.Settings;

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

                if (executablePath != null)
                {
                    MainWindow.ShowGlobalNotification(
                        "Compatible browser found!",
                        "Launching browser for login.",
                        FAInfoBarSeverity.Success
                    );
                }

                if (executablePath == null)
                {
                    var fetcher = new BrowserFetcher();
                    var installed = fetcher.GetInstalledBrowsers().FirstOrDefault(b => b.Browser == SupportedBrowser.Chromium);
                    if (installed != null) executablePath = installed.GetExecutablePath();

                    if (executablePath == null)
                    {
                        App.Logger.Info("No browser found, downloading Chromium...");
                        MainWindow.ShowGlobalNotification(
                            "No compatible browser found",
                            "Please wait while we download Chromium for login.",
                            FAInfoBarSeverity.Warning
                        );
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
                mainPage.Response += (_, e) => ObserveRedirectBody(e.Response);

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

        private static void ObserveRedirectBody(IResponse response)
        {
            if ((int)response.Status is < 300 or >= 400)
                return;

            _ = Task.Run(async () =>
            {
                try { await response.BufferAsync(); }
                catch { /* expected: the body is unavailable for redirect responses */ }
            });
        }

        private static readonly string[] CompatibleBrowsers =
        [
            "chrome",
            "google-chrome",
            "google-chrome-stable",
            "chromium",
            "chromium-browser",
            "brave",
            "brave-browser",
            "edge",
            "msedge",
            "microsoft-edge",
            "vivaldi",
            "opera",
            "opera-stable",
            "helium",
            "thorium",
        ];

        private static bool IsCompatibleBrowser(string? executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                return false;

            string executableName = Path.GetFileNameWithoutExtension(executablePath);

            return CompatibleBrowsers.Any(browser =>
                executableName.Equals(browser, StringComparison.OrdinalIgnoreCase));
        }

        private static string? GetSystemBrowserPath()
        {
            string? browserPath = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                browserPath = GetWindowsBrowserPath();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                browserPath = GetLinuxBrowserPath();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                browserPath = GetMacOsBrowserPath();

            return IsCompatibleBrowser(browserPath) ? browserPath : null;
        }

        private static string? GetWindowsBrowserPath()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");

                string? progId = key?.GetValue("ProgId")?.ToString();

                if (string.IsNullOrWhiteSpace(progId))
                    return null;

                using var commandKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(
                    $@"{progId}\shell\open\command");

                string? command = commandKey?.GetValue(null)?.ToString();

                return GetExecutableFromCommand(command);
            }
            catch
            {
                return null;
            }
        }

        private static string? GetLinuxBrowserPath()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-mime",
                    Arguments = "query default x-scheme-handler/http",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process == null)
                    return null;

                string desktopFile = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (string.IsNullOrWhiteSpace(desktopFile))
                    return null;

                string[] dataDirectories =
                [
                    Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                        ?? Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".local",
                            "share"),

                    ..(Environment.GetEnvironmentVariable("XDG_DATA_DIRS")
                        ?? "/usr/local/share:/usr/share")
                        .Split(':', StringSplitOptions.RemoveEmptyEntries)
                ];

                foreach (string directory in dataDirectories)
                {
                    string desktopPath = Path.Combine(directory, "applications", desktopFile);

                    if (!File.Exists(desktopPath))
                        continue;

                    string? exec = File.ReadLines(desktopPath)
                        .Select(line => line.Trim())
                        .FirstOrDefault(line =>
                            line.StartsWith("Exec=", StringComparison.OrdinalIgnoreCase));

                    string? executable = GetExecutableFromCommand(
                        exec?["Exec=".Length..]);

                    if (executable != null)
                        return executable;
                }
            }
            catch
            {
            }

            return null;
        }

        private static string? GetMacOsBrowserPath()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "defaults",
                    Arguments = "read com.apple.LaunchServices/com.apple.launchservices.secure LSHandlers",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process == null)
                    return null;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string? bundleIdentifier = null;
                bool isHttpHandler = false;

                foreach (string line in output.Split('\n'))
                {
                    string trimmed = line.Trim();

                    if (trimmed.Contains(
                            "\"LSHandlerURLScheme\" = http",
                            StringComparison.Ordinal))
                    {
                        isHttpHandler = true;
                    }
                    else if (isHttpHandler &&
                             trimmed.StartsWith(
                                 "LSHandlerRoleAll = ",
                                 StringComparison.Ordinal))
                    {
                        bundleIdentifier = trimmed
                            .Replace(
                                "LSHandlerRoleAll = ",
                                "",
                                StringComparison.Ordinal)
                            .Trim()
                            .TrimEnd(';')
                            .Trim('"');

                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(bundleIdentifier))
                    return null;

                using var findProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "mdfind",
                    Arguments = $"kMDItemCFBundleIdentifier == '{bundleIdentifier}'",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (findProcess == null)
                    return null;

                string applicationPath = findProcess.StandardOutput
                    .ReadToEnd()
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? "";

                findProcess.WaitForExit();

                if (string.IsNullOrWhiteSpace(applicationPath))
                    return null;

                string? executableName = GetPlistValue(
                    Path.Combine(applicationPath, "Contents", "Info.plist"),
                    "CFBundleExecutable");

                if (string.IsNullOrWhiteSpace(executableName))
                    return null;

                string executablePath = Path.Combine(
                    applicationPath,
                    "Contents",
                    "MacOS",
                    executableName);

                return File.Exists(executablePath) ? executablePath : null;
            }
            catch
            {
                return null;
            }
        }

        private static string? GetExecutableFromCommand(string? command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return null;

            command = command.Trim();

            string executable;

            if (command.StartsWith('"'))
            {
                int endQuote = command.IndexOf('"', 1);

                if (endQuote <= 1)
                    return null;

                executable = command[1..endQuote];
            }
            else
            {
                executable = command
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? "";
            }

            if (string.IsNullOrWhiteSpace(executable))
                return null;

            if (executable.Equals("flatpak", StringComparison.OrdinalIgnoreCase) ||
                executable.Equals("snap", StringComparison.OrdinalIgnoreCase))
                return null;

            if (Path.IsPathFullyQualified(executable))
                return File.Exists(executable) ? executable : null;

            string? path = Environment.GetEnvironmentVariable("PATH");

            if (string.IsNullOrWhiteSpace(path))
                return null;

            foreach (string directory in path.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries))
            {
                string executablePath = Path.Combine(directory, executable);

                if (File.Exists(executablePath))
                    return executablePath;
            }

            return null;
        }

        private static string? GetPlistValue(string plistPath, string key)
        {
            if (!File.Exists(plistPath))
                return null;

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "defaults",
                Arguments = $"read \"{plistPath}\" {key}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
                return null;

            string value = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
