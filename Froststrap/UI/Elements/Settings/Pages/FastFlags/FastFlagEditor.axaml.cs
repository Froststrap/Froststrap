using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentIcons.Common;
using Froststrap.UI.Elements.Dialogs;
using System.Collections.ObjectModel;

namespace Froststrap.UI.Elements.Settings.Pages.FastFlags
{
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

            this.Focus();

            ReloadList();
        }

        public void ReloadList()
        {
            if (_dataGrid is null) return;

            _fastFlagList.Clear();

            var presetFlags = FastFlagManager.PresetFlags.Values;

            foreach (var pair in App.FastFlags.Prop.OrderBy(x => x.Key))
            {
                if (!pair.Key.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var entry = new FastFlag
                {
                    Name = pair.Key,
                    Value = pair.Value?.ToString() ?? string.Empty,
                    Preset = presetFlags.Contains(pair.Key) ? FluentIcons.Common.Symbol.CheckmarkCircle : FluentIcons.Common.Symbol.CircleOff
                };

                _fastFlagList.Add(entry);
            }

            if (_dataGrid.ItemsSource is null)
                _dataGrid.ItemsSource = _fastFlagList;

            if (!(_fastFlagList.Any() || App.FastFlags.Prop.Any()))
                EmptyTextBlock.IsVisible = true;
            else
                EmptyTextBlock.IsVisible = false;

            if (DeleteSelectedButton != null)
                DeleteSelectedButton.IsEnabled = false;

            UpdateTotalFlagsCount();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                switch (e.Key)
                {
                    case Key.Z:
                        App.FastFlags.Undo();
                        ReloadList();
                        e.Handled = true;
                        break;

                    case Key.Y:
                        App.FastFlags.Redo();
                        ReloadList();
                        e.Handled = true;
                        break;
                }
            }
        }

        public void UpdateTotalFlagsCount()
        {
            int count = _fastFlagList.Count;
            TotalFlagsTextBlock.Text = $"Total Flags: {count}";
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            App.FrostRPC?.SetDialog("Add FastFlag");

            ShowAddDialog();
        }

        private async void ShowAddDialog()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not Window parentWindow) return;

            App.FrostRPC?.SetDialog("Add FastFlag");

            var dialog = new AddFastFlagDialog();
            await dialog.ShowDialog(parentWindow);

            App.FrostRPC?.ClearDialog();

            if (dialog.Result != MessageBoxResult.OK)
                return;

            if (dialog.Tabs.SelectedIndex == 0)
            {
                await AddSingle(dialog.FlagNameTextBox.Text?.Trim() ?? string.Empty, dialog.FlagValueTextBox.Text ?? string.Empty);
            }
            else if (dialog.Tabs.SelectedIndex == 1)
            {
                await ImportJSON(dialog.JsonTextBox.Text ?? string.Empty);
            }
        }

        private async Task AddSingle(string name, string value)
        {
            FastFlag? entry;

            if (App.FastFlags.GetValue(name) is null)
            {
                entry = new FastFlag
                {
                    Name = name,
                    Value = value
                };

                if (!name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    ClearSearch();

                App.FastFlags.SetValue(entry.Name, entry.Value);
                _fastFlagList.Add(entry);
            }
            else
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_FastFlagEditor_AlreadyExists, MessageBoxImage.Information, MessageBoxButton.OK);

                bool refresh = false;

                if (!_showPresets && FastFlagManager.PresetFlags.Values.Contains(name))
                {
                    TogglePresetsButton.IsChecked = true;
                    _showPresets = true;
                    refresh = true;
                }

                if (!name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                {
                    ClearSearch(false);
                    refresh = true;
                }

                if (refresh)
                    ReloadList();

                entry = _fastFlagList.FirstOrDefault(x => x.Name == name);
            }

            var remoteManager = new RemoteDataManager();
            await remoteManager.LoadData();
            var base64Flags = DecodeBase64Flags(remoteManager.Prop.AllowedFastFlags);

            if (!base64Flags.Contains(name))
            {
                var result = await Frontend.ShowMessageBox(
                    $"'{name}' is not in the Roblox allowlist and won't work.\n\nRemove it now?",
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    App.FastFlags.SetValue(name, null);

                    if (entry != null)
                        _fastFlagList.Remove(entry);
                    else
                        ReloadList();

                    UpdateTotalFlagsCount();
                    return;
                }
            }

            if (entry != null)
            {
                DataGrid.SelectedItem = entry;
                DataGrid.ScrollIntoView(entry, null);
            }

            UpdateTotalFlagsCount();
        }

        private async Task ImportJSON(string json)
        {
            Dictionary<string, object>? list = null;
            json = json.Trim();

            if (!json.StartsWith('{')) json = '{' + json;
            if (!json.EndsWith('}'))
            {
                int lastIndex = json.LastIndexOf('}');
                json = lastIndex == -1 ? json + '}' : json.Substring(0, lastIndex + 1);
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                list = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
                if (list is null) throw new Exception("JSON returned null");
            }
            catch (Exception ex)
            {
                _ = Frontend.ShowMessageBox(
                    string.Format(Strings.Menu_FastFlagEditor_InvalidJSON, ex.Message),
                    MessageBoxImage.Error,
                    MessageBoxButton.OK
                );
                ShowAddDialog();
                return;
            }

            App.FastFlags.suspendUndoSnapshot = true;
            App.FastFlags.SaveUndoSnapshot();

            var conflictingFlags = App.FastFlags.Prop.Where(x => list.ContainsKey(x.Key)).Select(x => x.Key).ToList();
            bool overwriteConflicting = false;

            if (conflictingFlags.Any())
            {
                string message = string.Format(
                    Strings.Menu_FastFlagEditor_ConflictingImport,
                    conflictingFlags.Count,
                    string.Join(", ", conflictingFlags.Take(25))
                );

                if (conflictingFlags.Count > 25) message += "...";

                var result = await Frontend.ShowMessageBox(message, MessageBoxImage.Question, MessageBoxButton.YesNo);
                overwriteConflicting = result == MessageBoxResult.Yes;
            }

            foreach (var pair in list)
            {
                if (App.FastFlags.Prop.ContainsKey(pair.Key) && !overwriteConflicting)
                    continue;

                string? val = pair.Value?.ToString();
                if (val != null)
                    App.FastFlags.SetValue(pair.Key, val);
            }

            App.FastFlags.suspendUndoSnapshot = false;

            var remoteManager = new RemoteDataManager();
            await remoteManager.LoadData();
            var base64Flags = DecodeBase64Flags(remoteManager.Prop.AllowedFastFlags);

            var invalidFlags = list.Keys.Where(flag => !base64Flags.Contains(flag)).ToList();
            if (invalidFlags.Any())
            {
                var result = await Frontend.ShowMessageBox(
                    $"{invalidFlags.Count} imported flags are not in the allowlist and won't work.\n\nRemove them now?",
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var flagName in invalidFlags)
                        App.FastFlags.SetValue(flagName, null);
                }
            }

            ClearSearch();
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
            ReloadList();
        }

        private async void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(_fastFlagList.Any() || App.FastFlags.Prop.Any()))
            {
                await Frontend.ShowMessageBox(
                    "There are no flags to delete.",
                    MessageBoxImage.Information);
                return;
            }

            if (await Frontend.ShowMessageBox("Are you sure you want to delete all flags?",MessageBoxImage.Warning,
                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
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
                await Frontend.ShowMessageBox($"An error occurred:\n{ex.Message}", MessageBoxImage.Error);
            }
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
                await Frontend.ShowMessageBox($"An error occurred:\n{ex.Message}", MessageBoxImage.Error);
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

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "ESave JSON or TXT File",
                    SuggestedFileName = "FroststrapExport.json",
                    DefaultExtension = "json",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                        new FilePickerFileType("TXT Files") { Patterns = new[] { "*.txt" } }
                    }
                });

                if (file is not null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    using var writer = new StreamWriter(stream);
                    await writer.WriteAsync(json);

                    await Frontend.ShowMessageBox(
                        "FastFlags exported successfully.",
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox($"An error occurred while saving:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        private async void FlagProfiles_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not Window parentWindow) return;

            App.FrostRPC?.SetDialog("Profiles");

            var dialog = new FlagProfilesDialog();

            await dialog.ShowDialog(parentWindow);

            App.FrostRPC?.ClearDialog();

            if (dialog.Result != MessageBoxResult.OK)
                return;

            if (dialog.Tabs.SelectedIndex == 0)
            {
                string profileName = dialog.SaveProfile.Text?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(profileName))
                    return;

                App.FastFlags.SaveProfile(profileName);
            }
            else if (dialog.Tabs.SelectedIndex == 1)
            {
                var selectedValue = dialog.LoadProfile.SelectedValue?.ToString();
                if (string.IsNullOrEmpty(selectedValue))
                    return;

                App.FastFlags.LoadProfile(selectedValue, dialog.ClearFlags.IsChecked ?? false);
            }

            await Task.Delay(1000);
            ReloadList();
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
            if (sender is not ToggleButton tb)
                return;

            _showPresets = tb.IsChecked ?? true;
            ReloadList();
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.DataContext is not FastFlag entry || e.EditingElement is not TextBox textbox)
                return;

            App.FastFlags.suspendUndoSnapshot = true;
            App.FastFlags.SaveUndoSnapshot();

            string newText = textbox.Text?.Trim() ?? string.Empty;
            string header = e.Column.Header?.ToString() ?? string.Empty;

            if (header == Strings.Common_Name)
            {
                string oldName = entry.Name;

                if (newText == oldName || string.IsNullOrEmpty(newText))
                    goto EndEditing;

                if (App.FastFlags.GetValue(newText) is not null)
                {
                    _ = Frontend.ShowMessageBox(Strings.Menu_FastFlagEditor_AlreadyExists, MessageBoxImage.Information);
                    e.Cancel = true;
                    textbox.Text = oldName;
                    goto EndEditing;
                }

                App.FastFlags.SetValue(oldName, null);
                App.FastFlags.SetValue(newText, entry.Value);

                entry.Name = newText;

                if (!newText.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    ClearSearch();

                var presetFlags = FastFlagManager.PresetFlags.Values;
                entry.Preset = presetFlags.Contains(newText) ? FluentIcons.Common.Symbol.CheckmarkCircle : FluentIcons.Common.Symbol.CircleOff;
            }
            else if (header == Strings.Common_Value)
            {
                string newValue = newText;
                entry.Value = newValue;
                App.FastFlags.SetValue(entry.Name, newValue);
            }

        EndEditing:
            App.FastFlags.suspendUndoSnapshot = false;
        }

        private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_dataGrid is null || DeleteSelectedButton is null) return;

            DeleteSelectedButton.IsEnabled = _dataGrid.SelectedItems.Count > 0;
        }

        private void ClearSearch(bool refresh = true)
        {
            SearchTextBox.Text = "";
            _searchFilter = "";

            if (refresh)
                ReloadList();
        }

        private async void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            string newSearch = SearchTextBox.Text?.Trim() ?? "";

            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();

            try
            {
                await Task.Delay(70, _searchCancellationTokenSource.Token);

                _searchFilter = newSearch;
                ReloadList();
                ShowSearchSuggestion(newSearch);
            }
            catch (TaskCanceledException) { }
        }

        private void SuggestionTextBlock_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var suggestion = SuggestionKeywordRun.Text;
            if (!string.IsNullOrEmpty(suggestion))
            {
                SearchTextBox.Text = suggestion;
                SearchTextBox.CaretIndex = suggestion.Length;
            }
        }

        private void ShowSearchSuggestion(string searchFilter)
        {
            if (string.IsNullOrWhiteSpace(searchFilter))
            {
                return;
            }

            var bestMatch = App.FastFlags.Prop.Keys
                .Where(flag => flag.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(flag => !flag.StartsWith(searchFilter, StringComparison.OrdinalIgnoreCase))
                .ThenBy(flag => flag.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase))
                .ThenBy(flag => flag.Length)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(bestMatch))
            {
                SuggestionKeywordRun.Text = bestMatch;
            }
        }
    }
}
