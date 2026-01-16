using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Froststrap.UI.ViewModels.ContextMenu;

namespace Froststrap.UI.Elements.ContextMenu;

public partial class ServerInformation : Window
{
    public ServerInformation(Watcher watcher)
    {
		DataContext = new ServerInformationViewModel(watcher);
		InitializeComponent();
    }
}