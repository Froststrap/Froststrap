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
        Mismatched,
        NotSaved,
        Cancelled,
    }

    internal sealed record AccountImportItem(string Name, AccountImportOutcome Outcome);

    /// Encrypted export/import of Account Manager accounts (#240).
    internal static partial class AccountTransfer
    {
        public const string FileExtension = "fsaccounts";
        public const int MinPasswordLength = 12;

        // matches accountvault: 45-byte header, 1 MiB payload, 16-byte tag
        public const long MaxFileSize = 45 + 1024 * 1024 + 16;
        public const int MaxAccounts = 200;

        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);

        /// Names come from Roblox or a file, and message boxes render Markdown.
        public static string EscapeMarkdown(string text) => MarkdownSpecial().Replace(text, @"\$1");

        [GeneratedRegex(@"([\\`*_{}\[\]()#+\-.!|<>~])")]
        private static partial Regex MarkdownSpecial();

        // the same password typed on macOS (NFD) and Windows (NFC) must give the same key
        private static byte[] PasswordBytes(string password) =>
            Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC));

        public static Task<(AccountVaultResult Result, byte[] Data)> ExportAsync(IEnumerable<AccountManagerAccount> accounts, string password)
        {
            var list = accounts.Where(a => !string.IsNullOrEmpty(a.SecurityToken)).ToList();
            if (list.Count > MaxAccounts)
                return Task.FromResult((AccountVaultResult.InvalidArgument, Array.Empty<byte>()));

            var file = new AccountExportFile
            {
                Accounts = [.. list
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

        /// Saved accounts the file has an entry for. Names come from what's saved, not the file;
        /// the import later rejects any entry whose session turns out to belong to someone else.
        public static List<AccountManagerAccount> FindExisting(AccountManager manager, IEnumerable<AccountExportEntry> entries)
        {
            var ids = entries.Select(e => e.UserId).ToHashSet();
            return [.. manager.Accounts.Where(a => ids.Contains(a.UserId))];
        }

        /// Verifies each session with Roblox, then saves the confirmed ones in one go.
        /// Cancelling changes nothing; every entry is then reported as cancelled.
        public static async Task<List<AccountImportItem>> ImportAsync(
            AccountManager manager,
            IReadOnlyList<AccountExportEntry> entries,
            bool replaceExisting,
            IProgress<int>? progress,
            CancellationToken token)
        {
            var unique = entries.DistinctBy(e => e.UserId).ToList();
            var results = new List<AccountImportItem>();
            var toSave = new List<(int Index, AccountManagerAccount Account)>();

            for (int i = 0; i < unique.Count && !token.IsCancellationRequested; i++)
            {
                progress?.Report(i);

                var (item, account) = await CheckAsync(manager, unique[i], replaceExisting, token);
                results.Add(item);
                if (account is not null)
                    toSave.Add((results.Count - 1, account));
            }

            if (token.IsCancellationRequested)
                return [.. unique.Select(e => new AccountImportItem(NameOf(e), AccountImportOutcome.Cancelled))];

            if (toSave.Count == 0)
                return results;

            bool saved = manager.TryAddOrUpdateAccounts([.. toSave.Select(t => t.Account)], out var isNew);
            for (int i = 0; i < toSave.Count; i++)
            {
                var outcome = !saved ? AccountImportOutcome.NotSaved
                    : isNew[i] ? AccountImportOutcome.Added
                    : AccountImportOutcome.Updated;
                results[toSave[i].Index] = results[toSave[i].Index] with { Outcome = outcome };
            }

            return results;
        }

        private static string NameOf(AccountExportEntry entry) =>
            string.IsNullOrEmpty(entry.Username) ? entry.UserId.ToString(CultureInfo.InvariantCulture) : entry.Username;

        /// The account to save, or null with the reason it's skipped.
        private static async Task<(AccountImportItem Item, AccountManagerAccount? Account)> CheckAsync(
            AccountManager manager,
            AccountExportEntry entry,
            bool replaceExisting,
            CancellationToken token)
        {
            string name = NameOf(entry);

            if (entry.UserId <= 0 || string.IsNullOrWhiteSpace(entry.SecurityToken))
                return (new(name, AccountImportOutcome.Invalid), null);

            AccountManagerAccount? account;
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
            limit.CancelAfter(CheckTimeout);

            try
            {
                // the session must tell us who it belongs to; the file's user ID is never trusted
                account = await RobloxApiService.GetAccountInfoFromCookieAsync(entry.SecurityToken, limit.Token);

                if (account is null)
                {
                    var probe = new AccountManagerAccount(entry.SecurityToken, entry.UserId, entry.Username, entry.DisplayName);
                    bool? valid = await AccountManager.ValidateAccountAsync(probe, limit.Token);
                    return (new(name, valid == false ? AccountImportOutcome.Expired : AccountImportOutcome.Unreachable), null);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return (new(name, AccountImportOutcome.Cancelled), null);
            }
            catch (OperationCanceledException)
            {
                // our own time limit
                return (new(name, AccountImportOutcome.Unreachable), null);
            }

            // the replace prompt was about the file's accounts, so a session for anyone else can't ride along
            if (account.UserId != entry.UserId)
                return (new(name, AccountImportOutcome.Mismatched), null);

            name = string.IsNullOrEmpty(account.Username) ? name : account.Username;

            if (!replaceExisting && manager.Accounts.Any(a => a.UserId == account.UserId))
                return (new(name, AccountImportOutcome.Kept), null);

            // outcome is filled in once the batch is saved
            return (new(name, AccountImportOutcome.Added), account);
        }
    }
}
