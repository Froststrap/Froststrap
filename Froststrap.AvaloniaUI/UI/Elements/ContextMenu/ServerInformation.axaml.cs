using Froststrap.UI.ViewModels.ContextMenu;

namespace Froststrap.UI.Elements.ContextMenu;

public partial class ServerInformation : Base.AvaloniaWindow
{
    public ServerInformation(Watcher watcher)
    {
		DataContext = new ServerInformationViewModel(watcher);
		InitializeComponent();
    }
}