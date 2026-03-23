namespace Froststrap.UI.ViewModels.Installer
{
    public class WelcomeViewModel : NotifyPropertyChangedViewModel
    {
        // formatting is done here instead of in xaml, it's just a bit easier
        public string MainText => String.Format(
                    Strings.Installer_Welcome_MainText,
                    "[github.com/Froststrap/Froststrap](https://github.com/Froststrap/Froststrap)",
                    "https://github.com/Froststrap/Froststrap"
                );

        public bool CanContinue { get; set; } = false;
    }
}
