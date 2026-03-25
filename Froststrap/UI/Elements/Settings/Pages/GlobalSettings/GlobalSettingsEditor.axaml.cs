using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Data.Converters;
using System.Collections.ObjectModel;

namespace Froststrap.UI.Elements.Settings.Pages.GlobalSettings
{
    public partial class GlobalSettingsEditor : UserControl
    {
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

            var filtered = App.GlobalSettings.PresetPaths
                .OrderBy(x => x.Key)
                .Where(p => string.IsNullOrEmpty(_searchFilter) || p.Key.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));

            foreach (var preset in filtered)
            {
                var entry = new GlobalSetting { Name = preset.Key };

                if (preset.Value.Contains("Vector2"))
                {
                    entry.IsVector = true;
                    entry.VectorX = App.GlobalSettings.GetVectorValue(preset.Key, "X");
                    entry.VectorY = App.GlobalSettings.GetVectorValue(preset.Key, "Y");
                    entry.Value = string.Empty;
                }
                else
                {
                    entry.IsVector = false;
                    string? val = App.GlobalSettings.GetValue(preset.Value);
                    entry.Value = val ?? string.Empty;
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

            if (entry.IsVector)
            {
                if (header == "Vector X")
                {
                    entry.VectorX = newText;
                    App.GlobalSettings.SetVectorValue(entry.Name, "X", newText);
                }
                else if (header == "Vector Y")
                {
                    entry.VectorY = newText;
                    App.GlobalSettings.SetVectorValue(entry.Name, "Y", newText);
                }
            }
            else if (header == "Value")
            {
                entry.Value = newText;
                App.GlobalSettings.SetValue(App.GlobalSettings.PresetPaths[entry.Name], newText);
            }
        }
    }
}