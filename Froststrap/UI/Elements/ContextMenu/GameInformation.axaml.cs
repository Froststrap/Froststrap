using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Froststrap.UI.ViewModels.ContextMenu;

namespace Froststrap.UI.Elements.ContextMenu;

public partial class GameInformation : Base.AvaloniaWindow
{
    public GameInformation()
    {
        InitializeComponent();
    }

    public GameInformation(long placeId, long universeId) : this()
    {
        DataContext = new GameInformationViewModel(placeId, universeId);
    }
}