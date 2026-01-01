using Bloxstrap.Integrations;
using Bloxstrap.UI.Elements.AccountManagers.Pages;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;

namespace Bloxstrap.UI.Elements.AccountManagers
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            App.FrostRPC?.SetDialog("Account Manager");

            App.Logger.WriteLine("MainWindow", "Initializing account manager window");

            AccountManager.Shared.ActiveAccountChanged += OnActiveAccountChanged;

            UpdateNavigationItemsState();
        }

        private void OnActiveAccountChanged(AltAccount? account)
        {
            Dispatcher.Invoke(UpdateNavigationItemsState);
        }

        private void UpdateNavigationItemsState()
        {
            bool hasActiveAccount = AccountManager.Shared.ActiveAccount != null;

            if (friends != null)
            {
                friends.Opacity = hasActiveAccount ? 1 : 0.5;
                friends.IsEnabled = hasActiveAccount;
            }

            if (games != null)
            {
                games.Opacity = hasActiveAccount ? 1 : 0.5;
                games.IsEnabled = hasActiveAccount;
            }

            if (!hasActiveAccount)
            {
                var currentPage = RootNavigation.Current;
                if (currentPage != null)
                {
                    if (currentPage.PageTag?.ToString() == "friends" || currentPage.PageTag?.ToString() == "games")
                    {
                        RootNavigation.Navigate(typeof(AccountsPage));
                    }
                }
            }
        }

        public void ShowLoading(string message = "Loading...")
        {
            Dispatcher.Invoke(() =>
            {
                LoadingOverlayText.Text = message;
                LoadingOverlay.Visibility = Visibility.Visible;
            });
        }

        public void HideLoading()
        {
            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            AccountManager.Shared.ActiveAccountChanged -= OnActiveAccountChanged;

            base.OnClosed(e);
        }

        #region INavigationWindow methods

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods
    }
}