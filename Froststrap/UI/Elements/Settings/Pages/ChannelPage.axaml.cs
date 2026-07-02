using Avalonia.Controls;
using Avalonia.Interactivity;
using Froststrap.UI.Elements.Dialogs;

namespace Froststrap.UI.Elements.Settings.Pages;

public partial class ChannelPage : UserControl
{
    public ChannelPage()
    {
        InitializeComponent();

        App.FrostRPC?.SetPage("Deployment");
    }
}