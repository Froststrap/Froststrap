using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.ViewModels.Settings.FastFlags;
using Froststrap.UI.ViewModels.Settings.GlobalSettings;
using Froststrap.UI.ViewModels.Settings.Mods;
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

        private string _selectedPage = "integrations";
        public string SelectedPage { get => _selectedPage; set => SetProperty(ref _selectedPage, value); }

        private string _currentPageTitle = "Integrations";
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

        public IRelayCommand NavigateToIntegrationsCommand { get; }
        public IRelayCommand NavigateToBehaviourCommand { get; }
        public IRelayCommand NavigateToSoberSettingsCommand { get; }
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
        public bool SoberEnabled = App.SoberSettings.Loaded;

        public MainWindowViewModel()
        {
            _breadcrumbItems.CollectionChanged += OnBreadcrumbsChanged;

            OpenAboutCommand = new RelayCommand(OpenAbout);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            SaveAndLaunchSettingsCommand = new RelayCommand(SaveAndLaunchSettings);
            RestartAppCommand = new RelayCommand(RestartApp);
            CloseWindowCommand = new RelayCommand(CloseWindow);
            BreadcrumbItemClickedCommand = new RelayCommand<BreadcrumbItemModel>(HandleBreadcrumbItemClicked);
            SearchBar = new();

            NavigateToIntegrationsCommand = new RelayCommand(() => Navigate("integrations", "Integrations", Strings.Menu_Integrations_Description, new IntegrationsViewModel()));
            NavigateToBehaviourCommand = new RelayCommand(() => Navigate("behaviour", "Behaviour", Strings.Menu_Behaviour_Description, new BehaviourViewModel()));
            NavigateToSoberSettingsCommand = new RelayCommand(() => Navigate("sobersettings", "Sober Settings", null!, new SoberSettingsViewModel()));
            NavigateToPresetModsCommand = new RelayCommand(() => Navigate("mods", "Preset Mods", "Official built-in mods.", new ModsPresetsViewModel()));
            NavigateToFastFlagsCommand = new RelayCommand(() => Navigate("fastflags", "Fast Flags", Strings.Menu_FastFlags_Description, new FastFlagsViewModel()));
            NavigateToAppearanceCommand = new RelayCommand(() => Navigate("appearance", Strings.Menu_Appearance_Title, Strings.Menu_Appearance_Description, new AppearanceViewModel()));
            NavigateToRegionSelectorCommand = new RelayCommand(() => Navigate("regionselector", "Region Selector", null!, new RegionSelectorViewModel()));
            NavigateToGlobalSettingsCommand = new RelayCommand(() => Navigate("globalsettings", "Global Settings", Strings.Menu_GBSEditor_Description, new GlobalSettingsViewModel()));
            NavigateToShortcutsCommand = new RelayCommand(() => Navigate("shortcuts", "Shortcuts", Strings.Menu_Shortcuts_Description, new ShortcutsViewModel()));
            NavigateToQuickPlayCommand = new RelayCommand(() => Navigate("quickplay", "Quick Play", "Jump back into your recent games.", new QuickPlayViewModel()));
            NavigateToChannelsCommand = new RelayCommand(() => Navigate("channels", "Deployment", Strings.Menu_Channel_Description, new ChannelViewModel()));

            NavigateToGlobalSettingsEditorCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = "Global Settings", Tag = "globalsettings" },
                    new() { Content = "Editor", Tag = null, IsLast = true }
                ];
                Navigate("globalsettingseditor", "Editor", null!, new GlobalSettingsEditorViewModel(this), crumbs);
            });

            NavigateToFastFlagEditorCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = "Fast Flags", Tag = "fastflags" },
                    new() { Content = "Editor", Tag = null, IsLast = true }
                ];
                Navigate("fastflageditor", "Editor", Strings.Menu_FastFlagEditor_Description, new FastFlagEditorViewModel(this), crumbs);
            });

            NavigateToCommunityModsCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = "Preset Mods", Tag = "mods" },
                    new() { Content = "Community Mods", Tag = null, IsLast = true }
                ];
                Navigate("communitymods", "Community Mods", "Explore user-created mods.", new CommunityModsViewModel(), crumbs);
            });

            NavigateToModGeneratorCommand = new RelayCommand(() =>
            {
                Navigate("modgenerator", "Mod Generator", "Generate mods easily with a single click.", new ModGeneratorViewModel(), [
                    new() { Content = "Preset Mods", Tag = "mods" },
                    new() { Content = "Mod Generator", Tag = null, IsLast = true }
                ]);
            });

            NavigateToMyModsCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = "Preset Mods", Tag = "mods" },
                    new() { Content = "My Mods", Tag = null, IsLast = true }
                ];
                Navigate("custommods", "My Mods", Strings.Menu_Mods_Description, new ModsViewModel(), crumbs);
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
                    NavigateToIntegrationsCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.BehaviourViewModel":
                    NavigateToBehaviourCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.SoberSettingsViewModel":
                    NavigateToSoberSettingsCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.ModsViewModel":
                    NavigateToMyModsCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.FastFlagsViewModel":
                    NavigateToFastFlagsCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.AppearanceViewModel":
                    NavigateToAppearanceCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.GlobalSettingsViewModel":
                    NavigateToGlobalSettingsCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.ShortcutsViewModel":
                    NavigateToShortcutsCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.QuickPlayViewModel":
                    NavigateToQuickPlayCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.Mods.CommunityModsViewModel":
                    NavigateToCommunityModsCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.Mods.ModsPresetsViewModel":
                    NavigateToPresetModsCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.Mods.ModGeneratorViewModel":
                    NavigateToModGeneratorCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.FastFlags.FastFlagEditorViewModel":
                    NavigateToFastFlagEditorCommand.Execute(null); break;
                case "Froststrap.UI.ViewModels.Settings.GlobalSettings.GlobalSettingsEditorViewModel":
                    NavigateToGlobalSettingsEditorCommand.Execute(null); break;
                default:
                    NavigateToIntegrationsCommand.Execute(null); break;
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
            App.StorageSettings.Save();

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
                Process.Start(Paths.Application, "-player");
            else
                CloseWindow();
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
