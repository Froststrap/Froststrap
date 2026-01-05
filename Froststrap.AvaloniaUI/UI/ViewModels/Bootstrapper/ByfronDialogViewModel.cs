using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Froststrap.UI.ViewModels.Bootstrapper
{
    public class ByfronDialogViewModel : BootstrapperDialogViewModel
    {
        // Using dark theme for default values.
        public Bitmap ByfronLogoLocation { get; set; } = new Bitmap(AssetLoader.Open(new Uri("avares://Froststrap.AvaloniaUI/Resources/BootstrapperStyles/ByfronDialog/ByfronLogoDark.jpg")));

        public Thickness DialogBorder { get; set; } = new Thickness(0);

        public IBrush Background { get; set; } = Brushes.Black;

        public IBrush Foreground { get; set; } = new SolidColorBrush(Color.FromRgb(239, 239, 239));

        public IBrush IconColor { get; set; } = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        public IBrush ProgressBarBackground { get; set; } = new SolidColorBrush(Color.FromRgb(86, 86, 86));

        public bool VersionTextVisible => !CancelEnabled;

        public string VersionText { get; init; }

        public ByfronDialogViewModel(IBootstrapperDialog dialog, string version) : base(dialog)
        {
            VersionText = version;
        }
    }
}