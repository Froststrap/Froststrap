using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Froststrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for AddFastFlagDialog.xaml
    /// </summary>
    public partial class AddFastFlagDialog : Base.AvaloniaWindow
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;

        public AddFastFlagDialog()
        {
            InitializeComponent();
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    FileTypeFilter = new[]
                    {
                new FilePickerFileType(Strings.FileTypes_JSONFiles)
                {
                    Patterns = new[] { "*.json" }
                }
                    }
                });

            if (files?.Count > 0)
            {
                await using var stream = await files[0].OpenReadAsync();
                using var reader = new StreamReader(stream);
                JsonTextBox.Text = await reader.ReadToEndAsync();
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedIndex == 0 && (string.IsNullOrEmpty(FlagNameTextBox.Text) || string.IsNullOrEmpty(FlagValueTextBox.Text)))
            {
                Frontend.ShowMessageBox("Flag name and Flag value cannot be empty", MessageBoxImage.Information, MessageBoxButton.OK);
                return;
            }

            if (Tabs.SelectedIndex == 1 && string.IsNullOrEmpty(JsonTextBox.Text))
            {
                Frontend.ShowMessageBox("Json TextBox cannot be empty", MessageBoxImage.Information, MessageBoxButton.OK);
                return;
            }

            Result = MessageBoxResult.OK;
            Close();
        }
    }
}