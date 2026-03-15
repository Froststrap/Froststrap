using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;

namespace Froststrap.UI.ViewModels.About
{
    public class MainWindowViewModel : ReactiveObject, IRoutableViewModel, IScreen
    {
        private readonly RoutingState _router = new();
        public RoutingState Router => _router;

        public string? UrlPathSegment => "main";
        public IScreen HostScreen => this;

        private string _selectedPage = "about";
        public string SelectedPage
        {
            get => _selectedPage;
            set => this.RaiseAndSetIfChanged(ref _selectedPage, value);
        }

        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToAboutCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToTranslatorsCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToLicensesCommand { get; }

        private IRoutableViewModel Wrap(string segment, object viewModel) =>
            new AboutPageViewModelWrapper(this, segment, viewModel);

        public MainWindowViewModel()
        {
            var commandExceptionHandler = new Action<Exception>(ex =>
            {
                App.Logger.WriteException("AboutMainWindowViewModel::NavigationCommand", ex);
            });

            NavigateToAboutCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "about";
                    return _router.Navigate.Execute(Wrap("about", new AboutViewModel()))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Catch<IRoutableViewModel, Exception>(ex =>
                        {
                            commandExceptionHandler(ex);
                            return System.Reactive.Linq.Observable.Empty<IRoutableViewModel>();
                        });
                }
            );

            NavigateToTranslatorsCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "translators";
                    return _router.Navigate.Execute(Wrap("translators", new TranslatorsViewModel()))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Catch<IRoutableViewModel, Exception>(ex =>
                        {
                            commandExceptionHandler(ex);
                            return System.Reactive.Linq.Observable.Empty<IRoutableViewModel>();
                        });
                }
            );

            NavigateToLicensesCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "licenses";
                    return _router.Navigate.Execute(Wrap("licenses", new LicensesViewModel()))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Catch<IRoutableViewModel, Exception>(ex =>
                        {
                            commandExceptionHandler(ex);
                            return System.Reactive.Linq.Observable.Empty<IRoutableViewModel>();
                        });
                }
            );

            _router.NavigateAndReset.Execute(Wrap("about", new AboutViewModel())).Subscribe();
        }

        private sealed class AboutPageViewModelWrapper : ReactiveObject, IRoutableViewModel
        {
            public AboutPageViewModelWrapper(IScreen hostScreen, string urlPathSegment, object innerViewModel)
            {
                HostScreen = hostScreen;
                UrlPathSegment = urlPathSegment;
                InnerViewModel = innerViewModel;
            }

            public string? UrlPathSegment { get; }
            public IScreen HostScreen { get; }
            public object InnerViewModel { get; }
        }
    }
}
