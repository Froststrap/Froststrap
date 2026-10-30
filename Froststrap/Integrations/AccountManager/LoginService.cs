// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Froststrap.UI.Elements.Dialogs;
using System.Text.Json.Nodes;

namespace Froststrap.Integrations.AccountManager
{
    internal class LoginService
    {
        private static readonly Uri LoginUri = new("https://www.roblox.com/login");

        private const string LoadingHtml = """
            <!doctype html>
            <html>
            <head>
            <meta charset="utf-8">
            <style>
            html, body { height: 100%; margin: 0; }
            body {
                display: flex;
                flex-direction: column;
                align-items: center;
                justify-content: center;
                gap: 14px;
                background: #101014;
                color: #e6e6e6;
                font-family: "Segoe UI", system-ui, sans-serif;
            }
            .spinner {
                width: 28px;
                height: 28px;
                border: 3px solid #2b2b33;
                border-top-color: #8b5cf6;
                border-radius: 50%;
                animation: spin 0.8s linear infinite;
            }
            p { margin: 0; font-size: 14px; opacity: 0.8; }
            @keyframes spin { to { transform: rotate(360deg); } }
            </style>
            </head>
            <body><div class="spinner"></div><p>Preparing sign-in...</p></body>
            </html>
            """;

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

                var createJson = JsonNode.Parse(await createResponse.Content.ReadAsStringAsync(cancellationToken))!;
                string code = createJson["code"]!.GetValue<string>();
                string privateKey = createJson["privateKey"]!.GetValue<string>();
                DateTime expirationTime = createJson["expirationTime"]!.GetValue<DateTime>();

                await Dispatcher.UIThread.InvokeAsync(() => dialog.StartNewSignIn(code));

                var statusUrl = UrlBuilder.BuildApiUrl("apis", "auth-token-service/v1/login/status", secure: true);
                string? status = null;

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(4000, cancellationToken);

                    var statusPayload = new LoginCodeRequest { Code = code, PrivateKey = privateKey };
                    using var statusContent = new StringContent(JsonHelper.Serialize(statusPayload), Encoding.UTF8, "application/json");

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

            var statusJson = JsonNode.Parse(body);
            string? status = statusJson?["status"]?.GetValue<string>();
            string? accountName = statusJson?["accountName"]?.GetValue<string>();

            await Dispatcher.UIThread.InvokeAsync(() => dialog.UpdateStatus(status ?? "Error", accountName));
            return status;
        }

        private static async Task<AccountManagerAccount?> PerformFinalLoginAsync(string code, string privateKey, QuickSignCodeDialog dialog, CancellationToken token)
        {
            var loginUrl = UrlBuilder.BuildApiUrl("auth", "v2/login", secure: true);
            var loginData = new LoginTokenRequest { Ctype = "AuthToken", Cvalue = code, Password = privateKey };
            using var loginContent = new StringContent(JsonHelper.Serialize(loginData), Encoding.UTF8, "application/json");

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

                    using var retryContent = new StringContent(JsonHelper.Serialize(loginData), Encoding.UTF8, "application/json");
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

        public static async Task<AccountManagerAccount?> AddAccountByBrowserAsync(Func<string, Task<AccountManagerAccount?>> accountResolver)
        {
            NativeWebDialog? dialog = null;
            var completionSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var loginTimeout = TimeSpan.FromMinutes(10);
            var cookieTimeout = TimeSpan.FromSeconds(5);

            try
            {
                App.Logger.Info("Opening web view for account login...");

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    dialog = new NativeWebDialog
                    {
                        Title = "Preparing sign-in...",
                        CanUserResize = true,
                        DefaultBackground = Avalonia.Media.Color.Parse("#101014")
                    };

                    dialog.Resize(520, 720);
                    dialog.NavigateToString(LoadingHtml);

                    if (!string.IsNullOrEmpty(Paths.DataRoot))
                    {
                        dialog.EnvironmentRequested += (_, args) =>
                        {
                            if (args is WindowsWebView2EnvironmentRequestedEventArgs webView2Args)
                                webView2Args.UserDataFolder = Path.Combine(Paths.DataRoot, "WebView2");
                        };
                    }

                    dialog.Closing += (_, _) => completionSource.TrySetResult(null);

                    // AdapterCreated is the first point where the cookie manager is usable.
                    // Polling before that only burns UI-thread time and can leave the page
                    // waiting indefinitely on a cookie manager that will never appear.
                    dialog.AdapterCreated += (_, _) =>
                        _ = PrepareLoginPageAsync(dialog, completionSource, cookieTimeout);

                    ShowSetupIndicator(dialog);

                    var owner = GetActiveWindow();
                    if (owner != null)
                        dialog.Show(owner);
                    else
                        dialog.Show();
                });

                string? newCookie;

                try
                {
                    newCookie = await completionSource.Task.WaitAsync(loginTimeout);
                }
                catch (TimeoutException)
                {
                    newCookie = null;
                }

                if (string.IsNullOrEmpty(newCookie))
                {
                    App.Logger.Info("Browser login was cancelled or timed out.");

                    try { dialog?.Close(); }
                    catch { }

                    return null;
                }

                App.Logger.Info("Successfully captured .ROBLOSECURITY cookie.");

                try { dialog?.Close(); }
                catch { }

                return await accountResolver(newCookie);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                return null;
            }
        }

        private static Window? GetActiveWindow()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return null;

            return desktop.Windows.FirstOrDefault(window => window.IsActive) ?? desktop.MainWindow;
        }

        private static void ShowSetupIndicator(NativeWebDialog dialog)
        {
            try
            {
                if (dialog.TryGetWindow() is not { Content: null } window)
                    return;

                var progress = new ProgressBar
                {
                    IsIndeterminate = true,
                    Width = 220,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };

                var text = new TextBlock
                {
                    Text = "Preparing sign-in...",
                    FontSize = 14,
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#e6e6e6")),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };

                var stack = new StackPanel
                {
                    Spacing = 14,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Children = { progress, text }
                };

                window.Content = new Grid
                {
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#101014")),
                    Children = { stack }
                };
            }
            catch (Exception ex)
            {
                App.Logger.Debug(ex, "Failed to show the web view setup indicator");
            }
        }

        private static async Task PrepareLoginPageAsync(
            NativeWebDialog dialog,
            TaskCompletionSource<string?> completionSource,
            TimeSpan cookieTimeout)
        {
            try
            {
                // The loading page is supplied before the dialog is shown, so the
                // web view displays it as soon as the adapter is created.
                var cookieManager = dialog.TryGetCookieManager();

                if (cookieManager != null)
                    await ClearCookiesAsync(cookieManager, cookieTimeout);

                if (completionSource.Task.IsCompleted)
                    return;

                try
                {
                    dialog.Title = Localizer.Instance.Get("Menu_BrowserLogin_Title");
                    dialog.Source = LoginUri;
                }
                catch (Exception ex) { App.Logger.Error(ex); }

                while (!completionSource.Task.IsCompleted)
                {
                    try
                    {
                        cookieManager = dialog.TryGetCookieManager();

                        if (cookieManager != null)
                        {
                            var cookies = await cookieManager.GetCookiesAsync().WaitAsync(cookieTimeout);
                            var securityCookie = cookies.FirstOrDefault(cookie => cookie.Name == ".ROBLOSECURITY");

                            if (securityCookie != null && !string.IsNullOrEmpty(securityCookie.Value))
                            {
                                completionSource.TrySetResult(securityCookie.Value);
                                return;
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                        App.Logger.Warn("Timed out while reading web view cookies, retrying.");
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error(ex, "Failed to read web view cookies");
                    }

                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                completionSource.TrySetResult(null);
            }
        }

        private static async Task<bool> ClearCookiesAsync(NativeWebViewCookieManager cookieManager, TimeSpan timeout)
        {
            bool cleared = false;

            try
            {
                var cookies = await cookieManager.GetCookiesAsync().WaitAsync(timeout);

                foreach (var cookie in cookies)
                {
                    try
                    {
                        cookieManager.DeleteCookie(cookie);
                        cleared = true;
                    }
                    catch { }
                }

                if (cleared)
                    App.Logger.Info($"Cleared {cookies.Count} web view cookie(s) before login.");
            }
            catch (TimeoutException)
            {
                App.Logger.Warn("Timed out while clearing web view cookies.");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
            }

            return cleared;
        }
    }
}
