// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;
using System.Xml.XPath;

namespace Froststrap.UI.Elements.Settings.Pages.GlobalSettings
{
    internal partial class GlobalSettingsEditor : UserControl, IDisposable
    {
        // This all could of been done better but meh

        private readonly ObservableCollection<GlobalSetting> _globalSettingsList = [];
        private string _searchFilter = string.Empty;
        private CancellationTokenSource? _searchCancellationTokenSource;
        private bool _disposed;

        public GlobalSettingsEditor()
        {
            InitializeComponent();
            DataGrid.ItemsSource = _globalSettingsList;

            var columns = DataGrid.Columns;
            columns[0].SortKey = o => ((GlobalSetting)o!).Name;
            columns[1].SortKey = o => ((GlobalSetting)o!).Value;
            columns[2].SortKey = o => ((GlobalSetting)o!).VectorX;
            columns[3].SortKey = o => ((GlobalSetting)o!).VectorY;

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

        private void ValueTextBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
                CommitValue(textBox);
        }

        private void ValueTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || sender is not TextBox textBox)
                return;

            CommitValue(textBox);
            e.Handled = true;
        }

        private static void CommitValue(TextBox textBox)
        {
            if (textBox.DataContext is not GlobalSetting entry || entry.IsVector)
                return;

            string newText = textBox.Text?.Trim() ?? string.Empty;
            entry.Value = newText;

            string dynamicPath = $"{App.GlobalSettings.RootPaths["UserSettings"]}/*[@name='{entry.Name}']";
            App.GlobalSettings.Document?.XPathSelectElement(dynamicPath)?.SetValue(newText);
        }

        private void VectorXTextBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
                CommitVectorX(textBox);
        }

        private void VectorXTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || sender is not TextBox textBox)
                return;

            CommitVectorX(textBox);
            e.Handled = true;
        }

        private static void CommitVectorX(TextBox textBox)
        {
            if (textBox.DataContext is not GlobalSetting entry || !entry.IsVector)
                return;

            string newText = textBox.Text?.Trim() ?? string.Empty;
            entry.VectorX = newText;

            string dynamicPath = $"{App.GlobalSettings.RootPaths["UserSettings"]}/*[@name='{entry.Name}']/X";
            App.GlobalSettings.Document?.XPathSelectElement(dynamicPath)?.SetValue(newText);
        }

        private void VectorYTextBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
                CommitVectorY(textBox);
        }

        private void VectorYTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || sender is not TextBox textBox)
                return;

            CommitVectorY(textBox);
            e.Handled = true;
        }

        private static void CommitVectorY(TextBox textBox)
        {
            if (textBox.DataContext is not GlobalSetting entry || !entry.IsVector)
                return;

            string newText = textBox.Text?.Trim() ?? string.Empty;
            entry.VectorY = newText;

            string dynamicPath = $"{App.GlobalSettings.RootPaths["UserSettings"]}/*[@name='{entry.Name}']/Y";
            App.GlobalSettings.Document?.XPathSelectElement(dynamicPath)?.SetValue(newText);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _searchCancellationTokenSource?.Cancel();
                _searchCancellationTokenSource?.Dispose();
                _searchCancellationTokenSource = null;
            }

            _disposed = true;
        }
    }
}