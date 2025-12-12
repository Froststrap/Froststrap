using Bloxstrap.UI.ViewModels.Dialogs;

namespace Bloxstrap.UI.Elements.Dialogs
{
    public partial class CommunityModInfoDialog
    {
        public CommunityModInfoViewModel ViewModel { get; }

        public CommunityModInfoDialog(CommunityMod mod)
        {
            InitializeComponent();
            ViewModel = new CommunityModInfoViewModel(mod, this);
            DataContext = ViewModel;
        }
    }
}