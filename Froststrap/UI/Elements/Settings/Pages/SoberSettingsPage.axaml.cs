using Avalonia.Controls;

namespace Froststrap.UI.Elements.Settings.Pages;

public partial class SoberSettingsPage : UserControl
{
    public SoberSettingsPage()
    {
        InitializeComponent();

        App.FrostRPC?.SetPage("Sober Settings");
    }
}
