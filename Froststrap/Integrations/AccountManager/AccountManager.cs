// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.UI.Elements.Dialogs;
using System.Text.Json.Nodes;

namespace Froststrap.Integrations.AccountManager
{
    internal class AccountManager
    {
        private const string AccountsFile = "AccountManager.json";

        private readonly string _accountsLocation;
        private List<AccountManagerAccount> _accounts = [];

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

                var data = JsonHelper.Deserialize<AccountManagerData>(json);

                if (data?.Accounts != null)
                {
                    _accounts =
                    [
                        .. data.Accounts.Select(account => account with
                        {
                            SecurityToken = AccountSecurity.GetCredential(
                                account.UserId.ToString(CultureInfo.InvariantCulture)
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
                App.Logger.Error(ex, "Failed to load accounts");
            }
        }

        private string MigrateCredentials(string json)
        {
            try
            {
                var root = JsonNode.Parse(json) as JsonObject;

                if (root?[nameof(AccountManagerData.Accounts)] is not JsonArray accounts)
                    return json;

                bool foundLegacyCredentials = false;

                foreach (var accountNode in accounts)
                {
                    if (accountNode is not JsonObject account)
                        continue;

                    var userIdToken = account[nameof(AccountManagerAccount.UserId)];
                    var securityToken = account[nameof(AccountManagerAccount.SecurityToken)]?.GetValue<string>();

                    if (userIdToken == null || string.IsNullOrEmpty(securityToken))
                        continue;

                    foundLegacyCredentials = true;

                    long userId = userIdToken.GetValue<long>();

                    if (!AccountSecurity.TryUnprotect(securityToken, out string unprotectedToken))
                    {
                        throw new InvalidOperationException(
                            $"Failed to decrypt credential for account {userId}."
                        );
                    }

                    if (!AccountSecurity.SetCredential(
                            userId.ToString(CultureInfo.InvariantCulture),
                            unprotectedToken))
                    {
                        throw new InvalidOperationException(
                            $"Failed to store credential for account {userId}."
                        );
                    }

                    account.Remove(nameof(AccountManagerAccount.SecurityToken));
                }

                if (!foundLegacyCredentials)
                    return json;

                var migratedJson = root.ToJsonString(JsonHelper.Options);
                File.WriteAllText(_accountsLocation, migratedJson);

                App.Logger.Info("Successfully migrated account credentials to the OS credential store.");

                return migratedJson;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to migrate account credentials: {ex}");
                return json;
            }
        }

        /// Whether the last save wrote both the credentials and AccountManager.json.
        public bool LastSaveSucceeded { get; private set; } = true;

        public void SaveAccounts()
        {
            SaveAccountsCore(writeCredentials: true);
        }

        private void SaveAccountsCore(bool writeCredentials)
        {
            bool ok = true;

            try
            {
                if (writeCredentials)
                {
                    foreach (var account in _accounts)
                    {
                        if (string.IsNullOrEmpty(account.SecurityToken))
                            continue;

                        if (!AccountSecurity.SetCredential(
                            account.UserId.ToString(CultureInfo.InvariantCulture),
                            account.SecurityToken
                        ))
                        {
                            App.Logger.Warn($"Failed to save credential for account {account.UserId}.");
                            ok = false;
                        }
                    }
                }

                var data = new AccountManagerData
                {
                    Accounts = [.. _accounts],
                    ActiveAccountId = ActiveAccount?.UserId,
                    LastUpdated = DateTime.UtcNow,
                };

                // a failed write leaves the previous file intact
                AtomicFile.WriteAllText(_accountsLocation, JsonHelper.Serialize(data));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to save accounts");
                ok = false;
            }

            LastSaveSucceeded = ok;
        }

        public void ReorderAccounts(IReadOnlyList<AccountManagerAccount> ordered)
        {
            if (ordered.Count != _accounts.Count)
                return;

            _accounts = [.. ordered];
            SaveAccountsCore(writeCredentials: false);
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

        public AccountManagerAccount AddOrUpdateAccount(AccountManagerAccount account, out bool isNew)
        {
            var result = Merge(account, out isNew);

            if (!isNew && ActiveAccount?.UserId == result.UserId)
            {
                ActiveAccount = result;
                ActiveAccountChanged?.Invoke(result);
            }

            SaveAccounts();
            return result;
        }

        /// Adds or updates several accounts with a single save. If saving fails, the account list
        /// and the affected credentials are put back the way they were and false is returned.
        /// Nothing is changed if the current credentials can't be read first. The rollback is
        /// best effort, not a transaction: restoring a credential can fail too, and that's logged.
        public bool TryAddOrUpdateAccounts(IReadOnlyList<AccountManagerAccount> accounts, out List<bool> isNew)
        {
            isNew = [];

            // only a known previous state can be restored, so a read error stops the batch up front
            var credentialsBefore = new Dictionary<string, string?>();
            foreach (var id in accounts.Select(a => a.UserId.ToString(CultureInfo.InvariantCulture)).Distinct())
            {
                var read = AccountSecurity.ReadCredential(id, out string token);
                if (read == CredentialRead.Failed)
                {
                    App.Logger.Warn($"Couldn't read the saved credential for account {id}; not importing.");
                    return false;
                }
                credentialsBefore[id] = read == CredentialRead.Found ? token : null;
            }

            var listBefore = _accounts.ToList();
            var activeBefore = ActiveAccount;

            foreach (var account in accounts)
            {
                var merged = Merge(account, out bool added);
                isNew.Add(added);

                if (!added && ActiveAccount?.UserId == merged.UserId)
                    ActiveAccount = merged;
            }

            SaveAccounts();

            // trust the store, not the return values: every imported session must read back as written
            bool verified = LastSaveSucceeded && accounts.All(a =>
                AccountSecurity.ReadCredential(a.UserId.ToString(CultureInfo.InvariantCulture), out string saved) == CredentialRead.Found
                && saved == _accounts.Find(x => x.UserId == a.UserId)?.SecurityToken);

            if (verified)
            {
                if (ActiveAccount is not null && !ReferenceEquals(ActiveAccount, activeBefore))
                    ActiveAccountChanged?.Invoke(ActiveAccount);
                return true;
            }

            _accounts.Clear();
            _accounts.AddRange(listBefore);
            ActiveAccount = activeBefore;

            foreach (var (id, credential) in credentialsBefore)
            {
                bool restored = credential is null
                    ? AccountSecurity.DeleteCredential(id)
                    : AccountSecurity.SetCredential(id, credential);

                if (!restored)
                    App.Logger.Warn($"Couldn't restore the saved credential for account {id} after a failed save.");
            }

            // AccountManager.json may have been written before a credential failed
            SaveAccountsCore(writeCredentials: false);
            LastSaveSucceeded = false;
            return false;
        }

        // updates the list only; callers save and raise events
        private AccountManagerAccount Merge(AccountManagerAccount account, out bool isNew)
        {
            int index = _accounts.FindIndex(a => a.UserId == account.UserId);

            if (index < 0)
            {
                isNew = true;
                _accounts.Add(account);
                return account;
            }

            isNew = false;
            var existing = _accounts[index];

            var updated = existing with
            {
                Username = account.Username,
                DisplayName = account.DisplayName,
                SecurityToken = string.IsNullOrEmpty(account.SecurityToken)
                    ? existing.SecurityToken
                    : account.SecurityToken
            };

            _accounts[index] = updated;
            return updated;
        }

        public void AddAccount(AccountManagerAccount account) => AddOrUpdateAccount(account, out _);

        public async Task<bool> RefreshAccountInfoAsync(AccountManagerAccount account)
        {
            string? cookie = GetRoblosecurityForUser(account.UserId);
            if (string.IsNullOrEmpty(cookie))
                return false;

            var info = await RobloxApiService.GetAccountInfoFromCookieAsync(cookie);
            if (info == null || info.UserId != account.UserId)
                return false;

            AddOrUpdateAccount(info, out _);
            return true;
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
                        account.UserId.ToString(CultureInfo.InvariantCulture)
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
#pragma warning disable CA1822
        public string? GetRoblosecurityForUser(long userId) =>
            AccountSecurity.GetCredential(userId.ToString(CultureInfo.InvariantCulture));
#pragma warning restore CA1822

        public static Task<AccountManagerAccount?> AddAccountByQuickSignInAsync(
            QuickSignCodeDialog dialog,
            CancellationToken token
        ) =>
            LoginService.AddAccountByQuickSignInAsync(dialog, token);

        public static async Task<AccountManagerAccount?> AddAccountByBrowserAsync() =>
            await LoginService.AddAccountByBrowserAsync(async cookie =>
                await RobloxApiService.GetAccountInfoFromCookieAsync(cookie));

        public static Task<UserPresence?> GetUserPresenceAsync(long userId) =>
            RobloxApiService.GetUserPresenceAsync(userId);

        public static Task<bool?> ValidateAccountAsync(
            AccountManagerAccount account,
            CancellationToken token = default
        ) =>
            RobloxApiService.ValidateAccountAsync(account, token);

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
