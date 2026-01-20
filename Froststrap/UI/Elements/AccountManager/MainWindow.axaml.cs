using System.Collections.ObjectModel;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Froststrap.Integrations;
using Froststrap.UI.Elements.AccountManagers.Pages;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.Settings.Pages;

namespace Froststrap.UI.Elements.AccountManagers
{

	public partial class MainWindow : AvaloniaWindow
	{
		public static ObservableCollection<NavigationViewItem> MainNavigationItems { get; } = new();
		public static ObservableCollection<NavigationViewItem> FooterNavigationItems { get; } = new();
		public ObservableCollection<NavigationViewItem> NavigationItemsView { get; } = new();

		public MainWindow()
		{
			InitializeComponent();

			App.FrostRPC?.SetDialog("Account Manager");
			App.Logger.WriteLine("MainWindow", "Initializing account manager window");

			AccountManager.Shared.ActiveAccountChanged += OnActiveAccountChanged;

			string? lastPageName = App.State.Prop.LastPage;
			Type? lastPage = lastPageName is null ? null : Type.GetType(lastPageName);

			var allItems = RootNavigation.MenuItems.Cast<NavigationViewItem>().ToList();
			var allFooters = RootNavigation.FooterMenuItems.Cast<NavigationViewItem>().ToList();

			MainNavigationItems.Clear();
			foreach (var item in allItems) MainNavigationItems.Add(item);

			FooterNavigationItems.Clear();
			foreach (var item in allFooters) FooterNavigationItems.Add(item);

			if (lastPage != null)
				SafeNavigate(lastPage);
			else
				RootNavigation.SelectedItem = RootNavigation.MenuItems.Cast<NavigationViewItem>().FirstOrDefault();

			RootNavigation.SelectionChanged += OnNavigationChanged;
		}

		private void OnActiveAccountChanged(AltAccount? account)
		{
			Dispatcher.UIThread.Invoke(UpdateNavigationItemsState);
		}

		private void UpdateNavigationItemsState()
		{
			bool hasActiveAccount = AccountManager.Shared.ActiveAccount != null;

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
				if (RootNavigation.SelectedItem is NavigationViewItem currentItem)
				{
					string? tag = currentItem.Tag?.ToString();
					if (tag == "friends" || tag == "games")
					{
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

		private void OnNavigationChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
		{
			if (e.SelectedItem is NavigationViewItem navItem && navItem.Tag is Type pageType)
			{
				Navigate(pageType);
			}
		}

		private async void SafeNavigate(Type page)
		{
			await Task.Delay(500);

			if (page == typeof(RobloxSettingsPage) && !App.GlobalSettings.Loaded)
				return;

			Navigate(page);
		}

		public bool Navigate(Type pageType)
		{
			var targetItem = RootNavigation.MenuItems
				.OfType<NavigationViewItem>()
				.FirstOrDefault(nvi => nvi.Tag is Type t && t == pageType);

			if (RootFrame.Content?.GetType() != pageType)
			{
				RootFrame.Navigate(pageType);

				App.State.Prop.LastPage = pageType.FullName!;

				if (targetItem != null && RootNavigation.SelectedItem != targetItem)
				{
					RootNavigation.SelectedItem = targetItem;
				}

				return true;
			}

			return false;
		}

		public void ShowWindow() => Show();

		public void CloseWindow() => Close();

		#endregion
	}
}