/*
 *  Froststrap
 *  Copyright (c) Froststrap Team
 *
 *  This file is part of Froststrap and is distributed under the terms of the
 *  GNU Affero General Public License, version 3 or later.
 *
 *  SPDX-License-Identifier: AGPL-3.0-or-later
 */

using Bloxstrap.UI.Elements.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;
using System.Security.Cryptography;
using System.Web;
using System.Windows;

namespace Bloxstrap.Integrations
{
    public class AccountManager
    {
        private const string LOG_IDENT = "AccountManager";
        private const string AccountsFile = "AccountManager.json";

        public event Action? NoAccountsFound;

        public event Action<AccountManagerAccount?>? ActiveAccountChanged;

        public event Action<string, DateTime?>? QuickSignCodeCreated;
        public event Action<string, string?>? QuickSignStatusUpdated;

        private readonly string _accountsLocation;
        private List<AccountManagerAccount> _accounts = new();

        private Browser? _browser;
        private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("Froststrap_DPAPI_v1");

        public AccountManagerAccount? ActiveAccount { get; private set; }
        public long CurrentPlaceId { get; set; }
        public string CurrentServerInstanceId { get; set; } = "";

        public static AccountManager Shared { get; } = new AccountManager();
        public IReadOnlyList<AccountManagerAccount> Accounts => _accounts;
        private string? _browserTrackerId;

        public AccountManager()
        {
            _accountsLocation = Path.Combine(Paths.Base, AccountsFile);
            LoadAccounts();
        }

        private string Protect(string text) => string.IsNullOrEmpty(text) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(text), DpapiEntropy, DataProtectionScope.CurrentUser));
        private string Unprotect(string text) => string.IsNullOrEmpty(text) ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(text), DpapiEntropy, DataProtectionScope.CurrentUser));

        public void LoadAccounts()
        {
            if (!File.Exists(_accountsLocation)) return;
            try
            {
                var data = JsonConvert.DeserializeObject<AccountManagerData>(File.ReadAllText(_accountsLocation));
                if (data?.Accounts != null)
                {
                    _accounts = data.Accounts.Select(acc => acc with { SecurityToken = Unprotect(acc.SecurityToken) }).ToList();
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
                    Accounts = _accounts.Select(acc => acc with { SecurityToken = Protect(acc.SecurityToken) }).ToList(),
                    ActiveAccountId = ActiveAccount?.UserId,
                    LastUpdated = DateTime.UtcNow
                };
                File.WriteAllText(_accountsLocation, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
        }

        public void UpdateAccountToken(long userId, string newToken)
        {
            int index = _accounts.FindIndex(a => a.UserId == userId);
            if (index != -1)
            {
                _accounts[index] = _accounts[index] with { SecurityToken = newToken, LastUsed = DateTime.UtcNow };
                if (ActiveAccount?.UserId == userId) ActiveAccount = _accounts[index];
                SaveAccounts();
            }
        }

        public void CheckAndApplyCookieRotation(long userId, IEnumerable<string> headers)
        {
            const string KEY = ".ROBLOSECURITY=";
            var header = headers.FirstOrDefault(h => h.Contains(KEY, StringComparison.OrdinalIgnoreCase));
            if (header != null)
            {
                int start = header.IndexOf(KEY) + KEY.Length;
                int end = header.IndexOf(';', start);
                string token = (end == -1 ? header[start..] : header[start..end]).Trim();
                if (!string.IsNullOrEmpty(token)) UpdateAccountToken(userId, token);
            }
        }

        public void SetCurrentPlaceId(long placeId)
        {
            CurrentPlaceId = placeId;
            SaveAccounts();
        }

        public void SetCurrentServerInstanceId(string serverInstanceId)
        {
            CurrentServerInstanceId = serverInstanceId ?? "";
            SaveAccounts();
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

        public async Task<AccountManagerAccount?> AddAccountByQuickSignInAsync()
        {
            const string LOG_IDENT_QUICK_SIGN = $"{LOG_IDENT}::AddAccountByQuickSignIn";

            App.Logger.WriteLine(LOG_IDENT_QUICK_SIGN, "Starting Quick Sign-In (API flow).");

            QuickSignCodeDialog? quickSignWindow = null;
            var cts = new System.Threading.CancellationTokenSource();
            QuickTokenCreation? creation = null;

            try
            {
                creation = await CreateQuickTokenAsync().ConfigureAwait(false);
                if (creation == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_QUICK_SIGN, "Quick Sign-In: failed to create token.");
                    Frontend.ShowMessageBox("Failed to start Quick Sign-In. Please check your internet connection.", MessageBoxImage.Error);
                    return null;
                }

                App.FrostRPC?.SetDialog("Quick Sign-In");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    quickSignWindow = new QuickSignCodeDialog();
                    quickSignWindow.Closed += (s, e) => cts.Cancel();
                    quickSignWindow.StartNewSignIn(creation.Code);
                    quickSignWindow.Show();
                });

                QuickSignCodeCreated?.Invoke(creation.Code, creation.ExpirationTime);

                var status = await PollQuickTokenStatusAsync(creation.Code, creation.PrivateKey, creation.ExpirationTime, cts.Token, quickSignWindow).ConfigureAwait(false);
                if (status == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_QUICK_SIGN, "Quick Sign-In: polling failed or timed out.");
                    return null;
                }

                if (status.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.WriteLine(LOG_IDENT_QUICK_SIGN, "Quick Sign-In was cancelled by user.");
                    return null;
                }

                if (!status.Status.Equals("Validated", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.WriteLine(LOG_IDENT_QUICK_SIGN, $"Quick Sign-In ended with unexpected status: {status.Status}");
                    Frontend.ShowMessageBox($"Quick Sign-In failed: {status.Status}", MessageBoxImage.Error);
                    return null;
                }

                var roblosecurity = await PerformLoginWithAuthTokenAsync(creation.Code, creation.PrivateKey).ConfigureAwait(false);
                if (string.IsNullOrEmpty(roblosecurity))
                {
                    App.Logger.WriteLine(LOG_IDENT_QUICK_SIGN, "Quick Sign-In: login exchange failed.");
                    Frontend.ShowMessageBox("Failed to log in with Quick Sign-In. Please try again.", MessageBoxImage.Error);
                    return null;
                }

                var accountInfo = await GetAccountInfoFromCookie(roblosecurity).ConfigureAwait(false);
                if (accountInfo == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_QUICK_SIGN, "Quick Sign-In: failed to get account info with exchanged cookie.");
                    try { await LogoutRoblosecurityAsync(roblosecurity).ConfigureAwait(false); } catch { }
                    Frontend.ShowMessageBox("Failed to get account information. Please try again.", MessageBoxImage.Error);
                    return null;
                }

                if (!_accounts.Any(acc => acc.UserId == accountInfo.UserId))
                {
                    _accounts.Add(accountInfo);
                    SaveAccounts();

                    App.Logger.WriteLine(LOG_IDENT_QUICK_SIGN, $"Successfully added new account via Quick Sign-In: {accountInfo.Username}");
                    return accountInfo;
                }

                App.Logger.WriteLine(LOG_IDENT_QUICK_SIGN, $"Account '{accountInfo.Username}' already exists.");
                return _accounts.First(acc => acc.UserId == accountInfo.UserId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_QUICK_SIGN, ex);
                Frontend.ShowMessageBox($"Quick Sign-In error: {ex.Message}", MessageBoxImage.Error);
                return null;
            }
            finally
            {
                cts.Cancel();
                if (creation != null)
                {
                    try { await CancelQuickTokenAsync(creation.Code).ConfigureAwait(false); } catch { }
                }

                App.FrostRPC?.ClearDialog();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    quickSignWindow?.Close();
                });
            }
        }

        private record QuickTokenCreation(string Code, string PrivateKey, DateTime ExpirationTime, string Status);
        private record QuickTokenStatus(string Status, string? AccountName, string? AccountPictureUrl, DateTime? ExpirationTime);

        private async Task<QuickTokenCreation?> CreateQuickTokenAsync()
        {
            const string LOG_IDENT_CREATE_TOKEN = $"{LOG_IDENT}::CreateQuickToken";

            try
            {
                var content = new StringContent("{}", Encoding.UTF8, "application/json");

                var resp = await App.HttpClient.PostAsync("https://apis.roblox.com/auth-token-service/v1/login/create", content).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_CREATE_TOKEN, $"CreateQuickTokenAsync: non-success status {(int)resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");
                    return null;
                }

                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jo = JsonConvert.DeserializeObject<JObject>(body);
                if (jo == null) return null;

                string code = jo["code"]?.Value<string>() ?? "";
                string privateKey = jo["privateKey"]?.Value<string>() ?? "";
                string status = jo["status"]?.Value<string>() ?? "";
                string exp = jo["expirationTime"]?.Value<string>() ?? "";

                DateTime expiration = DateTime.UtcNow.AddMinutes(2);
                if (!string.IsNullOrEmpty(exp))
                {
                    if (!DateTime.TryParse(exp, out expiration))
                        expiration = DateTime.UtcNow.AddMinutes(2);
                    else
                        expiration = expiration.ToUniversalTime();
                }

                return new QuickTokenCreation(code, privateKey, expiration, status);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_CREATE_TOKEN, ex);
                return null;
            }
        }

        private async Task<QuickTokenStatus?> PollQuickTokenStatusAsync(string code, string privateKey, DateTime expirationTime, System.Threading.CancellationToken token, QuickSignCodeDialog? quickSignWindow = null)
        {
            const string LOG_IDENT_POLL_STATUS = $"{LOG_IDENT}::PollQuickTokenStatus";

            // Parameter validation
            if (string.IsNullOrEmpty(code))
            {
                App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, "PollQuickTokenStatusAsync: Code parameter is null or empty");
                return null;
            }

            if (string.IsNullOrEmpty(privateKey))
            {
                App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, "PollQuickTokenStatusAsync: PrivateKey parameter is null or empty");
                return null;
            }

            if (expirationTime == DateTime.MinValue)
            {
                App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, "PollQuickTokenStatusAsync: Invalid expiration time");
                return null;
            }

            try
            {
                App.HttpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");

                var timeout = expirationTime > DateTime.UtcNow ? expirationTime - DateTime.UtcNow : TimeSpan.FromMinutes(2);
                var deadline = DateTime.UtcNow + timeout;

                string? csrfToken = null;

                while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
                {
                    var payload = new { code = code, privateKey = privateKey };
                    var jsonPayload = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    HttpResponseMessage? resp = null;
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, "https://apis.roblox.com/auth-token-service/v1/login/status")
                        {
                            Content = content
                        };

                        if (!string.IsNullOrEmpty(csrfToken))
                        {
                            request.Headers.Add("X-CSRF-TOKEN", csrfToken);
                        }

                        request.Headers.Add("Origin", "https://www.roblox.com");
                        request.Headers.Add("Referer", "https://www.roblox.com/");

                        resp = await App.HttpClient.SendAsync(request, token).ConfigureAwait(false);

                        if (resp.StatusCode == HttpStatusCode.Forbidden && resp.Headers.Contains("x-csrf-token"))
                        {
                            csrfToken = resp.Headers.GetValues("x-csrf-token")?.FirstOrDefault();
                            App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, $"Received CSRF token, will retry: {csrfToken}");

                            await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                            continue;
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, $"HttpRequestException: {ex.Message}");
                        await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, $"Exception during HTTP request: {ex.Message}");
                        await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                        continue;
                    }

                    if (resp == null)
                    {
                        App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, "PollQuickTokenStatusAsync: Response is null. Retrying...");
                        await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                        continue;
                    }

                    if (!resp.IsSuccessStatusCode)
                    {
                        if (resp.StatusCode == HttpStatusCode.BadRequest)
                        {
                            var errorText = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(errorText) && (errorText.Contains("CodeInvalid") == true || errorText.Contains("\"CodeInvalid\"") == true))
                            {
                                App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, "PollQuickTokenStatusAsync: server reported CodeInvalid.");
                                try
                                {
                                    QuickSignStatusUpdated?.Invoke("Cancelled", null);

                                    if (quickSignWindow != null)
                                    {
                                        try
                                        {
                                            if (Application.Current != null && Application.Current.Dispatcher != null && !Application.Current.Dispatcher.HasShutdownFinished)
                                            {
                                                Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    // Check again in case quickSignWindow became null during invocation
                                                    if (quickSignWindow != null)
                                                    {
                                                        quickSignWindow.UpdateStatus("Cancelled", "Code expired or invalid");
                                                    }
                                                });
                                            }
                                            else
                                            {
                                                App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, "Dispatcher not available for quickSignWindow update");
                                            }
                                        }
                                        catch (Exception dispEx)
                                        {
                                            App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, $"Dispatcher exception: {dispEx.Message}");
                                        }
                                    }
                                }
                                catch (Exception invokeEx)
                                {
                                    App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, $"Event invocation exception: {invokeEx.Message}");
                                }
                                return new QuickTokenStatus("Cancelled", null, null, null);
                            }
                        }

                        App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, $"PollQuickTokenStatusAsync: status endpoint returned {(int)resp.StatusCode}. Retrying...");
                        await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                        continue;
                    }

                    string? body = null;
                    try
                    {
                        body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                    catch (Exception readEx)
                    {
                        App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, $"Error reading response content: {readEx.Message}");
                        await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                        continue;
                    }

                    if (string.IsNullOrEmpty(body))
                    {
                        App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, "PollQuickTokenStatusAsync: Response body is empty. Retrying...");
                        await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                        continue;
                    }

                    JObject? jo = null;
                    try
                    {
                        jo = JsonConvert.DeserializeObject<JObject>(body);
                    }
                    catch (Newtonsoft.Json.JsonException jsonEx)
                    {
                        App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, $"JSON deserialization error: {jsonEx.Message}");
                        await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                        continue;
                    }

                    if (jo == null)
                    {
                        App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, "PollQuickTokenStatusAsync: Deserialized JSON object is null. Retrying...");
                        await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                        continue;
                    }

                    string status = jo["status"]?.Value<string>() ?? "";
                    string? accountName = jo["accountName"]?.Value<string>();
                    string? accountPictureUrl = jo["accountPictureUrl"]?.Value<string>();
                    string? exp = jo["expirationTime"]?.Value<string>();

                    DateTime? expDt = null;
                    if (!string.IsNullOrEmpty(exp) && DateTime.TryParse(exp, out var e))
                    {
                        expDt = e.ToUniversalTime();
                    }

                    try
                    {
                        QuickSignStatusUpdated?.Invoke(status, accountName);

                        if (quickSignWindow != null && !string.IsNullOrEmpty(status))
                        {
                            try
                            {
                                if (Application.Current != null && Application.Current.Dispatcher != null && !Application.Current.Dispatcher.HasShutdownFinished)
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (quickSignWindow != null)
                                        {
                                            if (status == "Created" && string.IsNullOrEmpty(accountName))
                                            {
                                                quickSignWindow.UpdateStatus(status, "Ready for sign-in");
                                            }
                                            else
                                            {
                                                quickSignWindow.UpdateStatus(status, accountName ?? "Unknown");
                                            }
                                        }
                                    });
                                }
                                else
                                {
                                    App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, "Dispatcher not available for status update");
                                }
                            }
                            catch (Exception dispEx)
                            {
                                App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, $"Dispatcher invocation error: {dispEx.Message}");
                            }
                        }
                    }
                    catch (Exception statusEx)
                    {
                        App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, $"Status update error: {statusEx.Message}");
                    }

                    if (status.Equals("Validated", StringComparison.OrdinalIgnoreCase) ||
                        status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                    {
                        return new QuickTokenStatus(status, accountName, accountPictureUrl, expDt);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                }

                App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, "PollQuickTokenStatusAsync: timed out or cancelled.");

                // Safe timeout UI update
                if (quickSignWindow != null)
                {
                    try
                    {
                        if (Application.Current != null && Application.Current.Dispatcher != null && !Application.Current.Dispatcher.HasShutdownFinished)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (quickSignWindow != null)
                                {
                                    quickSignWindow.UpdateStatus("TimedOut", "Sign-in timed out");
                                }
                            });
                        }
                    }
                    catch (Exception dispEx)
                    {
                        App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, $"Timeout UI update error: {dispEx.Message}");
                    }
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                App.Logger?.WriteLine(LOG_IDENT_POLL_STATUS, "PollQuickTokenStatusAsync: Operation was cancelled.");
                return null;
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException(LOG_IDENT_POLL_STATUS, ex);
                return null;
            }
        }

        private async Task<string?> PerformLoginWithAuthTokenAsync(string code, string privateKey)
        {
            const string LOG_IDENT_LOGIN = $"{LOG_IDENT}::PerformLoginWithAuthToken";

            try
            {
                var handler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer(),
                    UseCookies = true,
                    UseDefaultCredentials = false
                };

                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.Add("Origin", "https://www.roblox.com");
                client.DefaultRequestHeaders.Add("Referer", "https://www.roblox.com/");

                var payload = new
                {
                    ctype = "AuthToken",
                    cvalue = code,
                    password = privateKey
                };

                var jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                string? csrfToken = null;
                int maxRetries = 3;

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v2/login")
                    {
                        Content = content
                    };

                    if (string.IsNullOrEmpty(csrfToken))
                    {
                        var csrfResponse = await client.GetAsync("https://auth.roblox.com/v2/login");
                        if (csrfResponse.Headers.TryGetValues("x-csrf-token", out var csrfValues))
                        {
                            csrfToken = csrfValues.FirstOrDefault();
                        }

                        if (string.IsNullOrEmpty(csrfToken))
                        {
                            var headRequest = new HttpRequestMessage(HttpMethod.Head, "https://auth.roblox.com/v2/login");
                            var headResponse = await client.SendAsync(headRequest);
                            if (headResponse.Headers.TryGetValues("x-csrf-token", out var headCsrfValues))
                            {
                                csrfToken = headCsrfValues.FirstOrDefault();
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(csrfToken))
                    {
                        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
                    }

                    var resp = await client.SendAsync(request).ConfigureAwait(false);

                    if (resp.StatusCode == HttpStatusCode.Forbidden && resp.Headers.Contains("x-csrf-token"))
                    {
                        csrfToken = resp.Headers.GetValues("x-csrf-token").FirstOrDefault();
                        App.Logger.WriteLine(LOG_IDENT_LOGIN, $"Received CSRF token on attempt {attempt + 1}, retrying...");
                        await Task.Delay(1000);
                        continue;
                    }

                    if (!resp.IsSuccessStatusCode)
                    {
                        App.Logger.WriteLine(LOG_IDENT_LOGIN, $"PerformLoginWithAuthTokenAsync: login returned {(int)resp.StatusCode} on attempt {attempt + 1}");

                        if (resp.StatusCode != HttpStatusCode.Forbidden)
                            return null;

                        continue;
                    }

                    if (resp.Headers.TryGetValues("Set-Cookie", out var setCookies))
                    {
                        foreach (var header in setCookies)
                        {
                            if (header.Contains(".ROBLOSECURITY="))
                            {
                                var start = header.IndexOf(".ROBLOSECURITY=") + ".ROBLOSECURITY=".Length;
                                var end = header.IndexOf(';', start);
                                if (end == -1) end = header.Length;

                                var token = header.Substring(start, end - start);
                                if (!string.IsNullOrEmpty(token))
                                {
                                    return token;
                                }
                            }
                        }
                    }

                    var cookies = handler.CookieContainer.GetCookies(new Uri("https://www.roblox.com"));
                    var securityCookie = cookies[".ROBLOSECURITY"];
                    if (securityCookie != null && !string.IsNullOrEmpty(securityCookie.Value))
                    {
                        return securityCookie.Value;
                    }

                    if (resp.IsSuccessStatusCode)
                    {
                        var responseBody = await resp.Content.ReadAsStringAsync();
                        App.Logger.WriteLine(LOG_IDENT_LOGIN, $"Login successful but no cookie found. Response: {responseBody}");
                    }

                    break;
                }

                App.Logger.WriteLine(LOG_IDENT_LOGIN, "PerformLoginWithAuthTokenAsync: no .ROBLOSECURITY found after all attempts.");
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_LOGIN, ex);
                return null;
            }
        }

        private async Task CancelQuickTokenAsync(string code)
        {
            const string LOG_IDENT_CANCEL = $"{LOG_IDENT}::CancelQuickToken";

            try
            {
                using var client = new HttpClient();
                var payload = new { code = code };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var resp = await client.PostAsync("https://apis.roblox.com/auth-token-service/v1/login/cancel", content).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    App.Logger.WriteLine(LOG_IDENT_CANCEL, $"CancelQuickTokenAsync: cancel returned {(int)resp.StatusCode}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_CANCEL, ex);
            }
        }

        // logout a .ROBLOSECURITY value
        private async Task LogoutRoblosecurityAsync(string roblosecurity)
        {
            const string LOG_IDENT_LOGOUT = $"{LOG_IDENT}::LogoutRoblosecurity";

            try
            {
                var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
                handler.CookieContainer.Add(new Cookie(".ROBLOSECURITY", roblosecurity, "/", ".roblox.com"));
                using var client = new HttpClient(handler);

                var req = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v2/logout");
                var resp = await client.SendAsync(req).ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.Forbidden && resp.Headers.TryGetValues("x-csrf-token", out var vals))
                {
                    var csrf = vals.FirstOrDefault();
                    if (!string.IsNullOrEmpty(csrf))
                    {
                        var req2 = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v2/logout");
                        req2.Headers.Add("X-CSRF-TOKEN", csrf);
                        await client.SendAsync(req2).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_LOGOUT, ex);
            }
        }

        public AccountManagerAccount? AddManualAccount(string cookie, long userId, string username, string displayName)
        {
            const string LOG_IDENT_ADD_MANUAL = $"{LOG_IDENT}::AddManualAccount";

            try
            {
                var existingAccount = _accounts.FirstOrDefault(acc => acc.UserId == userId);
                if (existingAccount != null)
                {
                    App.Logger.WriteLine(LOG_IDENT_ADD_MANUAL, $"Account '{username}' already exists");
                    return existingAccount;
                }

                var newAccount = new AccountManagerAccount(cookie, userId, username, displayName);
                _accounts.Add(newAccount);

                SaveAccounts();

                App.Logger.WriteLine(LOG_IDENT_ADD_MANUAL, $"Successfully added account: {username}");
                return newAccount;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_ADD_MANUAL, ex);
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

                var fetcher = new BrowserFetcher();
                string executablePath = null!;

                var installed = fetcher.GetInstalledBrowsers().FirstOrDefault(b => b.Browser == SupportedBrowser.Chromium);

                if (installed != null)
                {
                    try
                    {
                        var potentialPath = installed.GetExecutablePath();
                        if (File.Exists(potentialPath))
                        {
                            executablePath = potentialPath;
                            App.Logger.WriteLine(LOG_IDENT_BROWSER, $"Chromium found via BrowserFetcher: {potentialPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT_BROWSER, $"Error checking BrowserFetcher path: {ex.Message}");
                    }
                }

                if (executablePath == null)
                {
                    var localAppData = Paths.LocalAppData;
                    var specificPath = Path.Combine(localAppData, "PuppeteerSharp");

                    App.Logger.WriteLine(LOG_IDENT_BROWSER, $"Checking specific path: {specificPath}");

                    if (Directory.Exists(specificPath))
                    {
                        var chromeFiles = Directory.GetFiles(specificPath, "chrome.exe", SearchOption.AllDirectories);
                        if (chromeFiles.Length > 0)
                        {
                            executablePath = chromeFiles[0];
                            App.Logger.WriteLine(LOG_IDENT_BROWSER, $"Chromium found via directory search: {executablePath}");
                        }
                        else
                        {
                            App.Logger.WriteLine(LOG_IDENT_BROWSER, "No chrome.exe found in PuppeteerSharp directory");
                        }
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT_BROWSER, "PuppeteerSharp directory not found");
                    }
                }

                if (executablePath == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, "Chromium not found, downloading...");

                    try
                    {
                        var browserInfo = await fetcher.DownloadAsync();
                        executablePath = browserInfo.GetExecutablePath();
                        App.Logger.WriteLine(LOG_IDENT_BROWSER, $"Chromium downloaded: {browserInfo.BuildId}");

                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT_BROWSER, ex);
                        throw;
                    }
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, $"Using existing Chromium: {executablePath}");
                }

                App.Logger.WriteLine(LOG_IDENT_BROWSER, "Launching browser...");

                _browser = (Browser)await new PuppeteerExtra()
                    .Use(new StealthPlugin())
                    .LaunchAsync(new LaunchOptions
                    {
                        Headless = false,
                        DefaultViewport = null,
                        ExecutablePath = executablePath
                    });

                if (_browser == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, "Failed to launch browser.");
                    return null;
                }

                _browser.Closed += (sender, e) =>
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, "Browser closed by user.");
                    if (!completionSource.Task.IsCompleted)
                        completionSource.TrySetResult(null);
                };

                var pages = await _browser.PagesAsync();
                var mainPage = pages.FirstOrDefault();

                if (mainPage == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, "No browser pages available.");
                    return null;
                }

                mainPage.Close += (_, _) =>
                {
                    if (!completionSource.Task.IsCompleted)
                        completionSource.TrySetResult(null);
                };

                async Task SafeGoToAsync(string url)
                {
                    int attempts = 0;
                    while (true)
                    {
                        try
                        {
                            if (mainPage.IsClosed)
                            {
                                App.Logger.WriteLine(LOG_IDENT_BROWSER, "Page closed during navigation.");
                                return;
                            }

                            await mainPage.GoToAsync(url, new NavigationOptions
                            {
                                WaitUntil = new[] { WaitUntilNavigation.Networkidle0, WaitUntilNavigation.DOMContentLoaded },
                                Timeout = 60000
                            });
                            return;
                        }
                        catch (PuppeteerSharp.NavigationException)
                        {
                            attempts++;
                            if (attempts >= 3) throw;
                            App.Logger.WriteLine(LOG_IDENT_BROWSER, "Navigation failed, retrying...");
                            await Task.Delay(1000);
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteException(LOG_IDENT_BROWSER, ex);
                            throw;
                        }
                    }
                }

                await SafeGoToAsync("https://www.roblox.com/login");

                try
                {
                    if (mainPage == null || mainPage.IsClosed)
                    {
                        App.Logger.WriteLine(LOG_IDENT_BROWSER, "Page is closed, cannot wait for selector.");
                        return null;
                    }

                    await mainPage.WaitForSelectorAsync("#login-username", new WaitForSelectorOptions { Timeout = 60000 });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (mainPage?.IsClosed == false)
                    {
                        App.Logger.WriteLine(LOG_IDENT_BROWSER, "Login form might not have loaded properly, but continuing...");
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT_BROWSER, "Browser was closed while waiting for login form.");
                        return null;
                    }
                }

                mainPage.RequestFinished += async (_, _) =>
                {
                    try
                    {
                        if (mainPage.IsClosed || completionSource.Task.IsCompleted)
                            return;

                        var cookies = await mainPage.GetCookiesAsync("https://www.roblox.com/");
                        var securityCookie = cookies.FirstOrDefault(c => c.Name == ".ROBLOSECURITY");
                        if (securityCookie != null)
                        {
                            App.Logger.WriteLine(LOG_IDENT_BROWSER, "Successfully captured cookie.");
                            completionSource.TrySetResult(securityCookie.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT_BROWSER, ex);
                    }
                };

                string? newCookie = await completionSource.Task;

                if (string.IsNullOrEmpty(newCookie))
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, "Account add process cancelled or failed.");
                    return null;
                }

                var accountInfo = await GetAccountInfoFromCookie(newCookie);
                if (accountInfo == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, "Failed to fetch account info from the new cookie.");
                    return null;
                }

                if (!_accounts.Any(acc => acc.UserId == accountInfo.UserId))
                {
                    _accounts.Add(accountInfo);
                    SaveAccounts();

                    App.Logger.WriteLine(LOG_IDENT_BROWSER, $"Successfully added new account: {accountInfo.Username}");
                    return accountInfo;
                }

                App.Logger.WriteLine(LOG_IDENT_BROWSER, $"Account '{accountInfo.Username}' already exists.");
                return _accounts.First(acc => acc.UserId == accountInfo.UserId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_BROWSER, ex);
                return null;
            }
            finally
            {
                try
                {
                    if (_browser != null && !_browser.IsClosed)
                    {
                        await _browser.CloseAsync();
                        _browser = null;
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_BROWSER, $"Error closing browser: {ex.Message}");
                }
            }
        }

        private async Task<AccountManagerAccount?> GetAccountInfoFromCookie(string securityCookie)
        {
            const string LOG_IDENT_GET_INFO = $"{LOG_IDENT}::GetAccountInfoFromCookie";

            var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
            handler.CookieContainer.Add(new System.Net.Cookie(".ROBLOSECURITY", securityCookie, "/", ".roblox.com"));
            using var client = new HttpClient(handler);
            var response = await client.GetAsync("https://users.roblox.com/v1/users/authenticated");
            if (!response.IsSuccessStatusCode) return null;
            string json = await response.Content.ReadAsStringAsync();

            try
            {
                var jo = JsonConvert.DeserializeObject<JObject>(json);
                if (jo == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_GET_INFO, "GetAccountInfoFromCookie: response JSON was null");
                    return null;
                }

                long userId = jo["id"]?.Value<long>() ?? 0;
                string username = jo["name"]?.Value<string>() ?? string.Empty;
                string displayName = jo["displayName"]?.Value<string>() ?? string.Empty;

                if (userId == 0 || string.IsNullOrEmpty(username))
                {
                    App.Logger.WriteLine(LOG_IDENT_GET_INFO, "GetAccountInfoFromCookie: missing required fields in response JSON");
                    App.Logger.WriteLine(LOG_IDENT_GET_INFO, "Response JSON: " + json);
                    return null;
                }

                return new AccountManagerAccount(securityCookie, userId, username, displayName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_GET_INFO, ex);
                App.Logger.WriteLine(LOG_IDENT_GET_INFO, "Response JSON: " + json);
                return null;
            }
        }

        public async Task<bool> ValidateAccountAsync(AccountManagerAccount account)
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
                var response = await client.GetAsync("https://users.roblox.com/v1/users/authenticated");

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
                int removed = _accounts.RemoveAll(a => a.UserId == account.UserId);
                if (removed > 0)
                {
                    if (ActiveAccount is not null && ActiveAccount.UserId == account.UserId)
                        ActiveAccount = null;

                    SaveAccounts();

                    if (ActiveAccount is null && _accounts.Any())
                    {
                        SetActiveAccount(_accounts.First().UserId);
                    }

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

        public async Task LaunchAccountAsync(AccountManagerAccount? account, long placeId = 0, string serverId = "", bool followUser = false, bool joinVIP = false)
        {
            const string LOG_IDENT_MAIN = $"{LOG_IDENT}::LaunchAccount";

            if (account is null) return;

            try
            {
                SetActiveAccount(account.UserId);
                SaveAccounts();

                App.Logger.WriteLine(LOG_IDENT_MAIN, $"Initiating launch for {account.Username}");
                await JoinServer(account, placeId, serverId, followUser, joinVIP).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_MAIN, ex);
            }
        }

        public async Task<string> JoinServer(AccountManagerAccount account, long placeId, string jobId = "", bool followUser = false, bool joinVip = false)
        {
            const string LOG_IDENT_JOIN = "AccountManager::JoinServer";

            if (string.IsNullOrEmpty(_browserTrackerId))
            {
                Random r = new Random();
                _browserTrackerId = r.Next(100000, 175000).ToString() + r.Next(100000, 900000).ToString();
            }

            string csrf = await GetCsrfToken(account.SecurityToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(csrf))
                return "ERROR: Account Session Expired.";
            string? ticket = await GetAuthTicket(account.SecurityToken, csrf, placeId).ConfigureAwait(false);

            if (string.IsNullOrEmpty(ticket))
            {
                App.Logger.WriteLine(LOG_IDENT_JOIN, "Failed to retrieve authentication ticket.");
                return "ERROR: Invalid Authentication Ticket, re-add the account or try again\n(Failed to get Authentication Ticket, Roblox has probably signed you out)";
            }

            string launcherUrl;
            if (joinVip)
            {
                var match = Regex.Match(jobId, "privateServerLinkCode=(.+)");
                string linkCode = match.Success ? match.Groups[1].Value : "";
                launcherUrl = $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestPrivateGame&placeId={placeId}&accessCode={jobId}&linkCode={linkCode}";
            }
            else if (followUser)
            {
                launcherUrl = $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestFollowUser&userId={placeId}";
            }
            else
            {
                string jobIdParam = string.IsNullOrEmpty(jobId) ? "" : $"&gameId={jobId}";
                string requestType = string.IsNullOrEmpty(jobId) ? "RequestGame" : "RequestGameJob";
                launcherUrl = $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request={requestType}&placeId={placeId}{jobIdParam}&isPlayTogetherGame=false";
            }

            double launchTime = Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds * 1000);

            string launchArgs = $"roblox-player:1+launchmode:play+gameinfo:{ticket}+launchtime:{launchTime}+placelauncherurl:{HttpUtility.UrlEncode(launcherUrl)}+browsertrackerid:{_browserTrackerId}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = launchArgs,
                    UseShellExecute = true
                });

                return "Success";
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_JOIN, ex);
                return $"ERROR: {ex.Message}";
            }
        }


        public async Task<string?> GetAuthTicket(string securityCookie, string csrfToken, long placeId)
        {
            const string LOG_IDENT_AUTH_TICKET = $"{LOG_IDENT}::GetAuthTicket";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/");

                request.Headers.Add("Cookie", $".ROBLOSECURITY={securityCookie}");
                request.Headers.Add("X-CSRF-TOKEN", csrfToken);
                request.Headers.Add("Referer", $"https://www.roblox.com/games/{placeId}/");
                request.Headers.Add("Origin", "https://www.roblox.com");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                var resp = await App.HttpClient.SendAsync(request).ConfigureAwait(false);

                if (resp.Headers.TryGetValues("rbx-authentication-ticket", out var vals))
                {
                    return vals.FirstOrDefault();
                }

                string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                App.Logger.WriteLine(LOG_IDENT_AUTH_TICKET, $"Ticket Error Body: {body}");

                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_AUTH_TICKET, ex);
                return null;
            }
        }

        private async Task<string> GetCsrfToken(string securityCookie)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/logout");
            request.Headers.Add("Cookie", $".ROBLOSECURITY={securityCookie}");

            var resp = await App.HttpClient.SendAsync(request).ConfigureAwait(false);

            if (resp.Headers.TryGetValues("X-CSRF-TOKEN", out var tokens))
                return tokens.FirstOrDefault() ?? "";

            return "";
        }
    }
}