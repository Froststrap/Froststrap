using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Froststrap.UI.ViewModels.AccountManagers
{
    public partial class AccountManagerViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? _currentPage;

        [ObservableProperty]
        private string _selectedPage = "accounts";

        [ObservableProperty]
        private string _currentPageTitle = "Accounts";

        public IRelayCommand NavigateToAccountsCommand { get; }
        // FIXME: I shouldn't be using a init to null.
        public IRelayCommand NavigateToFriendsCommand { get; } = null!;
        // FIXME: I shouldn't be using a init to null.
        public IRelayCommand NavigateToGamesCommand { get; } = null!;

        public AccountManagerViewModel()
        {
            NavigateToAccountsCommand = new RelayCommand(() =>
                Navigate("accounts", "Accounts", () => new AccountsViewModel()));
        }

        private void Navigate(string pageKey, string title, Func<object> viewModelFactory)
        {
            try
            {
                SelectedPage = pageKey;
                CurrentPageTitle = title;

                App.State.Prop.LastPage = pageKey;
                App.State.Save();

                CurrentPage = viewModelFactory();
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException($"AccountManagerViewModel::NavigateTo{title}", ex);
            }
        }
    }
}
