using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Installer
{
    public class InstallViewModel : NotifyPropertyChangedViewModel
    {
        private readonly Froststrap.Installer installer = new();

        private readonly string _originalInstallLocation;

        public event EventHandler<bool>? SetCanContinueEvent;

        public string InstallLocation
        {
            get => installer.InstallLocation;
            set
            {
                if (!string.IsNullOrEmpty(ErrorMessage))
                {
                    SetCanContinueEvent?.Invoke(this, true);

                    installer.InstallLocationError = "";
                    OnPropertyChanged(nameof(ErrorMessage));
                }

                installer.InstallLocation = value;
                OnPropertyChanged(nameof(InstallLocation));
                OnPropertyChanged(nameof(DataFoundMessageVisibility));
            }
        }

        private List<ImportSettingsFrom> _availableImportSources = new();
        public List<ImportSettingsFrom> AvailableImportSources
        {
            get => _availableImportSources;
            set
            {
                _availableImportSources = value;
                OnPropertyChanged(nameof(AvailableImportSources));
                OnPropertyChanged(nameof(ImportSettingsEnabled));
                OnPropertyChanged(nameof(ShowNotFound)); // Update this too
            }
        }

        public bool ImportSettingsEnabled => AvailableImportSources.Count > 1;

        public bool ShowNotFound => AvailableImportSources.Count <= 1;

        public bool DataFoundMessageVisibility => installer.ExistingDataPresent;

        public string ErrorMessage => installer.InstallLocationError;

        public bool CreateDesktopShortcuts
        {
            get => installer.CreateDesktopShortcuts;
            set
            {
                installer.CreateDesktopShortcuts = value;
                OnPropertyChanged(nameof(CreateDesktopShortcuts));
            }
        }

        public bool CreateStartMenuShortcuts
        {
            get => installer.CreateStartMenuShortcuts;
            set
            {
                installer.CreateStartMenuShortcuts = value;
                OnPropertyChanged(nameof(CreateStartMenuShortcuts));
            }
        }

        public ImportSettingsFrom SelectedImportSource
        {
            get => installer.ImportSource;
            set
            {
                installer.ImportSource = value;
                OnPropertyChanged(nameof(SelectedImportSource));
            }
        }

        public bool ImportSettings
        {
            get => installer.ImportSettings;
            set
            {
                installer.ImportSettings = value;
                OnPropertyChanged(nameof(ImportSettings));
                // Trigger validation update if disabling import
                if (!value)
                {
                    installer.InstallLocationError = "";
                    SetCanContinueEvent?.Invoke(this, true);
                    OnPropertyChanged(nameof(ErrorMessage));
                }
            }
        }

        public ICommand BrowseInstallLocationCommand => new AsyncRelayCommand<object>(BrowseInstallLocation);

        public ICommand ResetInstallLocationCommand => new RelayCommand(ResetInstallLocation);

        public ICommand OpenFolderCommand => new RelayCommand(OpenFolder);

        public InstallViewModel()
        {
            _originalInstallLocation = installer.InstallLocation;
            UpdateAvailableImportSources();

            OnPropertyChanged(nameof(SelectedImportSource));
        }

        private void UpdateAvailableImportSources()
        {
            var availableSources = new List<ImportSettingsFrom> { ImportSettingsFrom.None };

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (Directory.Exists(Path.Combine(localAppData, "Bloxstrap")))
                availableSources.Add(ImportSettingsFrom.Bloxstrap);

            if (Directory.Exists(Path.Combine(localAppData, "Fishstrap")))
                availableSources.Add(ImportSettingsFrom.Fishstrap);

            if (Directory.Exists(Path.Combine(localAppData, "Lunastrap")))
                availableSources.Add(ImportSettingsFrom.Lunastrap);

            if (Directory.Exists(Path.Combine(localAppData, "Luczystrap")))
                availableSources.Add(ImportSettingsFrom.Luczystrap);

            AvailableImportSources = availableSources;

            SelectedImportSource = ImportSettingsFrom.None;
        }

        public async Task<bool> DoInstall()
        {
            if (!await installer.CheckInstallLocation())
            {
                SetCanContinueEvent?.Invoke(this, false);
                OnPropertyChanged(nameof(ErrorMessage));
                return false;
            }

            await installer.DoInstall();
            return true;
        }

        private async Task BrowseInstallLocation(object? parameter)
        {
            var topLevel = TopLevel.GetTopLevel(parameter as Control);

            if (topLevel == null)
            {
                var lifetime = Avalonia.Application.Current?.ApplicationLifetime
                    as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                topLevel = lifetime?.MainWindow;
            }

            if (topLevel == null) return;

            // 2. Open the Folder Picker
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Installation Folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                // TryGetLocalPath is the modern way to get the string path
                if (folders[0].TryGetLocalPath() is { } localPath)
                {
                    InstallLocation = localPath;
                }
            }
        }

        private void ResetInstallLocation()
        {
            InstallLocation = _originalInstallLocation;
            OnPropertyChanged(nameof(InstallLocation));
        }

        private void OpenFolder()
        {
            string path = Paths.Base;

            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start(new ProcessStartInfo("xdg-open", path) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open", path) { UseShellExecute = true });
            }
        }
    }
}