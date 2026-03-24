using Avalonia.Controls;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class CommunityModInfoDialog : AvaloniaWindow
    {
        public CommunityModInfoViewModel? ViewModel { get; }

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