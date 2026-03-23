using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.UI.ViewModels.AccountManagers;

namespace Froststrap.UI.Elements.AccountManagers
{
    public partial class MainWindow : Base.AvaloniaWindow
    {
        private AccountManagerViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            App.FrostRPC?.SetDialog("Account Manager");
            App.Logger?.WriteLine("MainWindow", "Initializing account manager window");

            _viewModel = new AccountManagerViewModel();
            DataContext = _viewModel;

            AccountManager.Shared.ActiveAccountChanged += OnActiveAccountChanged;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            this.Loaded += (s, e) => SetupNavigation();
            this.Unloaded += (s, e) => Cleanup();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_viewModel == null) return;

            if (e.PropertyName == nameof(AccountManagerViewModel.CurrentPage))
            {
                UpdatePageView(_viewModel.CurrentPage);
            }
            else if (e.PropertyName == nameof(AccountManagerViewModel.SelectedPage))
            {
                UpdateSelectedButtonStyle(_viewModel.SelectedPage);
            }
        }

        private void SetupNavigation()
        {
            if (_viewModel is null) return;

            string? lastPageName = App.State.Prop.LastPage;
            if (lastPageName == "friends")
                _viewModel.NavigateToFriendsCommand.Execute(null);
            else if (lastPageName == "games")
                _viewModel.NavigateToGamesCommand.Execute(null);
            else
                _viewModel.NavigateToAccountsCommand.Execute(null);

            UpdatePageView(_viewModel.CurrentPage);
            UpdateSelectedButtonStyle(_viewModel.SelectedPage);
        }

        private void OnActiveAccountChanged(AccountManagerAccount? account)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                _viewModel?.NavigateToAccountsCommand.Execute(null);
            });
        }

        private void UpdatePageView(object? viewModel)
        {
            var pageControl = this.FindControl<TransitioningContentControl>("PageContentControl");
            if (pageControl is null || viewModel is null) return;

            var view = ResolveViewForViewModel(viewModel);
            if (view is not null)
            {
                view.DataContext = viewModel;
                pageControl.Content = view;
            }
        }

        private void UpdateSelectedButtonStyle(string? selectedPage)
        {
            var mainGrid = this.FindControl<Grid>("MainGrid");
            if (mainGrid is null) return;

            var border = mainGrid.Children.OfType<Border>().FirstOrDefault();
            if (border?.Child is ScrollViewer scrollViewer && scrollViewer.Content is StackPanel stackPanel)
            {
                var unselectedBrush = new SolidColorBrush(Color.Parse("#888888"));
                var selectedBrush = new SolidColorBrush(Color.Parse("#00d4ff"));
                var highlightBgColor = new SolidColorBrush(Color.Parse("#333333"));

                foreach (var button in stackPanel.Children.OfType<Button>())
                {
                    if (button.Tag is string tag)
                    {
                        var isSelected = tag == selectedPage;
                        button.Background = isSelected ? highlightBgColor : Brushes.Transparent;

                        var icon = FindIconInButton(button);
                        if (icon != null)
                        {
                            icon.Foreground = isSelected ? selectedBrush : unselectedBrush;
                        }
                    }
                }
            }
        }

        private IconPacks.Avalonia.Material.PackIconMaterial? FindIconInButton(Button button)
        {
            if (button.Content is Panel panel)
                return panel.Children.OfType<IconPacks.Avalonia.Material.PackIconMaterial>().FirstOrDefault();

            if (button.Content is IconPacks.Avalonia.Material.PackIconMaterial icon)
                return icon;

            return null;
        }

        private Control? ResolveViewForViewModel(object viewModel)
        {
            var viewName = viewModel.GetType().Name.Replace("ViewModel", "");

            var viewTypeNames = new[]
            {
                $"Froststrap.UI.Elements.AccountManagers.Pages.{viewName}",
                $"Froststrap.UI.Elements.AccountManagers.Pages.{viewName}Page",
                $"Froststrap.UI.Elements.AccountManagers.{viewName}",
                $"Froststrap.UI.Elements.AccountManagers.{viewName}Page"
            };

            foreach (var name in viewTypeNames)
            {
                var type = Type.GetType(name) ?? System.Reflection.Assembly.GetExecutingAssembly().GetType(name);
                if (type != null && typeof(Control).IsAssignableFrom(type))
                {
                    return Activator.CreateInstance(type) as Control;
                }
            }
            return null;
        }

        private void Cleanup()
        {
            AccountManager.Shared.ActiveAccountChanged -= OnActiveAccountChanged;
            if (_viewModel != null)
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}