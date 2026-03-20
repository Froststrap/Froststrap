using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Froststrap.UI.Elements.Settings.Pages
{

    public class FastFlag
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Preset { get; set; }
    }

    public partial class FastFlagEditor : UserControl
    {
        private readonly ObservableCollection<FastFlag> _fastFlagList = new();
        private bool _showPresets = true;
        private string _searchFilter = string.Empty;
        private string _lastSearch = string.Empty;
        private DateTime _lastSearchTime = DateTime.MinValue;
        private const int _debounceDelay = 70;
        private CancellationTokenSource? _searchCancellationTokenSource;

        private DataGrid? _dataGrid;
        private TextBox? _searchTextBox;

        public FastFlagEditor()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("FastFlag Editor");
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            _dataGrid = this.FindControl<DataGrid>("DataGrid");
            _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
            ReloadList();
        }

        public void ReloadList()
        {
            if (_dataGrid is null) return;

            _fastFlagList.Clear();

            var presetFlags = FastFlagManager.PresetFlags.Values;

            foreach (var pair in App.FastFlags.Prop.OrderBy(x => x.Key))
            {
                if (!_showPresets && presetFlags.Contains(pair.Key))
                    continue;

                if (!pair.Key.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var entry = new FastFlag
                {
                    Name = pair.Key,
                    Value = pair.Value?.ToString() ?? string.Empty,
                    Preset = presetFlags.Contains(pair.Key) ? "✓" : ""
                };

                _fastFlagList.Add(entry);
            }

            if (_dataGrid.ItemsSource is null)
                _dataGrid.ItemsSource = _fastFlagList;

            UpdateTotalFlagsCount();
        }

        public string FlagCountText => $"Total flags: {_fastFlagList.Count}";

        public void UpdateTotalFlagsCount()
        {
            // No text block in simplified version
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            App.FrostRPC?.SetDialog("Add FastFlag");
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_dataGrid is null) return;

            App.FastFlags.SaveUndoSnapshot();
            App.FastFlags.suspendUndoSnapshot = true;

            var tempList = new List<FastFlag>();

            foreach (FastFlag entry in _dataGrid.SelectedItems.OfType<FastFlag>())
                tempList.Add(entry);

            foreach (FastFlag entry in tempList)
            {
                _fastFlagList.Remove(entry);
                App.FastFlags.SetValue(entry.Name, null);
            }

            App.FastFlags.suspendUndoSnapshot = false;
            UpdateTotalFlagsCount();
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDeleteAllFlagsConfirmation();
        }

        private async void ShowDeleteAllFlagsConfirmation()
        {
            if (await Frontend.ShowMessageBox(
                "Are you sure you want to delete all flags?",
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            if (!HasFlagsToDelete())
            {
                await Frontend.ShowMessageBox(
                    "There are no flags to delete.",
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                App.FastFlags.SaveUndoSnapshot();
                App.FastFlags.suspendUndoSnapshot = true;
                _fastFlagList.Clear();

                foreach (var key in App.FastFlags.Prop.Keys.ToList())
                {
                    App.FastFlags.SetValue(key, null);
                }

                App.FastFlags.suspendUndoSnapshot = false;
                ReloadList();
            }
            catch (Exception ex)
            {
                await HandleError(ex);
            }
        }

        private bool HasFlagsToDelete()
        {
            return _fastFlagList.Any() || App.FastFlags.Prop.Any();
        }

        private async Task HandleError(Exception ex)
        {
            await Frontend.ShowMessageBox($"An error occurred:\n{ex.Message}", MessageBoxImage.Error);
        }

        private async void CopyJSONButton_Click(object sender, RoutedEventArgs e)
        {
            var json = BuildFormattedJSON();
            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(json);
                }
            }
            catch (Exception ex)
            {
                await HandleError(ex);
            }
        }

        private void ExportJSONButton_Click(object sender, RoutedEventArgs e)
        {
            var json = BuildFormattedJSON();
            SaveJSONToFile(json);
        }

        private string BuildFormattedJSON()
        {
            var flags = App.FastFlags.Prop;

            var groupedFlags = flags
                .GroupBy(kvp =>
                {
                    var match = Regex.Match(kvp.Key, @"^[A-Z]+[a-z]*");
                    return match.Success ? match.Value : "Other";
                })
                .OrderBy(g => g.Key);

            var formattedJson = new StringBuilder();
            formattedJson.AppendLine("{");

            int totalItems = flags.Count;
            int writtenItems = 0;
            int groupIndex = 0;

            foreach (var group in groupedFlags)
            {
                if (groupIndex > 0)
                    formattedJson.AppendLine();

                var sortedGroup = group
                    .OrderByDescending(kvp => kvp.Key.Length + (kvp.Value?.ToString()?.Length ?? 0));

                foreach (var kvp in sortedGroup)
                {
                    writtenItems++;
                    bool isLast = (writtenItems == totalItems);
                    string line = $"    \"{kvp.Key}\": \"{kvp.Value}\"";

                    if (!isLast)
                        line += ",";

                    formattedJson.AppendLine(line);
                }

                groupIndex++;
            }

            formattedJson.AppendLine("}");
            return formattedJson.ToString();
        }

        private async void SaveJSONToFile(string json)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is null) return;

                // TODO: Implement file save dialog for Avalonia
                await Frontend.ShowMessageBox("File save dialog not yet implemented", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                await HandleError(ex);
            }
        }

        private void FlagProfiles_Click(object sender, RoutedEventArgs e)
        {
            App.FrostRPC?.SetDialog("Profiles");
        }

        private void AdvancedSettings_Click(object sender, RoutedEventArgs e)
        {
            App.FrostRPC?.SetDialog("Advanced Settings");
        }

        private async void CleanListButton_Click(object sender, RoutedEventArgs e)
        {
            App.FrostRPC?.SetDialog("Dialog: Cleaning List");
            App.FastFlags.suspendUndoSnapshot = true;
            App.FastFlags.SaveUndoSnapshot();

            try
            {
                var remoteManager = new RemoteDataManager();
                await remoteManager.LoadData();

                var base64Flags = DecodeBase64Flags(remoteManager.Prop.AllowedFastFlags);
                App.Logger.WriteLine("CleanList", $"Loaded {base64Flags.Count} allowed flags.");

                var allFlags = App.FastFlags.GetAllFlags();
                var invalidRemoved = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                foreach (var flag in allFlags)
                {
                    var name = flag.Name.Trim();
                    if (!base64Flags.Contains(name))
                    {
                        invalidRemoved[name] = flag.Value;
                        App.FastFlags.SetValue(name, null);
                    }
                }

                int totalChanges = invalidRemoved.Count;
                if (totalChanges == 0)
                {
                    await Frontend.ShowMessageBox("No invalid FastFlags detected.", MessageBoxImage.Information);
                    return;
                }

                await Frontend.ShowMessageBox(
                    $"{totalChanges} have been removed due to not being in allow list.",
                    MessageBoxImage.Information);

                ReloadList();
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox($"An error occurred during FastFlag cleanup: {ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                App.FastFlags.suspendUndoSnapshot = false;
                App.FrostRPC?.ClearDialog();
            }
        }

        private static HashSet<string> DecodeBase64Flags(string? base64)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(base64))
                return result;

            try
            {
                var cleanBase64 = base64.Trim().Replace("\n", "").Replace("\r", "");
                var bytes = Convert.FromBase64String(cleanBase64);
                var jsonText = Encoding.UTF8.GetString(bytes);

                using var doc = JsonDocument.Parse(jsonText);
                if (doc.RootElement.TryGetProperty("Allowed", out var allowed))
                {
                    foreach (var flagElem in allowed.EnumerateArray())
                    {
                        var flag = flagElem.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(flag))
                            result.Add(flag);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("DecodeBase64Flags", $"Failed to decode Base64: {ex.Message}");
            }

            return result;
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkbox)
                return;

            _showPresets = checkbox.IsChecked ?? true;
            ReloadList();
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            App.FastFlags.suspendUndoSnapshot = true;
            App.FastFlags.SaveUndoSnapshot();

            if (e.Row.DataContext is not FastFlag entry)
                return;

            // TODO: Handle cell editing for Avalonia DataGrid

            App.FastFlags.suspendUndoSnapshot = false;
            UpdateTotalFlagsCount();
        }

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textbox) return;

            string newSearch = textbox.Text?.Trim() ?? string.Empty;

            if (newSearch == _lastSearch && (DateTime.Now - _lastSearchTime).TotalMilliseconds < _debounceDelay)
                return;

            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();

            _searchFilter = newSearch;
            _lastSearch = newSearch;
            _lastSearchTime = DateTime.Now;

            try
            {
                await Task.Delay(_debounceDelay, _searchCancellationTokenSource.Token);

                if (_searchCancellationTokenSource.Token.IsCancellationRequested)
                    return;

                ReloadList();
            }
            catch (TaskCanceledException)
            {
            }
        }
    }
}
