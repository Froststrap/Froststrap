using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Froststrap.UI.ViewModels.About
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? _currentPage;

        [ObservableProperty]
        private string _selectedPage = "about";

        public IRelayCommand NavigateToAboutCommand { get; }
        public IRelayCommand NavigateToLicensesCommand { get; }

        public MainWindowViewModel()
        {
            NavigateToAboutCommand = new RelayCommand(() =>
            {
                try
                {
                    SelectedPage = "about";
                    CurrentPage = new AboutViewModel();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("AboutMainWindowViewModel::NavigateToAbout", ex);
                }
            });

            NavigateToLicensesCommand = new RelayCommand(() =>
            {
                try
                {
                    SelectedPage = "licenses";
                    CurrentPage = new LicensesViewModel();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("AboutMainWindowViewModel::NavigateToLicenses", ex);
                }
            });

            NavigateToAboutCommand.Execute(null);
        }
    }
}