using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using Froststrap.Models;
using System.Text.Json;

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

        private string _selectedAddMethod = "Quick Sign In";
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

        public List<string> AddMethods { get; } = ["Quick Sign In", "Browser", "Manual"];

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
            CurrentAccount = account;
            CurrentAccountAvatarUrl = account != null ? GetAccountAvatarUrl(account.UserId) : null;
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
                }

                if (newAccount != null && Accounts.All(a => a.UserId != newAccount.UserId))
                {
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

        public event Action? OnManualAddRequested;

        public void AddAccountDirect(AccountManagerAccount account)
        {
            if (Accounts.All(a => a.UserId != account.UserId))
            {
                var avatarUrl = GetAccountAvatarUrl(account.UserId);
                Accounts.Add(new AccountWithAvatar(account, avatarUrl));
                _accountManager.SetActiveAccount(account.UserId);
            }
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