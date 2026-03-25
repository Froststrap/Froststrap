using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;
using System.Xml.XPath;

namespace Froststrap.UI.Elements.Settings.Pages.GlobalSettings
{
    public partial class GlobalSettingsEditor : UserControl
    {
        // This all could of been done better but meh

        public static readonly IValueConverter DimIfTrue = new FuncValueConverter<bool, double>(x => x ? 0.3 : 1.0);
        public static readonly IValueConverter DimIfFalse = new FuncValueConverter<bool, double>(x => x ? 1.0 : 0.3);

        private readonly ObservableCollection<GlobalSetting> _globalSettingsList = new();
        private string _searchFilter = string.Empty;
        private CancellationTokenSource? _searchCancellationTokenSource;

        public GlobalSettingsEditor()
        {
            InitializeComponent();
            DataGrid.ItemsSource = _globalSettingsList;
            App.FrostRPC?.SetPage("Global Settings Editor");
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            if (!App.GlobalSettings.Loaded) App.GlobalSettings.Load();
            ReloadList();
        }

        public void ReloadList()
        {
            _globalSettingsList.Clear();

            if (App.GlobalSettings.Document == null)
            {
                App.GlobalSettings.Load();
            }

            string rootPath = App.GlobalSettings.RootPaths["UserSettings"];
            var propertiesContainer = App.GlobalSettings.Document?.XPathSelectElement(rootPath);

            if (propertiesContainer == null) return;

            var allSettings = propertiesContainer.Elements().OrderBy(x => x.Attribute("name")?.Value);

            foreach (var element in allSettings)
            {
                string? rawName = element.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(rawName)) continue;

                if (!string.IsNullOrEmpty(_searchFilter) &&
                    !rawName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var entry = new GlobalSetting { Name = rawName };

                if (element.Name.LocalName == "Vector2")
                {
                    entry.IsVector = true;
                    entry.VectorX = element.Element("X")?.Value ?? "0";
                    entry.VectorY = element.Element("Y")?.Value ?? "0";
                    entry.Value = string.Empty;
                }
                else
                {
                    entry.IsVector = false;
                    entry.Value = element.Value ?? string.Empty;
                    entry.VectorX = string.Empty;
                    entry.VectorY = string.Empty;
                }

                _globalSettingsList.Add(entry);
            }
        }

        private async void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();

            try
            {
                await Task.Delay(70, _searchCancellationTokenSource.Token);
                _searchFilter = (sender as TextBox)?.Text?.Trim() ?? "";
                ReloadList();
            }
            catch (TaskCanceledException) { }
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.DataContext is not GlobalSetting entry || e.EditingElement is not TextBox textbox)
                return;

            string newText = textbox.Text?.Trim() ?? string.Empty;
            string header = e.Column.Header?.ToString() ?? string.Empty;

            string dynamicPath = $"{App.GlobalSettings.RootPaths["UserSettings"]}/*[@name='{entry.Name}']";

            if (entry.IsVector)
            {
                if (header == "Vector X")
                {
                    entry.VectorX = newText;
                    App.GlobalSettings.Document?.XPathSelectElement($"{dynamicPath}/X")?.SetValue(newText);
                }
                else if (header == "Vector Y")
                {
                    entry.VectorY = newText;
                    App.GlobalSettings.Document?.XPathSelectElement($"{dynamicPath}/Y")?.SetValue(newText);
                }
            }
            else if (header == "Value")
            {
                entry.Value = newText;
                App.GlobalSettings.Document?.XPathSelectElement(dynamicPath)?.SetValue(newText);
            }
        }
    }
}