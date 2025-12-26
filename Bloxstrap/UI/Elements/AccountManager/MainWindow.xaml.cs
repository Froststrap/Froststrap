using Bloxstrap.UI.Elements.AccountManagers.Pages;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Common;

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