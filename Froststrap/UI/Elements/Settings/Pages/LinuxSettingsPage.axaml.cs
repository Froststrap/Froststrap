using Avalonia.Controls;

namespace Froststrap.UI.Elements.Settings.Pages;

public partial class LinuxSettingsPage : UserControl
{
    public LinuxSettingsPage()
    {
        InitializeComponent();

        App.FrostRPC?.SetPage("Sober Settings");
    }
}
