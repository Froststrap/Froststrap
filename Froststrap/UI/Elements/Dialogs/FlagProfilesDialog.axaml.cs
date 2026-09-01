using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Froststrap.UI.Elements.Base;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Froststrap.UI.Elements.Dialogs
{
    internal partial class FlagProfilesDialog : AvaloniaWindow
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;

        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private readonly ObservableCollection<string> _placeIds = [];
        private string? _currentProfile;
        private bool _isUpdatingPlaceIds;
        private bool _isRemoving;

        public FlagProfilesDialog()
        {
            InitializeComponent();

            PlaceIdsListBox.ItemsSource = _placeIds;

            LoadProfiles();
            LoadPresetProfiles();

            Tabs.SelectedIndex = 2;

            Tabs.SelectionChanged += (s, e) => OnTabSelectionChanged();
            SaveProfile.TextChanged += (s, e) => UpdateUiState();

            LoadProfile.SelectionChanged += (s, e) => UpdateUiState();
            LoadPresetProfile.SelectionChanged += (s, e) => UpdateUiState();
            PlaceProfile.SelectionChanged += (s, e) => UpdatePlaceIdsUiState();
        }

        private void OnTabSelectionChanged()
        {
            if (Tabs.SelectedIndex == 1 && PlaceProfile.Items.Count > 0 && PlaceProfile.SelectedItem == null)
            {
                PlaceProfile.SelectedItem = PlaceProfile.Items[0];
            }
            UpdateUiState();
        }

        private void LoadProfiles()
        {
            LoadProfile.Items.Clear();
            PlaceProfile.Items.Clear();

            string profilesDirectory = Paths.SavedFlagProfiles;

            if (!Directory.Exists(profilesDirectory))
                Directory.CreateDirectory(profilesDirectory);

            string[] Profiles = Directory.GetFiles(profilesDirectory);

            foreach (string rawProfileName in Profiles)
            {
                string ProfileName = Path.GetFileName(rawProfileName);
                LoadProfile.Items.Add(ProfileName);
                PlaceProfile.Items.Add(ProfileName);
            }

            LoadProfileEmptyText.IsVisible = LoadProfile.Items.Count == 0;

            RenamePanel.IsVisible = LoadProfile.Items.Count > 0;

            RenameTextBox.Text = string.Empty;
            RenameTextBox.IsEnabled = false;
        }

        private void LoadProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LoadProfile.SelectedItem is string selectedProfile)
            {
                RenameTextBox.Text = selectedProfile;
                RenameTextBox.IsEnabled = true;
            }
            else
            {
                RenameTextBox.Text = string.Empty;
                RenameTextBox.IsEnabled = false;
            }

            UpdateUiState();
        }

        private void LoadPresetProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUiState();
        }

        private void PlaceProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePlaceIdsUiState();
        }

        private void UpdatePlaceIdsUiState()
        {
            if (_isUpdatingPlaceIds) return;
            _isUpdatingPlaceIds = true;

            try
            {
                string? selectedProfile = PlaceProfile.SelectedItem as string;
                _currentProfile = selectedProfile;

                bool hasProfiles = PlaceProfile.Items.Count > 0;
                bool hasProfileSelected = selectedProfile != null;

                PlaceProfileGrid.IsVisible = hasProfiles;
                PlaceIdsContent.IsVisible = hasProfileSelected;
                PlaceProfileEmptyMessage.IsVisible = !hasProfiles || !hasProfileSelected;

                if (!hasProfiles)
                    PlaceProfileEmptyMessage.Text = Strings.Menu_FlagProfiles_NoProfilesFound;
                else if (!hasProfileSelected)
                    PlaceProfileEmptyMessage.Text = Strings.Menu_FlagProfiles_SelectProfileFirst;

                PlaceIdsListBox.SelectionChanged -= PlaceIdsListBox_SelectionChanged;

                try
                {
                    if (!_isRemoving)
                    {
                        _placeIds.Clear();

                        if (hasProfileSelected && selectedProfile != null)
                        {
                            if (App.Settings.Prop.ProfilePlaceIds.TryGetValue(selectedProfile, out var placeIds))
                            {
                                foreach (var id in placeIds)
                                    _placeIds.Add(id);
                            }
                        }
                    }
                }
                finally
                {
                    PlaceIdsListBox.SelectionChanged += PlaceIdsListBox_SelectionChanged;
                }

                AddPlaceButton.IsEnabled = hasProfileSelected;
                RemovePlaceButton.IsEnabled = false;

                PlaceIdInfoText.IsVisible = false;
            }
            finally
            {
                _isUpdatingPlaceIds = false;
            }
        }

        private void AddPlaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlaceProfile.SelectedItem is not string selectedProfile)
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_SelectProfileFirst, MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            string placeId = NewPlaceIdTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(placeId))
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_EnterPlaceId, MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            if (!Regex.IsMatch(placeId, @"^\d+$"))
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_PlaceIdNumeric, MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            if (_placeIds.Contains(placeId))
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_PlaceIdExists, MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            _placeIds.Add(placeId);
            NewPlaceIdTextBox.Text = string.Empty;
            SavePlaceIds(selectedProfile);
        }

        private void RemovePlaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlaceIdsListBox.SelectedItem is not string selectedPlaceId)
                return;

            if (PlaceProfile.SelectedItem is not string selectedProfile)
                return;

            _isRemoving = true;
            try
            {
                _placeIds.Remove(selectedPlaceId);
                SavePlaceIds(selectedProfile);
            }
            finally
            {
                _isRemoving = false;
            }
        }

        private void SavePlaceIds(string profileName)
        {
            if (_placeIds.Count > 0)
                App.Settings.Prop.ProfilePlaceIds[profileName] = [.. _placeIds];
            else
                App.Settings.Prop.ProfilePlaceIds.Remove(profileName);

            App.Settings.SaveSetting("ProfilePlaceIds");
        }

        private void PlaceIdsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                RemovePlaceButton?.IsEnabled = PlaceIdsListBox?.SelectedItem != null;
            }
            catch
            {
                // Swallow
            }
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadProfile.SelectedItem is not string oldProfileName)
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_SelectProfileRename, MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            string newName = RenameTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(newName))
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_NameCannotBeEmpty, MessageBoxImage.Error, MessageBoxButton.OK);
                return;
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                if (newName.Contains(c, StringComparison.Ordinal))
                {
                    _ = Frontend.ShowMessageBox(string.Format(CultureInfo.InvariantCulture, Strings.Menu_FlagProfiles_InvalidCharacter, c), MessageBoxImage.Error, MessageBoxButton.OK);
                    return;
                }
            }

            string profilesDirectory = Paths.SavedFlagProfiles;
            string oldPath = Path.Combine(profilesDirectory, oldProfileName);
            string newPath = Path.Combine(profilesDirectory, newName);

            if (File.Exists(newPath))
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_ProfileExists, MessageBoxImage.Error, MessageBoxButton.OK);
                return;
            }

            try
            {
                File.Move(oldPath, newPath);

                if (App.Settings.Prop.ProfilePlaceIds.TryGetValue(oldProfileName, out var placeIds))
                {
                    App.Settings.Prop.ProfilePlaceIds.Remove(oldProfileName);
                    App.Settings.Prop.ProfilePlaceIds[newName] = placeIds;
                    App.Settings.SaveSetting("ProfilePlaceIds");
                }

                LoadProfiles();
                LoadProfile.SelectedItem = newName;
                PlaceProfile.SelectedItem = newName;
            }
            catch (Exception ex)
            {
                _ = Frontend.ShowMessageBox(string.Format(CultureInfo.InvariantCulture, Strings.Menu_FlagProfiles_RenameFailed, ex.Message), MessageBoxImage.Error, MessageBoxButton.OK);
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadProfile.SelectedItem is not string selectedProfile)
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_SelectProfileCopy, MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            string profilesDirectory = Paths.SavedFlagProfiles;
            string profilePath = Path.Combine(profilesDirectory, selectedProfile);

            if (!File.Exists(profilePath))
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_ProfileNotFound, MessageBoxImage.Error, MessageBoxButton.OK);
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(profilePath);
                var flags = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText);

                if (flags == null)
                {
                    _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_ParseFailed, MessageBoxImage.Error, MessageBoxButton.OK);
                    return;
                }

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

                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    _ = clipboard.SetTextAsync(formattedJson.ToString());
                }
            }
            catch (Exception ex)
            {
                _ = Frontend.ShowMessageBox(string.Format(CultureInfo.InvariantCulture, Strings.Menu_FlagProfiles_CopyFailed, ex.Message), MessageBoxImage.Error, MessageBoxButton.OK);
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadProfile.SelectedItem is not string selectedProfile)
            {
                _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_SelectProfileUpdate, MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            try
            {
                var currentFlags = App.FastFlags.Prop;
                if (currentFlags == null)
                {
                    _ = Frontend.ShowMessageBox(Strings.Menu_FlagProfiles_GetFlagsFailed, MessageBoxImage.Error, MessageBoxButton.OK);
                    return;
                }

                string json = JsonSerializer.Serialize(currentFlags, _jsonOptions);

                string profilesDirectory = Path.Combine(Paths.Base, Paths.SavedFlagProfiles);
                string profilePath = Path.Combine(profilesDirectory, selectedProfile);

                File.WriteAllText(profilePath, json);

                LoadProfiles();
                LoadProfile.SelectedItem = selectedProfile;
            }
            catch (Exception ex)
            {
                _ = Frontend.ShowMessageBox(string.Format(CultureInfo.InvariantCulture, Strings.Menu_FlagProfiles_UpdateFailed, ex.Message), MessageBoxImage.Error, MessageBoxButton.OK);
            }
        }

        private void LoadPresetProfiles()
        {
            LoadPresetProfile.Items.Clear();

            var assembly = Assembly.GetExecutingAssembly();
            string resourcePrefix = "Froststrap.Resources.PresetFlags.";
            var resourceNames = assembly.GetManifestResourceNames();
            var profiles = resourceNames.Where(r => r.StartsWith(resourcePrefix, StringComparison.Ordinal));

            foreach (var resourceName in profiles)
            {
                string profileName = resourceName[resourcePrefix.Length..];
                LoadPresetProfile.Items.Add(profileName);
            }
        }

        private void UpdateUiState()
        {
            if (Tabs == null || OkButton == null || DeleteButton == null) return;

            int index = Tabs.SelectedIndex;

            ClearFlags.IsVisible = false;
            DeleteButton.IsVisible = false;
            RemovePlaceButton.IsVisible = false;

            switch (index)
            {
                case 0:
                    OkButton.IsEnabled = LoadPresetProfile.SelectedItem != null;
                    break;

                case 1:
                    OkButton.IsEnabled = true;
                    RemovePlaceButton.IsVisible = true;
                    UpdatePlaceIdsUiState();
                    RemovePlaceButton.IsEnabled = PlaceIdsListBox?.SelectedItem != null;
                    break;

                case 2:
                    OkButton.IsEnabled = !string.IsNullOrWhiteSpace(SaveProfile.Text);
                    break;

                case 3:
                    bool hasSelection = LoadProfile.SelectedItem != null;
                    OkButton.IsEnabled = hasSelection;
                    DeleteButton.IsEnabled = hasSelection;
                    ClearFlags.IsVisible = true;
                    DeleteButton.IsVisible = true;
                    break;
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Tabs.SelectedIndex)
            {
                case 0:
                    if (LoadPresetProfile.SelectedItem is string selectedPreset)
                        App.FastFlags.LoadPresetProfile(selectedPreset, clearFlags: true);
                    break;
                case 1:
                    break;
                case 2:
                    if (!string.IsNullOrWhiteSpace(SaveProfile.Text))
                        App.FastFlags.SaveProfile(SaveProfile.Text);
                    break;
                case 3:
                    if (LoadProfile.SelectedItem is string selectedProfile)
                        App.FastFlags.LoadProfile(selectedProfile, clearFlags: ClearFlags.IsChecked == true);
                    break;
            }

            this.Result = MessageBoxResult.OK;
            this.Close();
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            string? ProfileName = LoadProfile.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(ProfileName))
                return;

            App.Settings.Prop.ProfilePlaceIds.Remove(ProfileName);
            App.Settings.SaveSetting("ProfilePlaceIds");

            await FastFlagManager.DeleteProfile(ProfileName);
            LoadProfiles();
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            this.Result = MessageBoxResult.Cancel;
            this.Close();
        }
    }
}