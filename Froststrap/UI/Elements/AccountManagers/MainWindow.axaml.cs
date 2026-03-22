using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.ViewModels.AccountManagers;
using ReactiveUI;
using System.Reactive.Linq;

namespace Froststrap.UI.Elements.AccountManagers
{
    public partial class MainWindow : AvaloniaWindow
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

            this.Loaded += (s, e) => SetupNavigation();
            this.Unloaded += (s, e) => Cleanup();
        }

        private void SetupNavigation()
        {
            if (_viewModel is null) return;

            string? lastPageName = App.State.Prop.LastPage;
            if (lastPageName == "accounts")
                _viewModel.NavigateToAccountsCommand.Execute(System.Reactive.Unit.Default);
            else if (lastPageName == "friends")
                _viewModel.NavigateToFriendsCommand.Execute(System.Reactive.Unit.Default);
            else if (lastPageName == "games")
                _viewModel.NavigateToGamesCommand.Execute(System.Reactive.Unit.Default);
            else
                _viewModel.NavigateToAccountsCommand.Execute(System.Reactive.Unit.Default);

            _viewModel.Router.CurrentViewModel
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(vm => UpdatePageView(vm));

            _viewModel.WhenAnyValue(x => x.SelectedPage)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(page => UpdateSelectedButtonStyle(page));
        }

        private void OnActiveAccountChanged(AccountManagerAccount? account)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                if (_viewModel is not null)
                {
                    _viewModel.NavigateToAccountsCommand.Execute(System.Reactive.Unit.Default);
                }
            });
        }

        private void UpdatePageView(IRoutableViewModel? viewModel)
        {
            var pageControl = this.FindControl<TransitioningContentControl>("PageContentControl");
            if (pageControl is null || viewModel is null) return;

            // Handle wrapper ViewModels
            object? actualViewModel = viewModel;
            if (viewModel is AccountsPageViewModel apvm)
                actualViewModel = apvm.ViewModel;
            else if (viewModel is FriendsPageViewModel fpvm)
                actualViewModel = fpvm.ViewModel;
            else if (viewModel is GamesPageViewModel gpvm)
                actualViewModel = gpvm.ViewModel;

            var view = ResolveViewForViewModel(actualViewModel ?? viewModel);
            if (view is not null)
            {
                view.DataContext = actualViewModel ?? viewModel;
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

                        if (isSelected)
                        {
                            button.Background = highlightBgColor;
                        }
                        else
                        {
                            button.Background = new SolidColorBrush(Colors.Transparent);
                        }

                        // Update the icon color (shit doesn't work)
                        if (button.Content is Panel panel && panel.Children.FirstOrDefault() is IconPacks.Avalonia.Material.PackIconMaterial icon)
                        {
                            icon.Foreground = isSelected ? selectedBrush : unselectedBrush;
                        }
                    }
                }
            }
        }

        private Control? ResolveViewForViewModel(object viewModel)
        {
            var actualViewModelType = viewModel.GetType();
            var viewModelName = actualViewModelType.Name;
            var viewName = viewModelName.Replace("ViewModel", "");

            var viewTypeNames = new[]
            {
                $"Froststrap.UI.Elements.AccountManagers.Pages.{viewName}",
                $"Froststrap.UI.Elements.AccountManagers.Pages.{viewName}Page",
                $"Froststrap.UI.Elements.AccountManagers.{viewName}",
                $"Froststrap.UI.Elements.AccountManagers.{viewName}Page"
            };

            foreach (var viewTypeName in viewTypeNames)
            {
                var viewType = Type.GetType(viewTypeName);
                if (viewType is null)
                {
                    try
                    {
                        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        viewType = assembly.GetType(viewTypeName, false);
                    }
                    catch { }
                }

                if (viewType is not null && typeof(Control).IsAssignableFrom(viewType))
                {
                    try
                    {
                        return Activator.CreateInstance(viewType) as Control;
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.WriteLine("AccountManager MainWindow", $"Failed to create view {viewTypeName}: {ex.Message}");
                    }
                }
            }

            return null;
        }

        private void Cleanup()
        {
            AccountManager.Shared.ActiveAccountChanged -= OnActiveAccountChanged;
        }
    }
}
