namespace Froststrap.UI.ViewModels.Onboarding
{
    internal class Page3ViewModel : NotifyPropertyChangedViewModel
    {
        public Page3ViewModel()
        {
            App.Cookies.StateChanged += (_, state) => CookieLoadingFailed = state is not (CookieState.Success or CookieState.Unknown);
        }


        public static bool CookieLoadingFinished => true;

        public bool CookieAccess
        {
            get => App.Settings.Prop.AllowCookieAccess;
            set
            {
                App.Settings.Prop.AllowCookieAccess = value;
                if (value)
                    Task.Run(App.Cookies.LoadCookies);

                OnPropertyChanged(nameof(CookieAccess));
            }
        }

        private bool _cookieLoadingFailed;
        public bool CookieLoadingFailed
        {
            get => _cookieLoadingFailed;
            set
            {
                _cookieLoadingFailed = value;
                OnPropertyChanged(nameof(CookieLoadingFailed));
            }
        }

    }
}
