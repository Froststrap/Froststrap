using Avalonia.Threading;
using Froststrap.Integrations;
using FluentAvalonia.UI.Controls;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.AccountManagers.Pages;

namespace Froststrap.UI.Elements.AccountManagers
{
	public partial class MainWindow : AvaloniaWindow
	{
		public MainWindow()
		{
			InitializeComponent();

			App.FrostRPC?.SetDialog("Account Manager");
			App.Logger.WriteLine("MainWindow", "Initializing account manager window");

			AccountManager.Shared.ActiveAccountChanged += OnActiveAccountChanged;

			UpdateNavigationItemsState();
		}

		private void OnActiveAccountChanged(AltAccount? account)
		{
			// Avalonia's equivalent to Dispatcher.Invoke
			Dispatcher.UIThread.Invoke(UpdateNavigationItemsState);
		}

		private void UpdateNavigationItemsState()
		{
			bool hasActiveAccount = AccountManager.Shared.ActiveAccount != null;

			// In Avalonia, we use IsVisible and direct Opacity settings
			if (friends != null)
			{
				friends.Opacity = hasActiveAccount ? 1 : 0.5;
				friends.IsEnabled = hasActiveAccount;
			}

			if (games != null)
			{
				games.Opacity = hasActiveAccount ? 1 : 0.5;
				games.IsEnabled = hasActiveAccount;
			}

			if (!hasActiveAccount)
			{
				// Check current navigation selection
				if (RootNavigation.SelectedItem is NavigationViewItem currentItem)
				{
					string? tag = currentItem.Tag?.ToString();
					if (tag == "friends" || tag == "games")
					{
						// Navigate back to accounts if active account is lost
						Navigate(typeof(AccountsPage));
					}
				}
			}
		}

		public void ShowLoading(string message = "Loading...")
		{
			Dispatcher.UIThread.Invoke(() =>
			{
				LoadingOverlayText.Text = message;
				LoadingOverlay.IsVisible = true;
			});
		}

		public void HideLoading()
		{
			Dispatcher.UIThread.Invoke(() =>
			{
				LoadingOverlay.IsVisible = false;
			});
		}

		// Avalonia uses OnClosing or overriding Close, but for cleanup 
		// we usually override the DetachedFromVisualTree or use the closed event
		protected override void OnClosed(EventArgs e)
		{
			AccountManager.Shared.ActiveAccountChanged -= OnActiveAccountChanged;
			base.OnClosed(e);
		}

		#region Navigation Methods

		// Navigation in Avalonia (FluentAvalonia) usually works by 
		// changing the Content of a ContentControl or Frame
		public bool Navigate(Type pageType)
		{
			var targetItem = RootNavigation.MenuItems
				.OfType<NavigationViewItem>()
				.FirstOrDefault(nvi => nvi.Tag is Type t && t == pageType);

			if (targetItem != null)
			{
				RootNavigation.SelectedItem = targetItem;
				RootFrame.Navigate(pageType);
				App.State.Prop.LastPage = pageType.FullName!;

				return true;
			}

			return false;
		}

		public void ShowWindow() => Show();

		public void CloseWindow() => Close();

		#endregion
	}
}