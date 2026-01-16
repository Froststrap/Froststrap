using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Froststrap.UI.ViewModels.ContextMenu;

namespace Froststrap.UI.Elements.ContextMenu;

public partial class GameInformation : Window
{
    public GameInformation(long placeId, long universeId)
    {
		DataContext = new GameInformationViewModel(placeId, universeId);
		InitializeComponent();
    }
}