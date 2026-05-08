using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Froststrap.UI.ViewModels.AccountManagers;

public partial class AccountManagerViewModel
{
    public object? CurrentPage { get; set; }

    public string SelectedPage { get; set; } = "accounts";

    public string CurrentPageTitle { get; set; } = "Accounts";

    public IRelayCommand NavigateToAccountsCommand { get; }

    public AccountManagerViewModel()
    {
        NavigateToAccountsCommand = new RelayCommand(() =>
            Navigate("accounts", "Accounts", () => new AccountsViewModel()));
        NavigateToAccountsCommand.Execute(null);
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