namespace Froststrap.UI.ViewModels.Bootstrapper
{
    internal class TwentyFiveDialogViewModel(IBootstrapperDialog dialog) : BootstrapperDialogViewModel(dialog)
    {
        public bool CancelButtonVisibility => CancelEnabled;
    }
}