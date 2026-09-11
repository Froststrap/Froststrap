using Froststrap.UI.Elements.Dialogs;
using Newtonsoft.Json;

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
            if (!File.Exists(_accountsLocation)) return;
            try
            {
                var data = JsonConvert.DeserializeObject<AccountManagerData>(File.ReadAllText(_accountsLocation));
                if (data?.Accounts != null)
                {
                    _accounts = [.. data.Accounts.Select(acc => acc with { SecurityToken = AccountSecurity.Unprotect(acc.SecurityToken) })];
                    if (data.ActiveAccountId.HasValue)
                        ActiveAccount = _accounts.Find(a => a.UserId == data.ActiveAccountId);
                }
            }
            catch (Exception ex) { App.Logger.Error("Unhandled exception: ", ex); }
        }

        public void SaveAccounts()
        {
            try
            {
                var data = new AccountManagerData
                {
                    Accounts = [.. _accounts.Select(acc => acc with { SecurityToken = AccountSecurity.Protect(acc.SecurityToken) })],
                    ActiveAccountId = ActiveAccount?.UserId,
                    LastUpdated = DateTime.UtcNow,
                };
                File.WriteAllText(_accountsLocation, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception ex) { App.Logger.Error("Unhandled exception: ", ex); }
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
            if (_accounts.Any(a => a.UserId == account.UserId)) return;
            _accounts.Add(account);
            SaveAccounts();
        }

        public bool RemoveAccount(AccountManagerAccount account)
        {
            try
            {
                bool wasActive = ActiveAccount?.UserId == account.UserId;
                int removed = _accounts.RemoveAll(a => a.UserId == account.UserId);

                if (removed > 0)
                {
                    if (wasActive)
                    {
                        ActiveAccount = _accounts.FirstOrDefault();
                        ActiveAccountChanged?.Invoke(ActiveAccount);
                    }

                    SaveAccounts();
                    App.Logger.Info($"Removed account {account.Username} ({account.UserId}).");
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
            _accounts.FirstOrDefault(x => x.UserId == userId)?.SecurityToken;

        public static Task<AccountManagerAccount?> AddAccountByQuickSignInAsync(QuickSignCodeDialog dialog, CancellationToken token) =>
            LoginService.AddAccountByQuickSignInAsync(dialog, token);

        public async Task<AccountManagerAccount?> AddAccountByBrowserAsync() =>
            await _loginService.AddAccountByBrowserAsync(async cookie =>
            {
                var accountInfo = await RobloxApiService.GetAccountInfoFromCookieAsync(cookie);
                if (accountInfo == null) return null;

                var existing = _accounts.FirstOrDefault(acc => acc.UserId == accountInfo.UserId);
                if (existing == null)
                {
                    AddAccount(accountInfo);
                    return accountInfo;
                }
                return existing;
            });

        public static Task<UserPresence?> GetUserPresenceAsync(long userId) => RobloxApiService.GetUserPresenceAsync(userId);
        public static Task<bool?> ValidateAccountAsync(AccountManagerAccount account) => RobloxApiService.ValidateAccountAsync(account);
        public static bool WriteCookieFileForAccount(AccountManagerAccount account) => AccountCookieWriter.WriteCookieFileForAccount(account);

        public Task<Dictionary<long, string?>> GetAvatarUrlsBulkAsync(List<long> userIds) => _avatarService.GetAvatarUrlsBulkAsync(userIds);
        public string? GetCachedAvatarUrl(long userId) => _avatarService.GetCachedAvatarUrl(userId);
    }
}
