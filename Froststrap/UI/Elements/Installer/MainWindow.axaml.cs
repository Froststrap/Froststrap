using Froststrap.UI.ViewModels.Installer;
using Froststrap.UI.Elements.Installer.Pages;
using Froststrap.UI.Elements.Base;
using System.ComponentModel;

namespace Froststrap.UI.Elements.Installer
{
    public partial class MainWindow : AvaloniaWindow
    {
        internal readonly MainWindowViewModel _viewModel = new();

        private Type _currentPage = typeof(WelcomePage);

        private readonly List<Type> _pages = new()
        {
            typeof(WelcomePage),
            typeof(InstallPage),
            typeof(CompletionPage)
        };

        private DateTimeOffset _lastNavigation = DateTimeOffset.Now;

        public Func<Task<bool>>? NextPageCallback;

        public NextAction CloseAction = NextAction.Terminate;

        public bool Finished => _currentPage == _pages.Last();

        public MainWindow()
        {
            DataContext = _viewModel;
            InitializeComponent();

            _viewModel.CloseWindowRequest += (_, _) => CloseWindow();

            App.FrostRPC?.SetDialog("Installer");

            _viewModel.PageRequest += (_, type) =>
            {
                if (DateTimeOffset.Now.Subtract(_lastNavigation).TotalMilliseconds < 500)
                    return;

                if (type == "next")
                    NextPage();
                else if (type == "back")
                    BackPage();

                _lastNavigation = DateTimeOffset.Now;
            };

            Navigate(typeof(WelcomePage));

            App.Logger.WriteLine("MainWindow", "Initializing installer window");
            Closing += MainWindow_Closing;
        }

        async void NextPage()
        {
            if (NextPageCallback is not null)
            {
                if (!await NextPageCallback())
                    return;
            }

            if (_currentPage == _pages.Last())
                return;

            var nextPageIndex = _pages.IndexOf(_currentPage) + 1;
            var page = _pages[nextPageIndex];

            Navigate(page);

            _viewModel.SetButtonEnabled("next", page != _pages.Last());
            _viewModel.SetButtonEnabled("back", true);
        }

        void BackPage()
        {
            if (_currentPage == _pages.First())
                return;

            var prevPageIndex = _pages.IndexOf(_currentPage) - 1;
            var page = _pages[prevPageIndex];

            Navigate(page);

            _viewModel.SetButtonEnabled("next", true);
            _viewModel.SetButtonEnabled("back", page != _pages.First());
        }

        async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (Finished)
                return;

            e.Cancel = true;

            var result = await Frontend.ShowMessageBox(Strings.Installer_ShouldCancel, MessageBoxImage.Warning, MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                Closing -= MainWindow_Closing;
                Close();
            }
        }

        public void SetNextButtonText(string text) => _viewModel.SetNextButtonText(text);

        public void SetButtonEnabled(string type, bool state) => _viewModel.SetButtonEnabled(type, state);

        #region Navigation methods

        public bool Navigate(Type pageType)
        {
            _currentPage = pageType;
            NextPageCallback = null;

            var pageInstance = Activator.CreateInstance(pageType);
            RootFrame.Content = pageInstance;

            var index = _pages.IndexOf(pageType);
            if (index >= 0 && index < RootNavigation.MenuItems.Count())
            {
                RootNavigation.SelectedItem = RootNavigation.MenuItems.ElementAt(index);
            }

            return true;
        }

        public void CloseWindow() => Close();

        #endregion
    }
}