using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages;

public partial class ProfileSettingsPage : UserControl
{
    public ProfileSettingsPage()
    {
        InitializeComponent();
        App.FrostRPC?.SetPage("Settings Profiles");
    }
}