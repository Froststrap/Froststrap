/*
 *  Froststrap
 *  Copyright (c) Froststrap Team
 *
 *  This file is part of Froststrap and is distributed under the terms of the
 *  GNU Affero General Public License, version 3 or later.
 *
 *  SPDX-License-Identifier: AGPL-3.0-or-later
 *
 *  Description: Nix flake for shipping for Nix-darwin, Nix, NixOS, and modules
 *               of the Nix ecosystem. 
 */

using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;
using System.Web;
using Avalonia;
using Froststrap.UI.Elements.Dialogs;

namespace Froststrap.Integrations
{
    public record AltAccount(string SecurityToken, long UserId, string Username, string DisplayName);

    public class AccountManager
    {
        private const string LOG_IDENT = "AccountManager";
        private const string AccountsFile = "AccountManager.json";
        private readonly string _accountsLocation;
        private Browser? _browser;
        private List<AltAccount> _accounts = new();

        private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("Froststrap_DPAPI_v1");

        public static AccountManager Shared { get; } = new AccountManager();

        public IReadOnlyList<AltAccount> Accounts => _accounts.AsReadOnly();
        public AltAccount? ActiveAccount { get; private set; }

        public event Action? NoAccountsFound;

        public event Action<AltAccount?>? ActiveAccountChanged;

        public event Action<string, DateTime?>? QuickSignCodeCreated;
        public event Action<string, string?>? QuickSignStatusUpdated;

        public string CurrentPlaceId { get; private set; } = "";
        public string CurrentServerInstanceId { get; private set; } = "";

        public AccountManager()
        {
            _accountsLocation = Path.Combine(Paths.Cache, AccountsFile);
            LoadAccounts();
        }

        private static string ProtectString(string? plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return string.Empty;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(plaintext);
                var protectedBytes = ProtectedData.Protect(bytes, DpapiEntropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(protectedBytes);
            }
            catch (Exception)
            {
                return plaintext;
            }
        }

        private static string UnprotectString(string? protectedText)
        {
            if (string.IsNullOrEmpty(protectedText))
                return string.Empty;

            try
            {
                // Try base64 decode -> unprotect. If it's not base64 or unprotect fails, assume plaintext.
                var protectedBytes = Convert.FromBase64String(protectedText);
                var bytes = ProtectedData.Unprotect(protectedBytes, DpapiEntropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                // Not base64 -> plaintext
                return protectedText ?? string.Empty;
            }
            catch (CryptographicException)
            {
                // Could not unprotect -> assume plaintext (or different machine/profile)
                return protectedText ?? string.Empty;
            }
            catch (Exception)
            {
                return protectedText ?? string.Empty;
            }
        }

        public void LoadAccounts()
        {
            const string LOG_IDENT_LOAD = $"{LOG_IDENT}::LoadAccounts";

            App.Logger.WriteLine(LOG_IDENT_LOAD, "Loading accounts...");

            if (!File.Exists(_accountsLocation))
            {
                App.Logger.WriteLine(LOG_IDENT_LOAD, "Accounts file not found.");
                _accounts = new();
                NoAccountsFound?.Invoke();
                return;
            }

            string json = File.ReadAllText(_accountsLocation);
            if (string.IsNullOrWhiteSpace(json))
            {
                App.Logger.WriteLine(LOG_IDENT_LOAD, "Accounts file is empty.");
                _accounts = new();
                NoAccountsFound?.Invoke();
                return;
            }

            try
            {
                var managerData = JsonConvert.DeserializeObject<AccountManagerData>(json);
                if (managerData?.Accounts is not null && managerData.Accounts.Any())
                {
                    _accounts = managerData.Accounts
                        .Select(acc => new AltAccount(
                            UnprotectString(acc.SecurityToken),
                            acc.UserId,
                            acc.Username,
                            acc.DisplayName))
                        .ToList();

                    if (managerData.ActiveAccountId.HasValue)
                    {
                        var cachedAccount = _accounts.FirstOrDefault(acc => acc.UserId == managerData.ActiveAccountId.Value);
                        if (cachedAccount != null)
                        {
                            ActiveAccount = cachedAccount;
                            ActiveAccountChanged?.Invoke(ActiveAccount);
                            App.Logger.WriteLine(LOG_IDENT_LOAD, $"Restored active account from file: {cachedAccount.Username}");
                        }
                        else
                        {
                            App.Logger.WriteLine(LOG_IDENT_LOAD, $"Saved active account ID {managerData.ActiveAccountId} not found in loaded accounts");
                            if (_accounts.Any())
                                SetActiveAccount(_accounts.First());
                        }
                    }
                    else if (_accounts.Any())
                    {
                        SetActiveAccount(_accounts.First());
                    }

                    CurrentPlaceId = managerData.CurrentPlaceId ?? "";
                    CurrentServerInstanceId = managerData.CurrentServerInstanceId ?? "";

                    App.Logger.WriteLine(LOG_IDENT_LOAD, $"Restored Place ID: {CurrentPlaceId}, Server Instance ID: {CurrentServerInstanceId}");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_LOAD, "Accounts file deserialized to empty or null list.");
                    _accounts = new();
                    NoAccountsFound?.Invoke();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_LOAD, ex);
                _accounts = new();
                NoAccountsFound?.Invoke();
            }

            if (_accounts.Any())
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    await ValidateAllAccountsAsync();
                });
            }
        }

        public void SaveAccounts()
        {
            const string LOG_IDENT_SAVE = $"{LOG_IDENT}::SaveAccounts";

            App.Logger.WriteLine(LOG_IDENT_SAVE, "Saving accounts...");
            try
            {
                var protectedAccounts = _accounts
                    .Select(a => new AltAccount(ProtectString(a.SecurityToken), a.UserId, a.Username, a.DisplayName))
                    .ToList();

                var managerData = new AccountManagerData
                {
                    Accounts = protectedAccounts,
                    ActiveAccountId = ActiveAccount?.UserId,
                    LastUpdated = DateTime.UtcNow,
                    CurrentPlaceId = CurrentPlaceId,
                    CurrentServerInstanceId = CurrentServerInstanceId,
                };

                string json = JsonConvert.SerializeObject(managerData, Formatting.Indented);
                File.WriteAllText(_accountsLocation, json);

                App.Logger.WriteLine(LOG_IDENT_SAVE, $"Saved {_accounts.Count} accounts with active account: {ActiveAccount?.Username ?? "None"}");
                App.Logger.WriteLine(LOG_IDENT_SAVE, $"Saved Place ID: {CurrentPlaceId}, Server Instance ID: {CurrentServerInstanceId}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_SAVE, ex);
            }
        }

        public void SetCurrentPlaceId(string placeId)
        {
            CurrentPlaceId = placeId ?? "";
            SaveAccounts();
        }

        public void SetCurrentServerInstanceId(string serverInstanceId)
        {
            CurrentServerInstanceId = serverInstanceId ?? "";
            SaveAccounts();
        }

        public void SetActiveAccount(AltAccount? account)
        {
            const string LOG_IDENT_SET_ACTIVE = $"{LOG_IDENT}::SetActiveAccount";

            ActiveAccount = account;
            App.Logger.WriteLine(LOG_IDENT_SET_ACTIVE, $"Set active account to: {account?.Username ?? "None"}");

            SaveAccounts();

            // Notify listeners that active account changed
            try
            {
                ActiveAccountChanged?.Invoke(ActiveAccount);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_SET_ACTIVE, ex);
            }
        }

        public string? GetRoblosecurityForUser(long userId)
        {
            var a = _accounts.FirstOrDefault(x => x.UserId == userId);
            return a?.SecurityToken;
        }

        public async Task<string> LaunchToPlaceAsync(AltAccount account, long placeId, string jobId = "", bool followUser = false, bool joinVIP = false, bool isTeleport = false)
        {
            const string LOG_IDENT_LAUNCH = $"{LOG_IDENT}::LaunchToPlace";

            if (account is null) return "ERROR: No account supplied";

            try
            {
                SetActiveAccount(account);
                SaveAccounts();

                App.Logger.WriteLine(LOG_IDENT_LAUNCH, $"Preparing launch for {account.Username} to place {placeId}.");

                var csrf = await GetCsrfTokenAsync(account.SecurityToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(csrf))
                {
                    return "ERROR: Failed to obtain CSRF token";
                }

                var ticket = await GetAuthTicketAsync(account.SecurityToken, csrf, placeId).ConfigureAwait(false);
                if (string.IsNullOrEmpty(ticket))
                {
                    return "ERROR: Failed to obtain authentication ticket";
                }

                string placeLauncher = BuildPlaceLauncherUrl(placeId, jobId, isTeleport, joinVIP, followUser);

                string browserTrackerId = new Random().Next(100000, 999999).ToString() + new Random().Next(100000, 999999).ToString();

                double launchTime = Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds * 1000);

                string encodedPlaceLauncher = Uri.EscapeDataString(placeLauncher);
                string launchUri = $"roblox-player:1+launchmode:play+gameinfo:{ticket}+launchtime:{launchTime}+placelauncherurl:{encodedPlaceLauncher}+browsertrackerid:{browserTrackerId}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";

                App.Logger.WriteLine(LOG_IDENT_LAUNCH, $"Starting launcher via protocol for user {account.Username}.");

                var psi = new ProcessStartInfo(launchUri)
                {
                    UseShellExecute = true
                };

                Process.Start(psi);

                return "Success";
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_LAUNCH, ex);
                return $"ERROR: {ex.Message}";
            }
        }

        private string BuildPlaceLauncherUrl(long placeId, string jobId, bool isTeleport, bool joinVIP, bool followUser)
        {
            if (joinVIP && !string.IsNullOrEmpty(jobId))
            {
                return $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestPrivateGame&placeId={placeId}&accessCode={HttpUtility.UrlEncode(jobId)}";
            }

            if (followUser)
            {
                return $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestFollowUser&userId={placeId}";
            }

            string jobSegment = string.IsNullOrEmpty(jobId) ? "" : $"&gameId={HttpUtility.UrlEncode(jobId)}";
            string teleportSegment = isTeleport ? "&isTeleport=true" : "";
            string requestType = string.IsNullOrEmpty(jobId) ? "RequestGame" : "RequestGameJob";

            return $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request={requestType}&placeId={placeId}{jobSegment}{teleportSegment}";
        }

        public async Task<string?> GetCsrfTokenAsync(string securityCookie)
        {
            const string LOG_IDENT_CSRF = $"{LOG_IDENT}::GetCsrfToken";

            try
            {
                var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
                handler.CookieContainer.Add(new Cookie(".ROBLOSECURITY", securityCookie, "/", ".roblox.com"));
                using var client = new HttpClient(handler);

                var req = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/");

                var resp = await client.SendAsync(req).ConfigureAwait(false);

                if (resp.Headers.TryGetValues("x-csrf-token", out var vals))
                {
                    var token = vals.FirstOrDefault();
                    return token;
                }

                var headReq = new HttpRequestMessage(HttpMethod.Head, "https://auth.roblox.com/v1/authentication-ticket/");
                var headResp = await client.SendAsync(headReq).ConfigureAwait(false);

                if (headResp.Headers.TryGetValues("x-csrf-token", out var vals2))
                {
                    var token2 = vals2.FirstOrDefault();
                    return token2;
                }

                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_CSRF, ex);
                return null;
            }
        }

        private async Task<string?> GetAuthTicketAsync(string securityCookie, string csrfToken, long placeId)
        {
            const string LOG_IDENT_AUTH_TICKET = $"{LOG_IDENT}::GetAuthTicket";

            try
            {
                var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
                handler.CookieContainer.Add(new Cookie(".ROBLOSECURITY", securityCookie, "/", ".roblox.com"));
                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

                var req = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/");
                req.Headers.Add("X-CSRF-TOKEN", csrfToken);
                req.Headers.Referrer = new Uri($"https://www.roblox.com/games/{placeId}/");

                req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                var resp = await client.SendAsync(req).ConfigureAwait(false);

                if (resp.Headers.TryGetValues("rbx-authentication-ticket", out var vals))
                {
                    var ticket = vals.FirstOrDefault();
                    return ticket;
                }

                string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_AUTH_TICKET, ex);
                return null;
            }
        }

        public async Task<AltAccount?> AddAccountByQuickSignInAsync()
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
                using var client = new HttpClient();

                var content = new StringContent("{}", Encoding.UTF8, "application/json");

                var resp = await client.PostAsync("https://apis.roblox.com/auth-token-service/v1/login/create", content).ConfigureAwait(false);

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
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

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

                        resp = await client.SendAsync(request, token).ConfigureAwait(false);

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

        public AltAccount? AddManualAccount(string cookie, long userId, string username, string displayName)
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

                var newAccount = new AltAccount(cookie, userId, username, displayName);
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

        public async Task<AltAccount?> AddAccountByBrowser()
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
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
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

        public bool RemoveAccount(AltAccount account)
        {
            const string LOG_IDENT_REMOVE = $"{LOG_IDENT}::RemoveAccount";

            try
            {
                int removed = _accounts.RemoveAll(a => a.UserId == account.UserId);
                if (removed > 0)
                {
                    if (ActiveAccount is not null && ActiveAccount.UserId == account.UserId)
                    {
                        ActiveAccount = null;
                    }

                    SaveAccounts();

                    if (ActiveAccount is null && _accounts.Any())
                    {
                        SetActiveAccount(_accounts.First());
                    }

                    App.Logger.WriteLine(LOG_IDENT_REMOVE, $"Removed account {account.Username} ({account.UserId}).");
                    return true;
                }

                App.Logger.WriteLine(LOG_IDENT_REMOVE, $"Attempted to remove account {account.Username} but it was not found.");
                return false;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_REMOVE, ex);
                return false;
            }
        }

        private async Task<AltAccount?> GetAccountInfoFromCookie(string securityCookie)
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

                return new AltAccount(securityCookie, userId, username, displayName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_GET_INFO, ex);
                App.Logger.WriteLine(LOG_IDENT_GET_INFO, "Response JSON: " + json);
                return null;
            }
        }

        internal async void LaunchRobloxWithAccount(AltAccount account)
        {
            const string LOG_IDENT_LAUNCH = $"{LOG_IDENT}::LaunchRobloxWithAccount";

            if (account is null)
            {
                App.Logger.WriteLine(LOG_IDENT_LAUNCH, "Cannot launch - no account provided");
                return;
            }

            try
            {
                SetActiveAccount(account);
                SaveAccounts();

                App.Logger.WriteLine(LOG_IDENT_LAUNCH, $"Launching Roblox for {account.Username}");

                string result = await LaunchToPlaceAsync(account, placeId: 0, jobId: "");

                if (result == "Success")
                {
                    App.Logger.WriteLine(LOG_IDENT_LAUNCH, $"Successfully launched Roblox for {account.Username}");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_LAUNCH, $"Launch failed for {account.Username}: {result}");
                    FallbackToSystemProtocol();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_LAUNCH, ex);
                FallbackToSystemProtocol();
            }
        }

        public async Task LaunchAccountToPlaceAsync(AltAccount account, long placeId, string serverId = "")
        {
            const string LOG_IDENT_LAUNCH_PLACE = $"{LOG_IDENT}::LaunchAccountToPlace";

            if (account is null)
            {
                App.Logger.WriteLine(LOG_IDENT_LAUNCH_PLACE, "Cannot launch - no account provided");
                return;
            }

            try
            {
                App.Logger.WriteLine(LOG_IDENT_LAUNCH_PLACE, $"Attempting to launch {account.Username} to place {placeId} (server {serverId})");

                SetActiveAccount(account);
                SaveAccounts();

                var result = await LaunchToPlaceAsync(account, placeId, jobId: serverId).ConfigureAwait(false);

                if (result == "Success")
                {
                    App.Logger.WriteLine(LOG_IDENT_LAUNCH_PLACE, $"Successfully launched {account.Username} to place {placeId}");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_LAUNCH_PLACE, $"Launch failed for {account.Username}: {result}");
                    FallbackToSystemProtocol(placeId, serverId);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_LAUNCH_PLACE, ex);
                FallbackToSystemProtocol(placeId, serverId);
            }
        }

        public async Task LaunchAccountByIdentifierAsync(string identifier, long placeId, string serverId = "")
        {
            const string LOG_IDENT_LAUNCH_ID = $"{LOG_IDENT}::LaunchAccountByIdentifier";

            var account = GetAccount(identifier);
            if (account == null)
            {
                App.Logger.WriteLine(LOG_IDENT_LAUNCH_ID, $"Account '{identifier}' not found, using fallback launch");
                FallbackToSystemProtocol(placeId, serverId);
                return;
            }

            await LaunchAccountToPlaceAsync(account, placeId, serverId);
        }

        private void FallbackToSystemProtocol(long placeId = 0, string serverId = "")
        {
            const string LOG_IDENT_FALLBACK = $"{LOG_IDENT}::FallbackToSystemProtocol";

            try
            {
                string robloxUri;

                if (placeId == 0)
                {
                    robloxUri = "roblox-player:";
                }
                else
                {
                    robloxUri = string.IsNullOrEmpty(serverId)
                        ? $"roblox://experiences/start?placeId={placeId}"
                        : $"roblox://experiences/start?placeId={placeId}&gameInstanceId={serverId}";
                }

                App.Logger.WriteLine(LOG_IDENT_FALLBACK, $"Falling back to system protocol: {robloxUri}");

                Process.Start(new ProcessStartInfo
                {
                    FileName = robloxUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_FALLBACK, ex);
            }
        }

        public AltAccount? GetAccount(string identifier)
        {
            return _accounts.FirstOrDefault(acc =>
                acc.Username.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
                acc.UserId.ToString() == identifier);
        }

        public async Task<bool> ValidateAccountAsync(AltAccount account)
        {
            const string LOG_IDENT_VALIDATE = $"{LOG_IDENT}::ValidateAccount";

            try
            {
                string decryptedCookie = UnprotectString(account.SecurityToken);

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

        public async Task ValidateAllAccountsAsync()
        {
            const string LOG_IDENT_VALIDATE_ALL = $"{LOG_IDENT}::ValidateAllAccounts";

            App.Logger.WriteLine(LOG_IDENT_VALIDATE_ALL, "Starting validation of all accounts...");

            var invalidAccounts = new List<AltAccount>();

            foreach (var account in _accounts.ToList())
            {
                bool isValid = await ValidateAccountAsync(account);

                if (!isValid)
                {
                    invalidAccounts.Add(account);
                    App.Logger.WriteLine(LOG_IDENT_VALIDATE_ALL, $"Account {account.Username} is invalid and will be removed");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_VALIDATE_ALL, $"Account {account.Username} is valid, continuing to next account");
                }
            }

            foreach (var invalidAccount in invalidAccounts)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var result = Frontend.ShowMessageBox(
                        $"Account '{invalidAccount.DisplayName}' (@{invalidAccount.Username}) is no longer valid and will be removed.\n\nReason: Cookie expired or invalid",
                        MessageBoxImage.Warning,
                        MessageBoxButton.OK
                    );

                    RemoveAccount(invalidAccount);
                });
            }

            if (invalidAccounts.Count > 0)
            {
                App.Logger.WriteLine(LOG_IDENT_VALIDATE_ALL, $"Removed {invalidAccounts.Count} invalid accounts");

                if (ActiveAccount != null && invalidAccounts.Any(acc => acc.UserId == ActiveAccount.UserId))
                {
                    ActiveAccount = _accounts.FirstOrDefault();
                    ActiveAccountChanged?.Invoke(ActiveAccount);
                }

                SaveAccounts();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT_VALIDATE_ALL, "All accounts are valid");
            }
        }
    }
}