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

            viewModel.CloseWindowRequest += (s, e) =>
            {
                Close();
            };

            DataContext = viewModel;

            Random chance = new();
            if (chance.Next(0, 10000) == 1)
            {
                LaunchTitle.Text = "Cartistrap";
            }
        }
    }
}