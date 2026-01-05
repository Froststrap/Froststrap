using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
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
            set => installer.CreateDesktopShortcuts = value;
        }

        public bool CreateStartMenuShortcuts
        {
            get => installer.CreateStartMenuShortcuts;
            set => installer.CreateStartMenuShortcuts = value;
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

        public ICommand BrowseInstallLocationCommand => new RelayCommand(BrowseInstallLocation);

        public ICommand ResetInstallLocationCommand => new RelayCommand(ResetInstallLocation);

        public ICommand OpenFolderCommand => new RelayCommand(OpenFolder);

        public ImportSettingsFrom SelectedImportSource
        {
            get => installer.ImportSource;
            set
            {
                installer.ImportSource = value;
                OnPropertyChanged(nameof(SelectedImportSource));
            }
        }

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

        public bool DoInstall()
        {
            if (!installer.CheckInstallLocation())
            {
                SetCanContinueEvent?.Invoke(this, false);
                OnPropertyChanged(nameof(ErrorMessage));
                return false;
            }

            installer.DoInstall();
            return true;
        }

        private async void BrowseInstallLocation()
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null)
                return;

            var storageProvider = mainWindow.StorageProvider;

            var folder = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Install Location",
                AllowMultiple = false
            });

            if (folder != null && folder.Count > 0)
            {
                InstallLocation = folder[0].Path.LocalPath;
                OnPropertyChanged(nameof(InstallLocation));
            }
        }

        private void ResetInstallLocation()
        {
            InstallLocation = _originalInstallLocation;
            OnPropertyChanged(nameof(InstallLocation));
        }

        private void OpenFolder() => Process.Start("explorer.exe", Paths.Base);
    }
}