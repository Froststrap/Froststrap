using Bloxstrap.UI.ViewModels.AccountManagers;
using System.Windows;

namespace Bloxstrap.UI.Elements.AccountManagers.Pages
{
    public partial class AccountsPage
    {
        private AccountsViewModel? _viewModel;

        public AccountsPage()
        {
            _viewModel = new AccountsViewModel();
            DataContext = _viewModel;
            InitializeComponent();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AccountsViewModel viewModel)
            {
                viewModel.Cleanup();
            }
        }
    }
}