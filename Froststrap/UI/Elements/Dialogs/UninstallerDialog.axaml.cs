using Avalonia.Controls;
using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class UninstallerDialog : Base.AvaloniaWindow
    {
        public bool Confirmed { get; private set; } = false;
        public bool KeepData { get; private set; } = true;

        public UninstallerDialog()
        {
            var viewModel = new UninstallerViewModel();
            viewModel.ConfirmUninstallRequest += (_, _) =>
            {
                Confirmed = true;
                KeepData = viewModel.KeepData;
                Close();
            };

            DataContext = viewModel;

            InitializeComponent();

            App.FrostRPC?.SetDialog("Uninstaller");
        }
    }
}