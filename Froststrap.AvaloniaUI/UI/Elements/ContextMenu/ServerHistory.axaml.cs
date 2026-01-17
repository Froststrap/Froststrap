using Froststrap.Integrations;
using Froststrap.UI.ViewModels.ContextMenu;


namespace Froststrap.UI.Elements.ContextMenu
{
    /// <summary>
    /// Interaction logic for ServerInformation.xaml
    /// </summary>
    public partial class ServerHistory: Base.AvaloniaWindow
    {
        public ServerHistory(ActivityWatcher watcher)
        {
            var viewModel = new ServerHistoryViewModel(watcher);

            viewModel.RequestCloseEvent += (_, _) => Close();

            DataContext = viewModel;
            InitializeComponent();
        }
    }
}