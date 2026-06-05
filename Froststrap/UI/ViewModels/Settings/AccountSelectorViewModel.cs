using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;

namespace Froststrap.UI.ViewModels.Settings
{
    public partial class AccountSelectorViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private const string LOG_IDENT = "AccountSelectorViewModel";
        private readonly AccountManager _accountManager = null!;
        private readonly Dictionary<long, string?> _accountAvatarUrls = [];


        private AccountManagerAccount? _currentAccount;
        public AccountManagerAccount? CurrentAccount
        {
            get => _currentAccount;
            set => SetProperty(ref _currentAccount, value);
        }

        private string? _currentAccountAvatarUrl;
        public string? CurrentAccountAvatarUrl
        {
            get => _currentAccountAvatarUrl;
            set => SetProperty(ref _currentAccountAvatarUrl, value);
        }

        private ObservableCollection<AccountWithAvatar> _accounts = [];
        public ObservableCollection<AccountWithAvatar> Accounts
        {
            get => _accounts;
            set => SetProperty(ref _accounts, value);
        }

        private string _selectedAddMethod = "Local Cookie";
        public string SelectedAddMethod
        {
            get => _selectedAddMethod;
            set => SetProperty(ref _selectedAddMethod, value);
        }

        private bool _isDropdownOpen;
        public bool IsDropdownOpen
        {
            get => _isDropdownOpen;
            set => SetProperty(ref _isDropdownOpen, value);
        }

        private bool _isAddingAccount;
        public bool IsAddingAccount
        {
            get => _isAddingAccount;
            set => SetProperty(ref _isAddingAccount, value);
        }

        public List<string> AddMethods { get; } = ["Local Cookie", "Quick Sign In", "Manual", "Browser"];

        public string CurrentAccountDisplayName => CurrentAccount == null ? "Not Logged In" : $"@{CurrentAccount.Username}";

        public AccountSelectorViewModel()
        {
            if (Design.IsDesignMode)
                return;

            _accountManager = AccountManager.Shared;
            _accountManager.ActiveAccountChanged += OnActiveAccountChanged;

            Task.Run(async () =>
            {
                try
                {
                    await InitializeDataAsync();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine($"{LOG_IDENT}::Init", $"Safe catch: {ex.Message}");
                }
            });
        }

        private async Task InitializeDataAsync()
        {
            try
            {
                await LoadDataAsync();
                App.Logger.WriteLine($"{LOG_IDENT}::InitializeDataAsync", "Initialised");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::InitializeDataAsync", $"Exception: {ex.Message}");
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var mgr = _accountManager;
                var accountIds = mgr.Accounts.Select(acc => acc.UserId).ToList();

                if (accountIds.Count > 0)
                {
                    var avatarUrls = await mgr.GetAvatarUrlsBulkAsync(accountIds);
                    foreach (var kvp in avatarUrls)
                    {
                        _accountAvatarUrls[kvp.Key] = kvp.Value;
                    }
                }

                CurrentAccount = mgr.ActiveAccount;
                if (CurrentAccount != null)
                {
                    CurrentAccountAvatarUrl = GetAccountAvatarUrl(CurrentAccount.UserId);
                }
                OnPropertyChanged(nameof(CurrentAccountDisplayName));

                Accounts.Clear();
                foreach (var account in mgr.Accounts)
                {
                    var url = _accountAvatarUrls.TryGetValue(account.UserId, out var u) ? u : null;
                    Accounts.Add(new AccountWithAvatar(account, url));
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to load account data: {ex.Message}");
            }
        }

        private void OnActiveAccountChanged(AccountManagerAccount? account)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentAccount = account;
                CurrentAccountAvatarUrl = account != null ? GetAccountAvatarUrl(account.UserId) : null;
                OnPropertyChanged(nameof(CurrentAccountDisplayName));
            });
        }

        [RelayCommand]
        private void SelectAccount(AccountWithAvatar item)
        {
            _accountManager.SetActiveAccount(item.UserId);
            IsDropdownOpen = false;
        }

        [RelayCommand]
        private void ToggleDropdown() => IsDropdownOpen = !IsDropdownOpen;

        [RelayCommand]
        private void DeleteAccount(AccountWithAvatar item)
        {
            _accountManager.RemoveAccount(item.Account);
            Accounts.Remove(item);
        }

        [RelayCommand]
        private async Task AddAccount()
        {
            IsAddingAccount = true;

            try
            {
                AccountManagerAccount? newAccount = null;

                switch (SelectedAddMethod)
                {
                    case "Quick Sign In":
                        newAccount = await _accountManager.AddAccountByQuickSignInAsync();
                        break;
                    case "Browser":
                        newAccount = await _accountManager.AddAccountByBrowser();
                        break;
                    case "Manual":
                        OnManualAddRequested?.Invoke();
                        return;
                    case "Local Cookie":
                        newAccount = await ImportFromRobloxClient();
                        break;
                }

                if (newAccount != null && Accounts.All(a => a.UserId != newAccount.UserId))
                {
                    _accountManager.AddAccount(newAccount);

                    var avatarUrlMap = await _accountManager.GetAvatarUrlsBulkAsync([newAccount.UserId]);
                    var url = avatarUrlMap.GetValueOrDefault(newAccount.UserId);
                    _accountAvatarUrls[newAccount.UserId] = url;

                    Accounts.Add(new AccountWithAvatar(newAccount, url));
                    _accountManager.SetActiveAccount(newAccount.UserId);
                    IsDropdownOpen = false;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error adding account: {ex.Message}");
            }
            finally
            {
                IsAddingAccount = false;
            }
        }

        private static async Task<AccountManagerAccount?> ImportFromRobloxClient()
        {
            const string LOG_IDENT = "ImportFromRobloxClient";

            var cookieManager = new CookiesManager();

            await cookieManager.LoadCookies();

            if (!cookieManager.Loaded)
            {
                string error = cookieManager.State switch
                {
                    CookieState.NotAllowed => "Cookie access is disabled in settings.",
                    CookieState.NotFound => "Roblox cookie file not found.",
                    CookieState.Invalid => "Cookie found but is invalid or expired.",
                    CookieState.Failed => "Failed to load cookie file.",
                    _ => "Could not load Roblox cookie."
                };
                _ = Frontend.ShowMessageBox(error);
                return null;
            }

            var authUser = await cookieManager.GetAuthenticated();
            if (authUser == null || authUser.Id == 0)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to get authenticated user from cookie.");
                return null;
            }

            string cookieValue = cookieManager.GetAuthCookie();
            if (string.IsNullOrEmpty(cookieValue))
            {
                App.Logger.WriteLine(LOG_IDENT, "Auth cookie is empty.");
                return null;
            }

            return new AccountManagerAccount(
                securityToken: cookieValue,
                userId: authUser.Id,
                username: authUser.Username,
                displayName: authUser.DisplayName
            );
        }

        public event Action? OnManualAddRequested;

        public async void AddAccountDirect(AccountManagerAccount account)
        {
            _accountManager.AddAccount(account);

            string? avatarUrl = null;
            try
            {
                var urlMap = await _accountManager.GetAvatarUrlsBulkAsync([account.UserId]);
                avatarUrl = urlMap.GetValueOrDefault(account.UserId);
                if (avatarUrl != null)
                    _accountAvatarUrls[account.UserId] = avatarUrl;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to fetch avatar for {account.UserId}: {ex.Message}");
            }

            if (Accounts.All(a => a.UserId != account.UserId))
            {
                Accounts.Add(new AccountWithAvatar(account, avatarUrl));
            }

            _accountManager.SetActiveAccount(account.UserId);

            IsDropdownOpen = false;
        }

        public class AccountWithAvatar(AccountManagerAccount account, string? avatarUrl)
        {
            public AccountManagerAccount Account { get; } = account;
            public string? AvatarUrl { get; } = avatarUrl;

            public string Username => Account.Username;
            public string DisplayName => Account.DisplayName;
            public long UserId => Account.UserId;
        }

        public string? GetAccountAvatarUrl(long userId)
        {
            return _accountAvatarUrls.TryGetValue(userId, out var url)
                ? url
                : _accountManager.GetCachedAvatarUrl(userId);
        }

        public void Dispose()
        {
            _accountManager.ActiveAccountChanged -= OnActiveAccountChanged;
            GC.SuppressFinalize(this);
        }
    }
}