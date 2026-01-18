using Avalonia.Controls;
using Froststrap.UI.ViewModels.AccountManagers;

namespace Froststrap.UI.Elements.AccountManagers.Pages
{
	public partial class AccountsPage: UserControl
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