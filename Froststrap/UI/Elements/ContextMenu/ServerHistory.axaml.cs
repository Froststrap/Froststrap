using Froststrap.Integrations;
using Froststrap.UI.ViewModels.ContextMenu;

namespace Froststrap.UI.Elements.ContextMenu
{
    /// <summary>
    /// Interaction logic for ServerHistory.axaml
    /// </summary>
    public partial class ServerHistory : Base.AvaloniaWindow
    {
        public ServerHistory()
        {
            InitializeComponent();
        }

        public ServerHistory(ActivityWatcher watcher) : this()
        {
            var viewModel = new ServerHistoryViewModel(watcher);

            viewModel.RequestCloseEvent += (_, _) => Close();

            DataContext = viewModel;
        }
    }
}