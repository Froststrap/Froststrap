using System;
using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Input;
using Froststrap.UI.ViewModels.About;
using CommunityToolkit.Mvvm.ComponentModel;

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
                UpdateSelectedButtonStyle(_viewModel.SelectedPage);
            }
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

        private Control? ResolveViewForViewModel(object viewModel)
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
                        var view = Activator.CreateInstance(viewType) as Control;
                        if (view != null) return view;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("About.MainWindow", $"Failed to create view {viewTypeName}: {ex.Message}");
                    }
                }
            }

            return null;
        }

        private void UpdateSelectedButtonStyle(string selectedTag)
        {
            var mainGrid = this.FindControl<Grid>("MainGrid");
            if (mainGrid == null) return;

            if (mainGrid.Children.Count > 0 && mainGrid.Children[0] is Border sidebarBorder &&
                sidebarBorder.Child is ScrollViewer sv && sv.Content is StackPanel sp)
            {
                var selectedBrush = new SolidColorBrush(Color.Parse("#00d4ff"));
                var unselectedBrush = new SolidColorBrush(Color.Parse("#888888"));

                foreach (var child in sp.Children)
                {
                    if (child is Button button && button.Tag is string tag)
                    {
                        var isSelected = tag == selectedTag;
                        button.Background = isSelected ? new SolidColorBrush(Color.Parse("#333333")) : Brushes.Transparent;
                        button.Foreground = isSelected ? selectedBrush : unselectedBrush;

                        if (button.Content is IconPacks.Avalonia.Material.PackIconMaterial icon)
                        {
                            icon.Foreground = isSelected ? selectedBrush : unselectedBrush;
                        }
                    }
                }
            }
        }
    }
}