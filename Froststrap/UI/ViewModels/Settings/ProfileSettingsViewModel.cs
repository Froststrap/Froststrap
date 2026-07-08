using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels.Settings
{
    public class ProfileEntry : ObservableObject
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public DateTime LastModified { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }

    public class ProfileSettingsViewModel : ObservableObject
    {
        private static readonly string ProfilesDir = Path.Combine(Paths.Base, "ProfileSettings");
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private static readonly FilePickerFileType _jsonFileType = new("JSON")
        {
            Patterns = ["*.json"],
            MimeTypes = ["application/json"]
        };

        public ObservableCollection<ProfileEntry> Profiles { get; } = [];

        private ProfileEntry? _selectedProfile;
        public ProfileEntry? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value))
                {
                    NewProfileName = value?.Name ?? string.Empty;
                    ExportCommand?.NotifyCanExecuteChanged();
                    UpdateCommand?.NotifyCanExecuteChanged();
                    DeleteCommand?.NotifyCanExecuteChanged();
                    RenameCommand?.NotifyCanExecuteChanged();
                    UseCommand?.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(HasSelectedProfile));
                }
            }
        }

        private string _newProfileName = string.Empty;
        public string NewProfileName
        {
            get => _newProfileName;
            set => SetProperty(ref _newProfileName, value);
        }

        public bool HasSelectedProfile => SelectedProfile != null;
        public bool CanExport => SelectedProfile != null;
        public bool CanUpdate => SelectedProfile != null;
        public bool CanDelete => SelectedProfile != null;
        public bool CanRename => SelectedProfile != null && !string.IsNullOrWhiteSpace(NewProfileName);
        public bool CanUse => SelectedProfile != null;

        public IAsyncRelayCommand SaveCommand { get; }
        public IAsyncRelayCommand<Control> ImportCommand { get; }
        public IAsyncRelayCommand<Control> ExportCommand { get; }
        public IAsyncRelayCommand RenameCommand { get; }
        public IAsyncRelayCommand UseCommand { get; }
        public IAsyncRelayCommand UpdateCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }

        public ProfileSettingsViewModel()
        {
            Directory.CreateDirectory(ProfilesDir);
            LoadProfiles();

            SaveCommand = new AsyncRelayCommand(SaveProfile);
            ImportCommand = new AsyncRelayCommand<Control>(ImportProfile, _ => true);
            ExportCommand = new AsyncRelayCommand<Control>(ExportProfile, _ => CanExport);
            RenameCommand = new AsyncRelayCommand(RenameProfile, () => CanRename);
            UseCommand = new AsyncRelayCommand(UseProfile, () => CanUse);
            UpdateCommand = new AsyncRelayCommand(UpdateProfile, () => CanUpdate);
            DeleteCommand = new AsyncRelayCommand(DeleteProfile, () => CanDelete);
        }

        private void LoadProfiles()
        {
            Profiles.Clear();
            foreach (var file in Directory.GetFiles(ProfilesDir, "*.json"))
            {
                try
                {
                    var content = File.ReadAllText(file);
                    _ = JsonSerializer.Deserialize<FullSettingsExport>(content, _jsonOptions);
                    var name = Path.GetFileNameWithoutExtension(file);
                    var lastModified = File.GetLastWriteTimeUtc(file);
                    Profiles.Add(new ProfileEntry
                    {
                        Name = name,
                        LastModified = lastModified,
                        FilePath = file
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("ProfileSettingsViewModel::LoadProfiles", $"Failed to load {file}: {ex.Message}");
                }
            }
        }

        private static void ExportSettings(string filePath)
        {
            var export = new FullSettingsExport
            {
                Settings = App.Settings.Prop,
                State = App.State.Prop,
                PlayerState = App.PlayerState.Prop,
                StudioState = App.StudioState.Prop,
                AppStorage = App.AppStorage.Prop,
                FastFlags = App.FastFlags.Prop,
                GlobalBasicSettingsXml = App.GlobalSettings.Document?.ToString(),
                SoberSettings = OperatingSystem.IsLinux() ? App.SoberSettings.Prop : null,
                Version = App.Version,
                ExportDate = DateTime.UtcNow
            };
            File.WriteAllText(filePath, JsonSerializer.Serialize(export, _jsonOptions));
        }

        private static void ImportSettings(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Settings export file not found.", filePath);

            var json = File.ReadAllText(filePath);
            var export = JsonSerializer.Deserialize<FullSettingsExport>(json, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize settings export.");

            if (export.Settings is not null)
            {
                App.Settings.Prop = export.Settings;
                App.Settings.Save();
            }
            if (export.State is not null)
            {
                App.State.Prop = export.State;
                App.State.Save();
            }
            if (export.PlayerState is not null)
            {
                App.PlayerState.Prop = export.PlayerState;
                App.PlayerState.Save();
            }
            if (export.StudioState is not null)
            {
                App.StudioState.Prop = export.StudioState;
                App.StudioState.Save();
            }
            if (export.AppStorage is not null)
            {
                App.AppStorage.Prop = export.AppStorage;
                App.AppStorage.Save();
            }
            if (export.FastFlags is not null)
            {
                App.FastFlags.Prop = export.FastFlags;
                App.FastFlags.Save();
            }
            if (!string.IsNullOrEmpty(export.GlobalBasicSettingsXml))
            {
                var gbsPath = GBSEditor.FileLocation;
                File.WriteAllText(gbsPath, export.GlobalBasicSettingsXml);
                App.GlobalSettings.Load();
                App.GlobalSettings.Save();
            }
            if (OperatingSystem.IsLinux() && export.SoberSettings is not null)
            {
                App.SoberSettings.Prop = export.SoberSettings;
                App.SoberSettings.Save();
            }
        }

        private async Task SaveProfile()
        {
            var name = NewProfileName.Trim();
            if (string.IsNullOrEmpty(name))
            {
                await Frontend.ShowMessageBox(Strings.ProfileSettings_EnterProfileName, MessageBoxImage.Warning);
                return;
            }

            var fileName = SanitizeFileName(name) + ".json";
            var filePath = Path.Combine(ProfilesDir, fileName);

            if (File.Exists(filePath))
            {
                var overwrite = await Frontend.ShowMessageBox(string.Format(Strings.ProfileSettings_ProfileExists, name), MessageBoxImage.Question, MessageBoxButton.YesNo);
                if (overwrite != MessageBoxResult.Yes)
                    return;
            }

            ExportSettings(filePath);
            LoadProfiles();
            SelectedProfile = Profiles.FirstOrDefault(p => p.FilePath == filePath);
            NewProfileName = string.Empty;
        }

        private async Task ImportProfile(Control? control)
        {
            if (control == null)
            {
                await Frontend.ShowMessageBox(Strings.ProfileSettings_MainWindowNotFound, MessageBoxImage.Error);
                return;
            }

            var topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is not Window parentWindow)
            {
                await Frontend.ShowMessageBox(Strings.ProfileSettings_MainWindowNotFound, MessageBoxImage.Error);
                return;
            }

            var storageProvider = parentWindow.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select settings export to import",
                FileTypeFilter = [_jsonFileType],
                AllowMultiple = false
            });

            if (files.Count == 0) return;
            var file = files[0];
            var filePath = file.Path.AbsolutePath;

            try
            {
                ImportSettings(filePath);
                await Frontend.ShowMessageBox(Strings.ProfileSettings_ImportSuccess, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox(string.Format(Strings.ProfileSettings_ImportFailed, ex.Message), MessageBoxImage.Error);
                return;
            }

            var name = Path.GetFileNameWithoutExtension(filePath);
            var destName = name;
            int counter = 1;
            while (File.Exists(Path.Combine(ProfilesDir, SanitizeFileName(destName) + ".json")))
                destName = $"{name} ({counter++})";

            var destPath = Path.Combine(ProfilesDir, SanitizeFileName(destName) + ".json");
            File.Copy(filePath, destPath);
            LoadProfiles();
            SelectedProfile = Profiles.FirstOrDefault(p => p.FilePath == destPath);
        }

        private async Task ExportProfile(Control? control)
        {
            if (SelectedProfile == null) return;
            if (control == null)
            {
                await Frontend.ShowMessageBox(Strings.ProfileSettings_MainWindowNotFound, MessageBoxImage.Error);
                return;
            }

            var topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is not Window parentWindow)
            {
                await Frontend.ShowMessageBox(Strings.ProfileSettings_MainWindowNotFound, MessageBoxImage.Error);
                return;
            }

            var storageProvider = parentWindow.StorageProvider;

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export profile",
                DefaultExtension = "json",
                FileTypeChoices = [_jsonFileType],
                SuggestedFileName = SelectedProfile.Name + ".json"
            });

            if (file == null) return;
            File.Copy(SelectedProfile.FilePath, file.Path.AbsolutePath, true);
            await Frontend.ShowMessageBox(string.Format(Strings.ProfileSettings_ExportSuccess, file.Path.AbsolutePath), MessageBoxImage.Information);
        }

        private async Task RenameProfile()
        {
            if (SelectedProfile == null) return;
            var newName = NewProfileName.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                await Frontend.ShowMessageBox(Strings.ProfileSettings_EnterNewName, MessageBoxImage.Warning);
                return;
            }
            if (newName == SelectedProfile.Name)
                return;

            var newFileName = SanitizeFileName(newName) + ".json";
            var newPath = Path.Combine(ProfilesDir, newFileName);

            if (File.Exists(newPath) && newPath != SelectedProfile.FilePath)
            {
                var overwrite = await Frontend.ShowMessageBox(string.Format(Strings.ProfileSettings_ProfileExistsRename, newName), MessageBoxImage.Question, MessageBoxButton.YesNo);
                if (overwrite != MessageBoxResult.Yes)
                    return;
                File.Delete(newPath);
            }

            File.Move(SelectedProfile.FilePath, newPath);
            SelectedProfile.FilePath = newPath;
            SelectedProfile.Name = newName;
            SelectedProfile.LastModified = File.GetLastWriteTimeUtc(newPath);
            var index = Profiles.IndexOf(SelectedProfile);
            if (index >= 0)
                Profiles[index] = SelectedProfile;
            NewProfileName = newName;
        }

        private async Task UseProfile()
        {
            if (SelectedProfile == null) return;
            var confirm = await Frontend.ShowMessageBox(string.Format(Strings.ProfileSettings_ApplyProfile, SelectedProfile.Name), MessageBoxImage.Question, MessageBoxButton.YesNo);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                ImportSettings(SelectedProfile.FilePath);
                await Frontend.ShowMessageBox(Strings.ProfileSettings_ApplySuccess, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox(string.Format(Strings.ProfileSettings_ApplyFailed, ex.Message), MessageBoxImage.Error);
            }
        }

        private async Task UpdateProfile()
        {
            if (SelectedProfile == null) return;
            var confirm = await Frontend.ShowMessageBox(string.Format(Strings.ProfileSettings_UpdateProfile, SelectedProfile.Name), MessageBoxImage.Question, MessageBoxButton.YesNo);
            if (confirm != MessageBoxResult.Yes) return;

            ExportSettings(SelectedProfile.FilePath);
            SelectedProfile.LastModified = File.GetLastWriteTimeUtc(SelectedProfile.FilePath);
            var index = Profiles.IndexOf(SelectedProfile);
            if (index >= 0)
                Profiles[index] = SelectedProfile;
        }

        private async Task DeleteProfile()
        {
            if (SelectedProfile == null) return;
            var confirm = await Frontend.ShowMessageBox(string.Format(Strings.ProfileSettings_DeleteProfile, SelectedProfile.Name), MessageBoxImage.Question, MessageBoxButton.YesNo);
            if (confirm != MessageBoxResult.Yes) return;

            File.Delete(SelectedProfile.FilePath);
            Profiles.Remove(SelectedProfile);
            SelectedProfile = Profiles.FirstOrDefault();
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}