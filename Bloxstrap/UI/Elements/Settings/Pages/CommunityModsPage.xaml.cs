using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for CommunityModsPage.xaml
    /// </summary>
    public partial class CommunityModsPage
    {
        public CommunityModsPage()
        {
            DataContext = new CommunityModsViewModel();
            InitializeComponent();
            App.FrostRPC?.SetPage("Community Mods");
        }
    }
}