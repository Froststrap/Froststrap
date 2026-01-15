using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Froststrap.UI.Elements.Settings.Pages;
using Froststrap.UI.ViewModels.Settings;
using Froststrap.Resources;

namespace Froststrap.UI.Elements.Settings
{
	public partial class MainWindow : Window
	{
		private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;

		public static ObservableCollection<NavigationViewItem> MainNavigationItems { get; } = new();
		public static ObservableCollection<NavigationViewItem> FooterNavigationItems { get; } = new();
		public ObservableCollection<NavigationViewItem> NavigationItemsView { get; } = new();

		public static List<string> DefaultNavigationOrder { get; private set; } = new();
		public static List<string> DefaultFooterOrder { get; private set; } = new();

		public MainWindow() : this(false) { }

		public MainWindow(bool showAlreadyRunningWarning)
		{
			var viewModel = new MainWindowViewModel();

			viewModel.RequestSaveNoticeEvent += (_, _) => SettingsSavedTip.IsOpen = true;
			viewModel.RequestCloseWindowEvent += (_, _) => Close();

			DataContext = viewModel;

			InitializeComponent();

			App.Logger.WriteLine("MainWindow", "Initializing settings window");

			if (showAlreadyRunningWarning)
				ShowAlreadyRunningSnackbar();

			gbs.Opacity = viewModel.GBSEnabled ? 1 : 0.5;
			gbs.IsEnabled = viewModel.GBSEnabled;

			LoadState();

			string? lastPageName = App.State.Prop.LastPage;
			Type? lastPage = lastPageName is null ? null : Type.GetType(lastPageName);

			App.RemoteData.Subscribe((object? sender, EventArgs e) =>
			{
				var data = App.RemoteData.Prop;
				AlertBar.IsVisible = data.AlertEnabled;
				AlertBar.Message = data.AlertContent;
				AlertBar.Severity = (InfoBarSeverity)data.AlertSeverity;
			});

			ApplyBackdrop();

			var allItems = RootNavigation.MenuItems.Cast<NavigationViewItem>().ToList();
			var allFooters = RootNavigation.FooterMenuItems.Cast<NavigationViewItem>().ToList();

			MainNavigationItems.Clear();
			foreach (var item in allItems) MainNavigationItems.Add(item);

			FooterNavigationItems.Clear();
			foreach (var item in allFooters) FooterNavigationItems.Add(item);

			CacheDefaultNavigationOrder();
			ReorderNavigationItemsFromSettings();
			RebuildNavigationItems();

			if (lastPage != null)
				SafeNavigate(lastPage);
			else
				RootNavigation.SelectedItem = RootNavigation.MenuItems.Cast<NavigationViewItem>().FirstOrDefault();

			RootNavigation.SelectionChanged += OnNavigationChanged;

			this.Closing += MainWindow_Closing;
			this.Closed += MainWindow_Closed;
		}

		private void ApplyBackdrop()
		{
			this.TransparencyLevelHint = new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur };
			this.Background = Brushes.Transparent;
		}



		private void OnNavigationChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
		{
			if (e.SelectedItem is NavigationViewItem navItem && navItem.Tag is Type pageType)
			{
				RootFrame.Navigate(pageType);
				App.State.Prop.LastPage = pageType.FullName!;
			}
		}

		private async void SafeNavigate(Type page)
		{
			await Task.Delay(500);

			if (page == typeof(RobloxSettingsPage) && !App.GlobalSettings.Loaded)
				return;

			Navigate(page);
		}

		public void ApplyTheme()
		{
			var theme = App.Settings.Prop.Theme;
			ApplyBackdrop();
		}

		public void LoadState()
		{
			var screen = Screens.Primary?.Bounds;
			if (screen != null)
			{
				if (_state.Left > screen.Value.Width) _state.Left = 0;
				if (_state.Top > screen.Value.Height) _state.Top = 0;
			}

			if (_state.Width > 0) this.Width = _state.Width;
			if (_state.Height > 0) this.Height = _state.Height;

			if (_state.Left > 0 && _state.Top > 0)
			{
				this.WindowStartupLocation = WindowStartupLocation.Manual;
				this.Position = new PixelPoint((int)_state.Left, (int)_state.Top);
			}
		}

		private async void ShowAlreadyRunningSnackbar()
		{
			await Task.Delay(500);
			AlreadyRunningTip.IsOpen = true;
		}

		#region Navigation reorder & persistence helpers

		private void CacheDefaultNavigationOrder()
		{
			DefaultNavigationOrder = MainNavigationItems
				.Select(x => x.Tag?.ToString() ?? string.Empty)
				.ToList();

			DefaultFooterOrder = FooterNavigationItems
				.Select(x => x.Tag?.ToString() ?? string.Empty)
				.ToList();
		}

		private void RebuildNavigationItems()
		{
			RootNavigation.MenuItemsSource = MainNavigationItems;
			RootNavigation.FooterMenuItemsSource = FooterNavigationItems;

			NavigationItemsView.Clear();
			foreach (var item in MainNavigationItems)
				NavigationItemsView.Add(item);
		}

		public void ApplyNavigationReorder()
		{
			RebuildNavigationItems();

			var order = MainNavigationItems
				.Concat(FooterNavigationItems)
				.Select(item => item.Tag?.ToString() ?? string.Empty)
				.Where(s => !string.IsNullOrEmpty(s))
				.ToList();

			App.Settings.Prop.NavigationOrder = order;
		}

		private void ReorderNavigationItemsFromSettings()
		{
			if (App.Settings.Prop.NavigationOrder == null || App.Settings.Prop.NavigationOrder.Count == 0)
				return;

			var allItems = MainNavigationItems.Concat(FooterNavigationItems).ToList();
			var reorderedMain = new List<NavigationViewItem>();
			var reorderedFooter = new List<NavigationViewItem>();

			foreach (var tag in App.Settings.Prop.NavigationOrder)
			{
				var navItem = allItems.FirstOrDefault(i => i.Tag?.ToString() == tag);
				if (navItem != null)
				{
					if (MainNavigationItems.Contains(navItem))
						reorderedMain.Add(navItem);
					else if (FooterNavigationItems.Contains(navItem))
						reorderedFooter.Add(navItem);
				}
			}

			foreach (var item in MainNavigationItems.Where(i => !reorderedMain.Contains(i)))
				reorderedMain.Add(item);

			foreach (var item in FooterNavigationItems.Where(i => !reorderedFooter.Contains(i)))
				reorderedFooter.Add(item);

			MainNavigationItems.Clear();
			foreach (var item in reorderedMain) MainNavigationItems.Add(item);

			FooterNavigationItems.Clear();
			foreach (var item in reorderedFooter) FooterNavigationItems.Add(item);
		}

		public void ResetNavigationToDefault()
		{
			var available = MainNavigationItems.Concat(FooterNavigationItems).ToList();

			var reorderedMain = new List<NavigationViewItem>();
			var reorderedFooter = new List<NavigationViewItem>();

			foreach (var tag in DefaultNavigationOrder)
			{
				var navItem = available.FirstOrDefault(i => i.Tag?.ToString() == tag);
				if (navItem != null) reorderedMain.Add(navItem);
			}

			foreach (var tag in DefaultFooterOrder)
			{
				var navItem = available.FirstOrDefault(i => i.Tag?.ToString() == tag);
				if (navItem != null) reorderedFooter.Add(navItem);
			}

			foreach (var item in available.Where(i => !reorderedMain.Contains(i) && !reorderedFooter.Contains(i)))
				reorderedMain.Add(item);

			MainNavigationItems.Clear();
			foreach (var item in reorderedMain) MainNavigationItems.Add(item);

			FooterNavigationItems.Clear();
			foreach (var item in reorderedFooter) FooterNavigationItems.Add(item);

			RebuildNavigationItems();
			App.Settings.Prop.NavigationOrder?.Clear();
		}

		public int MoveNavigationItem(NavigationViewItem item, int direction)
		{
			if (item == null || !MainNavigationItems.Contains(item)) return -1;

			int index = MainNavigationItems.IndexOf(item);
			int newIndex = index + direction;

			if (newIndex < 0 || newIndex >= MainNavigationItems.Count) return -1;

			MainNavigationItems.Move(index, newIndex);
			ApplyNavigationReorder();

			return newIndex;
		}

		#endregion

		#region Navigation methods

		public Frame GetFrame() => RootFrame;

		public NavigationView GetNavigation() => RootNavigation;

		public bool Navigate(Type pageType)
		{
			RootFrame.Navigate(pageType);

			var item = MainNavigationItems.Concat(FooterNavigationItems)
										 .FirstOrDefault(x => x.Tag as Type == pageType);
			if (item != null) RootNavigation.SelectedItem = item;

			return true;
		}

		public void ShowWindow() => Show();

		public void CloseWindow() => Close();

		#endregion

		private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
		{
			if (App.FastFlags.Changed || App.PendingSettingTasks.Any())
			{
				// Avalonia dialogs are async.
				// To block closing, we cancel and show dialog.
				e.Cancel = true;
			}

			_state.Width = this.Bounds.Width;
			_state.Height = this.Bounds.Height;
			_state.Top = this.Position.Y;
			_state.Left = this.Position.X;

			App.State.Save();
		}

		private void MainWindow_Closed(object? sender, EventArgs e)
		{
			if (App.LaunchSettings.TestModeFlag.Active)
				LaunchHandler.LaunchRoblox(LaunchMode.Player);
			else
				App.SoftTerminate();
		}

		public void ShowLoading(string message = "Loading...")
		{
			LoadingOverlayText.Text = message;
			LoadingOverlay.IsVisible = true;
		}

		public void HideLoading()
		{
			LoadingOverlay.IsVisible = false;
		}
	}
}