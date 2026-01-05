using Avalonia.Media;

namespace Froststrap.UI.ViewModels.Bootstrapper
{
    public class FroststrapDialogViewModel : BootstrapperDialogViewModel
    {
        public BackgroundType WindowBackdropType { get; set; } = BackgroundType.Mica;

        public ISolidColorBrush BackgroundColourBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

        public FroststrapDialogViewModel(IBootstrapperDialog dialog) : base(dialog)
        {
        }
    }
}