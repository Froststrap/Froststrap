using Avalonia.Controls;

namespace Froststrap.UI.Elements.Settings.Pages;

public partial class BehaviourPage : UserControl
{
    public BehaviourPage()
    {
        InitializeComponent();

        App.FrostRPC?.SetPage("Bootstrapper");
    }
}
