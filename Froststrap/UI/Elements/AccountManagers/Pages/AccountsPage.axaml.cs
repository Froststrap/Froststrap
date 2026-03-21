using Avalonia.Controls;
using Froststrap.UI.ViewModels.AccountManagers;

namespace Froststrap.UI.Elements.AccountManagers.Pages
{
	public partial class AccountsPage: UserControl
	{
		private AccountsViewModel? _viewModel;

		public AccountsPage()
		{
            _viewModel = DataContext as AccountsViewModel;
            DataContext = new AccountsViewModel();
			InitializeComponent();
		}
	}
}