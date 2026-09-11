// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.UI.Elements.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Froststrap.Integrations.AccountManager
{
    internal class AccountManager
    {
        private const string AccountsFile = "AccountManager.json";

        private readonly string _accountsLocation;
        private List<AccountManagerAccount> _accounts = [];

        private readonly LoginService _loginService = new();
        private readonly AvatarService _avatarService = new();

        public event Action<AccountManagerAccount?>? ActiveAccountChanged;

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

        public void LoadAccounts()
        {
            if (!File.Exists(_accountsLocation))
                return;

            try
            {
                var json = File.ReadAllText(_accountsLocation);

                json = MigrateCredentials(json);

                var data = JsonConvert.DeserializeObject<AccountManagerData>(json);

                if (data?.Accounts != null)
                {
                    _accounts =
                    [
                        .. data.Accounts.Select(account => account with
                        {
                            SecurityToken = AccountSecurity.GetCredential(
                                account.UserId.ToString()
                            ) ?? string.Empty
                        })
                    ];

                    if (data.ActiveAccountId.HasValue)
                    {
                        ActiveAccount = _accounts.Find(
                            account => account.UserId == data.ActiveAccountId
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception: ", ex);
            }
        }

        private string MigrateCredentials(string json)
        {
            try
            {
                var root = JObject.Parse(json);
                var accounts = root["Accounts"] as JArray;

                if (accounts == null)
                    return json;

                bool foundLegacyCredentials = false;

                foreach (JObject account in accounts.OfType<JObject>())
                {
                    var userIdToken = account["UserId"];
                    var securityToken = account["SecurityToken"]?.Value<string>();

                    if (userIdToken == null || string.IsNullOrEmpty(securityToken))
                        continue;

                    foundLegacyCredentials = true;

                    long userId = userIdToken.Value<long>();

                    if (!AccountSecurity.TryUnprotect(
                            securityToken,
                            out string unprotectedToken
                        ))
                    {
                        throw new InvalidOperationException(
                            $"Failed to decrypt credential for account {userId}."
                        );
                    }

                    if (!AccountSecurity.SetCredential(
                            userId.ToString(),
                            unprotectedToken
                        ))
                    {
                        throw new InvalidOperationException(
                            $"Failed to store credential for account {userId}."
                        );
                    }

                    account.Remove("SecurityToken");
                }

                if (!foundLegacyCredentials)
                    return json;

                var migratedJson = root.ToString(Formatting.Indented);

                File.WriteAllText(_accountsLocation, migratedJson);

                App.Logger.Info(
                    "Successfully migrated account credentials to the OS credential store."
                );

                return migratedJson;
            }
            catch (Exception ex)
            {
                App.Logger.Error(
                    $"Failed to migrate account credentials: {ex}"
                );

                // Keep the original JSON if migration fails.
                return json;
            }
        }

        public void SaveAccounts()
        {
            try
            {
                foreach (var account in _accounts)
                {
                    if (string.IsNullOrEmpty(account.SecurityToken))
                        continue;

                    if (!AccountSecurity.SetCredential(
                        account.UserId.ToString(),
                        account.SecurityToken
                    ))
                    {
                        App.Logger.Warn($"Failed to save credential for account {account.UserId}.");
                    }
                }

                var data = new AccountManagerData
                {
                    Accounts = [.. _accounts],
                    ActiveAccountId = ActiveAccount?.UserId,
                    LastUpdated = DateTime.UtcNow,
                };

                File.WriteAllText(
                    _accountsLocation,
                    JsonConvert.SerializeObject(data, Formatting.Indented)
                );
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception: ", ex);
            }
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

        public void AddAccount(AccountManagerAccount account)
        {
            if (_accounts.Any(a => a.UserId == account.UserId))
                return;

            _accounts.Add(account);
            SaveAccounts();
        }

        public bool RemoveAccount(AccountManagerAccount account)
        {
            try
            {
                bool wasActive = ActiveAccount?.UserId == account.UserId;
                int removed = _accounts.RemoveAll(
                    a => a.UserId == account.UserId
                );

                if (removed > 0)
                {
                    AccountSecurity.DeleteCredential(
                        account.UserId.ToString()
                    );

                    if (wasActive)
                    {
                        ActiveAccount = _accounts.FirstOrDefault();
                        ActiveAccountChanged?.Invoke(ActiveAccount);
                    }

                    SaveAccounts();

                    App.Logger.Info(
                        $"Removed account {account.Username} ({account.UserId})."
                    );

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

        public string? GetRoblosecurityForUser(long userId) =>
            AccountSecurity.GetCredential(userId.ToString());

        public static Task<AccountManagerAccount?> AddAccountByQuickSignInAsync(
            QuickSignCodeDialog dialog,
            CancellationToken token
        ) =>
            LoginService.AddAccountByQuickSignInAsync(dialog, token);

        public async Task<AccountManagerAccount?> AddAccountByBrowserAsync() =>
            await _loginService.AddAccountByBrowserAsync(async cookie =>
            {
                var accountInfo =
                    await RobloxApiService.GetAccountInfoFromCookieAsync(cookie);

                if (accountInfo == null)
                    return null;

                var existing = _accounts.FirstOrDefault(
                    acc => acc.UserId == accountInfo.UserId
                );

                if (existing == null)
                {
                    AddAccount(accountInfo);
                    return accountInfo;
                }

                return existing;
            });

        public static Task<UserPresence?> GetUserPresenceAsync(long userId) =>
            RobloxApiService.GetUserPresenceAsync(userId);

        public static Task<bool?> ValidateAccountAsync(
            AccountManagerAccount account
        ) =>
            RobloxApiService.ValidateAccountAsync(account);

        public static bool WriteCookieFileForAccount(
            AccountManagerAccount account
        ) =>
            AccountCookieWriter.WriteCookieFileForAccount(account);

        public Task<Dictionary<long, string?>> GetAvatarUrlsBulkAsync(
            List<long> userIds
        ) =>
            _avatarService.GetAvatarUrlsBulkAsync(userIds);

        public string? GetCachedAvatarUrl(long userId) =>
            _avatarService.GetCachedAvatarUrl(userId);
    }
}
