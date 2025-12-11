using Bloxstrap.UI.ViewModels.ContextMenu;

namespace Bloxstrap.UI.Elements.ContextMenu
{
    /// <summary>
    /// Interaction logic for GameInformation.xaml
    /// </summary>
    public partial class GameInformation
    {
        public GameInformation(long placeId, long universeId)
        {
            DataContext = new GameInformationViewModel(placeId, universeId);
            InitializeComponent();
        }
    }
}