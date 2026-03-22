using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for LaunchMenuDialog.axaml
    /// </summary>
    public partial class LaunchMenuDialog : Base.AvaloniaWindow
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

            Random Chance = new();
            if (Chance.Next(0, 10000) == 1)
            {
                LaunchTitle.Text = "Cartistrap";
            }
        }
    }
}