using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using Froststrap.Models;

namespace Froststrap.UI.ViewModels.Settings
{
    public partial class AccountSelectorViewModel : ObservableObject
    {
        private readonly AccountManager _accountManager;

        [ObservableProperty]
        private AccountManagerAccount? currentAccount;

        [ObservableProperty]
        private ObservableCollection<AccountManagerAccount> accounts = new();

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
            _accountManager = AccountManager.Shared;
            InitializeAccounts();
            _accountManager.ActiveAccountChanged += OnActiveAccountChanged;
        }

        private void InitializeAccounts()
        {
            CurrentAccount = _accountManager.ActiveAccount;
            
            Accounts.Clear();
            foreach (var account in _accountManager.Accounts)
            {
                Accounts.Add(account);
            }
        }

        private void OnActiveAccountChanged(AccountManagerAccount? account)
        {
            CurrentAccount = account;
        }

        [RelayCommand]
        private void SelectAccount(AccountManagerAccount account)
        {
            _accountManager.SetActiveAccount(account.UserId);
            IsDropdownOpen = false;
        }

        [RelayCommand]
        private void ToggleDropdown()
        {
            IsDropdownOpen = !IsDropdownOpen;
        }

        [RelayCommand]
        private void DeleteAccount(AccountManagerAccount account)
        {
            _accountManager.RemoveAccount(account);
            Accounts.Remove(account);
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
                        // Manual is handled via event - fire and return
                        OnManualAddRequested?.Invoke();
                        return;
                }

                if (newAccount != null && !Accounts.Any(a => a.UserId == newAccount.UserId))
                {
                    Accounts.Add(newAccount);
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
                Accounts.Add(account);
                _accountManager.SetActiveAccount(account.UserId);
            }
            IsDropdownOpen = false;
        }

        public void Dispose()
        {
            _accountManager.ActiveAccountChanged -= OnActiveAccountChanged;
        }
    }
}
