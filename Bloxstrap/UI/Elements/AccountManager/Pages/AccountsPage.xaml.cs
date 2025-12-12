using Bloxstrap.UI.ViewModels.AccountManagers;
using Bloxstrap.UI.ViewModels.Settings;
using System.Windows;

namespace Bloxstrap.UI.Elements.AccountManagers.Pages
{
    public partial class AccountsPage
    {
        private AccountsViewModel? _viewModel;

        public AccountsPage()
        {
            DataContext = new AccountsViewModel();
            InitializeComponent();
            _viewModel = DataContext as AccountsViewModel;
        }
    }
}