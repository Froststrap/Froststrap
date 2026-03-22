using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;

namespace Froststrap.UI.ViewModels.AccountManagers
{
    public class AccountsPageViewModel : ReactiveObject, IRoutableViewModel
    {
        public string? UrlPathSegment => "accounts";
        public IScreen HostScreen { get; set; } = null!;
        public AccountsViewModel ViewModel { get; }

        public AccountsPageViewModel()
        {
            ViewModel = new AccountsViewModel();
        }
    }

    public class FriendsPageViewModel : ReactiveObject, IRoutableViewModel
    {
        public string? UrlPathSegment => "friends";
        public IScreen HostScreen { get; set; } = null!;
        public FriendsViewModel ViewModel { get; }

        public FriendsPageViewModel()
        {
            ViewModel = new FriendsViewModel();
        }
    }

    public class GamesPageViewModel : ReactiveObject, IRoutableViewModel
    {
        public string? UrlPathSegment => "games";
        public IScreen HostScreen { get; set; } = null!;
        public GamesViewModel ViewModel { get; }

        public GamesPageViewModel()
        {
            ViewModel = new GamesViewModel();
        }
    }

    public class AccountManagerViewModel : ReactiveObject, IRoutableViewModel, IScreen
    {
        public string? UrlPathSegment => "accountmanager";
        public IScreen HostScreen { get; }
        public RoutingState Router { get; }

        private string _selectedPage = "accounts";
        public string SelectedPage
        {
            get => _selectedPage;
            set => this.RaiseAndSetIfChanged(ref _selectedPage, value);
        }

        private string _currentPageTitle = "Accounts";
        public string CurrentPageTitle
        {
            get => _currentPageTitle;
            set => this.RaiseAndSetIfChanged(ref _currentPageTitle, value);
        }

        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToAccountsCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToFriendsCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToGamesCommand { get; }

        public AccountManagerViewModel()
        {
            HostScreen = this;
            Router = new RoutingState();

            NavigateToAccountsCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "accounts";
                    CurrentPageTitle = "Accounts";
                    App.State.Prop.LastPage = "accounts";
                    App.State.Save();
                    var vm = new AccountsPageViewModel();
                    vm.HostScreen = this;
                    return Router.Navigate.Execute(vm)
                        .ObserveOn(RxSchedulers.MainThreadScheduler);
                }
            );

            NavigateToFriendsCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "friends";
                    CurrentPageTitle = "Friends";
                    App.State.Prop.LastPage = "friends";
                    App.State.Save();
                    var vm = new FriendsPageViewModel();
                    vm.HostScreen = this;
                    return Router.Navigate.Execute(vm)
                        .ObserveOn(RxSchedulers.MainThreadScheduler);
                }
            );

            NavigateToGamesCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "games";
                    CurrentPageTitle = "Games";
                    App.State.Prop.LastPage = "games";
                    App.State.Save();
                    var vm = new GamesPageViewModel();
                    vm.HostScreen = this;
                    return Router.Navigate.Execute(vm)
                        .ObserveOn(RxSchedulers.MainThreadScheduler);
                }
            );
        }
    }
}
