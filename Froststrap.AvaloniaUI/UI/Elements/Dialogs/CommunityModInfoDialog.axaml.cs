using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class CommunityModInfoDialog : Base.AvaloniaWindow
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