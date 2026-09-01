using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.Onboarding.Pages;
using Froststrap.UI.ViewModels.Onboarding;

namespace Froststrap.UI.Elements.Onboarding
{
    internal partial class MainWindow : AvaloniaWindow
    {
        public static MainWindow? Instance { get; private set; }
        internal readonly MainWindowViewModel _viewModel = new();
        private Type _currentPage = typeof(Page1);

        private readonly List<Type> _pages =
        [
            typeof(Page1),
            typeof(Page2),
            typeof(Page3),
            typeof(Page4),
            typeof(Page5),
            typeof(Page6),
            typeof(Page7)
        ];

        public Func<Task<bool>>? NextPageCallback;
        public NextAction CloseAction = NextAction.Terminate;
        public bool Finished => _currentPage == _pages.Last();

        public MainWindow()
        {
            Instance = this;
            DataContext = _viewModel;
            InitializeComponent();

            RootNavigation.PageCount = _pages.Count;

            _viewModel.PageRequest += (_, type) =>
            {
                if (type == "next")
                    NextPage();
                else if (type == "back")
                    BackPage();
            };

            Navigate(typeof(Page1));

            App.Logger.Debug("Initializing onboarding window");
        }

        async void NextPage()
        {
            if (NextPageCallback is not null)
            {
                if (!await NextPageCallback())
                    return;
            }

            if (_currentPage == _pages.Last())
            {
                Close();
                return;
            }

            App.Settings.Save();
            var nextPageIndex = _pages.IndexOf(_currentPage) + 1;
            var page = _pages[nextPageIndex];
            Navigate(page);
        }

        void BackPage()
        {
            if (_currentPage == _pages.First())
                return;

            var prevPageIndex = _pages.IndexOf(_currentPage) - 1;
            var page = _pages[prevPageIndex];

            Navigate(page);
        }

        public void SetNextButtonText(string text) => _viewModel.SetNextButtonText(text);

        #region Navigation methods

        public bool Navigate(Type pageType)
        {
            _currentPage = pageType;
            NextPageCallback = null;

            var pageInstance = Activator.CreateInstance(pageType);
            RootFrame.Content = pageInstance;

            var index = _pages.IndexOf(pageType);
            if (index >= 0)
                RootNavigation.CurrentIndex = index;

            if (_currentPage == _pages.Last())
                SetNextButtonText(Strings.Common_Finish);
            else
                SetNextButtonText(Strings.Common_Next);

            _viewModel.BackButtonEnabled = _currentPage != _pages.First();

            return true;
        }
        #endregion
    }
}
