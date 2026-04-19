using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Froststrap.UI.Elements.Base;

namespace Froststrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for AddFastFlagDialog.xaml
    /// </summary>
    public partial class AddFastFlagDialog : AvaloniaWindow
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;

        public AddFastFlagDialog()
        {
            InitializeComponent();

            Tabs.SelectionChanged += (s, e) => UpdateUiState();
            FlagNameTextBox.TextChanged += (s, e) => UpdateUiState();
            JsonTextBox.TextChanged += (s, e) => UpdateUiState();
        }

        private void UpdateUiState()
        {
            if (Tabs == null || OkButton == null) return;

            bool isValid = false;

            if (Tabs.SelectedIndex == 0)
            {
                isValid = !string.IsNullOrWhiteSpace(FlagNameTextBox.Text);
            }
            else if (Tabs.SelectedIndex == 1)
            {
                isValid = !string.IsNullOrWhiteSpace(JsonTextBox.Text);
            }

            OkButton.IsEnabled = isValid;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            this.Result = MessageBoxResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Result = MessageBoxResult.Cancel;
            this.Close();
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var options = new FilePickerOpenOptions
            {
                Title = "Import Flags",
                AllowMultiple = false,
                FileTypeFilter = new[]
                { 
                    new FilePickerFileType("Configuration Files")
                    {
                        Patterns = new[] { "*.json", "*.txt" }
                    },
                    new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                    new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } }
                }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);

            if (files.Count > 0)
            {
                await using var stream = await files[0].OpenReadAsync();
                using var reader = new StreamReader(stream);
                JsonTextBox.Text = await reader.ReadToEndAsync();
            }
        }
    }
}