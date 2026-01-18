namespace Froststrap.UI.ViewModels.Bootstrapper
{
    public class ClassicFluentDialogViewModel : BootstrapperDialogViewModel
    {
        public double FooterOpacity => Environment.OSVersion.Version.Build >= 22000 ? 0.4 : 1;

        public ClassicFluentDialogViewModel(IBootstrapperDialog dialog) : base(dialog)
        {
        }
    }
}
