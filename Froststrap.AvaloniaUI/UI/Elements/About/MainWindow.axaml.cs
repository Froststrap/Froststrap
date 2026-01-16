using System;
using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Froststrap;

namespace Froststrap.UI.Elements.About
{
	public partial class MainWindow : Base.AvaloniaWindow
	{
		public MainWindow()
		{
			InitializeComponent();

			// Set up RPC and Logging
			App.FrostRPC?.SetDialog("About");
			App.Logger.WriteLine("MainWindow", "Initializing about window");

			// Localization adjustment
			// Note: In Avalonia, we find controls by name if not using 
			// the source generator or if they are nested.
			var translatorsText = this.FindControl<TextBlock>("TranslatorsText");
			if (translatorsText != null && Locale.CurrentCulture.Name.StartsWith("tr"))
			{
				translatorsText.FontSize = 9;
			}

			// Set up Navigation Event
			RootNavigation.SelectionChanged += OnNavigationSelectionChanged;

			// Navigate to the default page
			RootFrame.Navigate(typeof(Pages.AboutPage));
		}

		private void OnNavigationSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
		{
			if (e.SelectedItem is NavigationViewItem nvi && nvi.Tag is string tag)
			{
				// Simple tag-based navigation
				switch (tag)
				{
					case "about":
						RootFrame.Navigate(typeof(Pages.AboutPage));
						break;
					case "translators":
						RootFrame.Navigate(typeof(Pages.TranslatorsPage));
						break;
					case "licenses":
						RootFrame.Navigate(typeof(Pages.LicensesPage));
						break;
				}
			}
		}

		#region Navigation Helpers
		// These replace the INavigationWindow methods to keep your logic working

		public Frame GetFrame() => RootFrame;

		public NavigationView GetNavigation() => RootNavigation;

		public bool Navigate(Type pageType) => RootFrame.Navigate(pageType);

		public void ShowWindow() => Show();

		public void CloseWindow() => Close();

		#endregion
	}
}