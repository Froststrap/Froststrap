using Bloxstrap.UI.Elements.ContextMenu;
using Bloxstrap.UI.Elements.Dialogs;
using Bloxstrap.UI.ViewModels.Settings;
using Microsoft.Win32;
using System.Windows;
using Wpf.Ui.Hardware;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for ChannelPage.xaml
    /// </summary>
    public partial class ChannelPage
    {
        public ChannelPage()
        {
            DataContext = new ChannelViewModel();
            InitializeComponent();
            App.FrostRPC?.SetPage("Settings");
        }

        private void OpenChannelListDialog_Click(object sender, RoutedEventArgs e)
        {
            App.FrostRPC?.SetDialog("Channel List");

            var dialog = new ChannelListsDialog();
            dialog.Owner = Window.GetWindow(this);

            dialog.ShowDialog();

            App.FrostRPC?.ClearDialog();
        }
    }
}