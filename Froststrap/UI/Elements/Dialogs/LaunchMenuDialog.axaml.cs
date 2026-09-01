using Froststrap.UI.ViewModels.Dialogs;
using System.Security.Cryptography;

namespace Froststrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for LaunchMenuDialog.axaml
    /// </summary>
    internal partial class LaunchMenuDialog : Base.AvaloniaWindow
    {
        public NextAction CloseAction = NextAction.Terminate;

        public LaunchMenuDialog()
        {
            InitializeComponent();

            var viewModel = new LaunchMenuViewModel();

            viewModel.CloseWindowRequest += (_, closeAction) =>
            {
                CloseAction = closeAction;
                Close();
            };

            DataContext = viewModel;

            int randomNumber = RandomNumberGenerator.GetInt32(0, 10000);
            if (randomNumber == 1)
            {
                LaunchTitle.Text = "Cartistrap";
            }
        }
    }
}