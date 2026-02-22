using Bloxstrap.UI.ViewModels.Settings;
using Bloxstrap.UI.Elements.Dialogs;
using System.Windows;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for BehaviourPage.xaml
    /// </summary>
    public partial class BehaviourPage
    {
        public BehaviourPage()
        {
            DataContext = new BehaviourViewModel();
            InitializeComponent();
            App.FrostRPC?.SetPage("Bootstrapper");
        }

        private void OpenMultiblox_Click(object sender, RoutedEventArgs e)
        {
            var window = new MultibloxDialog
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
        }
    }
}
