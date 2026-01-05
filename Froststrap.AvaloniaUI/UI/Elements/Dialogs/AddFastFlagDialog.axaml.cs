using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class AddFastFlagDialog : Base.AvaloniaWindow
    {
        public string? FormattedName { get; private set; }
        public string? FormattedValue { get; private set; }
        public bool Result { get; private set; } = false;

        public ObservableCollection<CommonValueItem> BooleanValues { get; } = new ObservableCollection<CommonValueItem>()
        {
            new CommonValueItem { Value = "True", Group = "Boolean" },
            new CommonValueItem { Value = "False", Group = "Boolean" },
        };

        public ObservableCollection<CommonValueItem> NumericValues { get; } = new ObservableCollection<CommonValueItem>()
        {
            new CommonValueItem { Value = "64", Group = "Integers" },
            new CommonValueItem { Value = "100", Group = "Integers" },
            new CommonValueItem { Value = "128", Group = "Integers" },
            new CommonValueItem { Value = "256", Group = "Integers" },
            new CommonValueItem { Value = "512", Group = "Integers" },
            new CommonValueItem { Value = "1024", Group = "Integers" },
            new CommonValueItem { Value = "2048", Group = "Integers" },
            new CommonValueItem { Value = "4096", Group = "Integers" },
            new CommonValueItem { Value = "8192", Group = "Integers" },
            new CommonValueItem { Value = "10000", Group = "Integers" },
            new CommonValueItem { Value = "16384", Group = "Integers" },
            new CommonValueItem { Value = "2147483647", Group = "Integers" },
            new CommonValueItem { Value = "-2147483648", Group = "Integers" },
        };

        public ObservableCollection<CommonValueItem> SpecialValues { get; } = new ObservableCollection<CommonValueItem>()
        {
            new CommonValueItem { Value = "null", Group = "Special" },
        };

        public ObservableCollection<CommonValueItem> AllValues { get; }

        public AddFastFlagDialog()
        {
            InitializeComponent();

            AllValues = new ObservableCollection<CommonValueItem>();
            foreach (var item in BooleanValues) AllValues.Add(item);
            foreach (var item in NumericValues) AllValues.Add(item);
            foreach (var item in SpecialValues) AllValues.Add(item);

            DataContext = this;
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select JSON or Text File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("JSON and Text Files")
                    {
                        Patterns = new[] { "*.json", "*.txt" }
                    }
                }
            });

            if (files.Count == 0)
                return;

            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            JsonTextBox.Text = await reader.ReadToEndAsync();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = Tabs.SelectedIndex;

            if (selectedIndex == 0)
            {
                string name = FlagNameTextBox?.Text?.Trim() ?? "";
                string value = FlagValueComboBox?.Text?.Trim() ?? "";

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value) && value != "Enter or select a value")
                {
                    FormattedName = name;
                    FormattedValue = value;
                    Result = true;
                    Close();
                    return;
                }
                else
                {
                    Frontend.ShowMessageBox(
                        "Please fill in both Name and Value with valid values.",
                        MessageBoxImage.Error,
                        MessageBoxButton.OK);
                    return;
                }
            }
            else if (selectedIndex == 1)
            {
                string json = JsonTextBox?.Text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(json))
                {
                    FormattedName = null;
                    FormattedValue = json;
                    Result = true;
                    Close();
                    return;
                }
                else
                {
                    Frontend.ShowMessageBox(
                        "Please enter valid JSON content.",
                        MessageBoxImage.Error,
                        MessageBoxButton.OK);
                    return;
                }
            }
        }

        private void FlagValueComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (FlagValueComboBox is ComboBox comboBox)
            {
                
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }
    }

    public class CommonValueItem
    {
        public string Value { get; set; } = "";
        public string Group { get; set; } = "";
        public override string ToString() => Value;
    }
}