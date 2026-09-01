using Avalonia.Controls;

namespace Froststrap.UI.Elements.Settings.Pages;

internal partial class BehaviourPage : UserControl
{
    public BehaviourPage()
    {
        InitializeComponent();

        App.FrostRPC?.SetPage("Bootstrapper");
    }
}
