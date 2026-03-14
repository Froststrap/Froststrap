using System;
using Avalonia;
using Avalonia.Controls;
using Froststrap;
using Avalonia.Media;
using ReactiveUI;
using Avalonia.Interactivity;
using Avalonia.Input;

namespace Froststrap.UI.Elements.About
{
	public partial class MainWindow : Base.AvaloniaWindow
	{
		public MainWindow()
		{
			InitializeComponent();
			HookTitleBar();

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

          // Navigate to the default page
			NavigateTo("about");
		}

		private void HookTitleBar()
		{
			var dragArea = this.FindControl<Panel>("TitleBarDragArea");
			if (dragArea != null)
			{
				dragArea.PointerPressed += (_, e) =>
				{
					if (e.GetCurrentPoint(dragArea).Properties.IsLeftButtonPressed)
						BeginMoveDrag(e);
				};
			}
		}

      private void OnMinimize(object? sender, RoutedEventArgs e) => this.WindowState = Avalonia.Controls.WindowState.Minimized;

		private void OnMaximize(object? sender, RoutedEventArgs e) =>
            this.WindowState = this.WindowState == Avalonia.Controls.WindowState.Maximized
				? Avalonia.Controls.WindowState.Normal
				: Avalonia.Controls.WindowState.Maximized;

		private void OnClose(object? sender, RoutedEventArgs e) => Close();

        private void OnNavClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
          if (sender is Button btn && btn.Tag is string tag)
			{
              NavigateTo(tag);
			}
		}

		private void NavigateTo(string tag)
		{
			var pageControl = this.FindControl<TransitioningContentControl>("PageContentControl");
			if (pageControl == null)
				return;

			Control? view = tag switch
			{
				"about" => new Pages.AboutPage(),
				"translators" => new Pages.TranslatorsPage(),
				"licenses" => new Pages.LicensesPage(),
				_ => null
			};

			if (view != null)
				pageControl.Content = view;

			UpdateSelectedButtonStyle(tag);
		}

		private void UpdateSelectedButtonStyle(string selectedTag)
		{
			var mainGrid = this.FindControl<Grid>("MainGrid");
			if (mainGrid == null)
				return;

			if (mainGrid.Children.Count == 0)
				return;

			if (mainGrid.Children[0] is Border sidebarBorder && sidebarBorder.Child is ScrollViewer sv && sv.Content is StackPanel sp)
			{
				foreach (var child in sp.Children)
				{
					if (child is Button button && button.Tag is string tag)
					{
						var selected = tag == selectedTag;
						button.Background = new SolidColorBrush(Colors.Transparent);
						button.Foreground = new SolidColorBrush(Color.Parse(selected ? "#00d4ff" : "#888888"));

						if (button.Content is LucideAvalonia.Lucide lucideIcon)
							lucideIcon.StrokeBrush = new SolidColorBrush(Color.Parse(selected ? "#00d4ff" : "#888888"));
					}
				}
			}
		}

	}
}