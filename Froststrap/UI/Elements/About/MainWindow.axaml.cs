using System.ComponentModel;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using Froststrap.UI.ViewModels.About;
using Froststrap.UI.Elements.Controls;

namespace Froststrap.UI.Elements.About
{
    public partial class MainWindow : Base.AvaloniaWindow
    {
        private MainWindowViewModel? _viewModel;

        public MainWindow()
        {
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;

            InitializeComponent();

            HookTitleBar();

            App.FrostRPC?.SetDialog("About");

            var translatorsText = this.FindControl<TextBlock>("TranslatorsText");
            if (translatorsText != null && Locale.CurrentCulture.Name.StartsWith("tr"))
            {
                translatorsText.FontSize = 9;
            }

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            _viewModel.NavigateToAboutCommand.Execute(null);

            UpdateSelectedNavigationViewItem(_viewModel.SelectedPage);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_viewModel == null) return;

            if (e.PropertyName == nameof(MainWindowViewModel.CurrentPage))
            {
                UpdatePageView(_viewModel.CurrentPage);
            }

            if (e.PropertyName == nameof(MainWindowViewModel.SelectedPage))
            {
                UpdateSelectedNavigationViewItem(_viewModel.SelectedPage);
            }
        }

        private void HookTitleBar()
        {
            var dragArea = this.FindControl<Panel>("TitleBarDragArea");
            dragArea?.PointerPressed += (_, e) =>
                {
                    if (e.GetCurrentPoint(dragArea).Properties.IsLeftButtonPressed)
                        BeginMoveDrag(e);
                };
        }

        private void OnMinimize(object? sender, RoutedEventArgs e) => this.WindowState = Avalonia.Controls.WindowState.Minimized;

        private void OnMaximize(object? sender, RoutedEventArgs e) =>
            this.WindowState = this.WindowState == Avalonia.Controls.WindowState.Maximized
                ? Avalonia.Controls.WindowState.Normal
                : Avalonia.Controls.WindowState.Maximized;

        private void OnClose(object? sender, RoutedEventArgs e) => Close();

        private void UpdatePageView(object? viewModel)
        {
            var pageControl = this.FindControl<TransitioningContentControl>("PageContentControl");
            if (pageControl == null)
                return;

            if (viewModel == null)
                return;

            var view = ResolveViewForViewModel(viewModel);
            if (view != null)
            {
                view.DataContext = viewModel;
                pageControl.Content = view;
            }
        }

        private static Control? ResolveViewForViewModel(object viewModel)
        {
            var actualViewModelType = viewModel.GetType();
            var viewModelName = actualViewModelType.Name;
            var viewName = viewModelName.Replace("ViewModel", "");

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
                var viewType = Type.GetType(viewTypeName);

                if (viewType == null)
                {
                    try
                    {
                        var assembly = Assembly.GetExecutingAssembly();
                        viewType = assembly.GetType(viewTypeName, false);
                    }
                    catch { }
                }

                if (viewType != null && typeof(Control).IsAssignableFrom(viewType))
                {
                    try
                    {
                        if (Activator.CreateInstance(viewType) is Control view) return view;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("About.MainWindow", $"Failed to create view {viewTypeName}: {ex.Message}");
                    }
                }
            }

            return null;
        }

        private void UpdateSelectedNavigationViewItem(string selectedTag)
        {
            var navView = this.FindControl<NavigationView>("NavView");
            if (navView == null) return;

            foreach (var item in navView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag is string tag)
                {
                    if (tag == selectedTag)
                    {
                        navView.SelectedItem = navItem;
                        return;
                    }
                }
            }
        }

        private void NavView_ItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
        {
            if (e.InvokedItemContainer is NavigationViewItem navItem && navItem.Tag is string tag)
            {
                if (tag == "about")
                {
                    _viewModel?.NavigateToAboutCommand.Execute(null);
                }
                else if (tag == "licenses")
                {
                    _viewModel?.NavigateToLicensesCommand.Execute(null);
                }
            }
        }
    }
}