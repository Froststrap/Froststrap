using Avalonia.Controls;
using Froststrap.UI.ViewModels.Installer;

namespace Froststrap.UI.Elements.Installer.Pages
{
    public partial class CompletionPage : UserControl
    {
        private readonly CompletionViewModel _viewModel = new();

        public CompletionPage()
        {
            InitializeComponent();

            _viewModel.CloseWindowRequest += (_, closeAction) =>
            {
                if (TopLevel.GetTopLevel(this) is MainWindow window)
                {
                    window.CloseAction = closeAction;
                    window.Close();
                }
            };

            Loaded += (sender, e) =>
            {
                if (TopLevel.GetTopLevel(this) is MainWindow window)
                {
                    window.SetNextButtonText(Strings.Common_Navigation_Next);
                    window.SetButtonEnabled("back", false);
                }
            };

            DataContext = _viewModel;
        }
    }
}