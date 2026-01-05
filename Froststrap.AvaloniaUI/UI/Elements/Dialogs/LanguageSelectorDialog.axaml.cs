using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for LanguageSelectorDialog.xaml
    /// </summary>
    public partial class LanguageSelectorDialog : Base.AvaloniaWindow
    {
        public LanguageSelectorDialog()
        {
            var viewModel = new LanguageSelectorViewModel();

            DataContext = viewModel;
            InitializeComponent();

            App.FrostRPC?.SetDialog("Language Selector");

            viewModel.CloseRequestEvent += (_, _) => Close();
        }
    }
}
