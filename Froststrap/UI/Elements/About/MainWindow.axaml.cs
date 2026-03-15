using System;
using Avalonia;
using Avalonia.Controls;
using Froststrap;
using Avalonia.Media;
using ReactiveUI;
using Avalonia.Interactivity;
using Avalonia.Input;
using Froststrap.UI.ViewModels.About;
using System.Reactive.Linq;

namespace Froststrap.UI.Elements.About
{
	public partial class MainWindow : Base.AvaloniaWindow
	{
		private MainWindowViewModel? _viewModel;

		public MainWindow()
		{
			App.Logger.WriteLine("About.MainWindow", "Constructor started");

			var viewModel = new MainWindowViewModel();
			_viewModel = viewModel;

			DataContext = viewModel;

			App.Logger.WriteLine("About.MainWindow", "InitializeComponent starting");
			InitializeComponent();
			App.Logger.WriteLine("About.MainWindow", "InitializeComponent completed");

			HookTitleBar();

			// Set up RPC and Logging
			App.FrostRPC?.SetDialog("About");
			App.Logger.WriteLine("About.MainWindow", "Initializing about window");

			// Localization adjustment
			var translatorsText = this.FindControl<TextBlock>("TranslatorsText");
			if (translatorsText != null && Locale.CurrentCulture.Name.StartsWith("tr"))
			{
				translatorsText.FontSize = 9;
			}

			App.Logger.WriteLine("About.MainWindow", "Setting up ReactiveUI routing");

			// Set up ReactiveUI routing
			viewModel.Router.CurrentViewModel
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(vm => 
				{
					App.Logger.WriteLine("About.MainWindow", $"CurrentViewModel changed: {vm?.GetType().Name ?? "null"}");
					UpdatePageView(vm);
				});

			viewModel.WhenAnyValue(x => x.SelectedPage)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(page => 
				{
					App.Logger.WriteLine("About.MainWindow", $"SelectedPage changed to: {page}");
					UpdateSelectedButtonStyle(page);
				});

			App.Logger.WriteLine("About.MainWindow", "Navigating to default page");

			// Navigate to default page
			viewModel.NavigateToAboutCommand.Execute().Subscribe(
				onNext: vm => App.Logger.WriteLine("About.MainWindow", "Navigation executed successfully"),
				onError: ex => App.Logger.WriteLine("About.MainWindow", $"Navigation error: {ex.Message}"),
				onCompleted: () => App.Logger.WriteLine("About.MainWindow", "Navigation completed")
			);

			App.Logger.WriteLine("About.MainWindow", "Constructor completed");
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

		private void UpdatePageView(IRoutableViewModel? viewModel)
		{
			var pageControl = this.FindControl<TransitioningContentControl>("PageContentControl");
			if (pageControl == null)
			{
				App.Logger.WriteLine("About.MainWindow", "ERROR: PageContentControl not found!");
				return;
			}

			if (viewModel == null)
			{
				App.Logger.WriteLine("About.MainWindow", "ERROR: viewModel is null!");
				return;
			}

			App.Logger.WriteLine("About.MainWindow", $"UpdatePageView called with: {viewModel.GetType().Name}");

			object dataContext = viewModel;

			var innerVmProp = viewModel.GetType().GetProperty("InnerViewModel");
			if (innerVmProp != null)
			{
				var inner = innerVmProp.GetValue(viewModel);
				if (inner != null)
				{
					App.Logger.WriteLine("About.MainWindow", $"Using inner ViewModel: {inner.GetType().Name}");
					dataContext = inner;
				}
			}

			var view = ResolveViewForViewModel(viewModel);
			if (view != null)
			{
				App.Logger.WriteLine("About.MainWindow", $"View resolved: {view.GetType().Name}");
				view.DataContext = dataContext;
				pageControl.Content = view;
				App.Logger.WriteLine("About.MainWindow", "Page view updated successfully");
			}
			else
			{
				App.Logger.WriteLine("About.MainWindow", "ERROR: Failed to resolve view for ViewModel!");
			}
		}

		private Control? ResolveViewForViewModel(IRoutableViewModel viewModel)
		{
			var actualViewModelType = viewModel.GetType();
			var innerVmProp = actualViewModelType.GetProperty("InnerViewModel");
			if (innerVmProp != null)
			{
				var innerVm = innerVmProp.GetValue(viewModel);
				if (innerVm != null)
				{
					actualViewModelType = innerVm.GetType();
					App.Logger.WriteLine("About.MainWindow", $"Extracted inner ViewModel type: {actualViewModelType.Name}");
				}
			}

			var viewModelName = actualViewModelType.Name;
			var viewName = viewModelName.Replace("ViewModel", "");

			App.Logger.WriteLine("About.MainWindow", $"Looking for view for ViewModel: {viewModelName} -> view name: {viewName}");

			var viewTypeNames = new[]
			{
				$"Froststrap.UI.Elements.About.Pages.{viewName}Page",
				$"Froststrap.UI.Elements.About.Pages.{viewName}",
				$"Froststrap.UI.Elements.About.{viewName}Page",
				$"Froststrap.UI.Elements.About.{viewName}",
				$"Froststrap.UI.Elements.{viewName}Page",
				$"Froststrap.UI.Elements.{viewName}"
			};

			foreach (var viewTypeName in viewTypeNames)
			{
				App.Logger.WriteLine("About.MainWindow", $"Trying to load view type: {viewTypeName}");
				var viewType = Type.GetType(viewTypeName);

				if (viewType == null)
				{
					try
					{
						var assembly = System.Reflection.Assembly.GetExecutingAssembly();
						viewType = assembly.GetType(viewTypeName, false);
					}
					catch { }
				}

				if (viewType != null && typeof(Control).IsAssignableFrom(viewType))
				{
					try
					{
						App.Logger.WriteLine("About.MainWindow", $"Found view type: {viewTypeName}");
						var view = Activator.CreateInstance(viewType) as Control;
						if (view != null)
						{
							view.DataContext = viewModel;
							App.Logger.WriteLine("About.MainWindow", $"Successfully created view: {viewType.Name}");
							return view;
						}
					}
					catch (Exception ex)
					{
						App.Logger.WriteLine("About.MainWindow", $"Failed to create view {viewTypeName}: {ex.Message}");
					}
				}
			}

			App.Logger.WriteLine("About.MainWindow", $"ERROR: No view found for ViewModel {viewModelName}");
			return null;
		}

		private void UpdateSelectedButtonStyle(string selectedTag)
		{
			App.Logger.WriteLine("About.MainWindow", $"UpdateSelectedButtonStyle called with: {selectedTag}");

			var mainGrid = this.FindControl<Grid>("MainGrid");
			if (mainGrid == null)
			{
				App.Logger.WriteLine("About.MainWindow", "ERROR: MainGrid not found!");
				return;
			}

			if (mainGrid.Children.Count == 0)
			{
				App.Logger.WriteLine("About.MainWindow", "ERROR: MainGrid has no children!");
				return;
			}

			App.Logger.WriteLine("About.MainWindow", $"MainGrid children count: {mainGrid.Children.Count}");

			if (mainGrid.Children[0] is Border sidebarBorder && sidebarBorder.Child is ScrollViewer sv && sv.Content is StackPanel sp)
			{
				App.Logger.WriteLine("About.MainWindow", $"Found StackPanel with {sp.Children.Count} buttons");

				var selectedBrush = new SolidColorBrush(Color.Parse("#00d4ff"));
				var unselectedBrush = new SolidColorBrush(Color.Parse("#888888"));

				foreach (var child in sp.Children)
				{
					if (child is Button button && button.Tag is string tag)
					{
						var isSelected = tag == selectedTag;
						App.Logger.WriteLine("About.MainWindow", $"Button tag: {tag}, isSelected: {isSelected}");

						button.Background = isSelected ? new SolidColorBrush(Color.Parse("#333333")) : new SolidColorBrush(Colors.Transparent);
						button.Foreground = isSelected ? selectedBrush : unselectedBrush;

						if (button.Content is LucideAvalonia.Lucide lucideIcon)
						{
							lucideIcon.StrokeBrush = isSelected ? selectedBrush : unselectedBrush;
						}
					}
				}
			}
			else
			{
				App.Logger.WriteLine("About.MainWindow", "ERROR: Could not find StackPanel structure!");
			}
		}
	}
}