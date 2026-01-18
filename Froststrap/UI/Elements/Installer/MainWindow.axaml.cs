using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Froststrap.Enums;
using Froststrap.Resources;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.Installer.Pages;
using Froststrap.UI.ViewModels.Installer;

namespace Froststrap.UI.Elements.Installer
{
	/// <summary>
	/// Interaction logic for MainWindow.axaml
	/// </summary>
	public partial class MainWindow : Base.AvaloniaWindow
	{
		internal readonly MainWindowViewModel _viewModel = new();

		private Type _currentPage = typeof(WelcomePage);

		private readonly List<Type> _pages = new()
		{
			typeof(WelcomePage),
			typeof(InstallPage),
			typeof(CompletionPage)
		};

		private DateTimeOffset _lastNavigation = DateTimeOffset.Now;

		public Func<bool>? NextPageCallback;

		public NextAction CloseAction = NextAction.Terminate;

		public bool Finished => _currentPage == _pages.Last();

		public MainWindow()
		{
			DataContext = _viewModel;
			InitializeComponent();

			_viewModel.CloseWindowRequest += (_, _) => CloseWindow();

			App.FrostRPC?.SetDialog("Installer");

			_viewModel.PageRequest += (_, type) =>
			{
				if (DateTimeOffset.Now.Subtract(_lastNavigation).TotalMilliseconds < 500)
					return;

				if (type == "next")
					NextPage();
				else if (type == "back")
					BackPage();

				_lastNavigation = DateTimeOffset.Now;
			};

			App.Logger.WriteLine("MainWindow", "Initializing installer window");

			Closing += MainWindow_Closing;

			Navigate(typeof(WelcomePage));
		}

		void NextPage()
		{
			if (NextPageCallback is not null && !NextPageCallback())
				return;

			if (_currentPage == _pages.Last())
				return;

			var page = _pages[_pages.IndexOf(_currentPage) + 1];

			Navigate(page);

			SetButtonEnabled("next", page != _pages.Last());
			SetButtonEnabled("back", true);
		}

		void BackPage()
		{
			if (_currentPage == _pages.First())
				return;

			var page = _pages[_pages.IndexOf(_currentPage) - 1];

			Navigate(page);

			SetButtonEnabled("next", true);
			SetButtonEnabled("back", page != _pages.First());
		}

		private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
		{
			if (Finished)
				return;

			var result = Frontend.ShowMessageBox(
				Strings.Installer_ShouldCancel,
				MessageBoxImage.Warning,
                MessageBoxButton.YesNo);

			if (result != MessageBoxResult.Yes)
				e.Cancel = true;
		}

		public void SetNextButtonText(string text) => _viewModel.SetNextButtonText(text);

		public void SetButtonEnabled(string type, bool state) => _viewModel.SetButtonEnabled(type, state);

		#region Navigation Logic

		public bool Navigate(Type pageType)
		{
			if (pageType == null) return false;

			_currentPage = pageType;
			NextPageCallback = null;

			bool navigated = RootFrame.Navigate(pageType);

			UpdateNavigationHighlight(_pages.IndexOf(pageType));

			return navigated;
		}

		private void UpdateNavigationHighlight(int index)
		{
			if (RootNavigation == null) return;

			var items = RootNavigation.MenuItems.Cast<NavigationViewItem>().ToList();

			if (index >= 0 && index < items.Count)
			{
				RootNavigation.SelectedItem = items[index];
			}
		}

		private void RootNavigation_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
		{
			if (e.SelectedItem is NavigationViewItem nvi)
			{
				var items = RootNavigation.MenuItems.Cast<NavigationViewItem>().ToList();
				int index = items.IndexOf(nvi);

				if (index != -1 && _pages[index] != _currentPage)
				{
					Navigate(_pages[index]);
				}
			}
		}

		public void ShowWindow() => Show();

		public void CloseWindow() => Close();

		#endregion
	}
}