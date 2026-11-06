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
        Expired,
        Unchecked,
        Invalid,
    }

    internal sealed record AccountImportItem(string Name, AccountImportOutcome Outcome);

    /// Encrypted export/import of Account Manager accounts (#240).
    internal static class AccountTransfer
    {
        public const string FileExtension = "fsaccounts";
        public const int MinPasswordLength = 12;

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
                byte[] pw = PasswordBytes(password);
                byte[] plain = JsonSerializer.SerializeToUtf8Bytes(file, JsonHelper.TypeInfo<AccountExportFile>(JsonHelper.PlainOptions));

                try
                {
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
                byte[] pw = PasswordBytes(password);
                byte[] plain = [];

                try
                {
                    var result = AccountVault.Open(pw, data, out plain);
                    if (result != AccountVaultResult.Ok)
                        return (result, new List<AccountExportEntry>());

                    var file = JsonHelper.Deserialize<AccountExportFile>(plain, JsonHelper.PlainOptions);
                    if (file is null || file.Format != AccountExportFile.FormatName)
                        return (AccountVaultResult.NotAVault, new List<AccountExportEntry>());
                    if (file.Version > 1)
                        return (AccountVaultResult.UnsupportedVersion, new List<AccountExportEntry>());

                    return (AccountVaultResult.Ok, file.Accounts);
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

        /// Checks each entry with Roblox and merges the working ones into the account manager.
        public static async Task<List<AccountImportItem>> ImportAsync(AccountManager manager, IEnumerable<AccountExportEntry> entries)
        {
            var results = new List<AccountImportItem>();

            foreach (var entry in entries.DistinctBy(e => e.UserId))
            {
                string name = string.IsNullOrEmpty(entry.Username)
                    ? entry.UserId.ToString(CultureInfo.InvariantCulture)
                    : entry.Username;

                if (entry.UserId <= 0 || string.IsNullOrWhiteSpace(entry.SecurityToken))
                {
                    results.Add(new(name, AccountImportOutcome.Invalid));
                    continue;
                }

                // trust Roblox over the file for who the session belongs to
                var account = await RobloxApiService.GetAccountInfoFromCookieAsync(entry.SecurityToken);
                bool isChecked = account is not null;

                if (account is null)
                {
                    account = new AccountManagerAccount(entry.SecurityToken, entry.UserId, entry.Username, entry.DisplayName);
                    if (await AccountManager.ValidateAccountAsync(account) == false)
                    {
                        results.Add(new(name, AccountImportOutcome.Expired));
                        continue;
                    }
                }

                manager.AddOrUpdateAccount(account, out bool isNew);

                // couldn't reach Roblox; keep it, the next validation pass sorts it out
                var outcome = !isChecked ? AccountImportOutcome.Unchecked
                    : isNew ? AccountImportOutcome.Added
                    : AccountImportOutcome.Updated;
                results.Add(new(string.IsNullOrEmpty(account.Username) ? name : account.Username, outcome));
            }

            return results;
        }
    }
}
