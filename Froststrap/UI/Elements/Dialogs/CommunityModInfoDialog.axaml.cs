using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class CommunityModInfoDialog : Base.AvaloniaWindow
    {
        public CommunityModInfoViewModel? ViewModel { get; private set; }

        public CommunityModInfoDialog()
        {
            InitializeComponent();
        }

        public CommunityModInfoDialog(CommunityMod mod) : this()
        {
            ViewModel = new CommunityModInfoViewModel(mod, this);
            DataContext = ViewModel;
        }
    }
}