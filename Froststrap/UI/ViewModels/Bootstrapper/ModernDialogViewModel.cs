using Avalonia;
using Avalonia.Media;

namespace Froststrap.UI.ViewModels.Bootstrapper
{
    internal class ModernDialogViewModel(IBootstrapperDialog dialog, string version) : BootstrapperDialogViewModel(dialog)
    {
        public Thickness DialogBorder { get; set; } = new(0);

        public IBrush Background { get; set; } = Brushes.Black;

        public IBrush Foreground { get; set; } = new SolidColorBrush(Color.FromRgb(239, 239, 239));

        public IBrush IconColor { get; set; } = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        public IBrush ProgressBarBackground { get; set; } = new SolidColorBrush(Color.FromRgb(86, 86, 86));

        public bool VersionTextVisible => !CancelEnabled;

        public string VersionText { get; init; } = version;
    }
}