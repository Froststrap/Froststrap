using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages;

public partial class AppearancePage : UserControl
{
    public AppearancePage()
    {
        InitializeComponent();

        App.FrostRPC?.SetPage("Appearance");
    }
}
