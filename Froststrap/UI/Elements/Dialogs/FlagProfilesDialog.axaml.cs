using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Reflection;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class FlagProfilesDialog : Base.AvaloniaWindow
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;

        public FlagProfilesDialog()
        {
            InitializeComponent();

            UpdateVisibility();
            UpdateOkButton();

            Tabs.SelectionChanged += (s, e) =>
            {
                UpdateVisibility();
                UpdateOkButton();
            };

            SaveProfile.TextChanged += (s, e) => UpdateOkButton();
            LoadProfile.SelectionChanged += LoadProfile_SelectionChanged;
            LoadPresetProfile.SelectionChanged += (s, e) => UpdateOkButton();

            LoadProfiles();
            LoadPresetProfiles();
        }

        private void UpdateVisibility()
        {
            bool isTab1 = Tabs.SelectedIndex == 1;
            ClearFlags.IsVisible = isTab1;
            DeleteButton.IsVisible = isTab1;

            RenamePanel.IsVisible = isTab1 && LoadProfile.SelectedItem != null;
        }

        private void UpdateOkButton()
        {
            if (OkButton == null) return;

            OkButton.IsEnabled = Tabs.SelectedIndex switch
            {
                0 => !string.IsNullOrEmpty(SaveProfile.Text),
                1 => LoadProfile.SelectedItem != null,
                2 => LoadPresetProfile.SelectedItem != null,
                _ => true
            };
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

            UpdateVisibility();
            UpdateOkButton();
        }

        private void LoadProfile_SelectionChanged(object? sender, SelectionChangedEventArgs e)
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

            UpdateVisibility();
            UpdateOkButton();
        }

        private async void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadProfile.SelectedItem is not string oldProfileName)
            {
                Frontend.ShowMessageBox("Please select a profile to rename.", MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            string newName = (RenameTextBox.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(newName))
            {
                Frontend.ShowMessageBox("New profile name cannot be empty.", MessageBoxImage.Error, MessageBoxButton.OK);
                return;
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                if (newName.Contains(c))
                {
                    Frontend.ShowMessageBox($"Profile name contains invalid character '{c}'.", MessageBoxImage.Error, MessageBoxButton.OK);
                    return;
                }
            }

            string profilesDirectory = Path.Combine(Paths.Base, Paths.SavedFlagProfiles);
            string oldPath = Path.Combine(profilesDirectory, oldProfileName);
            string newPath = Path.Combine(profilesDirectory, newName);

            if (File.Exists(newPath))
            {
                Frontend.ShowMessageBox("A profile with that name already exists.", MessageBoxImage.Error, MessageBoxButton.OK);
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
                Frontend.ShowMessageBox($"Failed to rename profile:\n{ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
            }
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadProfile.SelectedItem is not string selectedProfile)
            {
                Frontend.ShowMessageBox("Please select a profile to copy.", MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            string profilesDirectory = Path.Combine(Paths.Base, Paths.SavedFlagProfiles);
            string profilePath = Path.Combine(profilesDirectory, selectedProfile);

            if (!File.Exists(profilePath))
            {
                Frontend.ShowMessageBox("Selected profile file not found.", MessageBoxImage.Error, MessageBoxButton.OK);
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(profilePath);
                var flags = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText);

                if (flags == null)
                {
                    Frontend.ShowMessageBox("Failed to parse the selected profile.", MessageBoxImage.Error, MessageBoxButton.OK);
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

                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(formattedJson.ToString());
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to copy profile:\n{ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadProfile.SelectedItem is not string selectedProfile)
            {
                Frontend.ShowMessageBox("Please select a profile to update.", MessageBoxImage.Warning, MessageBoxButton.OK);
                return;
            }

            try
            {
                var currentFlags = App.FastFlags?.Prop;

                if (currentFlags == null)
                {
                    Frontend.ShowMessageBox("Failed to get current FastFlags.", MessageBoxImage.Error, MessageBoxButton.OK);
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
                Frontend.ShowMessageBox($"Failed to update profile:\n{ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
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

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedIndex == 0 && string.IsNullOrEmpty(SaveProfile.Text))
            {
                Frontend.ShowMessageBox("Profile name cannot be empty", MessageBoxImage.Information, MessageBoxButton.OK);
                return;
            }

            if (Tabs.SelectedIndex == 1 && LoadProfile.SelectedItem == null)
            {
                Frontend.ShowMessageBox("Please select a profile to load", MessageBoxImage.Information, MessageBoxButton.OK);
                return;
            }

            if (Tabs.SelectedIndex == 2 && LoadPresetProfile.SelectedItem == null)
            {
                Frontend.ShowMessageBox("Please select a preset profile", MessageBoxImage.Information, MessageBoxButton.OK);
                return;
            }

            switch (Tabs.SelectedIndex)
            {
                case 0: // Save tab
                    if (!string.IsNullOrWhiteSpace(SaveProfile.Text))
                    {
                        App.FastFlags?.SaveProfile(SaveProfile.Text);
                    }
                    break;
                case 1: // Load tab
                    if (LoadProfile.SelectedItem is string selectedProfile)
                    {
                        App.FastFlags?.LoadProfile(selectedProfile, clearFlags: ClearFlags.IsChecked == true);
                    }
                    break;

                case 2: // Preset Flags tab
                    if (LoadPresetProfile.SelectedItem is string selectedPreset)
                    {
                        App.FastFlags?.LoadPresetProfile(selectedPreset, clearFlags: true);
                    }
                    break;
            }

            Result = MessageBoxResult.OK;
            Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            string? ProfileName = LoadProfile.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(ProfileName))
                return;

            App.FastFlags?.DeleteProfile(ProfileName);
            LoadProfiles();
        }
    }
}