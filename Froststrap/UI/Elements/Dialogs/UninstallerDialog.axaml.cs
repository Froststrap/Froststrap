using Froststrap.UI.Elements.Base;
using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for UninstallerDialog.xaml
    /// </summary>
    public partial class UninstallerDialog : AvaloniaWindow
    {
        public bool Confirmed { get; private set; } = false;
        public bool KeepData { get; private set; } = true;

        public UninstallerDialog()
        {
            InitializeComponent();

            var viewModel = new UninstallerViewModel();

            viewModel.ConfirmUninstallRequest += (_, _) =>
            {
                Confirmed = true;
                KeepData = viewModel.KeepData;
                Close();
            };

            viewModel.CancelRequest += (_, _) =>
            {
                Confirmed = false;
                Close();
            };

            DataContext = viewModel;
            App.FrostRPC?.SetDialog("Uninstaller");
        }
    }
}
