using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class LaunchMenuDialog : Base.AvaloniaWindow
    {
        public NextAction CloseAction = NextAction.Terminate;

        public LaunchMenuDialog()
        {
            var viewModel = new LaunchMenuViewModel();
            viewModel.CloseWindowRequest += (_, closeAction) =>
            {
                CloseAction = closeAction;
                Close();
            };

            DataContext = viewModel;

            InitializeComponent();

            Random chance = new Random();
            if (chance.Next(0, 10000) == 1)
            {
                LaunchTitle.Text = "Cartistrap";
            }
        }
    }
}