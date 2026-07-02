using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Froststrap.UI.ViewModels.Bootstrapper
{
    public class ByfronDialogViewModel(IBootstrapperDialog dialog, string version) : BootstrapperDialogViewModel(dialog)
    {
        public Bitmap ByfronLogoLocation { get; set; } = new(AssetLoader.Open(new Uri("avares://Froststrap/Resources/BootstrapperStyles/ByfronDialog/ByfronLogoDark.jpg")));

        public Thickness DialogBorder { get; set; } = new(0);

        public IBrush Background { get; set; } = Brushes.Black;

        public IBrush Foreground { get; set; } = new SolidColorBrush(Color.FromRgb(239, 239, 239));

        public IBrush IconColor { get; set; } = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        public IBrush ProgressBarBackground { get; set; } = new SolidColorBrush(Color.FromRgb(86, 86, 86));

        public bool VersionTextVisible => !CancelEnabled;

        public string VersionText { get; init; } = version;
    }
}