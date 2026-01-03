using Bloxstrap.UI.Elements.Base;
using Bloxstrap.UI.ViewModels.Dialogs;

namespace Bloxstrap.UI.Elements.Dialogs
{
    public partial class ManualCookieDialog : WpfUiWindow
    {
        public ManualCookieDialogViewModel ViewModel { get; }

        public ManualCookieDialog()
        {
            ViewModel = new ManualCookieDialogViewModel(this);
            DataContext = ViewModel;

            InitializeComponent();
        }
    }
}