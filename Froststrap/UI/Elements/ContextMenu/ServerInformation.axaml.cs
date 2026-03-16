using Froststrap.UI.ViewModels.ContextMenu;

namespace Froststrap.UI.Elements.ContextMenu;

public partial class ServerInformation : Base.AvaloniaWindow
{
    public ServerInformation()
    {
        InitializeComponent();
    }

    public ServerInformation(Watcher watcher) : this()
    {
        DataContext = new ServerInformationViewModel(watcher);
    }
}