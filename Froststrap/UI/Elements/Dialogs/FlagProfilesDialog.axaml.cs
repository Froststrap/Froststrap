using Avalonia.Controls;
using Avalonia.Interactivity;
using DiscordRPC;
using Froststrap.UI.Elements.Base;
using System.Reflection;

namespace Froststrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for FlagProfilesDialog.xaml
    /// </summary>
    public partial class FlagProfilesDialog : AvaloniaWindow
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;

        public FlagProfilesDialog()
        {
            InitializeComponent();
            LoadProfiles();
            LoadPresetProfiles();

            Tabs.SelectionChanged += (s, e) => UpdateUiState();
            SaveProfile.TextChanged += (s, e) => UpdateUiState();

            LoadProfile.SelectionChanged += (s, e) => UpdateUiState();
            LoadPresetProfile.SelectionChanged += (s, e) => UpdateUiState();
        }

        private void LoadProfiles()
        {
            LoadProfile.Items.Clear();

            string profilesDirectory = Path.Combine(Paths.Base, Paths.SavedFlagProfiles);

            if (!Directory.Exists(profilesDirectory))
                Directory.CreateDirectory(profilesDirectory);

            string[] Profiles = Directory.GetFiles(profilesDirectory);

            foreach (string rawProfileName in Profiles)
            {
                string ProfileName = Path.GetFileName(rawProfileName);
                LoadProfile.Items.Add(ProfileName);
            }

            LoadProfileEmptyText.IsVisible = LoadProfile.Items.Count == 0;

            RenamePanel.IsVisible = LoadProfile.Items.Count == 0;

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

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadProfile.SelectedItem is not string oldProfileName)
            {
                _ = Frontend.ShowMessageBox("Please select a profile to rename.", MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            string newName = RenameTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(newName))
            {
                _ = Frontend.ShowMessageBox("New profile name cannot be empty.", MessageBoxImage.Error, MessageBoxButton.OK);
                return;
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                if (newName.Contains(c))
                {
                    _ = Frontend.ShowMessageBox($"Profile name contains invalid character '{c}'.", MessageBoxImage.Error, MessageBoxButton.OK);
                    return;
                }
            }

            string profilesDirectory = Path.Combine(Paths.Base, Paths.SavedFlagProfiles);
            string oldPath = Path.Combine(profilesDirectory, oldProfileName);
            string newPath = Path.Combine(profilesDirectory, newName);

            if (File.Exists(newPath))
            {
                _ = Frontend.ShowMessageBox("A profile with that name already exists.", MessageBoxImage.Error, MessageBoxButton.OK);
                return;
            }

            try
            {
                File.Move(oldPath, newPath);
                LoadProfiles();
                LoadProfile.SelectedItem = newName;
            }
            catch (Exception ex)
            {
                _ = Frontend.ShowMessageBox($"Failed to rename profile:\n{ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadProfile.SelectedItem is not string selectedProfile)
            {
                _ = Frontend.ShowMessageBox("Please select a profile to copy.", MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            string profilesDirectory = Path.Combine(Paths.Base, Paths.SavedFlagProfiles);
            string profilePath = Path.Combine(profilesDirectory, selectedProfile);

            if (!File.Exists(profilePath))
            {
                _ = Frontend.ShowMessageBox("Selected profile file not found.", MessageBoxImage.Error, MessageBoxButton.OK);
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(profilePath);
                var flags = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText);

                if (flags == null)
                {
                    _ = Frontend.ShowMessageBox("Failed to parse the selected profile.", MessageBoxImage.Error, MessageBoxButton.OK);
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
                _ = Frontend.ShowMessageBox($"Failed to copy profile:\n{ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
            }
        }


        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadProfile.SelectedItem is not string selectedProfile)
            {
                _ = Frontend.ShowMessageBox("Please select a profile to update.", MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            try
            {
                var currentFlags = App.FastFlags.Prop;

                if (currentFlags == null)
                {
                    _ = Frontend.ShowMessageBox("Failed to get current FastFlags.", MessageBoxImage.Error, MessageBoxButton.OK);
                    return;
                }

                string json = JsonSerializer.Serialize(currentFlags, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                string profilesDirectory = Path.Combine(Paths.Base, Paths.SavedFlagProfiles);
                string profilePath = Path.Combine(profilesDirectory, selectedProfile);

                File.WriteAllText(profilePath, json);

                LoadProfiles();
                LoadProfile.SelectedItem = selectedProfile;
            }
            catch (Exception ex)
            {
                _ = Frontend.ShowMessageBox($"Failed to update profile:\n{ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
            }
        }

        private void LoadPresetProfiles()
        {
            LoadPresetProfile.Items.Clear();

            var assembly = Assembly.GetExecutingAssembly();
            string resourcePrefix = "Froststrap.Resources.PresetFlags.";

            var resourceNames = assembly.GetManifestResourceNames();

            var profiles = resourceNames.Where(r => r.StartsWith(resourcePrefix));

            foreach (var resourceName in profiles)
            {
                string profileName = resourceName.Substring(resourcePrefix.Length);
                LoadPresetProfile.Items.Add(profileName);
            }
        } 

        private void UpdateUiState()
        {
            if (Tabs == null || OkButton == null || DeleteButton == null) return;

            int index = Tabs.SelectedIndex;

            bool isLoadTab = (index == 1);
            ClearFlags.IsVisible = isLoadTab;
            DeleteButton.IsVisible = isLoadTab;

            if (index == 0)
            {
                OkButton.IsEnabled = !string.IsNullOrWhiteSpace(SaveProfile.Text);
            }
            else if (index == 1)
            {
                bool hasSelection = LoadProfile.SelectedItem != null;
                OkButton.IsEnabled = hasSelection;
                DeleteButton.IsEnabled = hasSelection;
            }
            else if (index == 2)
            {
                OkButton.IsEnabled = LoadPresetProfile.SelectedItem != null;
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Tabs.SelectedIndex)
            {
                case 0: // Save tab
                    if (!string.IsNullOrWhiteSpace(SaveProfile.Text))
                    {
                        App.FastFlags.SaveProfile(SaveProfile.Text);
                    }
                    break;
                case 1: // Load tab
                    if (LoadProfile.SelectedItem is string selectedProfile)
                    {
                        App.FastFlags.LoadProfile(selectedProfile, clearFlags: ClearFlags.IsChecked == true);
                    }
                    break;

                case 2: // Preset Flags tab
                    if (LoadPresetProfile.SelectedItem is string selectedPreset)
                    {
                        App.FastFlags.LoadPresetProfile(selectedPreset, clearFlags: true);
                    }
                    break;
            }

            this.Result = MessageBoxResult.OK;
            this.Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            string? ProfileName = LoadProfile.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(ProfileName))
                return;

            App.FastFlags.DeleteProfile(ProfileName);
            LoadProfiles();
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            this.Result = MessageBoxResult.Cancel;
            this.Close();
        }
    }
}