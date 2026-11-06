// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Integrations.AccountManager
{
    internal static class RobloxApiService
    {
        public static async Task<AccountManagerAccount?> GetAccountInfoFromCookieAsync(string securityCookie)
        {
            try
            {
                using var handler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer(),
                    CheckCertificateRevocationList = true
                };
                handler.CookieContainer.Add(new Cookie(".ROBLOSECURITY", securityCookie, "/", ".roblox.com"));
                using var client = new HttpClient(handler);

                var response = await client.GetAsync(UrlBuilder.BuildApiUrl("users", "v1/users/authenticated", secure: true));
                response.EnsureSuccessStatusCode();

                var user = await JsonHelper.ReadFromJsonAsync<AuthenticatedUser>(response.Content);
                if (user == null) return null;

                return new AccountManagerAccount(
                    securityCookie,
                    user.Id,
                    user.Username,
                    user.DisplayName
                );
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
                var requestData = new PresenceBatchRequest { UserIds = [userId] };
                string jsonPayload = JsonHelper.Serialize(requestData);

                using var request = new HttpRequestMessage(HttpMethod.Post, UrlBuilder.BuildApiUrl("presence", "v1/presence/users", secure: true))
                {
                    Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json")
                };

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
                string cookie = account.SecurityToken;

                if (string.IsNullOrEmpty(cookie))
                    return false;

                using var handler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer(),
                    CheckCertificateRevocationList = true
                };
                handler.CookieContainer.Add(new Cookie(".ROBLOSECURITY", cookie, "/", ".roblox.com"));
                using var client = new HttpClient(handler);

                var response = await client.GetAsync(UrlBuilder.BuildApiUrl("users", "v1/users/authenticated", secure: true));

                if (response.StatusCode == HttpStatusCode.OK) return true;
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) return false;

                return null;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                return null;
            }
        }
    }
}
