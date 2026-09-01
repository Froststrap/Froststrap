using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.About
{
    internal partial class MainWindowViewModel : NotifyPropertyChangedViewModel
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
        public IRelayCommand RestartOnboardingCommand { get; }

        public MainWindowViewModel()
        {
            NavigateToAboutCommand = new RelayCommand(NavigateToAbout);
            NavigateToLicensesCommand = new RelayCommand(NavigateToLicenses);
            RestartOnboardingCommand = new RelayCommand(RestartOnboarding);

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
                App.Logger.Error("Unhandled exception: ", ex);
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
                App.Logger.Error("Unhandled exception: ", ex);
            }
        }

        private void RestartOnboarding()
        {
            try
            {
                var onboardingWindow = new Froststrap.UI.Elements.Onboarding.MainWindow();
                onboardingWindow.Show();
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to start onboarding: ", ex);
            }
        }
    }
}
