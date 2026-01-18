using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class MultibloxDialog : Base.AvaloniaWindow
    {
        private readonly MultibloxViewModel _viewModel = new();

        public MultibloxDialog()
        {
            InitializeComponent();
            DataContext = _viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.Dispose();
            App.Settings.Save();
            base.OnClosed(e);
        }
    }
}