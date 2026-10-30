// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Settings.Pages;
using Froststrap.UI.ViewModels.Settings.FastFlags;
using Froststrap.UI.ViewModels.Settings.GlobalSettings;
using Froststrap.UI.ViewModels.Settings.Mods;
using System.Collections.ObjectModel;
using LucideAvalonia.Enum;
using System.Collections.Specialized;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    internal class BreadcrumbItemModel
    {
        public string Content { get; set; } = string.Empty;
        public string? Tag { get; set; }
        public bool IsLast { get; set; }
    }

    internal class MainWindowViewModel : ObservableObject
    {
        private bool _isNotLinux = !OperatingSystem.IsLinux();
        public bool IsNotLinux { get => _isNotLinux; }
        private bool _isNotMacOS = !OperatingSystem.IsMacOS();
        public bool IsNotMacOS { get => _isNotMacOS; }

        private int _titlebarSpaceHeight = OperatingSystem.IsMacOS() ? 52 : 40;
        public int TitlebarSpaceHeight { get => _titlebarSpaceHeight; }
        private Thickness _titlebarPadding = OperatingSystem.IsMacOS() ? new Thickness(95,0,12,0) : OperatingSystem.IsLinux() ? new Thickness(8, 0) : new Thickness(8, 0, 140, 0);
        public Thickness TitlebarPadding { get => _titlebarPadding; }
        private Thickness _openSidebarTitleMargin = OperatingSystem.IsMacOS() ? new Thickness(56,0,0,0) : new Thickness(155,0,0,0);
        public Thickness OpenSidebarTitleMargin { get => _openSidebarTitleMargin; }
        private Thickness _textCorrection = OperatingSystem.IsMacOS() ? new Thickness(0,4,0,0) : new Thickness(0,0,0,0);
        public Thickness TextCorrection { get => _textCorrection; }
        private int _iconButtonSize = OperatingSystem.IsMacOS() ? 28 : 24;
        public int IconButtonSize { get => _iconButtonSize; }
        private int _iconButtonIconSize = OperatingSystem.IsMacOS() ? 14 : 14;
        public int IconButtonIconSize { get => _iconButtonIconSize; }
        private int _titlebarTextSize = OperatingSystem.IsMacOS() ? 18 : 13;
        public int TitlebarTextSize { get => _titlebarTextSize; }

        private static readonly Dictionary<string, LucideIconNames> _pageIconMap = new()
        {
            ["integrations"] = LucideIconNames.Plus,
            ["behaviour"] = LucideIconNames.Play,
            ["linuxsettings"] = LucideIconNames.Settings,
            ["mods"] = LucideIconNames.BookOpen,
            ["fastflags"] = LucideIconNames.Flag,
            ["appearance"] = LucideIconNames.Palette,
            ["regionselector"] = LucideIconNames.Globe,
            ["globalsettings"] = LucideIconNames.PenLine,
            ["shortcuts"] = LucideIconNames.Link2,
            ["quickplay"] = LucideIconNames.Gamepad2,
            ["channels"] = LucideIconNames.HardDriveUpload,
            ["globalsettingseditor"] = LucideIconNames.FilePenLine,
            ["fastflageditor"] = LucideIconNames.FilePenLine,
            ["communitymods"] = LucideIconNames.House,
            ["custommods"] = LucideIconNames.List,
            ["modgenerator"] = LucideIconNames.PaintbrushVertical,
        };

        private object? _currentPage;
        public object? CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        private LucideIconNames _currentPageIcon;
        public LucideIconNames CurrentPageIcon
        {
            get => _currentPageIcon;
            set => SetProperty(ref _currentPageIcon, value);
        }

        public static bool HasUnsavedChanges
        {
            get
            {
                bool dirty = false;
                void Check(string name, bool isDirty, Func<string>? detail = null)
                {
                    if (!isDirty) return;
                    dirty = true;
                    App.Logger.Info($"Unsaved changes in {name}: {detail?.Invoke()}");
                }

                Check("Settings", App.Settings.HasUnsavedChanges, App.Settings.DescribeChanges);
                Check("State", App.State.HasUnsavedChanges, App.State.DescribeChanges);
                Check("FastFlags", App.FastFlags.HasUnsavedChanges, App.FastFlags.DescribeChanges);
                if (OperatingSystem.IsLinux())
                    Check("SoberSettings", App.SoberSettings.HasUnsavedChanges);
                Check("GlobalSettings", App.GlobalSettings.HasUnsavedChanges);
                Check("PendingSettingTasks", App.PendingSettingTasks.Count > 0,
                    () => string.Join(", ", App.PendingSettingTasks.Keys));

                return dirty;
            }
        }

        private LaunchMode _selectedLaunchMode = LaunchMode.Player;

        private string _selectedPage = "integrations";
        public string SelectedPage { get => _selectedPage; set => SetProperty(ref _selectedPage, value); }

        private string _currentPageTitle = Localizer.Instance.Get("Menu_Integrations_Title");
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
        public bool GBSEnabled = App.GlobalSettings.Loaded;

        public NextAction CloseAction { get; private set; } = NextAction.Terminate;

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

            NavigateToIntegrationsCommand = new RelayCommand(() => Navigate("integrations", Localizer.Instance.Get("Menu_Integrations_Title"), Localizer.Instance.Get("Menu_Integrations_Description"), new IntegrationsViewModel()));
            NavigateToBehaviourCommand = new RelayCommand(() => Navigate("behaviour", Localizer.Instance.Get("Menu_Behaviour_Title"), Localizer.Instance.Get("Menu_Behaviour_Description"), new BehaviourViewModel()));
            NavigateToLinuxSettingsCommand = new RelayCommand(() => Navigate("linuxsettings", Localizer.Instance.Get("Menu_LinuxSettings_Title"), null!, new LinuxSettingsViewModel()));
            NavigateToPresetModsCommand = new RelayCommand(() => Navigate("mods", Localizer.Instance.Get("Menu_PresetMods_Title"), Localizer.Instance.Get("Menu_PresetMods_Description"), new ModsPresetsViewModel()));
            NavigateToFastFlagsCommand = new RelayCommand(() =>
            {
                var dialogService = new FastFlagsDialogService(this);
                var viewModel = new FastFlagsViewModel(
                    new DefaultFastFlagsService(),
                    new DefaultSettingsService(),
                    dialogService
                );
                Navigate("fastflags", Localizer.Instance.Get("Menu_FastFlags_Title"), Localizer.Instance.Get("Menu_FastFlags_Description"), viewModel);
            });
            NavigateToAppearanceCommand = new RelayCommand(() => Navigate("appearance", Localizer.Instance.Get("Menu_Appearance_Title"), Localizer.Instance.Get("Menu_Appearance_Description"), new AppearanceViewModel()));
            NavigateToRegionSelectorCommand = new RelayCommand(() => Navigate("regionselector", Localizer.Instance.Get("Menu_RegionSelector_Title"), Localizer.Instance.Get("Menu_RegionSelector_Description"), new RegionSelectorViewModel()));
            NavigateToGlobalSettingsCommand = new RelayCommand(() => Navigate("globalsettings", Localizer.Instance.Get("Menu_GlobalSettings_Title"), Localizer.Instance.Get("Menu_GBSEditor_Description"), new GlobalSettingsViewModel()));
            NavigateToShortcutsCommand = new RelayCommand(() => Navigate("shortcuts", Localizer.Instance.Get("Common_Shortcuts"), Localizer.Instance.Get("Menu_Shortcuts_Description"), new ShortcutsViewModel()));
            NavigateToQuickPlayCommand = new RelayCommand(() => Navigate("quickplay", Localizer.Instance.Get("Menu_QuickPlay_Title"), Localizer.Instance.Get("Menu_QuickPlay_Description"), new QuickPlayViewModel()));
            NavigateToChannelsCommand = new RelayCommand(() => Navigate("channels", Localizer.Instance.Get("Common_Deployment"), Localizer.Instance.Get("Menu_Channel_Description"), new ChannelViewModel()));

            NavigateToGlobalSettingsEditorCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = Localizer.Instance.Get("Menu_GlobalSettings_Title"), Tag = "globalsettings" },
                    new() { Content = Localizer.Instance.Get("Common_Editor"), Tag = null, IsLast = true }
                ];
                Navigate("globalsettingseditor", Localizer.Instance.Get("Common_Editor"), Localizer.Instance.Get("Menu_GlobalSettingsEditor_Description"), new GlobalSettingsEditorViewModel(this), crumbs);
            });

            NavigateToFastFlagEditorCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = Localizer.Instance.Get("Menu_FastFlags_Title"), Tag = "fastflags" },
                    new() { Content = Localizer.Instance.Get("Common_Editor"), Tag = null, IsLast = true }
                ];
                Navigate("fastflageditor", Localizer.Instance.Get("Common_Editor"), Localizer.Instance.Get("Menu_FastFlagEditor_Description"), new FastFlagEditorViewModel(this), crumbs);
            });

            NavigateToCommunityModsCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = Localizer.Instance.Get("Menu_PresetMods_Title"), Tag = "mods" },
                    new() { Content = Localizer.Instance.Get("Menu_CommunityMods_Title"), Tag = null, IsLast = true }
                ];
                Navigate("communitymods", Localizer.Instance.Get("Menu_CommunityMods_Title"), Localizer.Instance.Get("Menu_CommunityMods_Description"), new CommunityModsViewModel(), crumbs);
            });

            NavigateToModGeneratorCommand = new RelayCommand(() =>
            {
                Navigate("modgenerator", Localizer.Instance.Get("Menu_ModGenerator_Title"), Localizer.Instance.Get("Menu_ModGenerator_Description"), new ModGeneratorViewModel(), [
                    new() { Content = Localizer.Instance.Get("Menu_PresetMods_Title"), Tag = "mods" },
                    new() { Content = Localizer.Instance.Get("Menu_ModGenerator_Title"), Tag = null, IsLast = true }
                ]);
            });

            NavigateToMyModsCommand = new RelayCommand(() =>
            {
                ObservableCollection<BreadcrumbItemModel> crumbs = [
                    new() { Content = Localizer.Instance.Get("Menu_PresetMods_Title"), Tag = "mods" },
                    new() { Content = Localizer.Instance.Get("Menu_Mods_Title"), Tag = null, IsLast = true }
                ];
                Navigate("custommods", Localizer.Instance.Get("Menu_Mods_Title"), Localizer.Instance.Get("Menu_Mods_InfoBar"), new ModsViewModel(), crumbs);
            });

            var lastPageName = App.State.Prop.LastPage;
            if (lastPageName != null)
                NavigateToLastPage(lastPageName);
            else
                NavigateToIntegrationsCommand.Execute(null);

            SelectedLaunchMode = App.State.Prop.LastLaunchMode;
        }

        private void Navigate(string pageId, string title, string description, object viewModel, ObservableCollection<BreadcrumbItemModel>? customBreadcrumbs = null)
        {
            try
            {
                SelectedPage = pageId;
                CurrentPageTitle = title;
                CurrentPageDescription = description;
                CurrentPageIcon = _pageIconMap.TryGetValue(pageId, out var icon) ? icon : LucideIconNames.CircleQuestionMark;
                BreadcrumbItems = customBreadcrumbs ?? [];
                CurrentPage = viewModel;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
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
                case "Froststrap.UI.ViewModels.Settings.ModsPresetsViewModel":
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
                    break;
                default:
                    NavigateToIntegrationsCommand.Execute(null);
                    break;
            }
        }

        private void OpenAbout()
        {
            if (App.AboutOpen) return;

            App.FrostRPC?.SetDialog("About");
            new Elements.About.MainWindow().Show();
            App.FrostRPC?.ClearDialog();

            App.AboutOpen = true;
        }

        private void CloseWindow() => RequestCloseWindowEvent?.Invoke(this, EventArgs.Empty);

        public void SaveSettings()
        {
            if (CurrentPage != null)
            {
                App.State.Prop.LastPage = CurrentPage.GetType().FullName;
            }

            App.Settings.Save();
            App.State.Save();
            App.FastFlags.Save();
            App.GlobalSettings.Save();

            if (OperatingSystem.IsLinux())
                App.SoberSettings.Save();

            foreach (var pair in App.PendingSettingTasks)
            {
                var task = pair.Value;

                if (task.Changed)
                {
                    App.Logger.Info($"Executing pending task '{task}'");
                    task.Execute();
                }
            }

            App.PendingSettingTasks.Clear();
            RequestSaveNoticeEvent?.Invoke(this, EventArgs.Empty);
        }

        public void SaveAndLaunchSettings()
        {
            SaveSettings();
            LaunchHandler.ProcessNextAction(LaunchEntry.ToNextAction(SelectedLaunchMode));
        }

        public LaunchMode SelectedLaunchMode
        {
            get => _selectedLaunchMode;
            set
            {
                if (SetProperty(ref _selectedLaunchMode, value))
                {
                    App.State.Prop.LastLaunchMode = value;
                    App.State.SaveSetting("LastLaunchMode");
                    OnPropertyChanged(nameof(LaunchButtonText));
                }
            }
        }

        public static bool IsPlayerInstalled
        {
            get
            {
                if (OperatingSystem.IsLinux())
                    return App.LinuxPlayerInstalled;
                else
                    return App.IsPlayerInstalled;
            }
        }

        public static bool IsStudioInstalled => App.IsStudioInstalled;
        public static string PlayerMenuItemText => OperatingSystem.IsLinux() ? Localizer.Instance.Get("Common_Sober") : Localizer.Instance.Get("Common_Player");

        public string LaunchButtonText
        {
            get
            {
                if (SelectedLaunchMode == LaunchMode.Player)
                {
                    string modeName = OperatingSystem.IsLinux() ? Localizer.Instance.Get("Common_Sober") : Localizer.Instance.Get("Common_Player");
                    return IsPlayerInstalled
                        ? $"{Localizer.Instance.Get("Common_SaveAndLaunch")} {modeName}"
                        : $"{Localizer.Instance.Get("Common_SaveAndInstall")} {modeName}";
                }
                else
                {
                    return IsStudioInstalled
                        ? $"{Localizer.Instance.Get("Common_SaveAndLaunch")} {Localizer.Instance.Get("Common_Studio")}"
                        : $"{Localizer.Instance.Get("Common_SaveAndInstall")} {Localizer.Instance.Get("Common_Studio")}";
                }
            }
        }

        public void RestartApp()
        {
            SaveSettings();

            CloseAction = NextAction.LaunchSettings;
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
