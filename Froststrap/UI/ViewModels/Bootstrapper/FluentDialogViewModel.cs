using Avalonia.Controls;
using Avalonia.Media;
using Froststrap.RobloxInterfaces;

namespace Froststrap.UI.ViewModels.Bootstrapper
{
    public class FluentDialogViewModel : BootstrapperDialogViewModel
    {
        public List<WindowTransparencyLevel> WindowBackdropType { get; set; }
        public IBrush BackgroundColourBrush { get; set; } = Brushes.Transparent;
        public string VersionText { get; set; }
        public string ChannelText { get; set; }

        public FluentDialogViewModel(IBootstrapperDialog dialog, bool aero, string version) : base(dialog)
        {
            WindowBackdropType = aero
                ? [WindowTransparencyLevel.AcrylicBlur]
                : [WindowTransparencyLevel.None];

            var isLight = App.Settings.Prop.Theme.GetFinal() == Theme.Light;

            if (aero)
            {
                byte alpha = 127;
                var color = isLight
                    ? Color.FromArgb(alpha, 225, 225, 225)
                    : Color.FromArgb(alpha, 30, 30, 30);
                BackgroundColourBrush = new SolidColorBrush(color);
            }
            else
            {
                var color = isLight
                    ? Color.FromRgb(240, 240, 240)
                    : Color.FromRgb(30, 30, 30);
                BackgroundColourBrush = new SolidColorBrush(color);
            }

            VersionText = $"Version: V{ExtractMajorVersion(version)}";
            ChannelText = $"Channel: {Deployment.Channel}";

            Deployment.ChannelChanged += (_, newChannel) =>
            {
                ChannelText = $"Channel: {newChannel}";
                OnPropertyChanged(nameof(ChannelText));
            };
        }

        private static string ExtractMajorVersion(string versionStr)
        {
            string[] parts = versionStr.Split('.');
            return (parts.Length >= 2) ? parts[1] : "???";
        }
    }
}