using Avalonia.Media;

namespace Froststrap.UI.ViewModels.Bootstrapper
{
    public class TwentyFiveDialogViewModel(IBootstrapperDialog dialog) : BootstrapperDialogViewModel(dialog)
    {
        public bool CancelButtonVisibility => CancelEnabled;
    }
}