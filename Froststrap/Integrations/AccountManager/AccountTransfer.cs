// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Backend;
using System.Security.Cryptography;

namespace Froststrap.Integrations.AccountManager
{
    internal sealed class AccountExportFile
    {
        public const string FormatName = "froststrap-accounts";

        [JsonPropertyName("format")]
        public string Format { get; set; } = FormatName;

        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("exportedAt")]
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("accounts")]
        public List<AccountExportEntry> Accounts { get; set; } = [];
    }

    internal sealed class AccountExportEntry
    {
        [JsonPropertyName("userId")]
        public long UserId { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("securityToken")]
        public string SecurityToken { get; set; } = "";
    }

    internal enum AccountImportOutcome
    {
        Added,
        Updated,
        Kept,
        Expired,
        Unreachable,
        Invalid,
        Cancelled,
    }

    internal sealed record AccountImportItem(string Name, AccountImportOutcome Outcome);

    /// Encrypted export/import of Account Manager accounts (#240).
    internal static class AccountTransfer
    {
        public const string FileExtension = "fsaccounts";
        public const int MinPasswordLength = 12;

        // matches accountvault: 45-byte header, 1 MiB payload, 16-byte tag
        public const long MaxFileSize = 45 + 1024 * 1024 + 16;
        public const int MaxAccounts = 200;

        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);

        // the same password typed on macOS (NFD) and Windows (NFC) must give the same key
        private static byte[] PasswordBytes(string password) =>
            Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC));

        public static Task<(AccountVaultResult Result, byte[] Data)> ExportAsync(IEnumerable<AccountManagerAccount> accounts, string password)
        {
            var file = new AccountExportFile
            {
                Accounts = [.. accounts
                    .Where(a => !string.IsNullOrEmpty(a.SecurityToken))
                    .Select(a => new AccountExportEntry
                    {
                        UserId = a.UserId,
                        Username = a.Username,
                        DisplayName = a.DisplayName,
                        SecurityToken = a.SecurityToken,
                    })]
            };

            return Task.Run(() =>
            {
                byte[] pw = [];
                byte[] plain = [];

                try
                {
                    pw = PasswordBytes(password);
                    plain = JsonSerializer.SerializeToUtf8Bytes(file, JsonHelper.TypeInfo<AccountExportFile>(JsonHelper.PlainOptions));

                    var result = AccountVault.Seal(pw, plain, out byte[] sealedData);
                    return (result, sealedData);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(pw);
                    CryptographicOperations.ZeroMemory(plain);
                }
            });
        }

        /// Decrypts and parses an export. Entries come back unvalidated.
        public static Task<(AccountVaultResult Result, List<AccountExportEntry> Accounts)> ReadAsync(byte[] data, string password)
        {
            return Task.Run(() =>
            {
                byte[] pw = [];
                byte[] plain = [];

                try
                {
                    if (data.LongLength > MaxFileSize)
                        return (AccountVaultResult.NotAVault, new List<AccountExportEntry>());

                    pw = PasswordBytes(password);
                    var result = AccountVault.Open(pw, data, out plain);
                    if (result != AccountVaultResult.Ok)
                        return (result, new List<AccountExportEntry>());

                    var file = JsonHelper.Deserialize<AccountExportFile>(plain, JsonHelper.PlainOptions);
                    return Check(file);
                }
                catch (JsonException)
                {
                    return (AccountVaultResult.NotAVault, new List<AccountExportEntry>());
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(pw);
                    CryptographicOperations.ZeroMemory(plain);
                }
            });
        }

        // a correctly encrypted file can still hold JSON we didn't write
        internal static (AccountVaultResult, List<AccountExportEntry>) Check(AccountExportFile? file)
        {
            if (file is null || file.Format != AccountExportFile.FormatName || file.Version < 1)
                return (AccountVaultResult.NotAVault, []);
            if (file.Version > 1)
                return (AccountVaultResult.UnsupportedVersion, []);
            if (file.Accounts is null || file.Accounts.Count > MaxAccounts || file.Accounts.Any(a => a is null))
                return (AccountVaultResult.NotAVault, []);

            return (AccountVaultResult.Ok, file.Accounts);
        }

        /// Entries whose account is already saved, going by the file's user IDs.
        public static List<AccountExportEntry> FindExisting(AccountManager manager, IEnumerable<AccountExportEntry> entries) =>
            [.. entries.Where(e => manager.Accounts.Any(a => a.UserId == e.UserId)).DistinctBy(e => e.UserId)];

        /// Verifies each session with Roblox and adds the ones whose owner it can confirm.
        /// Stops early when cancelled; the rest are reported as cancelled.
        public static async Task<List<AccountImportItem>> ImportAsync(
            AccountManager manager,
            IReadOnlyList<AccountExportEntry> entries,
            bool replaceExisting,
            IProgress<int>? progress,
            CancellationToken token)
        {
            var results = new List<AccountImportItem>();
            var unique = entries.DistinctBy(e => e.UserId).ToList();

            for (int i = 0; i < unique.Count; i++)
            {
                var entry = unique[i];
                string name = string.IsNullOrEmpty(entry.Username)
                    ? entry.UserId.ToString(CultureInfo.InvariantCulture)
                    : entry.Username;

                if (token.IsCancellationRequested)
                {
                    results.AddRange(unique.Skip(i).Select(e => new AccountImportItem(
                        string.IsNullOrEmpty(e.Username) ? e.UserId.ToString(CultureInfo.InvariantCulture) : e.Username,
                        AccountImportOutcome.Cancelled)));
                    break;
                }

                progress?.Report(i);
                results.Add(await ImportOneAsync(manager, entry, name, replaceExisting, token));
            }

            return results;
        }

        private static async Task<AccountImportItem> ImportOneAsync(AccountManager manager, AccountExportEntry entry, string name, bool replaceExisting, CancellationToken token)
        {
            if (entry.UserId <= 0 || string.IsNullOrWhiteSpace(entry.SecurityToken))
                return new(name, AccountImportOutcome.Invalid);

            AccountManagerAccount? account;
            try
            {
                // the session must tell us who it belongs to; the file's user ID is never trusted
                account = await RobloxApiService.GetAccountInfoFromCookieAsync(entry.SecurityToken).WaitAsync(CheckTimeout, token);

                if (account is null)
                {
                    var probe = new AccountManagerAccount(entry.SecurityToken, entry.UserId, entry.Username, entry.DisplayName);
                    bool? valid = await AccountManager.ValidateAccountAsync(probe).WaitAsync(CheckTimeout, token);
                    return new(name, valid == false ? AccountImportOutcome.Expired : AccountImportOutcome.Unreachable);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return new(name, AccountImportOutcome.Cancelled);
            }
            catch (TimeoutException)
            {
                return new(name, AccountImportOutcome.Unreachable);
            }

            name = string.IsNullOrEmpty(account.Username) ? name : account.Username;

            if (!replaceExisting && manager.Accounts.Any(a => a.UserId == account.UserId))
                return new(name, AccountImportOutcome.Kept);

            manager.AddOrUpdateAccount(account, out bool isNew);
            return new(name, isNew ? AccountImportOutcome.Added : AccountImportOutcome.Updated);
        }
    }
}
