using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.Onboarding.Pages;
using Froststrap.UI.ViewModels.Onboarding;

namespace Froststrap.UI.Elements.Onboarding
{
    public partial class MainWindow : AvaloniaWindow
    {
        internal readonly MainWindowViewModel _viewModel = new();
        private Type _currentPage = typeof(Page1);

        private readonly List<Type> _pages =
        [
            typeof(Page1),
            typeof(Page2),
            typeof(Page3),
            typeof(Page4)
        ];

        private DateTimeOffset _lastNavigation = DateTimeOffset.Now;
        public Func<Task<bool>>? NextPageCallback;
        public NextAction CloseAction = NextAction.Terminate;
        public bool Finished => _currentPage == _pages.Last();

        public MainWindow()
        {
            DataContext = _viewModel;
            InitializeComponent();

            RootNavigation.PageCount = _pages.Count;

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

            Navigate(typeof(Page1));

            App.Logger.WriteLine("MainWindow", "Initializing installer window");
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

            return true;
        }
        #endregion
    }
}