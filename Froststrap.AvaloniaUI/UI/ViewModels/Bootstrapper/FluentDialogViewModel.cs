using Bloxstrap.AppData;
using Bloxstrap.RobloxInterfaces;
using System;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace Froststrap.UI.ViewModels.Bootstrapper
{
    public class FluentDialogViewModel : BootstrapperDialogViewModel
    {
        public BackgroundType WindowBackdropType { get; set; } = BackgroundType.Mica;
        public SolidColorBrush BackgroundColourBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        public string VersionText { get; set; }
        public string ChannelText
        {
            get => _channelText;
            set
            {
                _channelText = value;
                OnPropertyChanged(nameof(ChannelText));
            }
        }
        private string _channelText = string.Empty;
        public FluentDialogViewModel(IBootstrapperDialog dialog, bool aero, string version) : base(dialog)
        {
            const int alpha = 128;

            WindowBackdropType = aero ? BackgroundType.Aero : BackgroundType.Mica;

            if (aero)
            {
                BackgroundColourBrush = App.Settings.Prop.Theme.GetFinal() == Enums.Theme.Light ?
                    new SolidColorBrush(Color.FromArgb(alpha, 225, 225, 225)) :
                    new SolidColorBrush(Color.FromArgb(alpha, 30, 30, 30));
            }

            VersionText = $"{Strings.Common_Version}: V{ExtractMajorVersion(version)}";
            ChannelText = $"{Strings.Common_Channel}: {Deployment.Channel}";

            Deployment.ChannelChanged += (_, newChannel) =>
            {
                ChannelText = $"{Strings.Common_Channel}: {newChannel}";
            };
        }

        private static string ExtractMajorVersion(string versionStr)
        {
            string[] parts = versionStr.Split('.');
            return (parts.Length >= 2) ? parts[1] : "???";
        }
    }
}