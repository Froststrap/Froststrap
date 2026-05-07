using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Froststrap.UI.ViewModels.Settings;
using Froststrap.UI.Elements.ContextMenu;
using Froststrap.UI.Elements.Dialogs;

namespace Froststrap.UI.Elements.Settings.Pages;

public partial class ChannelPage : UserControl
{
    public ChannelPage()
    {
        InitializeComponent();

        App.FrostRPC?.SetPage("Settings");
    }

    private void OpenChannelListDialog_Click(object? sender, RoutedEventArgs e)
    {
        App.FrostRPC?.SetDialog("Channel List");

        var dialog = new ChannelListsDialog();

        if (TopLevel.GetTopLevel(this) is Window window)
        {
            dialog.ShowDialog(window);
        }

        App.FrostRPC?.ClearDialog();
    }
}