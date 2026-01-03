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

        private void AddProcessExclusion_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as BehaviourViewModel;
            if (vm != null && !string.IsNullOrWhiteSpace(vm.NewProcessName))
            {
                vm.AddProcessExclusion(vm.NewProcessName);
            }
        }

        private void SaveProcessEdit_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as BehaviourViewModel;
            if (vm != null && !string.IsNullOrEmpty(vm.SelectedProcess))
            {
                vm.UpdateProcessExclusion(vm.SelectedProcess, vm.EditProcessName);
            }
        }

        private void RemoveSelectedProcessExclusion_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as BehaviourViewModel;
            vm?.RemoveProcessExclusion(vm.SelectedProcess);
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
