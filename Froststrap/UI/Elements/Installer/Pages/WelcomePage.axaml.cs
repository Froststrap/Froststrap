using Avalonia.Controls;
using Froststrap.UI.ViewModels.Installer;

namespace Froststrap.UI.Elements.Installer.Pages
{
    public partial class WelcomePage : UserControl
    {
        private readonly WelcomeViewModel _viewModel = new();

        public WelcomePage()
        {
            InitializeComponent();

            Loaded += (sender, e) =>
            {
                if (TopLevel.GetTopLevel(this) is MainWindow window)
                {
                    window.SetButtonEnabled("next", true);

                    window.SetNextButtonText(Strings.Common_Navigation_Next);
                }
            };

            DataContext = _viewModel;
        }
    }
}