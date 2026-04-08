using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using Froststrap.Models;

namespace Froststrap.UI.ViewModels.Settings
{
    public partial class AccountSelectorViewModel : ObservableObject
    {
        private const string LOG_IDENT = "AccountSelectorViewModel";
        private readonly AccountManager _accountManager;
        private readonly Dictionary<long, string?> _accountAvatarUrls = new();

        [ObservableProperty]
        private AccountManagerAccount? currentAccount;

        [ObservableProperty]
        private string? currentAccountAvatarUrl;

        [ObservableProperty]
        private ObservableCollection<AccountWithAvatar> accounts = new();

        [ObservableProperty]
        private string selectedAddMethod = "Quick Sign In";

        [ObservableProperty]
        private bool isDropdownOpen = false;

        [ObservableProperty]
        private bool isAddingAccount = false;

        public List<string> AddMethods { get; } = new()
        {
            "Quick Sign In",
            "Browser",
            "Manual"
        };

        public AccountSelectorViewModel()
        {
            if (Design.IsDesignMode)
                return;

            _accountManager = AccountManager.Shared;
            _accountManager.ActiveAccountChanged += OnActiveAccountChanged;

            _ = InitializeDataAsync();
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

            Accounts.Clear();
            foreach (var account in mgr.Accounts)
            {
                var url = _accountAvatarUrls.TryGetValue(account.UserId, out var u) ? u : null;
                Accounts.Add(new AccountWithAvatar(account, url));
            }
        }

        private void OnActiveAccountChanged(AccountManagerAccount? account)
        {
            CurrentAccount = account;
            if (account != null)
            {
                CurrentAccountAvatarUrl = GetAccountAvatarUrl(account.UserId);
            }
            else
            {
                CurrentAccountAvatarUrl = null;
            }
        }

        [RelayCommand]
        private void SelectAccount(AccountWithAvatar item)
        {
            _accountManager.SetActiveAccount(item.UserId);
            IsDropdownOpen = false;
        }

        [RelayCommand]
        private void ToggleDropdown()
        {
            IsDropdownOpen = !IsDropdownOpen;
        }

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
                }

                if (newAccount != null && !Accounts.Any(a => a.UserId == newAccount.UserId))
                {
                    // Fetch avatar for the new account
                    var avatarUrl = await _accountManager.GetAvatarUrlsBulkAsync(new List<long> { newAccount.UserId });
                    var url = avatarUrl.GetValueOrDefault(newAccount.UserId);
                    _accountAvatarUrls[newAccount.UserId] = url;

                    Accounts.Add(new AccountWithAvatar(newAccount, url));
                    _accountManager.SetActiveAccount(newAccount.UserId);
                    IsDropdownOpen = false;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountSelectorViewModel", $"Error adding account: {ex.Message}");
            }
            finally
            {
                IsAddingAccount = false;
            }
        }

        public event Action? OnManualAddRequested;

        public void AddAccountDirect(AccountManagerAccount account)
        {
            if (!Accounts.Any(a => a.UserId == account.UserId))
            {
                var avatarUrl = GetAccountAvatarUrl(account.UserId);
                Accounts.Add(new AccountWithAvatar(account, avatarUrl));
                _accountManager.SetActiveAccount(account.UserId);
            }
            IsDropdownOpen = false;
        }

        public class AccountWithAvatar
        {
            public AccountManagerAccount Account { get; }
            public string? AvatarUrl { get; }

            public AccountWithAvatar(AccountManagerAccount account, string? avatarUrl)
            {
                Account = account;
                AvatarUrl = avatarUrl;
            }

            public string Username => Account.Username;
            public string DisplayName => Account.DisplayName;
            public long UserId => Account.UserId;
        }

        public string? GetAccountAvatarUrl(long userId)
        {
            if (_accountAvatarUrls.TryGetValue(userId, out var url))
            {
                return url;
            }
            return _accountManager.GetCachedAvatarUrl(userId);
        }

        public void Dispose()
        {
            _accountManager.ActiveAccountChanged -= OnActiveAccountChanged;
        }
    }
}
