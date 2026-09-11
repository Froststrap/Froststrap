// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

namespace Froststrap.Integrations.AccountManager
{
    internal static class RobloxApiService
    {
        public static async Task<AccountManagerAccount?> GetAccountInfoFromCookieAsync(string securityCookie)
        {
            try
            {
                using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
                handler.CookieContainer.Add(new Cookie(".ROBLOSECURITY", securityCookie, "/", ".roblox.com"));
                using var client = new HttpClient(handler);

                var response = await client.GetAsync(UrlBuilder.BuildApiUrl("users", "v1/users/authenticated", secure: true));
                response.EnsureSuccessStatusCode();

                var jo = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync());
                if (jo == null) return null;

                return new AccountManagerAccount(
                    securityCookie,
                    jo["id"]?.Value<long>() ?? 0,
                    jo["name"]?.Value<string>() ?? "",
                    jo["displayName"]?.Value<string>() ?? ""
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
                var requestData = new { userIds = new[] { userId } };
                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(requestData);

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
                string decryptedCookie = AccountSecurity.Unprotect(account.SecurityToken);
                if (string.IsNullOrEmpty(decryptedCookie)) return false;

                using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
                handler.CookieContainer.Add(new Cookie(".ROBLOSECURITY", decryptedCookie, "/", ".roblox.com"));
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
