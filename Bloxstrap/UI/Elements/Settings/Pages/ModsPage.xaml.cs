using System.Windows;
using System.Windows.Controls;
using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class ModsPage
    {
        private string _originalName = "";

        public ModsPage()
        {
            DataContext = new ModsViewModel();
            InitializeComponent();
        }

        private void ModName_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
                _originalName = textBox.Text;
        }

        private void ModName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && DataContext is ModsViewModel viewModel)
            {
                if (_originalName != textBox.Text)
                {
                    bool success = viewModel.RenameMod(_originalName, textBox.Text);

                    if (success)
                    {
                        var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                        binding?.UpdateSource();
                    }
                    else
                    {
                        textBox.Text = _originalName;
                    }
                }
            }
        }

        private void Page_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is ModsViewModel vm)
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                Task.Run(() => vm.ProcessDroppedFiles(files));
            }
        }
    }
}