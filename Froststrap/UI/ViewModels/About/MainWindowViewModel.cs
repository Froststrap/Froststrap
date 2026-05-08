using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.About
{
    public partial class MainWindowViewModel : NotifyPropertyChangedViewModel
    {
        private object? _currentPage;
        public object? CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        private string _selectedPage = "about";
        public string SelectedPage
        {
            get => _selectedPage;
            set => SetProperty(ref _selectedPage, value);
        }

        public IRelayCommand NavigateToAboutCommand { get; }
        public IRelayCommand NavigateToLicensesCommand { get; }

        public MainWindowViewModel()
        {
            NavigateToAboutCommand = new RelayCommand(NavigateToAbout);
            NavigateToLicensesCommand = new RelayCommand(NavigateToLicenses);

            NavigateToAbout();
        }

        private void NavigateToAbout()
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
        }

        private void NavigateToLicenses()
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
        }
    }
}