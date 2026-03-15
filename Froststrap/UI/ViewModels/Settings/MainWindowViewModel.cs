using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public class MainWindowViewModel : ReactiveObject, IRoutableViewModel, IScreen
    {
        private readonly RoutingState _router = new();
        public RoutingState Router => _router;

        public string? UrlPathSegment => "main";
        public IScreen HostScreen => this;

        private bool _testModeEnabled;
        public bool TestModeEnabled
        {
            get => _testModeEnabled;
            set
            {
                if (value && !App.State.Prop.TestModeWarningShown)
                {
                    _ = ShowTestModeWarningAsync();
                    return;
                }

                this.RaiseAndSetIfChanged(ref _testModeEnabled, value);
                App.LaunchSettings.TestModeFlag.Active = value;
            }
        }

        private async Task ShowTestModeWarningAsync()
        {
            var result = await Frontend.ShowMessageBox(
                Strings.Menu_TestMode_Prompt,
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                App.State.Prop.TestModeWarningShown = true;

                TestModeEnabled = true;
            }
            else
            {
                this.RaisePropertyChanged(nameof(TestModeEnabled));
            }
        }

        private bool _isSidebarExpanded;
        public bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            set
            {
                this.RaiseAndSetIfChanged(ref _isSidebarExpanded, value);
                App.Settings.Prop.IsNavigationSidebarExpanded = value;
            }
        }

        private string _selectedPage = "integrations";
        public string SelectedPage
        {
            get => _selectedPage;
            set => this.RaiseAndSetIfChanged(ref _selectedPage, value);
        }

        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToIntegrationsCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToBehaviourCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToModsCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToCommunityModsCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToFastFlagsCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToAppearanceCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> NavigateToRobloxSettingsCommand { get; }

        private IRoutableViewModel Wrap(string segment, object settingsViewModel) =>
            new SettingsPageViewModelWrapper(this, segment, settingsViewModel);

        public ICommand OpenAboutCommand => new RelayCommand(OpenAbout);
        public ICommand OpenAccountManagerCommand => new RelayCommand(OpenAccountManager);
        public ICommand SaveSettingsCommand => new RelayCommand(SaveSettings);
        public ICommand SaveAndLaunchSettingsCommand => new RelayCommand(SaveAndLaunchSettings);
        public ICommand RestartAppCommand => new RelayCommand(RestartApp);
        public ICommand CloseWindowCommand => new RelayCommand(CloseWindow);

        public EventHandler? RequestSaveNoticeEvent;
        public EventHandler? RequestCloseWindowEvent;
        public bool GBSEnabled = App.GlobalSettings.Loaded;
        public event EventHandler? SettingsSaved;

        public MainWindowViewModel()
        {
            _testModeEnabled = App.LaunchSettings.TestModeFlag.Active;
            _isSidebarExpanded = App.Settings.Prop.IsNavigationSidebarExpanded;

            // Shared exception handler for all commands
            var commandExceptionHandler = new Action<Exception>(ex =>
            {
                App.Logger.WriteException("MainWindowViewModel::NavigationCommand", ex);
            });

            NavigateToIntegrationsCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "integrations";
                    return _router.Navigate.Execute(Wrap("integrations", new IntegrationsViewModel()))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Catch<IRoutableViewModel, Exception>(ex =>
                        {
                            commandExceptionHandler(ex);
                            return System.Reactive.Linq.Observable.Empty<IRoutableViewModel>();
                        });
                }
            );

            NavigateToBehaviourCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "behaviour";
                    return _router.Navigate.Execute(Wrap("behaviour", new BehaviourViewModel()))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Catch<IRoutableViewModel, Exception>(ex =>
                        {
                            commandExceptionHandler(ex);
                            return System.Reactive.Linq.Observable.Empty<IRoutableViewModel>();
                        });
                }
            );

            NavigateToModsCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "mods";
                    return _router.Navigate.Execute(Wrap("mods", new ModsViewModel()))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Catch<IRoutableViewModel, Exception>(ex =>
                        {
                            commandExceptionHandler(ex);
                            return System.Reactive.Linq.Observable.Empty<IRoutableViewModel>();
                        });
                }
            );

            NavigateToCommunityModsCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "communitymods";
                    return _router.Navigate.Execute(Wrap("communitymods", new CommunityModsViewModel()))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Catch<IRoutableViewModel, Exception>(ex =>
                        {
                            commandExceptionHandler(ex);
                            return System.Reactive.Linq.Observable.Empty<IRoutableViewModel>();
                        });
                }
            );

            NavigateToFastFlagsCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "fastflags";
                    return _router.Navigate.Execute(Wrap("fastflags", new FastFlagsViewModel()))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Catch<IRoutableViewModel, Exception>(ex =>
                        {
                            commandExceptionHandler(ex);
                            return System.Reactive.Linq.Observable.Empty<IRoutableViewModel>();
                        });
                }
            );

            NavigateToAppearanceCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "appearance";
                    return _router.Navigate.Execute(Wrap("appearance", new AppearanceViewModel(null!)))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Catch<IRoutableViewModel, Exception>(ex =>
                        {
                            commandExceptionHandler(ex);
                            return System.Reactive.Linq.Observable.Empty<IRoutableViewModel>();
                        });
                }
            );

            NavigateToRobloxSettingsCommand = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    SelectedPage = "robloxsettings";
                    return _router.Navigate.Execute(Wrap("robloxsettings", new RobloxSettingsViewModel()))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Catch<IRoutableViewModel, Exception>(ex =>
                        {
                            commandExceptionHandler(ex);
                            return System.Reactive.Linq.Observable.Empty<IRoutableViewModel>();
                        });
                }
            );

            var lastPageName = App.State.Prop.LastPage;
            if (lastPageName != null)
            {
                NavigateToLastPage(lastPageName);
            }
            else
            {
                _router.NavigateAndReset.Execute(Wrap("integrations", new IntegrationsViewModel())).Subscribe();
            }
        }

        private void NavigateToLastPage(string pageTypeName)
        {
            var viewModel = pageTypeName switch
            {
                "Froststrap.UI.ViewModels.Settings.IntegrationsViewModel" => (IRoutableViewModel)Wrap("integrations", new IntegrationsViewModel()),
                "Froststrap.UI.ViewModels.Settings.BehaviourViewModel" => Wrap("behaviour", new BehaviourViewModel()),
                "Froststrap.UI.ViewModels.Settings.ModsViewModel" => Wrap("mods", new ModsViewModel()),
                "Froststrap.UI.ViewModels.Settings.CommunityModsViewModel" => Wrap("communitymods", new CommunityModsViewModel()),
                "Froststrap.UI.ViewModels.Settings.FastFlagsViewModel" => Wrap("fastflags", new FastFlagsViewModel()),
                "Froststrap.UI.ViewModels.Settings.AppearanceViewModel" => Wrap("appearance", new AppearanceViewModel(null!)),
                "Froststrap.UI.ViewModels.Settings.RobloxSettingsViewModel" => Wrap("robloxsettings", new RobloxSettingsViewModel()),
                _ => Wrap("integrations", new IntegrationsViewModel())
            };
            _router.Navigate.Execute(viewModel).Subscribe();
        }

        private void OpenAbout()
        {
            App.FrostRPC?.SetDialog("About");

            new Elements.About.MainWindow().Show();

            App.FrostRPC?.ClearDialog();
        }

        private void OpenAccountManager()
        {
            App.FrostRPC?.SetDialog("Account Manager");

            new Elements.AccountManagers.MainWindow().Show();

            App.FrostRPC?.ClearDialog();
        }

        private void CloseWindow() => RequestCloseWindowEvent?.Invoke(this, EventArgs.Empty);

        public void SaveSettings()
        {
            const string LOG_IDENT = "MainWindowViewModel::SaveSettings";

            App.Settings.Save();
            App.State.Save();
            App.FastFlags.Save();
            App.GlobalSettings.Save();

            foreach (var pair in App.PendingSettingTasks)
            {
                var task = pair.Value;

                if (task.Changed)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Executing pending task '{task}'");
                    task.Execute();
                }
            }

            App.PendingSettingTasks.Clear();

            RequestSaveNoticeEvent?.Invoke(this, EventArgs.Empty);
        }

        public void SaveAndLaunchSettings()
        {
            SaveSettings();
            if (!App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Application, "-player");
            else
                CloseWindow();
        }

        private async void RestartApp()
        {
            SaveSettings();

            SettingsSaved?.Invoke(this, EventArgs.Empty);

            await Task.Delay(750);

            var startInfo = new ProcessStartInfo(Environment.ProcessPath!)
            {
                Arguments = "-menu"
            };

            Process.Start(startInfo);

            App.FrostRPC?.Dispose();
            CloseWindow();
        }

        private sealed class SettingsPageViewModelWrapper : ReactiveObject, IRoutableViewModel
        {
            public SettingsPageViewModelWrapper(IScreen hostScreen, string urlPathSegment, object innerViewModel)
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
