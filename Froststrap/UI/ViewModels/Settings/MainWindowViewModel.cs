using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.ViewModels.Settings.FastFlags;
using Froststrap.UI.ViewModels.Settings.GlobalSettings;
using Froststrap.UI.ViewModels.Settings.Mods;
using Froststrap.UI.Elements.Settings.Pages;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public class BreadcrumbItemModel
    {
        public string Content { get; set; } = string.Empty;
        public string? Tag { get; set; }
        public bool IsLast { get; set; }
    }

    public class MainWindowViewModel : ObservableObject
    {
        private object? _currentPage;
        public object? CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public static bool HasUnsavedChanges =>
            App.Settings.HasUnsavedChanges ||
            App.State.HasUnsavedChanges ||
            App.FastFlags.HasUnsavedChanges ||
            App.AppStorage.HasUnsavedChanges ||
            (OperatingSystem.IsLinux() && App.SoberSettings.HasUnsavedChanges) ||
            App.GlobalSettings.HasUnsavedChanges ||
            App.PendingSettingTasks.Count > 0;

        private string _selectedPage = "integrations";
        public string SelectedPage { get => _selectedPage; set => SetProperty(ref _selectedPage, value); }

        private string _currentPageTitle = Strings.Menu_Integrations_Title;
        public string CurrentPageTitle { get => _currentPageTitle; set => SetProperty(ref _currentPageTitle, value); }

        private string _currentPageDescription = "";
        public string CurrentPageDescription { get => _currentPageDescription; set => SetProperty(ref _currentPageDescription, value); }

        public SearchBarViewModel SearchBar { get; }

        private ObservableCollection<BreadcrumbItemModel> _breadcrumbItems = [];
        public ObservableCollection<BreadcrumbItemModel> BreadcrumbItems
        {
            get => _breadcrumbItems;
            set
            {
                    _breadcrumbItems?.CollectionChanged -= OnBreadcrumbsChanged;

                SetProperty(ref _breadcrumbItems, value);

                    _breadcrumbItems?.CollectionChanged += OnBreadcrumbsChanged;

                UpdateBreadcrumbVisibility();
            }
        }

        private void OnBreadcrumbsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateBreadcrumbVisibility();

        private void UpdateBreadcrumbVisibility()
        {
            OnPropertyChanged(nameof(HasBreadcrumbs));
            OnPropertyChanged(nameof(ShowPageTitle));
        }

        public bool HasBreadcrumbs => BreadcrumbItems.Count > 0;
        public bool ShowPageTitle => !HasBreadcrumbs;

        public ICommand SetLaunchModeCommand { get; }

        public IRelayCommand NavigateToIntegrationsCommand { get; }
        public IRelayCommand NavigateToBehaviourCommand { get; }
        public IRelayCommand NavigateToLinuxSettingsCommand { get; }
        public IRelayCommand NavigateToMyModsCommand { get; }
        public IRelayCommand NavigateToFastFlagsCommand { get; }
        public IRelayCommand NavigateToFastFlagEditorCommand { get; }
        public IRelayCommand NavigateToAppearanceCommand { get; }
        public IRelayCommand NavigateToRegionSelectorCommand { get; }
        public IRelayCommand NavigateToGlobalSettingsCommand { get; }
        public IRelayCommand NavigateToGlobalSettingsEditorCommand { get; }
        public IRelayCommand NavigateToShortcutsCommand { get; }
        public IRelayCommand NavigateToQuickPlayCommand { get; }
        public IRelayCommand NavigateToChannelsCommand { get; }
        public IRelayCommand NavigateToCommunityModsCommand { get; }
        public IRelayCommand NavigateToPresetModsCommand { get; }
        public IRelayCommand NavigateToModGeneratorCommand { get; }

        public ICommand OpenAboutCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand SaveAndLaunchSettingsCommand { get; }
        public ICommand RestartAppCommand { get; }
        public ICommand CloseWindowCommand { get; }
        public ICommand BreadcrumbItemClickedCommand { get; }

        public EventHandler? RequestSaveNoticeEvent;
        public EventHandler? RequestCloseWindowEvent;
        public event EventHandler? SettingsSaved;
        public bool GBSEnabled = App.GlobalSettings.Loaded;

        public MainWindowViewModel()
        {
            _breadcrumbItems.CollectionChanged += OnBreadcrumbsChanged;

            SetLaunchModeCommand = new RelayCommand<LaunchMode>(mode => SelectedLaunchMode = mode);

            OpenAboutCommand = new RelayCommand(OpenAbout);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            SaveAndLaunchSettingsCommand = new RelayCommand(SaveAndLaunchSettings);
            RestartAppCommand = new RelayCommand(RestartApp);
            CloseWindowCommand = new RelayCommand(CloseWindow);
            BreadcrumbItemClickedCommand = new RelayCommand<BreadcrumbItemModel>(HandleBreadcrumbItemClicked);
            SearchBar = new();

            NavigateToIntegrationsCommand = new RelayCommand(() => Navigate("integrations", Strings.Menu_Integrations_Title, Strings.Menu_Integrations_Description, new IntegrationsViewModel()));
            NavigateToBehaviourCommand = new RelayCommand(() => Navigate("behaviour", Strings.Menu_Behaviour_Title, Strings.Menu_Behaviour_Description, new BehaviourViewModel()));
            NavigateToLinuxSettingsCommand = new RelayCommand(() => Navigate("linuxsettings", Strings.Menu_LinuxSettings_Title, null!, new LinuxSettingsViewModel()));
            NavigateToPresetModsCommand = new RelayCommand(() => Navigate("mods", Strings.Menu_PresetMods_Title, Strings.Menu_PresetMods_Description, new ModsPresetsViewModel()));
            NavigateToFastFlagsCommand = new RelayCommand(() =>
            {
                var dialogService = new FastFlagsDialogService(this);
                var viewModel = new FastFlagsViewModel(
                    new DefaultFastFlagsService(),
                    new DefaultSettingsService(),
                    dialogService
                );
                Navigate("fastflags", Strings.Menu_FastFlags_Title, Strings.Menu_FastFlags_Description, viewModel);});
            NavigateToAppearanceCommand = new RelayCommand(() => Navigate("appearance", Strings.Menu_Appearance_Title, Strings.Menu_Appearance_Description, new AppearanceViewModel()));
            NavigateToRegionSelectorCommand = new RelayCommand(() => Navigate("regionselector", Strings.Menu_RegionSelector_Title, null!, new RegionSelectorViewModel()));
            NavigateToGlobalSettingsCommand = new RelayCommand(() => Navigate("globalsettings", Strings.Menu_GlobalSettings_Title, Strings.Menu_GBSEditor_Description, new GlobalSettingsViewModel()));
            NavigateToShortcutsCommand = new RelayCommand(() => Navigate("shortcuts", Strings.Common_Shortcuts, Strings.Menu_Shortcuts_Description, new ShortcutsViewModel()));
            NavigateToQuickPlayCommand = new RelayCommand(() => Navigate("quickplay", Strings.Menu_QuickPlay_Title, Strings.Menu_QuickPlay_Description, new QuickPlayViewModel()));
            NavigateToChannelsCommand = new RelayCommand(() => Navigate("channels", Strings.Common_Deployment, Strings.Menu_Channel_Description, new ChannelViewModel()));

            NavigateToGlobalSettingsEditorCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = Strings.Menu_GlobalSettings_Title, Tag = "globalsettings" },
                    new() { Content = Strings.Common_Editor, Tag = null, IsLast = true }
                ];
                Navigate("globalsettingseditor", Strings.Common_Editor, Strings.Menu_GlobalSettingsEditor_Description, new GlobalSettingsEditorViewModel(this), crumbs);
            });

            NavigateToFastFlagEditorCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = Strings.Menu_FastFlags_Title, Tag = "fastflags" },
                    new() { Content = Strings.Common_Editor, Tag = null, IsLast = true }
                ];
                Navigate("fastflageditor", Strings.Common_Editor, Strings.Menu_FastFlagEditor_Description, new FastFlagEditorViewModel(this), crumbs);
            });

            NavigateToCommunityModsCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = Strings.Menu_PresetMods_Title, Tag = "mods" },
                    new() { Content = Strings.Menu_CommunityMods_Title, Tag = null, IsLast = true }
                ];
                Navigate("communitymods", Strings.Menu_CommunityMods_Title, Strings.Menu_CommunityMods_Description, new CommunityModsViewModel(), crumbs);
            });

            NavigateToModGeneratorCommand = new RelayCommand(() =>
            {
                Navigate("modgenerator", Strings.Menu_ModGenerator_Title, Strings.Menu_ModGenerator_Description, new ModGeneratorViewModel(), [
                    new() { Content = Strings.Menu_PresetMods_Title, Tag = "mods" },
                    new() { Content = Strings.Menu_ModGenerator_Title, Tag = null, IsLast = true }
                ]);
            });

            NavigateToMyModsCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = Strings.Menu_PresetMods_Title, Tag = "mods" },
                    new() { Content = Strings.Menu_Mods_Title, Tag = null, IsLast = true }
                ];
                Navigate("custommods", Strings.Menu_Mods_Title, Strings.Menu_Mods_Description, new ModsViewModel(), crumbs);
            });

            var lastPageName = App.State.Prop.LastPage;
            if (lastPageName != null)
                NavigateToLastPage(lastPageName);
            else
                NavigateToIntegrationsCommand.Execute(null);
        }

        private void Navigate(string pageId, string title, string description, object viewModel, ObservableCollection<BreadcrumbItemModel>? customBreadcrumbs = null)
        {
            try
            {
                SelectedPage = pageId;
                CurrentPageTitle = title;
                CurrentPageDescription = description;
                BreadcrumbItems = customBreadcrumbs ?? [];
                CurrentPage = viewModel;
                SearchBar.Clear();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("MainWindowViewModel::NavigationCommand", ex);
            }
        }

        private void NavigateToLastPage(string pageTypeName)
        {
            switch (pageTypeName)
            {
                case "Froststrap.UI.ViewModels.Settings.IntegrationsViewModel":
                    NavigateToIntegrationsCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.BehaviourViewModel":
                    NavigateToBehaviourCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.LinuxSettingsViewModel":
                    if (OperatingSystem.IsLinux())
                        NavigateToLinuxSettingsCommand.Execute(null);
                    else
                        NavigateToIntegrationsCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.Mods.ModsViewModel":
                    NavigateToMyModsCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.FastFlagsViewModel":
                    NavigateToFastFlagsCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.AppearanceViewModel":
                    NavigateToAppearanceCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.GlobalSettingsViewModel":
                    if (GBSEnabled)
                        NavigateToGlobalSettingsCommand.Execute(null);
                    else
                        NavigateToIntegrationsCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.ShortcutsViewModel":
                    NavigateToShortcutsCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.QuickPlayViewModel":
                    NavigateToQuickPlayCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.ChannelViewModel":
                    NavigateToChannelsCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.Mods.CommunityModsViewModel":
                    NavigateToCommunityModsCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.Mods.ModsPresetsViewModel":
                    NavigateToPresetModsCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.Mods.ModGeneratorViewModel":
                    NavigateToModGeneratorCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.RegionSelectorViewModel":
                    NavigateToRegionSelectorCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.FastFlags.FastFlagEditorViewModel":
                    NavigateToFastFlagEditorCommand.Execute(null);
                    break;
                case "Froststrap.UI.ViewModels.Settings.GlobalSettings.GlobalSettingsEditorViewModel":
                    if (GBSEnabled)
                        NavigateToGlobalSettingsEditorCommand.Execute(null);
                    else
                        NavigateToIntegrationsCommand.Execute(null);
                    break;
                default:
                    NavigateToIntegrationsCommand.Execute(null);
                    break;
            }
        }

        private void OpenAbout()
        {
            App.FrostRPC?.SetDialog("About");
            new Elements.About.MainWindow().Show();
            App.FrostRPC?.ClearDialog();
        }

        private void CloseWindow() => RequestCloseWindowEvent?.Invoke(this, EventArgs.Empty);

        public void SaveSettings()
        {
            const string LOG_IDENT = "MainWindowViewModel::SaveSettings";

            if (CurrentPage != null)
            {
                App.State.Prop.LastPage = CurrentPage.GetType().FullName;
            }

            App.Settings.Save();
            App.State.Save();
            App.FastFlags.Save();
            App.GlobalSettings.Save();
            App.AppStorage.Save();

            if (OperatingSystem.IsLinux())
                App.SoberSettings.Save();

            foreach (var pair in App.PendingSettingTasks)
            {
                var task = pair.Value;

                if (task.Changed)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Executing pending task '{task}'");
                    task.Execute();
                }
            }

            App.PendingSettingTasks.Clear();
            RequestSaveNoticeEvent?.Invoke(this, EventArgs.Empty);
        }

        public void SaveAndLaunchSettings()
        {
            SaveSettings();
            if (!App.LaunchSettings.TestModeFlag.Active)
            {

                string arg = SelectedLaunchMode == LaunchMode.Player ? "-player" : "-studio";
                Process.Start(Paths.Application, arg);
            }
            else
            {
                CloseWindow();
            }
        }

        public LaunchMode SelectedLaunchMode
        {
            get => App.Settings.Prop.DefaultSaveAndLaunchMode;
            set 
            { 
                App.Settings.Prop.DefaultSaveAndLaunchMode = value; 
                OnPropertyChanged(nameof(SelectedLaunchMode));
                OnPropertyChanged(nameof(LaunchButtonText));
            }
        }

        public static bool IsPlayerInstalled
        {
            get
            {
                if (OperatingSystem.IsLinux())
                {
                    var clientPath = Path.Combine(Paths.Versions, "Sober", "data", "sober", "packages", "x86_64", "com.roblox.client");
                    return Directory.Exists(clientPath) && Directory.EnumerateFiles(clientPath, "*", SearchOption.AllDirectories).Any();
                }
                else
                {
                    return App.IsPlayerInstalled;
                }
            }
        }

        public static bool IsStudioInstalled => App.IsStudioInstalled;
        public static string PlayerMenuItemText => OperatingSystem.IsLinux() ? Strings.Common_Sober : Strings.Common_Player;

        public string LaunchButtonText
        {
            get
            {
                if (SelectedLaunchMode == LaunchMode.Player)
                {
                    string modeName = OperatingSystem.IsLinux() ? Strings.Common_Sober : Strings.Common_Player;
                    return IsPlayerInstalled
                        ? $"{Strings.Common_SaveAndLaunch} {modeName}"
                        : $"{Strings.Common_SaveAndInstall} {modeName}";
                }
                else
                {
                    return IsStudioInstalled
                        ? $"{Strings.Common_SaveAndLaunch} {Strings.Common_Studio}"
                        : $"{Strings.Common_SaveAndInstall} {Strings.Common_Studio}";
                }
            }
        }

        private async void RestartApp()
        {
            SaveSettings();
            SettingsSaved?.Invoke(this, EventArgs.Empty);

            await Task.Delay(750);

            var startInfo = new ProcessStartInfo(Environment.ProcessPath!)
            {
                Arguments = "-menu"
            };

            Process.Start(startInfo);
            App.FrostRPC?.Dispose();
            App.FrostRPC = null;
            CloseWindow();
        }

        private void HandleBreadcrumbItemClicked(BreadcrumbItemModel? item)
        {
            if (item?.Tag == null || item.IsLast) return;

            switch (item.Tag)
            {
                case "mods":
                    NavigateToPresetModsCommand.Execute(null);
                    break;
                case "fastflags":
                    NavigateToFastFlagsCommand.Execute(null);
                    break;
                case "globalsettings":
                    NavigateToGlobalSettingsCommand.Execute(null);
                    break;
            }
        }

        public void SetSearchIndex(List<SearchBarItem> searchIndex)
        {
            SearchBar.SetSearchIndex(searchIndex);
        }

        public List<SearchBarItem> GetSearchIndex()
        {
            return SearchBar.GetSearchIndex();
        }
    }
}